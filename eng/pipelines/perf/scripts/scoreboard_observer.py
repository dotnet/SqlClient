# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Bounded external Diagnostic observers. Raw evidence is never interpreted as timing-neutral."""

from datetime import datetime, timezone
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import threading
import time

STACK_VERSION = "9.0.652701"
SQL = """
SET NOCOUNT ON;
SELECT
 (SELECT COUNT(*) FROM sys.dm_exec_requests r JOIN sys.dm_exec_sessions s ON r.session_id = s.session_id
  WHERE r.session_id <> @@SPID AND s.is_user_process = 1) ActiveUserRequests,
 (SELECT COUNT(*) FROM sys.dm_os_tasks WHERE task_state = 'RUNNABLE') RunnableTasks,
 (SELECT COUNT(*) FROM sys.dm_os_tasks WHERE task_state = 'RUNNING') RunningTasks,
 (SELECT SUM(runnable_tasks_count) FROM sys.dm_os_schedulers WHERE status = 'VISIBLE ONLINE') SchedulerRunnableQueue,
 (SELECT SUM(current_tasks_count) FROM sys.dm_os_schedulers WHERE status = 'VISIBLE ONLINE') SchedulerCurrentTasks,
 (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) UserSessions,
 (SELECT COUNT(*) FROM sys.dm_exec_connections) Connections,
 (SELECT SUM(cntr_value) FROM sys.dm_os_performance_counters
  WHERE counter_name = N'Logins/sec' AND instance_name = N'') LoginsCounter,
 (SELECT TOP (1) monitor.Payload.value('(Record/SchedulerMonitorEvent/SystemHealth/ProcessUtilization)[1]', 'int')
  FROM (SELECT TRY_CONVERT(xml, record) AS Payload, timestamp FROM sys.dm_os_ring_buffers
        WHERE ring_buffer_type = 'RING_BUFFER_SCHEDULER_MONITOR') monitor
  ORDER BY monitor.timestamp DESC) SqlProcessCpuPercent,
 JSON_QUERY((SELECT wait_type AS WaitType, waiting_tasks_count AS Tasks, wait_time_ms AS Milliseconds
             FROM sys.dm_os_wait_stats FOR JSON PATH)) Waits
FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
"""


def utc():
    return datetime.now(timezone.utc).isoformat()


def stack_counts(text):
    """Count observed thread blocks with BOTH a SqlClient Open frame and a blocking primitive."""
    blocks = re.split(r"(?m)^Thread\s+", text)[1:]
    if not blocks:
        raise ValueError("dotnet-stack output contained no thread blocks")
    observed = []
    for block in blocks:
        driver = re.search(r"(?:Microsoft|System)\.Data\.SqlClient\.[^\r\n]*(?:\.Open\b|\.OpenAsync\b|TryOpen|TryGetConnection)", block)
        blocking = re.search(r"(?:WaitHandle\.|ManualResetEventSlim\.Wait|Monitor\.Wait|"
                             r"SemaphoreSlim\.Wait\b|Task\.InternalWait|Task\.SpinThenBlockingWait|"
                             r"Thread\.Sleep|WaitOneCore|WaitMultipleIgnoringSyncContext)", block)
        if driver and blocking:
            observed.append(block.splitlines()[0].strip())
    return {"ObservedThreads": len(blocks), "OpenWithBlockingPrimitiveThreads": len(observed),
            "ThreadHeaders": observed,
            "Interpretation": "Exact co-observed frames only; not a duration or proof of every blocked open"}


def waits_delta(before, after):
    old = {row["WaitType"]: row for row in before["Waits"]}
    deltas = []
    for row in after["Waits"]:
        prior = old.get(row["WaitType"])
        if prior is None:
            continue
        tasks, milliseconds = row["Tasks"] - prior["Tasks"], row["Milliseconds"] - prior["Milliseconds"]
        if tasks < 0 or milliseconds < 0:
            raise ValueError("SQL cumulative waits reset during observation")
        deltas.append({"WaitType": row["WaitType"], "Tasks": tasks, "Milliseconds": milliseconds})
    return deltas


def validate_sql(sql):
    if not isinstance(sql.get("Waits"), list):
        raise ValueError("SQL observer did not return cumulative wait counters")
    for key in ("ActiveUserRequests", "RunnableTasks", "RunningTasks", "SchedulerRunnableQueue",
                "SchedulerCurrentTasks", "UserSessions", "Connections", "LoginsCounter"):
        if type(sql.get(key)) is not int or sql[key] < 0:
            raise ValueError(f"SQL observer metric unavailable: {key}")


def sql_command(sqlcmd):
    return [sqlcmd, "-S", os.environ["SQL_SERVER"], "-U", "sa",
            "-C", "-b", "-l", "15", "-t", "15", "-h", "-1", "-y", "0",
            "-w", "65535", "-Q", SQL]


def timestamp(text):
    if not isinstance(text, str):
        raise ValueError("Missing diagnostic window timestamp")
    text = re.sub(r"(\.\d{6})\d+(?=[+-]\d{2}:\d{2}$)", r"\1", text.replace("Z", "+00:00"))
    value = datetime.fromisoformat(text)
    if value.tzinfo is None:
        raise ValueError("Diagnostic timestamp lacks UTC offset")
    return value


def association(capture, window):
    """UTC intervals are evidence; an observed active marker alone does not prove containment."""
    if capture is None:
        return "NotCollectedOutsideActual"
    if not window or not window.get("StartedUtc") or not window.get("CompletedUtc"):
        return "AmbiguousBoundary"
    begin, end = timestamp(window["StartedUtc"]), timestamp(window["CompletedUtc"])
    started, completed = timestamp(capture["StartedUtc"]), timestamp(capture["CompletedUtc"])
    if end < begin or completed < started:
        raise ValueError("Invalid diagnostic capture/window interval")
    # Equality at the boundary is ambiguous at the observer's microsecond timestamp precision.
    if begin < started <= completed < end:
        return "ContainedInActual"
    if completed < begin or started > end:
        return "OutsideActual"
    return "AmbiguousBoundary"


def prepare_tools(work_dir):
    """Only install the declared pinned tool after observing its local executable is missing."""
    if os.name == "nt":
        raise RuntimeError("Diagnostic capability unsupported on Windows in this increment; "
                           "use Linux for the full Diagnostic matrix (Sampled remains supported)")
    tool_dir = Path(work_dir) / "observer-tools"
    tool_dir.mkdir(parents=True, exist_ok=True)
    stack = tool_dir / "dotnet-stack"
    if not stack.exists():
        with (tool_dir / "install.log").open("w", encoding="utf-8") as log:
            result = subprocess.run(
                ["dotnet", "tool", "install", "dotnet-stack", "--version", STACK_VERSION,
                 "--tool-path", str(tool_dir), "--allow-roll-forward"],
                stdout=log, stderr=subprocess.STDOUT, timeout=180)
        if result.returncode:
            raise RuntimeError("Pinned dotnet-stack installation failed; see observer-tools/install.log")
    version = subprocess.run([str(stack), "--version"], capture_output=True, text=True, timeout=15)
    if version.returncode or STACK_VERSION not in version.stdout:
        raise RuntimeError("dotnet-stack does not match the declared pinned version")
    sqlcmd = shutil.which("sqlcmd")
    if not sqlcmd:
        sqlcmd = next((path for path in ("/opt/mssql-tools18/bin/sqlcmd", "/opt/mssql-tools/bin/sqlcmd")
                       if os.path.isfile(path)), None)
    if not sqlcmd or not os.environ.get("SQL_PASSWORD") or not os.environ.get("SQL_SERVER"):
        raise RuntimeError("Diagnostic capability requires provisioned SQLCMD and SQL environment credentials")
    sql_env = dict(os.environ)
    sql_env["SQLCMDPASSWORD"] = os.environ["SQL_PASSWORD"]
    preflight = tool_dir / "sql-capability-preflight.json"
    with preflight.open("w", encoding="utf-8") as output:
        result = subprocess.run(sql_command(sqlcmd), stdout=output, stderr=subprocess.STDOUT,
                                env=sql_env, timeout=20)
    if result.returncode:
        raise RuntimeError("SQL Diagnostic capability/permission preflight failed; see observer-tools")
    validate_sql(json.loads(preflight.read_text(encoding="utf-8-sig").strip()))
    return {"stack": str(stack), "sqlcmd": sqlcmd, "sql_capability_verified": True}


class Observer:
    def __init__(self, cwd, case, process_id, tools):
        self.directory = Path(cwd) / "BenchmarkDotNet.Artifacts" / "external-diagnostics"
        self.directory.mkdir(parents=True, exist_ok=True)
        self.marker_path = Path(cwd) / "BenchmarkDotNet.Artifacts" / "diagnostic-invocation.json"
        self.case, self.process_id, self.tools = case, process_id, tools
        self.stop_event = threading.Event()
        self.thread = threading.Thread(target=self._run, daemon=True)
        self.error = None
        self.records = []
        self.windows = {}
        self.window_lock = threading.Lock()

    def marker(self):
        try:
            with self.marker_path.open(encoding="utf-8-sig") as handle:
                value = json.load(handle)
            if (value.get("Stage") == "WorkloadActual" and value.get("CaseId") == self.case
                    and value.get("ProcessId") == self.process_id and value.get("InvocationId")):
                with self.window_lock:
                    previous = self.windows.get(value["InvocationId"])
                    if not previous or value.get("CompletedUtc") or not previous.get("CompletedUtc"):
                        self.windows[value["InvocationId"]] = value
            return value
        except FileNotFoundError:
            return None

    def eligible(self, marker):
        return (isinstance(marker, dict) and marker.get("Active") is True
                and marker.get("Stage") == "WorkloadActual"
                and marker.get("CaseId") == self.case
                and marker.get("ProcessId") == self.process_id
                and isinstance(marker.get("InvocationId"), str) and marker["InvocationId"])

    def command(self, command, path, env=None):
        with path.open("w", encoding="utf-8") as handle:
            result = subprocess.run(command, stdout=handle, stderr=subprocess.STDOUT,
                                    env=env, timeout=20)
        if result.returncode:
            raise RuntimeError(f"Diagnostic collector failed; inspect {path.name}")

    def capture(self, marker):
        index = len(self.records) + 1
        start = utc()
        stack_path = self.directory / f"stack-{index}.txt"
        sql_path = self.directory / f"sql-{index}.json"
        sql_env = dict(os.environ)
        # SQLCMDPASSWORD avoids command-line passwords and does not serialize into metadata.
        sql_env["SQLCMDPASSWORD"] = os.environ["SQL_PASSWORD"]
        same = lambda value: self.eligible(value) and value["InvocationId"] == marker["InvocationId"]

        def capture_one(command, path, env=None):
            before = self.marker()
            if not same(before):
                return None
            started = utc()
            self.command(command, path, env)
            completed = utc()
            return {"StartedUtc": started, "CompletedUtc": completed, "Before": before, "After": self.marker()}

        # Start both observers while the marker is active; SQL is not delayed until stack dumping ends.
        with ThreadPoolExecutor(max_workers=2) as executor:
            stack_future = executor.submit(capture_one,
                [self.tools["stack"], "report", "--process-id", str(self.process_id)], stack_path)
            sql_future = executor.submit(capture_one,
                sql_command(self.tools["sqlcmd"]), sql_path, sql_env)
            stack_capture, sql_capture = stack_future.result(), sql_future.result()
        end = utc()
        sql = None
        if sql_capture:
            sql = json.loads(sql_path.read_text(encoding="utf-8-sig").strip())
            validate_sql(sql)
        counts = stack_counts(stack_path.read_text(encoding="utf-8", errors="replace")) if stack_capture else None
        previous = next((record for record in reversed(self.records) if record["Sql"] is not None), None)
        login_delta = sql["LoginsCounter"] - previous["Sql"]["LoginsCounter"] if previous and sql else None
        if login_delta is not None and login_delta < 0:
            raise ValueError("SQL cumulative logins counter reset during observation")
        def overlap(capture):
            if capture is None:
                return "NotCollectedOutsideActual"
            return "ContainedInActual" if same(capture["After"]) else "AmbiguousBoundary"
        record = dict(InvocationId=marker["InvocationId"], CaseId=self.case, ProcessId=self.process_id,
                      Stage="WorkloadActual", CaptureStartedUtc=start, CaptureCompletedUtc=end,
                      StackCapture=stack_capture, SqlCapture=sql_capture,
                      StackOverlap=overlap(stack_capture), SqlOverlap=overlap(sql_capture),
                      MarkerBefore=marker, StackFile=stack_path.name if stack_capture else None,
                      SqlFile=sql_path.name if sql_capture else None, StackCounts=counts, Sql=sql,
                      SqlProcessCpuAvailability="Available" if sql and sql.get("SqlProcessCpuPercent") is not None
                      else "Unavailable: no scheduler-monitor CPU observation",
                      SqlWaitDeltas=waits_delta(previous["Sql"], sql) if previous and sql else None,
                      SqlLoginsDelta=login_delta,
                      SqlCounterDeltaScope="SameActualInvocation" if previous and sql
                      and previous["InvocationId"] == marker["InvocationId"]
                      and previous["SqlOverlap"] == "ContainedInActual"
                      and overlap(sql_capture) == "ContainedInActual" else "CrossInvocationOrAmbiguous",
                      SqlWaitDeltaPreviousCapture=previous["SqlCapture"] if previous and sql else None)
        self.records.append(record)

    def _run(self):
        try:
            last = {}
            while not self.stop_event.wait(.01):
                marker = self.marker()
                if not self.eligible(marker):
                    continue
                invocation = marker["InvocationId"]
                if time.monotonic() - last.get(invocation, 0) < 1:
                    continue
                if len(self.records) >= 30:
                    raise RuntimeError("Diagnostic snapshot budget exceeded")
                self.capture(marker)
                last[invocation] = time.monotonic()
        except Exception as error:
            # Keep raw logs; error messages here contain no SQL credentials or configuration.
            self.error = f"{type(error).__name__}: diagnostic collection failed"

    def finish(self):
        self.stop_event.set()
        self.thread.join(timeout=45)
        if self.thread.is_alive():
            self.error = "Diagnostic observer did not stop within its bound"
        prepared = bool(self.tools.get("stack") and self.tools.get("sqlcmd")
                        and self.tools.get("sql_capability_verified"))
        if not prepared:
            self.error = self.error or "Required external collectors were not prepared"
        self.marker()
        expected = set()
        for path in (self.directory.parent / "results").glob("openasync-*.json"):
            with path.open(encoding="utf-8-sig") as handle:
                data = json.load(handle)
            if (data.get("MeasurementStage") == "WorkloadActual" and data.get("CaseId") == self.case
                    and data.get("ProcessId") == self.process_id):
                invocation = data.get("InvocationId")
                if not isinstance(invocation, str) or not invocation or invocation in expected:
                    self.error = self.error or "Missing or duplicate actual invocation identity"
                    continue
                expected.add(invocation)
                try:
                    begin, end = timestamp(data.get("StartedUtc")), timestamp(data.get("CompletedUtc"))
                    if end < begin:
                        raise ValueError("Reversed actual invocation window")
                    observed = self.windows.get(invocation)
                    if observed and (timestamp(observed.get("StartedUtc")) != begin
                                     or (observed.get("CompletedUtc")
                                         and timestamp(observed["CompletedUtc"]) != end)):
                        raise ValueError("Exported invocation window differs from observed marker")
                    self.windows[invocation] = dict(
                        Active=False, Stage="WorkloadActual", CaseId=self.case, ProcessId=self.process_id,
                        InvocationId=invocation, StartedUtc=data["StartedUtc"], CompletedUtc=data["CompletedUtc"],
                        WindowSource="ExportedActualArtifact")
                except (ValueError, TypeError):
                    self.error = self.error or "Invalid exported actual invocation window"
        if len(expected) != 3:
            self.error = self.error or "Missing three distinct actual invocation artifacts"
        for record in self.records:
            window = self.windows.get(record["InvocationId"])
            record["ActualWindow"] = window
            record["StackOverlap"] = association(record["StackCapture"], window)
            record["SqlOverlap"] = association(record["SqlCapture"], window)
            # A delta may span other invocations or unobserved intervals; never label it workload-local
            # unless both endpoint captures are proven contained in this one actual window.
            if (record["SqlCounterDeltaScope"] == "SameActualInvocation"
                    and (record["SqlOverlap"] != "ContainedInActual"
                         or association(record["SqlWaitDeltaPreviousCapture"], window) != "ContainedInActual")):
                record["SqlCounterDeltaScope"] = "CrossInvocationOrAmbiguous"
        coverage = []
        for invocation in sorted(expected):
            captures = [record for record in self.records if record["InvocationId"] == invocation]
            stacks = sum(record["StackOverlap"] == "ContainedInActual" for record in captures)
            sql = sum(record["SqlOverlap"] == "ContainedInActual" for record in captures)
            coverage.append(dict(InvocationId=invocation, CaseId=self.case, ProcessId=self.process_id,
                                 ActualWindow=self.windows.get(invocation), CaptureAttempts=len(captures),
                                 InWindowStackCaptures=stacks, InWindowSqlCaptures=sql,
                                 Coverage="Covered" if stacks and sql else "InsufficientWindowSamples"))
        output = dict(SchemaVersion=2, ToolVersion=STACK_VERSION,
                      DiagnosticOnly=True, TimingComparableWithSampled=False,
                      CollectorsPrepared=prepared,
                      CoverageDefinition="Covered means at least one contained capture from each collector; "
                      "not complete temporal or thread coverage.",
                      Coverage="Failed" if self.error else
                      "Covered" if coverage and all(row["Coverage"] == "Covered" for row in coverage)
                      else "InsufficientWindowSamples",
                      InvocationCoverage=coverage,
                      Error=self.error, Snapshots=self.records)
        (self.directory / "observations.json").write_text(json.dumps(output, indent=2), encoding="utf-8")
        if self.error:
            raise RuntimeError(self.error)
