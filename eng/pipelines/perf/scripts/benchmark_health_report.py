#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Compare worst-observed OpenAsyncBurst health metrics, retaining raw cohort provenance."""

import argparse
from collections import Counter, defaultdict
import json
import math
from pathlib import Path
import re

import compare_perf


METRICS = {
    "DispatchP99Ms": "Dispatch p99 (ms)",
    "FirstWorkerQueueP99Ms": "First worker queue p99 (ms)",
    "ThreadPoolThreadsPeak": "Peak ThreadPool threads",
    "ProcessThreadsPeak": "Peak process threads",
}
PHASES = {"Startup", "ExtraIteration"}
CONFIG_FIELDS = {
    "UseConnectionPoolV2", "UseManagedSni", "UseOptimizedAsync", "Runtime", "OS",
    "ProcessorCount", "MinimumWorkerThreads", "MaximumWorkerThreads",
    "DiagnosticRunMode", "FirstBenchmarkInProcess", "ProcessId", "DriverVersion", "DriverSha256",
}
PROCESS_FIELDS = {"ProcessId", "DriverVersion", "DriverSha256"}
OUTCOME_FIELDS = (
    "Failed", "Cancelled", "OpenFailures", "Queued", "InFlight",
    "FirstWorkerOutcomesMissing", "ObservationsDropped",
)


def _configuration(data):
    """Match recorded workload/settings, allowing process IDs and driver identity to differ."""
    config = data.get("Configuration")
    if not isinstance(config, dict) or any(config.get(key) is None for key in CONFIG_FIELDS):
        raise ValueError(f"Missing BDN matching configuration: {data['CaseId']}")
    result = {key: value for key, value in config.items() if key not in PROCESS_FIELDS}
    for key in ("ConfiguredInvocationsPerIteration", "OperationsPerInvoke"):
        value = data.get(key)
        if type(value) is not int or value <= 0:
            raise ValueError(f"Invalid BDN workload configuration: {key}")
        result[key] = value
    return json.dumps(result, sort_keys=True, separators=(",", ":"))


def _origin(path):
    """Read side and optional repetition from either canonical or copied artifact layouts."""
    sides = [part for part in path.parts[:-1] if part in ("baseline", "current")]
    repetitions = {int(match[1]) for part in path.parts[:-1]
                   if (match := re.fullmatch(r"rep([1-9][0-9]*)", part))}
    if len(sides) != 1 or len(repetitions) > 1:
        raise ValueError(f"Ambiguous BDN artifact origin: {path}")
    return sides[0], next(iter(repetitions), None)


def _load(root):
    """Deduplicate aliases, rejecting conflicting contents or origin instead of choosing one."""
    seen, owners = {}, {}
    for path in sorted(root.glob("**/async-health/*.json")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if data.get("SchemaVersion") != 2 or data.get("Kind") != "BenchmarkAsyncHealth":
            raise ValueError(f"Unknown BDN async-health schema: {path.name}")
        benchmark = data.get("Benchmark")
        if not isinstance(benchmark, str):
            raise ValueError(f"Missing BDN benchmark identity: {path.name}")
        if benchmark.rpartition(".")[0].rsplit(".", 1)[-1] != "OpenAsyncBurstRunner":
            continue
        if (not isinstance(data.get("CaseId"), str) or not data["CaseId"] or
            not isinstance(data.get("Parameters"), str)):
            raise ValueError(f"Invalid BDN case identity: {path.name}")
        status = data["Status"]
        if status not in ("Completed", "Failed"):
            raise ValueError("Invalid BDN diagnostic status.")
        if (not isinstance(data.get("Summary"), (dict, type(None))) or
            not isinstance(data.get("Measurement"), (dict, type(None)))):
            raise ValueError("Invalid BDN measurement evidence.")
        if status == "Completed" and (not data.get("Summary") or not data.get("Measurement")):
            raise ValueError("Completed BDN diagnostic has no measurement evidence.")
        execution = data.get("Measurement") is None
        if data["Phase"] not in PHASES and not (execution and status == "Failed"):
            raise ValueError("Invalid BDN diagnostic phase.")
        signature = _configuration(data)
        relative = path.relative_to(root)
        side, repetition = _origin(relative)
        owner = (side, benchmark, data["Parameters"])
        if owners.setdefault(data["CaseId"], owner) != owner:
            raise ValueError(f"Conflicting BDN case origins: {data['CaseId']}")
        identity = (data["CaseId"], "execution" if execution else data["Phase"])
        if identity in seen:
            previous = seen[identity]
            if (previous["data"] != data or previous["side"] != side or
                (repetition is not None and previous["repetition"] is not None and
                 repetition != previous["repetition"])):
                raise ValueError(f"Conflicting BDN artifact aliases: {data['CaseId']}")
            if repetition is not None:
                previous["repetition"] = repetition
            previous["sources"].append(relative.as_posix())
        else:
            seen[identity] = dict(data=data, side=side, repetition=repetition,
                                  sources=[relative.as_posix()], configuration=signature)
    if not seen:
        raise ValueError("No BDN OpenAsyncBurst async-health artifacts were produced.")
    return list(seen.values())


def _number(value):
    if value is not None and (type(value) not in (int, float) or
                              not math.isfinite(value) or value < 0):
        raise ValueError(f"Invalid nonnegative BDN metric: {value!r}")
    return value


def _reduce(records):
    """Take independent metric maxima, retaining every tied origin and incomplete cohort."""
    drivers = {(r["data"]["Configuration"]["DriverVersion"],
                r["data"]["Configuration"]["DriverSha256"]) for r in records}
    if len(drivers) != 1:
        raise ValueError("Multiple driver identities in one BDN side/configuration.")
    metrics, origins, missing_values = {}, {}, {}
    for metric in METRICS:
        values = [_number((r["data"].get("Summary") or {}).get(metric)) for r in records]
        metrics[metric] = max((v for v in values if v is not None), default=None)
        origins[metric] = [r for r, value in zip(records, values)
                           if value is not None and value == metrics[metric]]
        missing_values[metric] = values.count(None)

    phases = defaultdict(set)
    issues, unknown = Counter(), set()
    samples, missing_probes = [], []
    for record in records:
        data = record["data"]
        observed = phases[data["CaseId"]]
        if data.get("Measurement") is not None:
            observed.add(data["Phase"])
            summary = data.get("Summary") or {}
            samples.append(_number(summary.get("Samples")))
            missing_probes.append(_number(summary.get("Missing")))
            # Execution-failure markers can repeat the last window's snapshot.
            for field in OUTCOME_FIELDS:
                value = _number((data.get("Operations") or {}).get(field))
                if value is None:
                    unknown.add(field)
                else:
                    issues[field] += value
    return dict(
        records=records, metrics=metrics, origins=origins, driver=next(iter(drivers)),
        missingValues=missing_values, issues=issues, unknownCounters=sorted(unknown),
        samples=samples, missingProbes=missing_probes,
        missingPhases={case: sorted(PHASES - observed) for case, observed in sorted(phases.items())
                       if observed != PHASES},
        statuses=Counter(r["data"]["Status"] for r in records),
        health=Counter((r["data"].get("Summary") or {}).get("Health", "Invalid") for r in records),
        operationEvidence=Counter((r["data"].get("Summary") or {}).get(
            "OperationEvidence", "Unknown") for r in records))


def build_comparison(root, threshold=compare_perf.DEFAULT_THRESHOLD_PCT):
    """Pair only compatible settings; percentages compare maxima, never pooled sample percentiles."""
    if not math.isfinite(threshold) or threshold < 0:
        raise ValueError("The comparison threshold must be finite and nonnegative.")
    groups = defaultdict(lambda: defaultdict(list))
    for record in _load(Path(root)):
        data = record["data"]
        key = (data["Benchmark"], data["Parameters"], record["configuration"])
        groups[key][record["side"]].append(record)

    entries = []
    for (benchmark, parameters, signature), sides in sorted(groups.items()):
        baseline = _reduce(sides["baseline"]) if "baseline" in sides else None
        current = _reduce(sides["current"]) if "current" in sides else None
        metrics = {}
        for metric in METRICS:
            before = baseline["metrics"][metric] if baseline else None
            after = current["metrics"][metric] if current else None
            metrics[metric] = dict(
                baseline=before, current=after,
                delta=after - before if before is not None and after is not None else None,
                deltaPct=compare_perf._pct(before, after) if before not in (None, 0) else None)
        delta = metrics["DispatchP99Ms"]["deltaPct"]
        status = (compare_perf.classify_change(delta, threshold) if baseline and current else
                  "current-only" if current else "baseline-only")
        entries.append(dict(benchmark=benchmark, parameters=parameters,
                            configuration=json.loads(signature), baseline=baseline, current=current,
                            metrics=metrics, status=status, key=(benchmark, parameters, signature)))
    entries.sort(key=lambda e: (e["metrics"]["DispatchP99Ms"]["deltaPct"] is None,
                               -(e["metrics"]["DispatchP99Ms"]["deltaPct"] or 0), e["key"]))
    return entries


def _text(value):
    return str(value).replace("|", "\\|").replace("\r", " ").replace("\n", " ")


def _value(value, metric, signed=False):
    if value is None:
        return "N/A"
    precision = 4 if metric.endswith("Ms") else 0
    return format(value, f"{'+' if signed else ''}.{precision}f")


def _cell(values, metric):
    pct = values["deltaPct"]
    percent = f"{pct:+.2f}%" if pct is not None else "N/A"
    return (f"{_value(values['current'], metric)} "
            f"({percent}; {_value(values['delta'], metric, signed=True)})")


def _counts(values):
    return ", ".join(f"{key}={value}" for key, value in sorted(values.items())) or "none"


def _provenance(entry, index):
    """Keep coverage and per-metric origins outside the compact metric table."""
    lines = [f"### {index}. {_text(entry['benchmark'])} {_text(entry['parameters'])}", "",
             "Matching configuration: `" + _text(json.dumps(
                 entry["configuration"], sort_keys=True)) + "`", ""]
    for side in ("baseline", "current"):
        group = entry[side]
        if group is None:
            lines.extend([f"**{side}: unmatched configuration** (no compatible side).", ""])
            continue
        lines.extend([
            f"**{side}**: {len(group['records'])} unique records; "
            f"status: {_counts(group['statuses'])}; scheduler: {_counts(group['health'])}; "
            f"operation evidence: {_counts(group['operationEvidence'])}.",
            "Probe sample counts by window: `" + json.dumps(group["samples"]) +
            "`; missing/censored probe counts by window: `" +
            json.dumps(group["missingProbes"]) + "` (null means unavailable).",
            "Missing phases: `" + _text(json.dumps(group["missingPhases"], sort_keys=True)) + "`.",
            "Outcome/retention issues: " + _counts(
                {key: value for key, value in group["issues"].items() if value}) +
            "; unknown counters: " + (", ".join(group["unknownCounters"]) or "none") + ".",
            "Missing metric values: " + _counts(
                {key: value for key, value in group["missingValues"].items() if value}) + ".",
            f"Driver: `{_text(group['driver'][0])}`; SHA-256 `{_text(group['driver'][1])}`.", "",
        ])
        for metric in METRICS:
            origins = [
                f"{r['data']['CaseId']}/{r['data']['Phase']} "
                f"(rep {r['repetition'] if r['repetition'] is not None else 'unlabelled'})"
                for r in group["origins"][metric]]
            lines.append(f"- {METRICS[metric]} maximum "
                         f"{_value(group['metrics'][metric], metric)}: "
                         + _text("; ".join(origins) or "N/A"))
        lines.extend(["", "Raw cohort provenance (all duplicate aliases retained):", ""])
        for record in group["records"]:
            data = record["data"]
            phase = data["Phase"] if data.get("Measurement") is not None else "execution"
            sources = "; ".join(f"`{_text(path)}`" for path in record["sources"])
            lines.append(f"- `{_text(data['CaseId'])}/{phase}`; "
                         f"rep {record['repetition'] or 'unlabelled'}; "
                         f"PID {data['Configuration']['ProcessId']}; "
                         f"{data['Status']}; {sources}")
        lines.append("")
    return lines


def render(root, threshold=compare_perf.DEFAULT_THRESHOLD_PCT, source_label=None):
    """Render observed relative status separately from absolute health/coverage qualifications."""
    entries = build_comparison(root, threshold)
    lines = ["# OpenAsyncBurst async-health comparison", ""]
    if source_label:
        lines.extend(["Source: " + _text(source_label), ""])
    lines.extend([
        f"Observed relative dispatch-p99 status; threshold **{threshold:g}%**. "
        "Increases strictly above the threshold are regressions, decreases strictly below its "
        "negative are improvements, and equality is unchanged.",
        "These indicators are not best-of-N-confirmed regressions and not an absolute "
        "scheduler-health verdict.",
        "Each metric cell is **current maximum (signed percentage delta; signed raw difference)** "
        "to the baseline maximum. Startup, ExtraIteration and available repetitions are reduced "
        "independently per metric. Existing cohort p99 values are not pooled or recomputed.",
        "Percentages are N/A when either value is missing or the baseline is zero, including 0 to 0.",
        "",
        "| Status | Method | Parameters | " + " | ".join(METRICS.values()) + " |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ])
    identities = Counter((e["benchmark"], e["parameters"]) for e in entries)
    for index, entry in enumerate(entries, 1):
        parameters = entry["parameters"]
        if identities[(entry["benchmark"], parameters)] > 1:
            parameters += f" (configuration {index})"
        values = [compare_perf.STATUS_LABELS[entry["status"]],
                  entry["benchmark"].rsplit(".", 1)[-1], parameters]
        values.extend(_cell(entry["metrics"][metric], metric) for metric in METRICS)
        lines.append("| " + " | ".join(_text(value) for value in values) + " |")
    groups = [entry[side] for entry in entries for side in ("baseline", "current") if entry[side]]
    health = sum((group["health"] for group in groups), Counter())
    missing_phases = sum(len(group["missingPhases"]) for group in groups)
    failures = sum(group["statuses"].get("Failed", 0) for group in groups)
    lines.extend([
        "", "## Coverage and provenance", "",
        f"Unique records: {sum(len(group['records']) for group in groups)}; "
        f"scheduler classifications: {_counts(health)}; "
        f"cases with missing phases: {missing_phases}; failed records: {failures}.",
        "**Coverage warning:** relative improvement or neutrality is not proof of absolute "
        "scheduler health. Short/censored cohorts remain insufficient; missing phases, missing "
        "outcomes and failures are not waived when both sides are unhealthy.",
        "Startup includes fixture initialization; ExtraIteration excludes successful fixture "
        "setup/cleanup. Raw phase JSON is unchanged. Individual maxima can come from different "
        "phases, processes and repetitions; tied origins and all artifact aliases are listed below.",
        "",
    ])
    for side in ("baseline", "current"):
        side_groups = [entry[side] for entry in entries if entry[side]]
        evidence = sum((group["operationEvidence"] for group in side_groups), Counter())
        issues = sum((group["issues"] for group in side_groups), Counter())
        unknown = sorted({field for group in side_groups for field in group["unknownCounters"]})
        lines.append(f"- **{side} coverage:** operation evidence: {_counts(evidence)}; "
                     f"outcome/retention issues: {_counts(issues)}; "
                     f"unknown counters: {', '.join(unknown) or 'none'}.")
    lines.extend(["", "<details>",
                  "<summary>Per-row coverage and raw cohort provenance</summary>", ""])
    failed = False
    for index, entry in enumerate(entries, 1):
        lines.extend(_provenance(entry, index))
        failed |= any(group and group["statuses"].get("Failed", 0)
                      for group in (entry["baseline"], entry["current"]))
    lines.extend(["</details>", ""])
    return "\n".join(lines) + "\n", failed


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, required=True)
    parser.add_argument("--threshold", type=float, default=compare_perf.DEFAULT_THRESHOLD_PCT)
    parser.add_argument("--out-md", type=Path,
                        help="Alternate output path; useful for preserving historical reports.")
    parser.add_argument("--source-label", help="Identify historical evidence in a regenerated report.")
    args = parser.parse_args(argv)
    markdown, failed = render(args.results_dir, args.threshold, args.source_label)
    output = args.out_md or args.results_dir / "benchmark-async-health.md"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(markdown, encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
