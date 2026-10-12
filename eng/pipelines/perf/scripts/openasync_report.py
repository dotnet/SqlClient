# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Validation and per-invocation reporting for the preliminary OpenAsync profile."""

import glob
import json
import math
import os


PROFILE = "openasync-preliminary"
CASES = [f"OpenAsyncScoreboard-{scenario}-{api}-{concurrency}"
         for scenario in ("E1", "E2", "E3", "E4")
         for api in ("Sync", "Async") for concurrency in (1, 8, 32)]
NOTICE = ("Preliminary instrumented measurements, not phase-compliance evidence or "
          "proof of blocked threads. Percentiles describe individual opens within each "
          "invocation, never pooled invocations or batch means. No outliers discarded; "
          "no regression gate or best-of-N reruns.")
RESOURCE_FIELDS = (
    "CpuMilliseconds", "AllocatedBytes", "Gen0Collections", "Gen1Collections",
    "Gen2Collections", "ProcessThreadsBefore", "ProcessThreadsAfter",
    "ThreadPoolThreadsBefore", "ThreadPoolThreadsAfter", "PeakOutstandingOpens",
    "PeakOpenConnections",
)
IDENTITY_FIELDS = (
    "CaseId", "Scenario", "Api", "Concurrency", "OperationsPerWorker", "PoolSize",
    "HoldMilliseconds", "UseConnectionPoolV2", "Runtime", "OS", "DriverVersion",
    "DriverAssemblyVersion", "ProcessorCount",
)
OPTIONAL_METADATA = (
    "WorkloadVersion", "UseOptimizedAsyncBehaviour", "UseManagedSniOnWindows",
    "ConnectTimeoutSeconds", "ConnectRetryCount", "Enlist", "MeasurementStage",
    "IsInstrumented", "UnavailableDiagnostics",
)


def apply_profile(config, profile):
    """Modify only the opt-in profile; keep default configs untouched."""
    if profile == "default":
        return
    if profile != PROFILE:
        raise ValueError("Unknown benchmark profile")
    for benchmark in config["Benchmarks"].values():
        benchmark["Enabled"] = False
    config["Benchmarks"]["OpenAsyncScoreboardRunnerConfig"] = {
        "Enabled": True, "LaunchCount": 1, "IterationCount": 3, "InvocationCount": 1,
        "WarmupCount": 0, "RunStrategy": "Monitoring", "TimeoutMinutes": 5,
    }
    config["OpenAsyncScoreboard"] = {
        "Scenarios": ["E1", "E2", "E3", "E4"], "Apis": ["Sync", "Async"],
        "Concurrency": [1, 8, 32], "OperationsPerWorker": 16,
        "SaturatedPoolSize": 4, "HoldMilliseconds": 10,
    }


def number(value, field, integer=False, positive=False):
    if (isinstance(value, bool) or not isinstance(value, (int, float))
            or not math.isfinite(value) or value < 0
            or (integer and not isinstance(value, int)) or (positive and value == 0)):
        raise ValueError(f"Invalid {field}: expected finite nonnegative "
                         f"{'integer' if integer else 'number'}"
                         f"{' greater than zero' if positive else ''}")
    return value


def distribution(values):
    """Linear-interpolated percentiles; absent timings stay absent, never zero."""
    if not values:
        return None
    values = sorted(values)

    def percentile(p):
        index = (len(values) - 1) * p
        low = math.floor(index)
        high = math.ceil(index)
        return values[low] + (values[high] - values[low]) * (index - low)

    return {"Count": len(values), "Min": values[0], "P50": percentile(.50),
            "P95": percentile(.95), "P99": percentile(.99), "Max": values[-1]}


def summarize_invocation(data, expected_case):
    """Validate schema/accountability and return one row, retaining resource metadata."""
    required = set(IDENTITY_FIELDS + RESOURCE_FIELDS) | {
        "SchemaVersion", "Invocation", "ElapsedMilliseconds", "AdmissionMilliseconds", "Samples"}
    if (not isinstance(data, dict) or not required.issubset(data)
            or set(data) - required - set(OPTIONAL_METADATA)):
        raise ValueError("Supplemental schema fields do not match schema v1")
    if type(data["SchemaVersion"]) is not int or data["SchemaVersion"] != 1:
        raise ValueError("Unsupported supplemental SchemaVersion")
    if expected_case not in CASES or data["CaseId"] != expected_case:
        raise ValueError("Supplemental CaseId does not match selected case")
    _, scenario, api, concurrency = expected_case.split("-")
    concurrency = int(concurrency)
    if (data["Scenario"], data["Api"], data["Concurrency"]) != (scenario, api, concurrency):
        raise ValueError("Supplemental scenario/API/concurrency mismatch")
    for field in ("Concurrency", "OperationsPerWorker", "ProcessorCount"):
        number(data[field], field, integer=True, positive=True)
    number(data["PoolSize"], "PoolSize", integer=True)
    expected_pool = 0 if scenario == "E3" else 4 if scenario == "E2" else concurrency
    if data["PoolSize"] != expected_pool:
        raise ValueError("Unexpected pool size for preliminary scenario")
    operations = 1 if scenario == "E4" else 16
    if data["OperationsPerWorker"] != operations:
        raise ValueError("Unexpected operation count for preliminary scenario")
    number(data["HoldMilliseconds"], "HoldMilliseconds")
    if data["HoldMilliseconds"] != (10 if scenario == "E2" else 0):
        raise ValueError("Unexpected preliminary hold duration")
    if type(data["UseConnectionPoolV2"]) is not bool:
        raise ValueError("UseConnectionPoolV2 must be boolean")
    for field in ("Runtime", "OS", "DriverVersion", "DriverAssemblyVersion"):
        if not isinstance(data[field], str) or not data[field].strip():
            raise ValueError(f"Missing {field}")
    for field in ("WorkloadVersion", "ConnectTimeoutSeconds", "ConnectRetryCount"):
        if field in data:
            number(data[field], field, integer=True)
    for field in ("UseOptimizedAsyncBehaviour", "UseManagedSniOnWindows", "Enlist", "IsInstrumented"):
        if field in data and type(data[field]) is not bool:
            raise ValueError(f"{field} must be boolean")
    if "MeasurementStage" in data and data["MeasurementStage"] != "WorkloadActual":
        raise ValueError("Only measured workload invocations may be reported")
    if "IsInstrumented" in data and not data["IsInstrumented"]:
        raise ValueError("Preliminary profile must identify instrumented measurements")
    if "UnavailableDiagnostics" in data and (
            not isinstance(data["UnavailableDiagnostics"], list)
            or any(not isinstance(item, str) for item in data["UnavailableDiagnostics"])):
        raise ValueError("Invalid unavailable diagnostics metadata")
    number(data["Invocation"], "Invocation", integer=True)
    elapsed = number(data["ElapsedMilliseconds"], "ElapsedMilliseconds", positive=True)
    for field in RESOURCE_FIELDS:
        number(data[field], field, integer=field != "CpuMilliseconds")
    if data["PeakOutstandingOpens"] > concurrency or data["PeakOpenConnections"] > concurrency:
        raise ValueError("Peak concurrency exceeds worker count")
    if data["PoolSize"] and data["PeakOpenConnections"] > data["PoolSize"]:
        raise ValueError("Peak open connections exceed pool capacity")
    admission = data["AdmissionMilliseconds"]
    if not isinstance(admission, list) or len(admission) != concurrency:
        raise ValueError("Admission timings must account for every worker")
    for timing in admission:
        if number(timing, "AdmissionMilliseconds") > elapsed:
            raise ValueError("Admission timing exceeds invocation duration")
    samples = data["Samples"]
    if not isinstance(samples, list) or len(samples) != concurrency * operations:
        raise ValueError("Incomplete sample count")
    expected = {(worker, operation) for worker in range(concurrency)
                for operation in range(operations)}
    seen = set()
    latencies, returns = [], []
    outcomes = {"Success": 0, "Canceled": 0, "Failed": 0}
    completed_at_return = 0
    for sample in samples:
        if not isinstance(sample, dict) or set(sample) != {
            "Worker", "Operation", "StartMilliseconds", "TaskReturnMilliseconds",
            "CompletionMilliseconds", "CompletedAtReturn", "Outcome", "ErrorType", "SqlErrorNumber",
        }:
            raise ValueError("Sample fields do not match schema v1")
        key = (number(sample["Worker"], "Worker", integer=True),
               number(sample["Operation"], "Operation", integer=True))
        if key not in expected or key in seen:
            raise ValueError("Duplicate or unexpected worker/operation sample")
        seen.add(key)
        start = number(sample["StartMilliseconds"], "StartMilliseconds")
        completion = number(sample["CompletionMilliseconds"], "CompletionMilliseconds")
        if completion < start or completion > elapsed:
            raise ValueError("Invalid relative completion duration")
        outcome = sample["Outcome"]
        if outcome not in outcomes:
            raise ValueError("Unknown sample outcome")
        outcomes[outcome] += 1
        returned, completed = sample["TaskReturnMilliseconds"], sample["CompletedAtReturn"]
        if api == "Sync":
            if returned is not None or completed is not None:
                raise ValueError("Sync sample must not invent task-return timings")
        elif returned is not None:
            number(returned, "TaskReturnMilliseconds")
            if returned < start or returned > elapsed or type(completed) is not bool:
                raise ValueError("Invalid task-return timing/completion status")
            # An already completed task may be observed after completion; neither order is assumed.
            returns.append(returned - start)
            completed_at_return += int(completed)
        elif completed is not None or outcome == "Success":
            raise ValueError("Missing async task-return timing")
        if outcome == "Success":
            if sample["ErrorType"] is not None or sample["SqlErrorNumber"] is not None:
                raise ValueError("Successful sample contains an error")
        else:
            if not isinstance(sample["ErrorType"], str) or not sample["ErrorType"]:
                raise ValueError("Failed/canceled sample is missing ErrorType")
            if sample["SqlErrorNumber"] is not None and type(sample["SqlErrorNumber"]) is not int:
                raise ValueError("Invalid SqlErrorNumber")
        latencies.append(completion - start)
    row = {field: data[field] for field in IDENTITY_FIELDS + RESOURCE_FIELDS}
    row.update({field: data[field] for field in OPTIONAL_METADATA if field in data})
    row.update(Invocation=data["Invocation"], ElapsedMilliseconds=elapsed,
               SuccessThroughputPerSecond=outcomes["Success"] * 1000 / elapsed,
               OpenLatencyMilliseconds=distribution(latencies),
               TaskReturnMilliseconds=distribution(returns),
               Outcomes=outcomes, CompletedAtReturnCount=completed_at_return if api == "Async" else None,
               AdmissionMilliseconds=admission, AdmissionDistributionMilliseconds=distribution(admission))
    if scenario == "E4" and not (outcomes["Failed"] or outcomes["Canceled"]) and (
            data["PeakOpenConnections"] != concurrency):
        raise ValueError("Retained E4 cohort did not reach its configured concurrency")
    return row


def load_case(directory, case):
    paths = sorted(glob.glob(os.path.join(directory, "**", "openasync-*.json"), recursive=True))
    if len(paths) != 3:
        raise ValueError(f"{case}: expected exactly 3 measured invocation supplements, found {len(paths)}")
    rows = []
    for path in paths:
        with open(path, encoding="utf-8-sig") as handle:
            rows.append(summarize_invocation(json.load(handle), case))
    if len({row["Invocation"] for row in rows}) != 3:
        raise ValueError(f"{case}: duplicate invocation identifiers")
    identity = {field: rows[0].get(field) for field in IDENTITY_FIELDS + OPTIONAL_METADATA}
    if any(any(row.get(field) != value for field, value in identity.items()) for row in rows):
        raise ValueError(f"{case}: invocation metadata changed within case")
    return sorted(rows, key=lambda row: row["Invocation"])


def write_summary(results_dir, units, has_baseline):
    rows = []
    for variant in (("baseline", "current") if has_baseline else ("current",)):
        for case in units:
            for row in load_case(os.path.join(results_dir, variant, case), case):
                rows.append(dict(row, Variant=variant))
    report = {"SchemaVersion": 1, "BenchmarkProfile": PROFILE, "Notice": NOTICE, "Invocations": rows}
    with open(os.path.join(results_dir, "openasync-summary.json"), "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2, allow_nan=False)
    lines = ["# Preliminary OpenAsync scoreboard", "", NOTICE, "",
             "All durations below are milliseconds. Resources and full distributions are retained in JSON.", "",
             "| Variant / case / invocation | Success/s | Open p50 / p95 / p99 | "
             "Task return p50 / p95 / p99 | Success / canceled / failed | "
             "Admission p95 | Peak outstanding / open | CPU ms / bytes / GC 0,1,2 | "
             "Process threads before→after / pool threads before→after |",
             "| --- | ---: | --- | --- | --- | ---: | --- | --- | --- |"]
    for row in rows:
        def percentiles(value):
            return "N/A" if value is None else " / ".join(f"{value[p]:.4f}" for p in ("P50", "P95", "P99"))
        lines.append(
            f"| {row['Variant']} / {row['CaseId']} / {row['Invocation']} | "
            f"{row['SuccessThroughputPerSecond']:.2f} | {percentiles(row['OpenLatencyMilliseconds'])} | "
            f"{percentiles(row['TaskReturnMilliseconds'])} | "
            f"{row['Outcomes']['Success']} / {row['Outcomes']['Canceled']} / {row['Outcomes']['Failed']} | "
            f"{row['AdmissionDistributionMilliseconds']['P95']:.4f} | "
            f"{row['PeakOutstandingOpens']} / {row['PeakOpenConnections']} | "
            f"{row['CpuMilliseconds']:.2f} / {row['AllocatedBytes']} / "
            f"{row['Gen0Collections']},{row['Gen1Collections']},{row['Gen2Collections']} | "
            f"{row['ProcessThreadsBefore']}→{row['ProcessThreadsAfter']} / "
            f"{row['ThreadPoolThreadsBefore']}→{row['ThreadPoolThreadsAfter']} |")
    with open(os.path.join(results_dir, "openasync-summary.md"), "w", encoding="utf-8") as handle:
        handle.write("\n".join(lines) + "\n")
    if any(row["Outcomes"]["Failed"] or row["Outcomes"]["Canceled"] for row in rows):
        raise ValueError("OpenAsync run contains failed/canceled opens; see supplemental summary")
