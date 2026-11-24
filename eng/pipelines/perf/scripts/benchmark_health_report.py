#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Compare worst-observed async health metrics, retaining raw cohort provenance."""

import argparse
from collections import Counter, defaultdict
import json
import math
from pathlib import Path
import re

DISPATCH_WARNING_THRESHOLD_MS = 1.0
DISPATCH_UNHEALTHY_THRESHOLD_MS = 10.0
STATUS_LABELS = {
    "healthy": "\U0001f7e2 healthy",
    "warning": "\U0001f7e1 warning",
    "unhealthy": "\U0001f534 unhealthy",
    "unknown": "\u2754 unknown",
}
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
    if not root.is_dir():
        raise ValueError(f"Results directory does not exist: {root}")

    seen, owners = {}, {}
    for path in sorted(root.glob("**/async-health/*.json")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if data.get("SchemaVersion") not in (2, 3) or data.get("Kind") != "BenchmarkAsyncHealth":
            raise ValueError(f"Unknown BDN async-health schema: {path.name}")
        benchmark = data.get("Benchmark")
        if not isinstance(benchmark, str) or not benchmark:
            raise ValueError(f"Missing BDN benchmark identity: {path.name}")
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
    return list(seen.values())


def _number(value):
    if value is not None and (type(value) not in (int, float) or
                              not math.isfinite(value) or value < 0):
        raise ValueError(f"Invalid nonnegative BDN metric: {value!r}")
    return value


def _resource_evidence(data):
    measurement = data.get("Measurement") or {}
    if measurement.get("ObserverError") is not None:
        return "Invalid"
    dropped = _number(measurement.get("ResourceSamplesDropped"))
    samples = measurement.get("Resources")
    if dropped is None or not isinstance(samples, list):
        return "Unknown"
    return "Truncated" if dropped else "Complete" if samples else "Missing"


def _reduce(records):
    """Take independent metric maxima, retaining every tied origin and incomplete cohort."""
    data = [record["data"] for record in records]
    summaries = [item.get("Summary") or {} for item in data]
    drivers = {(item["Configuration"]["DriverVersion"], item["Configuration"]["DriverSha256"])
               for item in data}
    if len(drivers) != 1:
        raise ValueError("Multiple driver identities in one BDN side/configuration.")
    metrics, origins, missing_values = {}, {}, {}
    for metric in METRICS:
        values = [_number(summary.get(metric)) for summary in summaries]
        metrics[metric] = max((v for v in values if v is not None), default=None)
        origins[metric] = [r for r, value in zip(records, values)
                           if value is not None and value == metrics[metric]]
        missing_values[metric] = values.count(None)

    phases = defaultdict(set)
    issues, unknown = Counter(), set()
    samples, missing_probes = [], []
    for item, summary in zip(data, summaries):
        observed = phases[item["CaseId"]]
        # Execution-failure markers can repeat the last window's snapshot.
        if item.get("Measurement") is None:
            continue
        observed.add(item["Phase"])
        samples.append(_number(summary.get("Samples")))
        missing_probes.append(_number(summary.get("Missing")))
        for field in OUTCOME_FIELDS:
            value = _number((item.get("Operations") or {}).get(field))
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
        statuses=Counter(item["Status"] for item in data),
        resourceEvidence=Counter(_resource_evidence(item) for item in data),
        operationEvidence=Counter(s.get("OperationEvidence", "Unknown") for s in summaries),
        dispatchEvidence=Counter(s.get("DispatchEvidence", "Unknown") for s in summaries),
        firstQueueEvidence=Counter(s.get("FirstWorkerQueueEvidence", "Unknown") for s in summaries),
        firstQueueSamples=[summary.get("FirstWorkerQueueSamples") for summary in summaries])


def build_comparison(root):
    """Pair only compatible settings; percentages compare maxima, never pooled sample percentiles."""
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
            delta = after - before if before is not None and after is not None else None
            metrics[metric] = dict(
                baseline=before, current=after, delta=delta,
                deltaPct=delta / before * 100 if delta is not None and before else None)
        dispatch = metrics["DispatchP99Ms"]["current"]
        if dispatch is None:
            status = "unknown"
        elif dispatch > DISPATCH_UNHEALTHY_THRESHOLD_MS:
            status = "unhealthy"
        elif dispatch >= DISPATCH_WARNING_THRESHOLD_MS:
            status = "warning"
        else:
            status = "healthy"
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
            f"status: {_counts(group['statuses'])}; "
            f"resource evidence: {_counts(group['resourceEvidence'])}; "
            f"operation evidence: {_counts(group['operationEvidence'])}.",
            f"Dispatch p99 sampling: {_counts(group['dispatchEvidence'])}; "
            f"first-worker p99 sampling: {_counts(group['firstQueueEvidence'])}; "
            f"first-worker sample counts: `{json.dumps(group['firstQueueSamples'])}`.",
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


def render(root, source_label=None):
    """Render current dispatch health separately from relative deltas and coverage qualifications."""
    entries = build_comparison(root)
    lines = ["# Benchmark async-health comparison", ""]
    if source_label:
        lines.extend(["Source: " + _text(source_label), ""])
    if not entries:
        lines.append("No async-health artifacts were collected.")
        return "\n".join(lines) + "\n", False

    lines.extend([
        f"Current dispatch-p99 health: below **{DISPATCH_WARNING_THRESHOLD_MS:g}ms** is "
        f"healthy (green); from **{DISPATCH_WARNING_THRESHOLD_MS:g}ms** through "
        f"**{DISPATCH_UNHEALTHY_THRESHOLD_MS:g}ms** inclusive is warning (yellow); above "
        f"**{DISPATCH_UNHEALTHY_THRESHOLD_MS:g}ms** is unhealthy (red). "
        "Missing current values are unknown.",
        "These indicators use observed current latency, not best-of-N confirmation or "
        "percentage change. Coverage qualifications remain separate.",
        "Each metric cell is **current maximum (signed percentage delta; signed raw difference)** "
        "to the baseline maximum. Startup, ExtraIteration and available repetitions are reduced "
        "independently per metric. Existing cohort p99 values are not pooled or recomputed.",
        "Percentages are N/A when either value is missing or the baseline is zero, including 0 to 0.",
        "",
        "| Status | Benchmark | Parameters | " + " | ".join(METRICS.values()) + " |",
        "| --- | --- | --- | --- | --- | --- | --- |",
    ])
    identities = Counter((e["benchmark"], e["parameters"]) for e in entries)
    for index, entry in enumerate(entries, 1):
        parameters = entry["parameters"]
        if identities[(entry["benchmark"], parameters)] > 1:
            parameters += f" (configuration {index})"
        benchmark = ".".join(entry["benchmark"].rsplit(".", 2)[-2:])
        values = [STATUS_LABELS[entry["status"]], benchmark, parameters]
        values.extend(_cell(entry["metrics"][metric], metric) for metric in METRICS)
        lines.append("| " + " | ".join(_text(value) for value in values) + " |")
    groups = [entry[side] for entry in entries for side in ("baseline", "current") if entry[side]]
    resources = sum((group["resourceEvidence"] for group in groups), Counter())
    missing_phases = sum(len(group["missingPhases"]) for group in groups)
    failures = sum(group["statuses"].get("Failed", 0) for group in groups)
    lines.extend([
        "", "## Coverage and provenance", "",
        f"Unique records: {sum(len(group['records']) for group in groups)}; "
        f"resource evidence: {_counts(resources)}; "
        f"cases with missing phases: {missing_phases}; failed records: {failures}.",
        "**Coverage warning:** a green or yellow indicator is not proof of complete or sufficient "
        "evidence. Short/censored cohorts remain insufficient; missing phases, missing outcomes and failures "
        "are not waived by a low observed p99.",
        "Startup includes fixture initialization; ExtraIteration excludes successful fixture "
        "setup/cleanup. Raw phase JSON is unchanged. Individual maxima can come from different "
        "phases, processes and repetitions; tied origins and all artifact aliases are listed below.",
        "",
    ])
    for side in ("baseline", "current"):
        side_groups = [entry[side] for entry in entries if entry[side]]
        evidence = sum((group["operationEvidence"] for group in side_groups), Counter())
        dispatch = sum((group["dispatchEvidence"] for group in side_groups), Counter())
        first_queue = sum((group["firstQueueEvidence"] for group in side_groups), Counter())
        resources = sum((group["resourceEvidence"] for group in side_groups), Counter())
        issues = sum((group["issues"] for group in side_groups), Counter())
        unknown = sorted({field for group in side_groups for field in group["unknownCounters"]})
        lines.append(f"- **{side} coverage:** operation evidence: {_counts(evidence)}; "
                     f"dispatch p99 sampling: {_counts(dispatch)}; "
                     f"first-worker p99 sampling: {_counts(first_queue)}; "
                     f"resource evidence: {_counts(resources)}; "
                     f"outcome/retention issues: {_counts(issues)}; "
                     f"unknown counters: {', '.join(unknown) or 'none'}.")
    lines.extend(["", "<details>",
                  "<summary>Per-row coverage and raw cohort provenance</summary>", ""])
    for index, entry in enumerate(entries, 1):
        lines.extend(_provenance(entry, index))
    lines.extend(["</details>", ""])
    return "\n".join(lines) + "\n", failures > 0


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, required=True)
    parser.add_argument("--out-md", type=Path,
                        help="Alternate output path; useful for preserving historical reports.")
    parser.add_argument("--source-label", help="Identify historical evidence in a regenerated report.")
    args = parser.parse_args(argv)
    markdown, failed = render(args.results_dir, args.source_label)
    output = args.out_md or args.results_dir / "benchmark-async-health-report-github.md"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(markdown, encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
