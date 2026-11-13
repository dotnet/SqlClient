#!/usr/bin/env python3
"""Fresh-process open health experiments. Credentials stay in the child environment, never artifacts."""

import argparse
import csv
from datetime import datetime, timezone
import getpass
import hashlib
import json
import math
import os
from pathlib import Path
import random
import statistics
import subprocess
import sys


def cases(mode):
    """Vary one dimension from the 64-worker V2 cold baseline, plus paired dispatch controls."""
    baseline = dict(Workers=64, PoolSize=100, MinimumThreads=16, MaximumThreads=32767,
                    DurationSeconds=8, ConnectTimeoutSeconds=3 if mode == "stalled" else 15,
                    PoolV2=True, LongRunning=False, Pooling=True, WarmPool=False,
                    Query=True, Burst=mode == "stalled", StalledPrelogin=mode == "stalled")
    variants = [
        ("w16", dict(Workers=16)), ("w32", dict(Workers=32)), ("w64", {}),
        ("w128", dict(Workers=128)), ("w256", dict(Workers=256)),
        ("w64-long", dict(LongRunning=True)), ("w256-long", dict(Workers=256, LongRunning=True)),
        ("w64-min64", dict(MinimumThreads=64)),
        ("w64-max16", dict(MaximumThreads=16)),
        ("w64-max32", dict(MaximumThreads=32)),
        ("w64-v1", dict(PoolV2=False)),
        ("w64-nonpooled", dict(Pooling=False)),
    ]
    if mode == "sql":
        variants += [
            ("w64-warm", dict(WarmPool=True)),
            ("w64-pool16", dict(PoolSize=16)),
            ("w256-pool16", dict(Workers=256, PoolSize=16)),
            ("w64-openonly", dict(Query=False)),
        ]
    return [(name, dict(baseline, **changes)) for name, changes in variants]


def open_path_cases(workers):
    """Balanced success-path coverage; a warm non-pooled driver still opens a new connection."""
    baseline = dict(cases("sql"))["w64"]
    result = []
    for count in workers:
        for mode in ("v1", "v2", "nonpooled"):
            pooled = mode != "nonpooled"
            states = ["cold", "initialized-cold", "warm", "warm-contended"] if pooled else [
                "cold", "initialized"]
            for state in states:
                if state == "warm-contended" and count == 1:
                    continue
                for query in (False, True):
                    operation = "query" if query else "open"
                    name = f"{mode}-{state}-{operation}-w{count}"
                    result.append((name, dict(
                        baseline, Workers=count, Pooling=pooled, PoolV2=mode == "v2",
                        PoolSize=max(1, count // 4) if state == "warm-contended" else max(100, count),
                        WarmPool=state in ("warm", "warm-contended"),
                        WarmDriver=state.startswith("initialized"), Query=query)))
    return result


def percentile(values, fraction):
    values = sorted(values)
    return values[max(0, math.ceil(len(values) * fraction) - 1)] if values else None


def summarize(data):
    """Keep completion-cohort tails separate from missing work and failed-open durations."""
    measurement = data["Measurement"]
    probes = measurement["Probes"]
    start, end = measurement["WorkloadStartMilliseconds"], measurement["WorkloadEndMilliseconds"]
    samples = [p for p in probes["Samples"] if start <= p["PostedMilliseconds"] <= end]
    delays = [p["ExecutedMilliseconds"] - p["PostedMilliseconds"] for p in samples
              if p["ExecutedMilliseconds"] is not None]
    cohorts = {}
    for p in samples:
        if p["ExecutedMilliseconds"] is not None and p["ExecutedMilliseconds"] <= end:
            bucket = int((p["ExecutedMilliseconds"] - start) / 1000)
            cohorts.setdefault(bucket, []).append(p["ExecutedMilliseconds"] - p["PostedMilliseconds"])
    resources = measurement["Resources"]
    operations = [o for o in data["Observations"] if o["EndMs"] <= data["CutoffMs"]]
    opens = [o["OpenEndMs"] - o["StartMs"] for o in data["Observations"]
             if o["OpenEndMs"] is not None and o["OpenEndMs"] <= data["CutoffMs"]]
    failed = [o["EndMs"] - o["StartMs"] for o in operations
              if o["Outcome"] == "Failed" and o["OpenEndMs"] is None]
    queue = [o["StartMs"] - o["OfferedMs"] for o in operations]
    missing = (probes["Dropped"] + probes["Rejected"] + probes["Skipped"] +
               probes["Outstanding"] + probes["ProducerMissed"])
    p99 = percentile(delays, .99)
    eligible = (data["Status"] == "Completed" and len(delays) >= 100 and not missing
                and not measurement["ResourceSamplesDropped"])
    first = {}
    for operation in data["Observations"]:
        first.setdefault(operation["Worker"], operation)
    row = dict(
        status=data["Status"], source=data["Source"], cpu=data["ProcessorCount"],
        platform=data.get("OS"), runtime=data.get("Runtime"),
        dispatch_p99_ms=p99, dispatch_max_ms=max(delays, default=None),
        worst_1s_completion_p99_ms=max((percentile(v, .99) for v in cohorts.values()), default=None),
        probe_samples=len(delays), missing_probes=missing,
        outstanding_at_end=measurement["OutstandingAtWorkloadEnd"],
        scheduler_health="Fail" if p99 is not None and p99 > 100 else
                         "Pass" if eligible else "Insufficient",
        tp_peak=max((r["Threads"] for r in resources), default=0),
        process_peak=max((r["ProcessThreads"] for r in resources), default=0),
        queued_peak=max((r["QueueDepth"] for r in resources), default=0),
        producer_p99_ms=percentile([p["PostedMilliseconds"] - p["ScheduledMilliseconds"]
                                    for p in samples], .99),
        open_success_p99_ms=percentile(opens, .99),
        first_open_success_p99_ms=percentile(
            [o["OpenEndMs"] - o["StartMs"] for o in first.values()
             if o["OpenEndMs"] is not None], .99)
             if len(first) == data["Settings"]["Workers"] else None,
        first_worker_samples=len(first),
        first_worker_samples_missing=data["Settings"]["Workers"] - len(first),
        first_acquisition_from_offer_p99_ms=percentile(
            [o["OpenEndMs"] - o["OfferedMs"] for o in first.values()
             if o["OpenEndMs"] is not None], .99)
             if len(first) == data["Settings"]["Workers"] else None,
        observations_dropped=data["ObservationsDropped"],
        open_failure_p99_ms=percentile(failed, .99),
        initial_or_yield_queue_p99_ms=percentile(queue, .99),
        completed_per_second=data["AtCutoff"]["Completed"] / (data["CutoffMs"] / 1000),
        failures=data["AtCutoff"]["Failed"], inflight_at_end=data["AtCutoff"]["InFlight"],
        unstarted_workers=data["AtCutoff"]["QueuedWorkers"],
        inline_opens=data.get("Final", {}).get("InlineOpens"),
        pending_opens=data.get("Final", {}).get("PendingOpens"),
    )
    return row


def healthy_sql(row):
    """A low successful-only percentile or an empty workload cannot satisfy the regression gate."""
    return (row["source"] == "AuthenticatedSql" and row["status"] == "Completed" and
            row["scheduler_health"] == "Pass" and row["failures"] == 0 and
            row["completed_per_second"] > 0 and row["unstarted_workers"] == 0 and
            row.get("exit_code", 0) == 0)


def compare_baseline(rows, baseline_rows, manifest, baseline_manifest):
    """Compare like-for-like success paths; a faster scheduler cannot hide lost useful work."""
    def contract(m):
        return (dict(m["cases"]), sorted(m["rtt"]), m["repetitions"], m["mode"])

    if contract(manifest) != contract(baseline_manifest) or baseline_manifest["status"] != "Completed":
        raise ValueError("Baseline settings, RTTs, mode and repetition count must match the candidate.")

    def index(values):
        expected = {(name, int(rtt), rep) for name, _ in manifest["cases"]
                    for rtt in manifest["rtt"] for rep in range(1, manifest["repetitions"] + 1)}
        actual = [(r["case"], int(r["rtt_ms"]), int(r["repetition"])) for r in values]
        if len(actual) != len(expected) or set(actual) != expected:
            raise ValueError("Baseline/candidate repetitions are missing or duplicated.")
        grouped = {}
        for row in values:
            grouped.setdefault((row["case"], int(row["rtt_ms"])), []).append(row)
        return grouped

    baseline, candidate = index(baseline_rows), index(rows)
    failures = []
    for key, group in candidate.items():
        previous = baseline[key]
        if {int(r["cpu"]) for r in group} != {int(r["cpu"]) for r in previous}:
            raise ValueError("Baseline/candidate CPU counts differ.")
        for field in ("platform", "runtime"):
            if (not all(r.get(field) for r in group + previous) or
                    {r[field] for r in group} != {r[field] for r in previous}):
                raise ValueError("Baseline/candidate platform and runtime must be known and match.")
        if any(r["source"] != "AuthenticatedSql" or r["status"] != "Completed" or
               int(r["exit_code"]) != 0 or int(r["failures"]) or int(r["unstarted_workers"]) or
               float(r["completed_per_second"]) <= 0 for r in previous):
            raise ValueError("Baseline must complete valid SQL work without unexpected failures.")
        if not all(healthy_sql(r) for r in group):
            failures.append(f"{key}: candidate scheduler coverage, completion or workload gate failed")
        rate = statistics.median(float(r["completed_per_second"]) for r in group)
        previous_rate = statistics.median(float(r["completed_per_second"]) for r in previous)
        if rate < previous_rate * .9:
            failures.append(f"{key}: median completed-work throughput regressed more than 10%")
        threads = max(float(r["process_peak"]) for r in group)
        previous_threads = max(float(r["process_peak"]) for r in previous)
        if threads > max(previous_threads * 1.25, previous_threads + 8):
            failures.append(f"{key}: peak process threads exceeded the 25% / 8-thread allowance")
    return failures


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--mode", choices=("stalled", "sql"), required=True)
    parser.add_argument("--repetitions", type=int, default=3)
    parser.add_argument("--duration-seconds", type=int,
                        help="Override observation duration (1..120); use > connect timeout to study timeouts.")
    parser.add_argument("--connect-timeout-seconds", type=int,
                        help="Override connection and query timeout (1..60).")
    parser.add_argument("--rtt", type=int, nargs="+", default=[0])
    parser.add_argument("--only", nargs="+")
    parser.add_argument("--suite", choices=("study", "open-paths"), default="study",
                        help="Study ladder or balanced V1/V2/non-pooled cold/warm success paths.")
    parser.add_argument("--workers", type=int, nargs="+",
                        help="Worker counts for --suite open-paths (default 64).")
    parser.add_argument("--native-sni", action="store_true",
                        help="Use native SNI on Windows; run separately from managed-SNI results.")
    parser.add_argument("--netem", action="store_true")
    parser.add_argument("--prompt-sql", action="store_true")
    parser.add_argument("--revision", default="unspecified")
    parser.add_argument("--require-healthy-sql", action="store_true",
                        help="Fail unless every SQL repetition meets the documented health budget.")
    parser.add_argument("--baseline-summary", type=Path,
                        help="Also gate matched throughput and process-thread cost against this CSV.")
    args = parser.parse_args()
    if args.duration_seconds is not None and not 1 <= args.duration_seconds <= 120:
        parser.error("Observation duration must be 1..120 seconds.")
    if args.connect_timeout_seconds is not None and not 1 <= args.connect_timeout_seconds <= 60:
        parser.error("Connection timeout must be 1..60 seconds.")
    if args.workers and (args.suite != "open-paths" or
                        any(w < 1 or w > 1024 for w in args.workers) or
                        len(set(args.workers)) != len(args.workers)):
        parser.error("--workers requires open-paths and unique counts in 1..1024.")
    if args.suite == "open-paths" and args.mode != "sql":
        parser.error("open-paths is a successful SQL suite; use study/stalled for negative controls.")
    if args.native_sni and sys.platform != "win32":
        parser.error("Native SNI is Windows-only.")
    if len(set(args.rtt)) != len(args.rtt):
        parser.error("RTT values must be unique.")
    if args.repetitions < 1 or any(r < 0 or r % 2 for r in args.rtt):
        parser.error("Use positive repetitions and nonnegative even RTT milliseconds.")
    if args.mode == "stalled" and (args.rtt != [0] or args.netem or args.prompt_sql):
        parser.error("Stalled prelogin is not an RTT simulation and never changes netem.")
    if args.require_healthy_sql and args.mode != "sql":
        parser.error("The healthy-SQL gate cannot be applied to negative controls.")
    if args.baseline_summary and args.mode != "sql":
        parser.error("Baseline comparison is supported only for successful SQL workloads.")
    if any(args.rtt) and not args.netem:
        parser.error("Nonzero RTT requires --netem.")
    if args.netem and sys.platform != "linux":
        parser.error("Netem requires Linux.")
    binary = args.binary.resolve(strict=True)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    env = dict(os.environ)
    if args.prompt_sql:
        if not sys.stdin.isatty():
            parser.error("Password input requires a private interactive terminal.")
        password = getpass.getpass("SQL sa password (not saved): ")
        if not password:
            parser.error("An empty password is not accepted.")
        env["SQLCLIENT_HEALTH_CONNECTION_STRING"] = (
            'Server=tcp:127.0.0.1,1433;Database=master;User ID=sa;Encrypt=true;'
            'TrustServerCertificate=true;Password="' + password.replace('"', '""') + '"')
        del password
    if args.mode == "sql" and not env.get("SQLCLIENT_HEALTH_CONNECTION_STRING"):
        parser.error("Set SQLCLIENT_HEALTH_CONNECTION_STRING privately or use --prompt-sql.")
    available = open_path_cases(args.workers or [64]) if args.suite == "open-paths" else cases(args.mode)
    selection = [(n, dict(c, UseManagedSni=not args.native_sni))
                 for n, c in available if not args.only or n in args.only]
    for _, config in selection:
        if args.duration_seconds is not None:
            config["DurationSeconds"] = args.duration_seconds
        if args.connect_timeout_seconds is not None:
            config["ConnectTimeoutSeconds"] = args.connect_timeout_seconds
    if not selection or (args.only and set(args.only) - {n for n, _ in selection}):
        parser.error("Unknown or empty case selection.")
    manifest = dict(status="Running", started=datetime.now(timezone.utc).isoformat(),
                    revision=args.revision, mode=args.mode, suite=args.suite, rtt=args.rtt,
                    repetitions=args.repetitions, seed=5014,
                    binary_sha256=hashlib.sha256(binary.read_bytes()).hexdigest(),
                    driver_sha256=hashlib.sha256(
                        (binary.parent / "Microsoft.Data.SqlClient.dll").read_bytes()).hexdigest(),
                    runtime=subprocess.check_output([args.dotnet, "--info"], text=True),
                    cases=selection, notes=[
                        "One fresh process per case/repetition; randomized order per RTT.",
                        "100ms p99 is an investigative scheduler budget, not a product SLA.",
                        "SyntheticStalledPrelogin is a negative control, not a SQL or RTT measurement.",
                        "SQL operations/SNI are recorded per case; PoolBlockingPeriod=NeverBlock.",
                        "WarmDriver initializes then empties the pool; non-pooled always connects physically.",
                    ])
    rows = []
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2))
    try:
        for rtt in args.rtt:
            controller = None
            try:
                if args.netem:
                    import netem
                    controller = netem.NetworkDelay(output / f"netem-{rtt}-state.json",
                                                    output / f"netem-{rtt}.json")
                    if any(q.get("root") and q["kind"] != "noqueue" for q in controller.queues()):
                        raise RuntimeError("An existing netem owner must exit before the sweep.")
                    controller.setup(rtt // 2, "127.0.0.1")
                jobs = [(rep, name, config) for rep in range(1, args.repetitions + 1)
                        for name, config in selection]
                random.Random(5014).shuffle(jobs)
                for rep, name, config in jobs:
                    stem = f"rtt{rtt}-{name}-r{rep}"
                    case = output / (stem + ".case.json")
                    result = output / (stem + ".result.json")
                    case.write_text(json.dumps(config, indent=2))
                    print(stem, flush=True)
                    with (output / (stem + ".log")).open("w") as log:
                        run = subprocess.run([args.dotnet, str(binary), "--openasync-health",
                                              str(case), str(result)], env=env, stdout=log,
                                             stderr=subprocess.STDOUT,
                                             timeout=180 + config["DurationSeconds"])
                    if not result.exists():
                        raise RuntimeError(f"No result: {stem}; inspect its log.")
                    data = json.loads(result.read_text())
                    row = dict(case=name, repetition=rep, rtt_ms=rtt, **summarize(data))
                    row["exit_code"] = run.returncode
                    rows.append(row)
                    with (output / "summary.csv").open("w", newline="") as target:
                        writer = csv.DictWriter(target, fieldnames=list(rows[0]))
                        writer.writeheader()
                        writer.writerows(rows)
            finally:
                if controller is not None:
                    controller.cleanup()
        manifest["status"] = "Completed"
    except BaseException as error:
        manifest["status"] = "Interrupted" if isinstance(error, KeyboardInterrupt) else "Failed"
        manifest["error_type"] = type(error).__name__
        raise
    finally:
        (output / "manifest.json").write_text(json.dumps(manifest, indent=2))
    if args.baseline_summary:
        with args.baseline_summary.open(newline="") as stream:
            baseline_rows = list(csv.DictReader(stream))
        baseline_manifest = json.loads(args.baseline_summary.with_name("manifest.json").read_text())
        failures = compare_baseline(rows, baseline_rows, manifest, baseline_manifest)
        (output / "comparison.json").write_text(json.dumps(
            dict(status="Fail" if failures else "Pass", failures=failures,
                 throughput_regression_percent=10, process_thread_regression_percent=25,
                 process_thread_absolute_allowance=8), indent=2))
        if failures:
            print("\n".join(failures), file=sys.stderr)
            return 1
    if args.require_healthy_sql and not all(healthy_sql(row) for row in rows):
        print("Health gate failed: one or more SQL repetitions failed or were insufficient.",
              file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
