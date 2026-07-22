#!/usr/bin/env python3
"""Translate BenchmarkDotNet "full" JSON reports into Kusto perf-results rows.

Implements the mapping documented on the InternalDriverTools wiki, page 270
("Performance Results Database Specification") and the SqlClient conversion
subpage.  Produces two newline-delimited JSON (NDJSON) files ready for Kusto
ingestion:

  * PerfRun            - one row describing this run (baseline OR current).
  * PerfBenchmarkResult - one row per benchmark method/parameter combination.

All benchmark timings in BenchmarkDotNet are expressed in NANOSECONDS; the
schema stores milliseconds, so every time value is divided by 1,000,000.

Only the Python standard library is required.
"""

import argparse
import datetime
import glob
import json
import os
import sys


NS_PER_MS = 1_000_000.0


def _utcnow_iso():
    return datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%fZ")


def _branch_category(branch_name):
    """Map a git ref to one of the schema's BranchCategory buckets."""
    if not branch_name:
        return "other"
    name = branch_name
    for prefix in ("refs/heads/", "refs/"):
        if name.startswith(prefix):
            name = name[len(prefix):]
            break
    if name.startswith("pull/") or branch_name.startswith("refs/pull/"):
        return "pull_request"
    if name == "main" or name == "master":
        return "main"
    if name.startswith("release/"):
        return "release"
    if name.startswith("dev/"):
        return "dev"
    if name.startswith("feat/") or name.startswith("feature/"):
        return "feature"
    return "other"


def _parse_parameters(parameters):
    """Parse BenchmarkDotNet's 'Parameters' string into a structured bag.

    Example input: "RowCount=1000, UseAsync=True"
    """
    bag = {}
    if not parameters:
        return bag
    for part in parameters.split(","):
        part = part.strip()
        if not part or "=" not in part:
            continue
        key, _, value = part.partition("=")
        bag[key.strip()] = value.strip()
    return bag


def _driver_specific_metrics(bench):
    metrics = {}
    memory = bench.get("Memory") or {}
    for field in ("Gen0Collections", "Gen1Collections", "Gen2Collections",
                  "TotalOperations", "BytesAllocatedPerOperation"):
        if field in memory and memory[field] is not None:
            metrics[field] = memory[field]
    for metric in bench.get("Metrics", []) or []:
        descriptor = metric.get("Descriptor") or {}
        legend = descriptor.get("Legend") or descriptor.get("Id")
        if legend is not None and metric.get("Value") is not None:
            metrics[legend] = metric["Value"]
    return metrics


def _percentile(stats, name):
    pct = stats.get("Percentiles") or {}
    value = pct.get(name)
    return (value / NS_PER_MS) if value is not None else None


def translate(input_dir, ctx):
    """Return (run_row, [result_rows]) for every full-report JSON in input_dir."""
    derived_run_id = "{driver}|{commit}|{pipeline}".format(
        driver=ctx["driver_name"],
        commit=ctx["commit_hash"],
        pipeline=ctx["pipeline_run_id"],
    )
    now = _utcnow_iso()

    run_row = {
        "DerivedRunId": derived_run_id,
        "DriverName": ctx["driver_name"],
        "MachineName": ctx["machine_name"],
        "AgentName": ctx["agent_name"],
        "PipelineRunId": ctx["pipeline_run_id"],
        "BuildUrl": ctx["build_url"],
        "RunDate": ctx["run_date"] or now,
        "BranchName": ctx["branch_name"],
        "BranchCategory": _branch_category(ctx["branch_name"]),
        "VersionString": ctx["version_string"],
        "CommitHash": ctx["commit_hash"],
        "CommitDate": ctx["commit_date"] or None,
        "IsComparableBase": bool(ctx["is_comparable_base"]),
        "IngestedAt": now,
    }

    result_rows = []
    runtime_seen = None
    platform_seen = None

    pattern = os.path.join(input_dir, "**", "*-report-full.json")
    for path in sorted(glob.glob(pattern, recursive=True)):
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                data = json.load(handle)
        except (OSError, ValueError) as exc:
            print(f"WARNING: could not parse {path}: {exc}", file=sys.stderr)
            continue

        host = data.get("HostEnvironmentInfo") or {}
        runtime = host.get("RuntimeVersion")
        architecture = host.get("Architecture")
        runtime_seen = runtime_seen or runtime
        platform_seen = platform_seen or architecture

        for bench in data.get("Benchmarks", []):
            stats = bench.get("Statistics") or {}
            mean_ns = stats.get("Mean")
            if mean_ns is None:
                continue
            mean_ms = mean_ns / NS_PER_MS

            btype = bench.get("Type", "")
            method = bench.get("Method", "")
            params = bench.get("Parameters", "") or ""
            memory = bench.get("Memory") or {}

            benchmark_id = "{run}|{name}|{method}|{sig}".format(
                run=derived_run_id, name=btype, method=method, sig=params)

            result_rows.append({
                "BenchmarkId": benchmark_id,
                "DerivedRunId": derived_run_id,
                "BenchmarkName": btype,
                "MethodName": method,
                "ParameterSignature": params,
                "ParameterBag": _parse_parameters(params),
                "JobName": (bench.get("Job") or bench.get("JobConfig") or ""),
                "MeanMs": mean_ms,
                "P50Ms": _percentile(stats, "P50"),
                "P95Ms": _percentile(stats, "P95"),
                "P99Ms": _percentile(stats, "P99"),
                "ThroughputOpsPerSec": (1000.0 / mean_ms) if mean_ms else None,
                "ErrorMs": (stats.get("StandardError") / NS_PER_MS)
                           if stats.get("StandardError") is not None else None,
                "StdDevMs": (stats.get("StandardDeviation") / NS_PER_MS)
                            if stats.get("StandardDeviation") is not None else None,
                "MemoryAllocatedBytes": memory.get("BytesAllocatedPerOperation"),
                "Runtime": runtime,
                "Platform": architecture,
                "DriverSpecificMetrics": _driver_specific_metrics(bench),
                "IngestedAt": now,
            })

    return run_row, result_rows


def _write_ndjson(path, rows):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        for row in rows:
            handle.write(json.dumps(row, separators=(",", ":")) + "\n")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input-dir", required=True,
                        help="Directory containing *-report-full.json for one pass.")
    parser.add_argument("--out-run", required=True, help="PerfRun NDJSON output path.")
    parser.add_argument("--out-results", required=True,
                        help="PerfBenchmarkResult NDJSON output path.")
    parser.add_argument("--driver-name", default="Microsoft.Data.SqlClient")
    parser.add_argument("--machine-name", default="")
    parser.add_argument("--agent-name", default="")
    parser.add_argument("--pipeline-run-id", required=True)
    parser.add_argument("--build-url", default="")
    parser.add_argument("--run-date", default="")
    parser.add_argument("--branch-name", default="")
    parser.add_argument("--version-string", default="")
    parser.add_argument("--commit-hash", required=True)
    parser.add_argument("--commit-date", default="")
    parser.add_argument("--is-comparable-base", default="false")
    args = parser.parse_args(argv)

    ctx = {
        "driver_name": args.driver_name,
        "machine_name": args.machine_name,
        "agent_name": args.agent_name,
        "pipeline_run_id": args.pipeline_run_id,
        "build_url": args.build_url,
        "run_date": args.run_date,
        "branch_name": args.branch_name,
        "version_string": args.version_string,
        "commit_hash": args.commit_hash,
        "commit_date": args.commit_date,
        "is_comparable_base": str(args.is_comparable_base).lower() in ("1", "true", "yes"),
    }

    run_row, result_rows = translate(args.input_dir, ctx)
    _write_ndjson(args.out_run, [run_row])
    _write_ndjson(args.out_results, result_rows)

    print(f"Wrote 1 PerfRun row -> {args.out_run}")
    print(f"Wrote {len(result_rows)} PerfBenchmarkResult row(s) -> {args.out_results}")
    if not result_rows:
        print("WARNING: no benchmark results were translated.", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
