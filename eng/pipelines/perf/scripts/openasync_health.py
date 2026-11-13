#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Matched fresh-process OpenAsync health phase for the existing perf runners."""

import argparse
from contextlib import contextmanager
import csv
import hashlib
import json
import math
import os
from pathlib import Path
import random
import re
import signal
import subprocess
import sys
import tempfile
import time


SCRIPTS = Path(__file__).resolve().parent
REPO = SCRIPTS.parents[3]
PERF = Path("src/Microsoft.Data.SqlClient/tests/PerformanceTests")
PROJECT = PERF / "OpenAsyncHealth/OpenAsyncHealth.csproj"
DRIVER = Path("src/Microsoft.Data.SqlClient/src/Microsoft.Data.SqlClient.csproj")
BUDGETS = {
    "absolute_dispatch_p99_ms": 100,
    "minimum_probes": 100,
    "latency_increase_percent": 25,
    "dispatch_allowance_ms": 25,
    "first_acquisition_allowance_ms": 50,
    "throughput_loss_percent": 10,
    "process_threads_increase_percent": 25,
    "process_threads_allowance": 8,
    "missing_probe_fraction_allowance": 0.05,
}

class EvidenceError(ValueError):
    """Safe, harness-authored diagnostic; never constructed from private configuration values."""


def cases(workers=64, duration=8, managed=True, optimized=True):
    """Twenty symmetric success paths; non-pooled initialization never claims a warm SQL pool."""
    result = []
    for mode in ("v1", "v2", "nonpooled"):
        pooled = mode != "nonpooled"
        states = ("cold", "initialized", "warm", "warm-contended") if pooled else (
            "cold", "initialized")
        for state in states:
            for query in (False, True):
                name = f"{mode}-{state}-{'query' if query else 'open'}"
                result.append((name, dict(
                    Workers=workers, PoolSize=workers // 4 if state == "warm-contended"
                    else max(100, workers), MinimumThreads=16, DurationSeconds=duration,
                    ConnectTimeoutSeconds=15, PoolV2=mode == "v2", Pooling=pooled,
                    WarmPool=state in ("warm", "warm-contended"),
                    WarmDriver=state == "initialized", UseManagedSni=managed,
                    UseOptimizedAsync=optimized, Query=query, StalledPrelogin=False)))
    return result


def write_json(path, value):
    """Atomically publish evidence, including partial runs."""
    temporary = path.with_suffix(path.suffix + ".tmp")
    temporary.write_text(json.dumps(value, indent=2, allow_nan=False) + "\n", encoding="utf-8")
    temporary.replace(path)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def percentile(values):
    values = sorted(values)
    return values[max(0, math.ceil(len(values) * .99) - 1)] if values else None


def number(value):
    if isinstance(value, bool) or not isinstance(value, (float, int)) or not math.isfinite(value):
        raise EvidenceError("Evidence contains a non-finite or nonnumeric measurement.")
    return value


def summarize(data, settings, exit_code):
    """Reject invalid evidence; label censored percentiles instead of manufacturing a pass."""
    if data["SchemaVersion"] != 1 or data["Settings"] != settings:
        raise EvidenceError("Harness schema/settings do not match the requested case.")
    if exit_code != 0:
        raise EvidenceError(f"Harness failed (exit {exit_code}); raw evidence retained.")
    if data["Status"] == "Unsupported":
        if not settings["PoolV2"] or data.get("Reason") != "PoolV2Unavailable":
            raise EvidenceError("Unexpected unsupported workload.")
        return dict(evidence="unsupported", absolute_health="Unmeasured")
    if (data["Status"] != "Completed" or not data["Drained"] or
            data["Source"] != "AuthenticatedSql"):
        raise EvidenceError("Incomplete or non-SQL workload cannot establish health.")

    window = data["Measurement"]
    probes = window["Probes"]
    if window["ObserverError"] or window["ResourceSamplesDropped"] or probes["Rejected"]:
        raise EvidenceError("Observer failed or exhausted retention.")
    start = number(window["WorkloadStartMilliseconds"])
    end = number(window["WorkloadEndMilliseconds"])
    cutoff = number(data["CutoffMs"])
    if (cutoff < settings["DurationSeconds"] * 1000 or
            cutoff > settings["DurationSeconds"] * 1000 + 2000 or end <= start):
        raise EvidenceError("Truncated or descheduled measurement window.")
    for key in ("Total", "Posted", "Executed", "Dropped", "Rejected", "Skipped",
                "ProducerMissed", "Outstanding"):
        if number(probes[key]) < 0:
            raise EvidenceError("Negative probe counter.")
    if (probes["Posted"] != probes["Executed"] + probes["Outstanding"] or
            probes["Total"] != probes["Posted"] + probes["Dropped"] + probes["Rejected"] +
            probes["Skipped"] + probes["ProducerMissed"] or
            len(probes["Samples"]) != probes["Posted"]):
        raise EvidenceError("Probe counters do not reconcile.")
    if probes["Skipped"]:
        raise EvidenceError("Probe retention exhausted.")

    delays, producer, outstanding = [], [], 0
    for probe in probes["Samples"]:
        posted = number(probe["PostedMilliseconds"])
        scheduled = number(probe["ScheduledMilliseconds"])
        executed = probe["ExecutedMilliseconds"]
        if posted < scheduled:
            raise EvidenceError("Probe time ran backwards.")
        if executed is None:
            outstanding += 1
        elif number(executed) < posted:
            raise EvidenceError("Probe completion preceded submission.")
        if start <= posted <= end:
            producer.append(posted - scheduled)
            if executed is not None:
                delays.append(executed - posted)
    if outstanding != probes["Outstanding"]:
        raise EvidenceError("Outstanding probe count does not match raw samples.")

    counts, final = data["AtCutoff"], data["Final"]
    for counters in (counts, final):
        for field in ("Started", "Opened", "Completed", "Failed", "Cancelled", "InFlight",
                      "WorkersStarted", "InlineOpens", "PendingOpens"):
            if number(counters[field]) < 0:
                raise EvidenceError("Negative work counter.")
        if counters["Started"] != sum(counters[k] for k in (
                "Completed", "Failed", "Cancelled", "InFlight")):
            raise EvidenceError("Work counters do not reconcile.")
    if (counts["Completed"] <= 0 or counts["Failed"] or final["Failed"] or counts["Cancelled"] or
            final["InFlight"] or counts["WorkersStarted"] != settings["Workers"] or data["Errors"]):
        raise EvidenceError("Failed, empty, unstarted or undrained SQL work; inspect raw outcomes.")
    for key in ("Started", "Opened", "Completed", "Failed", "Cancelled"):
        if final[key] < counts[key]:
            raise EvidenceError("Final work counters preceded the cutoff snapshot.")
    if final["Opened"] < final["Completed"] or final["Opened"] > final["Started"]:
        raise EvidenceError("Open and completion counts do not reconcile.")

    first = {}
    for operation in data["Observations"]:
        worker = operation["Worker"]
        if worker not in range(settings["Workers"]):
            raise EvidenceError("Invalid worker identity.")
        offered, begin, finish = (number(operation[k]) for k in ("OfferedMs", "StartMs", "EndMs"))
        opened = operation["OpenEndMs"]
        if not 0 <= offered <= begin <= finish:
            raise EvidenceError("Operation time ran backwards.")
        if opened is not None and not begin <= number(opened) <= finish:
            raise EvidenceError("Invalid open completion time.")
        if operation["Outcome"] not in ("Completed", "Failed", "Cancelled"):
            raise EvidenceError("Unknown operation outcome.")
        if operation["Outcome"] == "Completed" and opened is None:
            raise EvidenceError("Completed work has no successful open.")
        first.setdefault(worker, operation)
    dropped = number(data["ObservationsDropped"])
    if dropped < 0 or len(data["Observations"]) + dropped != sum(
            final[k] for k in ("Completed", "Failed", "Cancelled")):
        raise EvidenceError("Operation retention counters do not reconcile.")
    if len(first) != settings["Workers"]:
        raise EvidenceError("Missing first-worker outcome, despite completed drain.")

    resources = window["Resources"]
    if not resources:
        raise EvidenceError("Missing process resource observations.")
    for resource in resources:
        for key in ("TimestampMilliseconds", "Threads", "ProcessThreads", "QueueDepth",
                    "AllocatedBytes", "Gen2Collections"):
            if number(resource[key]) < 0:
                raise EvidenceError("Negative resource measurement.")

    # If the producer stops getting CPU before the window ends, its next loop never records
    # those missed slots. They remain missing evidence, not a magically smaller denominator.
    expected_slots = math.floor((end - start) / 25)
    unobserved_slots = max(0, expected_slots - probes["Total"])
    missing = (probes["Dropped"] + probes["Outstanding"] + probes["ProducerMissed"] +
               unobserved_slots)
    first_complete = [o for o in first.values()
                      if o["Outcome"] == "Completed" and o["EndMs"] <= cutoff]
    censored = bool(missing or len(delays) < BUDGETS["minimum_probes"] or
                    len(first_complete) != settings["Workers"])
    p99 = percentile(delays)
    return dict(
        evidence="censored" if censored else "complete",
        absolute_health="Fail" if p99 is not None and p99 > BUDGETS["absolute_dispatch_p99_ms"]
        else "Insufficient" if censored else "Pass",
        dispatch_p99_ms=p99, producer_p99_ms=percentile(producer),
        first_acquisition_p99_ms=percentile(
            [o["OpenEndMs"] - o["OfferedMs"] for o in first_complete]),
        first_queue_p99_ms=percentile([o["StartMs"] - o["OfferedMs"] for o in first.values()]),
        first_incomplete=settings["Workers"] - len(first_complete),
        probes=len(delays), missing_probes=missing,
        missing_fraction=missing / max(1, probes["Total"], expected_slots),
        throughput=counts["Completed"] * 1000 / cutoff,
        process_threads=max(r["ProcessThreads"] for r in resources),
        threadpool_threads=max(r["Threads"] for r in resources),
        observations_dropped=dropped, cancelled=final["Cancelled"],
        inflight_at_cutoff=counts["InFlight"],
        runtime=data["Runtime"], platform=data["OS"], cpu=data["ProcessorCount"],
        maximum_threads=data["MaximumThreads"], driver=data["DriverVersion"],
        driver_sha256=data["DriverSha256"])


def compare_pair(baseline, current):
    """A complete matched baseline, not an absolute budget, decides latency regressions."""
    if current["evidence"] == "unsupported":
        raise EvidenceError("Candidate cannot execute the required production-path suite.")
    if baseline is None or baseline["evidence"] == "unsupported":
        return dict(status="uncompared", regressions=[], improvements=[])
    for field in ("runtime", "platform", "cpu", "maximum_threads"):
        if current[field] != baseline[field]:
            raise EvidenceError(f"Candidate/baseline {field} differs.")

    regressions, improvements = [], []
    checks = [
        ("throughput", 1 + BUDGETS["throughput_loss_percent"] / 100, 0, True),
        ("process_threads", 1 + BUDGETS["process_threads_increase_percent"] / 100,
         BUDGETS["process_threads_allowance"], False),
    ]
    if baseline["evidence"] == current["evidence"] == "complete":
        checks += [
            ("dispatch_p99_ms", 1 + BUDGETS["latency_increase_percent"] / 100,
             BUDGETS["dispatch_allowance_ms"], False),
            ("first_acquisition_p99_ms", 1 + BUDGETS["latency_increase_percent"] / 100,
             BUDGETS["first_acquisition_allowance_ms"], False),
        ]
    for field, factor, allowance, higher_is_better in checks:
        before, after = baseline[field], current[field]
        if higher_is_better:
            if after < before * (1 - BUDGETS["throughput_loss_percent"] / 100):
                regressions.append(field)
            if before < after * (1 - BUDGETS["throughput_loss_percent"] / 100):
                improvements.append(field)
        else:
            if after > max(before * factor, before + allowance):
                regressions.append(field)
            if before > max(after * factor, after + allowance):
                improvements.append(field)
    if (current["missing_fraction"] >
            baseline["missing_fraction"] + BUDGETS["missing_probe_fraction_allowance"] or
            current["first_incomplete"] > baseline["first_incomplete"] or
            (baseline["evidence"] == "complete" and current["evidence"] == "censored")):
        regressions.append("coverage")
    if baseline["evidence"] == "censored" and current["evidence"] == "complete":
        improvements.append("coverage")
    status = "regression" if regressions else "improvement" if improvements else "unchanged"
    if not regressions and baseline["evidence"] == current["evidence"] == "censored":
        status = "inconclusive-baseline-debt"
    elif not regressions and baseline["absolute_health"] != "Pass":
        status = "improvement" if current["absolute_health"] == "Pass" else "baseline-debt"
    return dict(status=status, regressions=regressions, improvements=improvements)


def aggregate(pairs, repetitions):
    """Confirm the SAME metric in a strict majority; never let unrelated noisy metrics vote."""
    expected = set(range(repetitions))
    if {p["repetition"] for p in pairs} != expected or len(pairs) != repetitions:
        raise EvidenceError("Missing or duplicated matched repetitions.")
    metrics = sorted({metric for pair in pairs for metric in pair["regressions"]})
    confirmed = [metric for metric in metrics if sum(
        metric in p["regressions"] for p in pairs) > repetitions // 2]
    if confirmed:
        status = "regression"
    elif any(p["status"] == "uncompared" for p in pairs):
        status = "uncompared"
    elif any(p["status"] == "inconclusive-baseline-debt" for p in pairs):
        status = "inconclusive-baseline-debt"
    elif any(p["status"] == "baseline-debt" for p in pairs):
        status = "baseline-debt"
    elif metrics:
        status = "unconfirmed-noise"
    elif all(p["status"] == "improvement" for p in pairs):
        status = "improvement"
    else:
        status = "unchanged"
    return dict(status=status, confirmed_metrics=confirmed, pairs=pairs)


def build_harness(repo, destination, *, driver_root=None, package=None, timeout=600):
    """Compile candidate harness sources against each driver, even if the baseline predates it."""
    command = ["dotnet", "build", str(repo / PROJECT), "-c", "Release", "--nologo",
               "-v", "minimal", "-o", str(destination), "-p:ReferenceType=Project"]
    if package:
        command += [f"-p:HealthPackageVersion={package}",
                    "-p:WarningsNotAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904"]
    else:
        project = (driver_root or repo) / DRIVER
        if not project.is_file():
            raise EvidenceError("Source baseline lacks the unified driver project.")
        command += [f"-p:HealthDriverProject={project}"]
    subprocess.run(command, cwd=repo, check=True, timeout=timeout)
    binary = destination / "OpenAsyncHealth.dll"
    if not binary.is_file():
        raise EvidenceError("Health harness build produced no executable.")
    return binary


@contextmanager
def affinity(spec):
    """Pin the parent before process creation: the CLR must see the final CPU set at startup."""
    if not spec:
        yield
        return
    if not re.fullmatch(r"\d+(?:-\d+)?(?:,\d+(?:-\d+)?)*", spec):
        raise EvidenceError("Invalid health CPU set.")
    cpus = set()
    for part in spec.split(","):
        bounds = [int(n) for n in part.split("-")]
        if bounds[0] > bounds[-1] or bounds[-1] > 4095:
            raise EvidenceError("Invalid health CPU range.")
        cpus.update(range(bounds[0], bounds[-1] + 1))
    if sys.platform == "linux":
        previous = os.sched_getaffinity(0)
        os.sched_setaffinity(0, cpus)
        try:
            if os.sched_getaffinity(0) != cpus:
                raise EvidenceError("Requested health CPU affinity was not applied.")
            yield
        finally:
            os.sched_setaffinity(0, previous)
    elif os.name == "nt":
        import ctypes
        from ctypes import wintypes
        kernel = ctypes.WinDLL("kernel32", use_last_error=True)
        kernel.GetCurrentProcess.restype = wintypes.HANDLE
        get = kernel.GetProcessAffinityMask
        get.argtypes = [wintypes.HANDLE, ctypes.POINTER(ctypes.c_size_t),
                        ctypes.POINTER(ctypes.c_size_t)]
        set_mask = kernel.SetProcessAffinityMask
        set_mask.argtypes = [wintypes.HANDLE, ctypes.c_size_t]
        process = kernel.GetCurrentProcess()
        previous, system = ctypes.c_size_t(), ctypes.c_size_t()
        if max(cpus) >= ctypes.sizeof(ctypes.c_size_t) * 8:
            raise EvidenceError("Health affinity cannot span Windows processor groups.")
        if not get(process, ctypes.byref(previous), ctypes.byref(system)):
            raise ctypes.WinError(ctypes.get_last_error())
        if not set_mask(process, sum(1 << cpu for cpu in cpus)):
            raise ctypes.WinError(ctypes.get_last_error())
        try:
            yield
        finally:
            if not set_mask(process, previous):
                raise ctypes.WinError(ctypes.get_last_error())
    else:
        raise EvidenceError("CPU affinity unsupported on this platform.")


def run_case(binary, settings, folder, env, timeout):
    """Keep raw data/logs on every failure; kill a timed-out child before returning."""
    folder.mkdir(parents=True, exist_ok=False)
    case, result = folder / "case.json", folder / "result.json"
    write_json(case, settings)
    with (folder / "run.log").open("w", encoding="utf-8") as log:
        proc = subprocess.Popen(["dotnet", str(binary), "--openasync-health", str(case),
                                 str(result)], env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            code = proc.wait(timeout=timeout)
        finally:
            if proc.poll() is None:
                proc.kill()
                proc.wait(timeout=30)
    if not result.is_file():
        raise EvidenceError(f"No result in {folder.name}; inspect its run.log.")
    return summarize(json.loads(result.read_text(encoding="utf-8")), settings, code)


def execute(binaries, selection, output, env, repetitions, rtts, server, budget_seconds=1800):
    """Interleave matched pairs, rotating first side and case order across repetitions."""
    rows, pairs = [], {}
    deadline = time.monotonic() + budget_seconds
    for rtt in rtts:
        controller = None
        try:
            if rtt:
                import netem
                controller = netem.NetworkDelay(output / f"netem-{rtt}-state.json",
                                                output / f"netem-{rtt}.json")
                controller.setup(rtt // 2, server)
            for repetition in range(repetitions):
                order = selection.copy()
                random.Random(5014 + repetition).shuffle(order)
                for name, settings in order:
                    sides = list(binaries)
                    if repetition % 2:
                        sides.reverse()
                    matched = {}
                    for side in sides:
                        remaining = deadline - time.monotonic()
                        if remaining <= 0:
                            raise TimeoutError("OpenAsync health phase exceeded its 30-minute budget.")
                        stem = f"rtt{rtt}-{name}-r{repetition}-{side}"
                        print(f"OpenAsync health: {stem}", flush=True)
                        row = run_case(binaries[side], settings, output / stem, env,
                                       min(180, remaining))
                        matched[side] = row
                        rows.append(dict(case=name, rtt=rtt, repetition=repetition, side=side, **row))
                        write_json(output / "rows.json", rows)
                    comparison = compare_pair(matched.get("baseline"), matched["current"])
                    pairs.setdefault(f"rtt{rtt}-{name}", []).append(dict(
                        repetition=repetition, **comparison))
        finally:
            if controller is not None:
                controller.cleanup()
    return rows, {name: aggregate(values, repetitions) for name, values in pairs.items()}


def report(output, rows, results):
    """Publish machine-readable gates and a top-level ADO summary without credentials."""
    write_json(output / "comparison.json", dict(budgets=BUDGETS, cases=results))
    fields = sorted({key for row in rows for key in row})
    with (output / "summary.csv").open("w", newline="", encoding="utf-8") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        writer.writerows(rows)
    lines = ["# OpenAsync health", "",
             "Matched regressions are separate from the 100 ms absolute scheduler budget.",
             "Baseline debt and censored comparisons are **not health passes**.", "",
             "| Case | Matched finding | Confirmed regressions | Candidate absolute health |",
             "| --- | --- | --- | --- |"]
    for name, result in sorted(results.items()):
        states = sorted({r["absolute_health"] for r in rows
                         if f"rtt{r['rtt']}-{r['case']}" == name and r["side"] == "current"})
        lines.append(f"| {name} | {result['status']} | "
                     f"{', '.join(result['confirmed_metrics']) or '-'} | {', '.join(states)} |")
    lines += ["", "Raw case settings, outcomes, probes, resource samples, logs, hashes and "
              "comparison details are in the `openasync-health` artifact subfolder.", ""]
    (output.parent / "openasync-health.md").write_text("\n".join(lines), encoding="utf-8")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=REPO)
    parser.add_argument("--results-dir", type=Path, required=True)
    baseline = parser.add_mutually_exclusive_group()
    baseline.add_argument("--baseline-source-root", type=Path)
    baseline.add_argument("--baseline-package-version")
    parser.add_argument("--baseline-label", default="")
    parser.add_argument("--runner-config", type=Path, required=True)
    parser.add_argument("--client-cpus", default="")
    parser.add_argument("--rtt", nargs="+", type=int, default=[0])
    parser.add_argument("--server", default="localhost")
    args = parser.parse_args(argv)
    if (len(set(args.rtt)) != len(args.rtt) or
            any(r < 0 or r > 200 or r % 2 for r in args.rtt) or
            (any(args.rtt) and sys.platform != "linux")):
        parser.error("RTT must be unique even milliseconds in 0..200; nonzero requires Linux.")

    output = args.results_dir.resolve() / "openasync-health"
    output.mkdir(parents=True, exist_ok=False)
    deadline = time.monotonic() + 1800
    manifest = dict(schema=1, status="Running", baseline=args.baseline_label,
                    rtt=args.rtt, repetitions=3, budgets=BUDGETS, client_cpus=args.client_cpus,
                    harness_sources={}, builds={})
    write_json(output / "manifest.json", manifest)
    previous_signal = signal.getsignal(signal.SIGTERM)

    def interrupted(signum, frame):
        raise InterruptedError("Health phase terminated.")

    signal.signal(signal.SIGTERM, interrupted)
    try:
        repo = args.repo.resolve()
        config = json.loads(args.runner_config.read_text(encoding="utf-8-sig"))
        connection = config["ConnectionString"]
        if not connection or connection == "<redacted>":
            raise EvidenceError("Private runner configuration has no connection string.")
        env = dict(os.environ)
        env["SQLCLIENT_HEALTH_CONNECTION_STRING"] = connection
        for key in ("SQL_PASSWORD", "RUNNER_CONFIG", "PERF_LIST_BENCHMARKS", "PERF_BENCHMARK"):
            env.pop(key, None)
        selection = cases(managed=sys.platform != "win32" or config["UseManagedSniOnWindows"],
                          optimized=config["UseOptimizedAsyncBehaviour"])
        manifest["cases"] = selection
        for pattern in ("Measurements/OpenAsyncHealth*.cs", "Measurements/ThreadPool*.cs",
                        "OpenAsyncHealth/*.cs", "OpenAsyncHealth/*.csproj"):
            for path in sorted((repo / PERF).glob(pattern)):
                manifest["harness_sources"][str(path.relative_to(repo))] = sha256(path)
        with tempfile.TemporaryDirectory(prefix="openasync-health-build-") as temporary:
            builds = Path(temporary)
            binaries = {}
            def build_timeout():
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise TimeoutError("OpenAsync health exceeded its total 30-minute budget.")
                return min(600, remaining)

            if args.baseline_source_root or args.baseline_package_version:
                binaries["baseline"] = build_harness(
                    repo, builds / "baseline", driver_root=args.baseline_source_root,
                    package=args.baseline_package_version, timeout=build_timeout())
            binaries["current"] = build_harness(repo, builds / "current", timeout=build_timeout())
            for side, binary in binaries.items():
                manifest["builds"][side] = dict(
                    harness_sha256=sha256(binary),
                    application_root_driver_sha256=sha256(
                        binary.with_name("Microsoft.Data.SqlClient.dll")))
            write_json(output / "manifest.json", manifest)
            with affinity(args.client_cpus):
                rows, results = execute(binaries, selection, output, env, 3, args.rtt, args.server,
                                        budget_seconds=max(0, deadline - time.monotonic()))
        report(output, rows, results)
        regressions = sum(r["status"] == "regression" for r in results.values())
        manifest["status"] = "Regression" if regressions else "Completed"
        manifest["findings"] = {status: sum(r["status"] == status for r in results.values())
                                for status in sorted({r["status"] for r in results.values()})}
        print(f"OpenAsync health matched findings: {manifest['findings']}", flush=True)
        return 1 if regressions else 0
    except (Exception, KeyboardInterrupt) as error:
        manifest["status"] = "InvalidEvidence"
        manifest["error_type"] = type(error).__name__
        if isinstance(error, EvidenceError):
            manifest["reason"] = str(error)
        # Never dump exception text: a JSON/config/build exception may contain private values.
        print(f"ERROR: OpenAsync health {type(error).__name__}; evidence retained in {output}",
              file=sys.stderr)
        if isinstance(error, EvidenceError):
            print(f"ERROR: {error}", file=sys.stderr)
        (output.parent / "openasync-health.md").write_text(
            "# OpenAsync health\n\n**Invalid evidence: no health pass was established.**\n\n"
            "Inspect openasync-health/manifest.json and per-case logs/results.\n", encoding="utf-8")
        return 2
    finally:
        signal.signal(signal.SIGTERM, previous_signal)
        write_json(output / "manifest.json", manifest)


if __name__ == "__main__":
    sys.exit(main())
