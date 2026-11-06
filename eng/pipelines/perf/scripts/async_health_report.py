#!/usr/bin/env python3
"""Versioned, report-only async-health results. Never consumes BDN time statistics."""

import argparse
import csv
import json
import math
from pathlib import Path


NOTICE = ("Report-only interference/ThreadPool health, not proof of async correctness. "
          "Windows are configured invocation batches, not independent process replicates. "
          "Percentiles describe executed probes only; dropped, rejected, skipped and missed probes are reported separately "
          "and may bias latency estimates. Supported does not imply loss-free observation. "
          "Recovery is separate; missing probes are not zero-latency observations.")


def number(value, name):
    if type(value) not in (int, float) or not math.isfinite(value) or value < 0:
        raise ValueError(f"Invalid {name}")
    return value


def distribution(values, supported):
    values = sorted(values)
    if not supported or not values:
        return dict(Count=len(values), P50=None, P95=None, P99=None, Max=None)
    return dict(Count=len(values), **{
        label: values[min(len(values) - 1, math.ceil(len(values) * q) - 1)]
        for label, q in (("P50", .50), ("P95", .95), ("P99", .99), ("Max", 1))})


def summarize(row):
    if row.get("SchemaVersion") not in (1, 2, 3, 4) or row.get("MeasurementMode") != "async-health":
        raise ValueError("Expected async-health schema version 1, 2, 3 or 4")
    if row.get("Status") not in ("Completed", "Failed"):
        raise ValueError("Invalid async-health status")
    for key in ("Benchmark", "Parameters", "CaseId", "RunId"):
        if not isinstance(row.get(key), str) or (key != "Parameters" and not row[key]):
            raise ValueError(f"Invalid identity field {key}")
    for key in ("ProcessId", "ProcessRepetition"):
        if type(row.get(key)) is not int or row[key] < 1:
            raise ValueError(f"Invalid identity field {key}")
    repetition = row.get("DiagnosticRepetition", 1)
    diagnostic_runs = row.get("Options", {}).get("DiagnosticRuns", 1)
    if (type(repetition) is not int or type(diagnostic_runs) is not int
            or not 1 <= repetition <= diagnostic_runs):
        raise ValueError("Invalid diagnostic repetition")
    result = dict(row, SchemaVersion=max(2, row["SchemaVersion"]),
                  DiagnosticRepetition=repetition)
    if "Measurement" not in row:
        if row["Status"] != "Failed":
            raise ValueError("Completed result is missing its measurement")
        return dict(result, Coverage="Failed")
    window, options = row["Measurement"], row["Options"]
    for key in ("Window", "Invocations", "OperationsPerInvoke"):
        if type(row.get(key)) is not int or row[key] < 1:
            raise ValueError(f"Invalid workload field {key}")
    if row.get("CompletedInvocations") != (row["Invocations"] if row["Status"] == "Completed" else None):
        raise ValueError("Invalid completed invocation count")
    if (not isinstance(row["Configuration"], dict) or row["Policy"] != "ConfiguredInvocationBatch"
            or type(options["MinimumSamples"]) is not int or not 1 <= options["MinimumSamples"] <= 100000
            or number(options["MinimumWindowMilliseconds"], "minimum window") == 0):
        raise ValueError("Invalid configuration or coverage policy")
    start = number(window["WorkloadStartMilliseconds"], "start")
    end = number(window["WorkloadEndMilliseconds"], "end")
    stop = number(window["RecoveryEndMilliseconds"], "recovery end")
    if not start <= end <= stop:
        raise ValueError("Invalid workload/recovery boundaries")
    probes = dict(window["Probes"])
    probes.setdefault("Total", probes.get("Scheduled"))
    count_keys = ("Total", "Posted", "Executed", "Dropped", "ProducerMissed", "Outstanding")
    if row["SchemaVersion"] >= 3:
        count_keys += ("Skipped",)
    else:
        probes.pop("Skipped", None)
    if row["SchemaVersion"] >= 4:
        count_keys += ("Rejected",)
    else:
        probes.pop("Rejected", None)
    for key in count_keys:
        number(probes[key], key)
        if type(probes[key]) is not int:
            raise ValueError("Probe counts must be integers")
    if row["SchemaVersion"] == 1:
        probes["Dropped"] -= probes["ProducerMissed"]
        number(probes["Dropped"], "Dropped")
    result["Measurement"] = dict(window, Probes=probes)
    if (probes["Total"] != probes["Posted"] + probes["Dropped"] + probes["ProducerMissed"]
            + probes.get("Skipped", 0) + probes.get("Rejected", 0)
            or probes["Posted"] != probes["Executed"] + probes["Outstanding"]
            or probes["Posted"] != len(probes["Samples"])):
        raise ValueError("Probe accounting does not reconcile")
    work, recovery, late_work = [], [], []
    for sample in probes["Samples"]:
        scheduled = number(sample["ScheduledMilliseconds"], "scheduled")
        posted = number(sample["PostedMilliseconds"], "posted")
        executed = sample["ExecutedMilliseconds"]
        if scheduled < start or posted < scheduled or posted > stop:
            raise ValueError("Probe posted outside observer window")
        if executed is not None and not posted <= number(executed, "executed") <= stop:
            raise ValueError("Invalid execution timestamp")
        if posted < end:
            if executed is not None and executed <= end:
                work.append(sample)
            else:
                late_work.append(sample)
        elif executed is not None:
            recovery.append(sample)
    if sum(p["ExecutedMilliseconds"] is not None for p in probes["Samples"]) != probes["Executed"]:
        raise ValueError("Executed sample count does not reconcile")
    coverage = ("Failed" if row["Status"] == "Failed" or window["ObserverError"] else
                "Incomplete" if probes["Outstanding"] or late_work
                or window.get("ResourceSamplesDropped", 0) else
                "Insufficient" if end - start < options["MinimumWindowMilliseconds"]
                or len(work) < options["MinimumSamples"] else "Supported")
    # Delayed work probes keep their original cohort, separate from recovery probes.
    def metrics(samples, supported):
        return {
            "DispatchMilliseconds": distribution(
                [p["ExecutedMilliseconds"] - p["PostedMilliseconds"] for p in samples], supported),
            "ScheduledToExecutedMilliseconds": distribution(
                [p["ExecutedMilliseconds"] - p["ScheduledMilliseconds"] for p in samples], supported),
            "ProducerLatenessMilliseconds": distribution(
                [p["PostedMilliseconds"] - p["ScheduledMilliseconds"] for p in samples], supported),
        }
    resource_work = [s for s in window["Resources"] if start <= s["TimestampMilliseconds"] <= end]
    process_baseline = window.get("ProcessThreadsAtStart")
    process_samples = [s["ProcessThreads"] for s in resource_work if "ProcessThreads" in s]
    for count in ([process_baseline] if process_baseline is not None else []) + process_samples:
        if type(count) is not int or count < 1:
            raise ValueError("Invalid process thread count")
    process_peak = max(process_samples, default=None)
    resource_work.sort(key=lambda sample: sample["TimestampMilliseconds"])
    def sampled_delta(field):
        if not resource_work or any(field not in sample for sample in resource_work):
            return None
        values = [sample[field] for sample in resource_work]
        for value in values:
            number(value, field)
            if type(value) is not int:
                raise ValueError(f"Invalid resource counter {field}")
        if any(right < left for left, right in zip(values, values[1:])):
            raise ValueError(f"Decreasing resource counter {field}")
        return values[-1] - values[0]

    result.update(
        Coverage=coverage, DurationMilliseconds=end - start, WorkExecuted=len(work),
        DroppedProbes=probes["Dropped"], ProducerMissedProbes=probes["ProducerMissed"],
        SkippedProbes=probes.get("Skipped"),
        RejectedProbes=probes.get("Rejected"),
        AdmissionDroppedProbes=probes["Dropped"],
        WorkLateOrOutstanding=len(late_work),
        Work=metrics(work, coverage == "Supported"),
        Recovery=metrics(recovery, row["Status"] == "Completed" and not window["ObserverError"]
                         and not probes["Outstanding"]
                         and not window.get("ResourceSamplesDropped", 0)
                         and stop - end >= options["MinimumWindowMilliseconds"]
                         and len(recovery) >= options["MinimumSamples"]),
        LateWork=metrics([p for p in late_work if p["ExecutedMilliseconds"] is not None],
                         False),
        PeakBusyWorkerProxy=max((s["BusyWorkerProxy"] for s in resource_work), default=None),
        PeakThreads=max((s["Threads"] for s in resource_work), default=None),
        ProcessThreadsAtStart=process_baseline,
        PeakProcessThreads=process_peak,
        ProcessThreadIncrease=(max(0, process_peak - process_baseline)
                       if process_peak is not None and process_baseline is not None else None),
        PeakQueueDepth=max((s["QueueDepth"] for s in resource_work), default=None),
        ResourceSampleCount=len(resource_work),
        ResourceSampleSpanMilliseconds=(
            resource_work[-1]["TimestampMilliseconds"] - resource_work[0]["TimestampMilliseconds"]
            if resource_work else None),
        SampledCompletedWorkItemsDelta=sampled_delta("CompletedWorkItems"),
        SampledAllocatedBytesDelta=sampled_delta("AllocatedBytes"),
        SampledGen2CollectionsDelta=sampled_delta("Gen2Collections"))
    return result


def write_collected_summary(root):
    """Report sidecars from either pipeline layout without changing timing summaries."""
    root = Path(root)
    if not root.is_dir():
        raise FileNotFoundError(f"Results directory does not exist: {root}")

    windows = {}
    for path in sorted(root.rglob("async-health-*.json")):
        if path.name in ("async-health-summary.json", "async-health-process.json"):
            continue
        row = json.loads(path.read_text(encoding="utf-8-sig"))
        parts = path.relative_to(root).parts
        variant = next((part for part in parts if part in ("baseline", "current")), "unknown")
        key = (variant, row["RunId"], row["CaseId"], row.get("Window"), row["Status"])
        if key in windows:
            if windows[key]["raw"] != row:
                raise ValueError(f"Conflicting copies of async-health window: {path}")
            continue
        windows[key] = dict(raw=row, result=dict(summarize(row), Variant=variant,
                                               Source=str(path.relative_to(root))))
    if not windows:
        return 0

    rows = sorted((window["result"] for window in windows.values()),
                  key=lambda row: (row["Variant"], row["ProcessRepetition"],
                                   row["Benchmark"], row["Parameters"],
                                   row["DiagnosticRepetition"], row.get("Window", 0)))
    notice = (
        NOTICE + " Sampled resource deltas cover only the first-to-last workload resource "
        "sample, not exact invocation boundaries; they include observer/runtime activity. "
        "Process-thread peaks can miss short-lived threads. No recovery sampling phase is run."
    )
    summary = dict(SchemaVersion=4, MeasurementMode="async-health", Notice=notice, Windows=rows)
    (root / "async-health-summary.json").write_text(
        json.dumps(summary, indent=2, allow_nan=False), encoding="utf-8")

    flat_rows = []
    for row in rows:
        flat = {key: value for key, value in row.items()
                if value is None or isinstance(value, (str, int, float, bool))}
        for key, value in row.get("Measurement", {}).get("Probes", {}).items():
            if key != "Samples":
                flat[f"Probes.{key}"] = value
        for key in ("OutstandingAtWorkloadEnd", "ResourceSamplesDropped", "ObserverError"):
            flat[key] = row.get("Measurement", {}).get(key)
        for cohort in ("Work", "Recovery", "LateWork"):
            for metric, distribution_values in row.get(cohort, {}).items():
                for statistic, value in distribution_values.items():
                    flat[f"{cohort}.{metric}.{statistic}"] = value
        flat_rows.append(flat)
    fields = list(dict.fromkeys(key for row in flat_rows for key in row))
    with (root / "async-health-summary.csv").open("w", newline="", encoding="utf-8") as output:
        writer = csv.DictWriter(output, fieldnames=fields)
        writer.writeheader()
        writer.writerows(flat_rows)

    def display(value):
        if value is None:
            return "N/A"
        if isinstance(value, float):
            return f"{value:.3f}"
        return str(value).replace("|", "\\|").replace("\n", " ")

    def table(headers, values):
        return ["| " + " | ".join(headers) + " |",
                "| " + " | ".join("---" for _ in headers) + " |"] + [
                    "| " + " | ".join(display(value) for value in record) + " |"
                    for record in values]

    lines = ["# Async-health diagnostic statistics", "", notice, "",
             "Timing statistics remain in the BenchmarkDotNet reports. "
             "These are diagnostic windows, not a timing regression verdict.", ""]
    for row in rows:
        lines += [f'## {display(row["Benchmark"])} {display(row["Parameters"])}', "",
                  f'Variant: {row["Variant"]}; process repetition: {row["ProcessRepetition"]}; '
                  f'diagnostic repetition: {row["DiagnosticRepetition"]}; '
                  f'coverage: **{row["Coverage"]}**; '
                  f'duration: {display(row.get("DurationMilliseconds"))} ms; '
                  f'completed invocations: {display(row.get("CompletedInvocations"))}.', ""]
        probes = row.get("Measurement", {}).get("Probes", {})
        lines += table(
            ["Total", "Posted", "Executed", "Dropped", "Rejected", "Skipped",
             "Producer-missed", "Outstanding", "Late/outstanding work"],
            [[probes.get(key) for key in ("Total", "Posted", "Executed", "Dropped",
                                         "Rejected", "Skipped", "ProducerMissed", "Outstanding")]
             + [row.get("WorkLateOrOutstanding")]])
        lines += [""]
        distributions = []
        for cohort in ("Work", "Recovery", "LateWork"):
            for metric, values in row.get(cohort, {}).items():
                distributions.append([cohort, metric] + [
                    values.get(key) for key in ("Count", "P50", "P95", "P99", "Max")])
        lines += table(["Cohort", "Delay (ms)", "Count", "P50", "P95", "P99", "Max"],
                       distributions)
        lines += [""]
        metrics = [
            ("Peak busy worker proxy", row.get("PeakBusyWorkerProxy")),
            ("Peak pool threads", row.get("PeakThreads")),
            ("Process threads at start", row.get("ProcessThreadsAtStart")),
            ("Peak process threads", row.get("PeakProcessThreads")),
            ("Process thread increase", row.get("ProcessThreadIncrease")),
            ("Peak Queue depth", row.get("PeakQueueDepth")),
            ("Resource samples", row.get("ResourceSampleCount")),
            ("Resource sample span (ms)", row.get("ResourceSampleSpanMilliseconds")),
            ("Sampled completed work items delta", row.get("SampledCompletedWorkItemsDelta")),
            ("Sampled Allocated bytes delta", row.get("SampledAllocatedBytesDelta")),
            ("Sampled Gen2 collections delta", row.get("SampledGen2CollectionsDelta")),
        ]
        measurement = row.get("Measurement", {})
        metrics += [(key, measurement.get(key)) for key in (
            "OutstandingAtWorkloadEnd", "ResourceSamplesDropped", "ObserverError")]
        lines += table(["Resource / observer statistic", "Value"], metrics)
        lines += ["", f'Source: `{row["Source"]}`', ""]
    (root / "async-health-summary.md").write_text("\n".join(lines), encoding="utf-8")
    return len(rows)


def collect(cwd, destination):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    rows = []
    for path in sorted(Path(cwd, "BenchmarkDotNet.Artifacts", "async-health").glob("async-health-*.json")):
        target = destination / path.name
        target.write_bytes(path.read_bytes())
        row = json.loads(path.read_text(encoding="utf-8"))
        summarize(row)  # Validate before accepting the result, including accounting.
        rows.append(row)
    return rows


def write_summary(root, units, baseline, repetitions, comparison_switch=None):
    root = Path(root)
    rows, processes = [], []
    for rep in range(1, repetitions + 1):
        for variant in (("baseline", "current") if baseline else ("current",)):
            for unit in units:
                directory = root / "repetitions" / f"rep{rep}" / variant / unit
                process = json.loads((directory / "async-health-process.json").read_text(encoding="utf-8"))
                processes.append(dict(process, Variant=variant, Unit=unit, ProcessRepetition=rep))
                for path in sorted(directory.glob("async-health-*.json")):
                    if path.name == "async-health-process.json":
                        continue
                    row = summarize(json.loads(path.read_text(encoding="utf-8")))
                    if row["ProcessRepetition"] != rep or row["RunId"] != process["RunId"]:
                        raise ValueError("Mismatched process repetition")
                    rows.append(dict(row, Variant=variant, Unit=unit))
    rows.sort(key=lambda row: (row["ProcessRepetition"], row["Variant"], row["Unit"],
                              row["Benchmark"], row["Parameters"],
                              row["DiagnosticRepetition"], row.get("Window", 0)))
    comparisons = []
    indexed = {}
    for row in rows:
        if "Window" not in row:
            continue
        key = (row["Unit"], row["Benchmark"], row["Parameters"], row["ProcessRepetition"],
               row["DiagnosticRepetition"], row["Window"])
        by_variant = indexed.setdefault(key, {})
        if row["Variant"] in by_variant:
            raise ValueError("Duplicate async-health window identity")
        by_variant[row["Variant"]] = row
    for key, pair in indexed.items():
        if not baseline:
            break
        left, right = pair.get("baseline"), pair.get("current")
        match = left and right and all(left[field] == right[field] for field in (
            "SchemaVersion", "MeasurementMode", "Options", "Policy", "Invocations", "OperationsPerInvoke"))
        if match:
            left_config, right_config = dict(left["Configuration"]), dict(right["Configuration"])
            if comparison_switch:
                if left_config.pop(comparison_switch, None) is not False or right_config.pop(comparison_switch, None) is not True:
                    match = False
            match = match and left_config == right_config
        comparable = bool(match and left["Coverage"] == right["Coverage"] == "Supported")
        comparisons.append(dict(
            Identity=list(key), Comparable=comparable,
            ComparisonSwitch=comparison_switch,
            Reason="Matching supported windows" if comparable else "Missing, insufficient, failed or different accounting/configuration/shape",
            DispatchP99DeltaMilliseconds=(
                right["Work"]["DispatchMilliseconds"]["P99"] - left["Work"]["DispatchMilliseconds"]["P99"]
                if comparable else None),
            DroppedProbesDelta=(right["DroppedProbes"] - left["DroppedProbes"] if comparable else None),
            RejectedProbesDelta=(right["RejectedProbes"] - left["RejectedProbes"]
                                 if comparable and left["RejectedProbes"] is not None
                                 and right["RejectedProbes"] is not None else None),
            SkippedProbesDelta=(right["SkippedProbes"] - left["SkippedProbes"]
                                if comparable and left["SkippedProbes"] is not None
                                and right["SkippedProbes"] is not None else None),
            ProducerMissedProbesDelta=(right["ProducerMissedProbes"] - left["ProducerMissedProbes"]
                                      if comparable else None)))
    summary = dict(SchemaVersion=4, MeasurementMode="async-health", Notice=NOTICE,
                   ProcessRepetitions=repetitions, ComparisonSwitch=comparison_switch,
                   Processes=processes, Windows=rows, Comparisons=comparisons)
    (root / "async-health-summary.json").write_text(json.dumps(summary, indent=2, allow_nan=False), encoding="utf-8")
    lines = ["# Async-health measurement", "", NOTICE, "",
             "| Variant / process / diagnostic repetition | Benchmark / parameters | Window | Coverage | Duration ms / completed invocations | Work samples | Dropped / rejected / skipped / producer-missed / outstanding | Observed dispatch / scheduled / producer p99 ms | Peak busy workers | Peak pool threads | Process threads start / peak / increase |",
             "|---|---|---:|---|---|---:|---|---|---:|---:|---|"]
    def display(value):
        if value is None:
            return "N/A"
        if isinstance(value, float):
            return f"{value:.3f}"
        return str(value).replace("|", "\\|").replace("\n", " ")
    for row in rows:
        probes = row.get("Measurement", {}).get("Probes", {})
        values = [
            f'{row["Variant"]} / {row["ProcessRepetition"]} / {row["DiagnosticRepetition"]}',
            row["Benchmark"] + " " + row["Parameters"], row.get("Window"), row["Coverage"],
            f'{display(row.get("DurationMilliseconds"))} / {display(row.get("CompletedInvocations"))}',
            row.get("WorkExecuted"), (f'{probes.get("Dropped", "N/A")} / '
                                     f'{probes.get("Rejected", "N/A")} / '
                                     f'{probes.get("Skipped", "N/A")} / '
                                     f'{probes.get("ProducerMissed", "N/A")} / {probes.get("Outstanding", "N/A")}'),
            " / ".join(display(row.get("Work", {}).get(metric, {}).get("P99")) for metric in
                       ("DispatchMilliseconds", "ScheduledToExecutedMilliseconds", "ProducerLatenessMilliseconds")),
            row.get("PeakBusyWorkerProxy"), row.get("PeakThreads"),
            " / ".join(display(row.get(key)) for key in
                       ("ProcessThreadsAtStart", "PeakProcessThreads", "ProcessThreadIncrease"))]
        lines.append("| " + " | ".join(display(v) for v in values) + " |")
    for process in processes:
        if process["Status"] != "Completed":
            lines += ["", f'**Failed process:** {process["Variant"]} / {process["Unit"]} / '
                      f'repetition {process["ProcessRepetition"]}: {process["Error"]}']
    lines += ["", "## Baseline / candidate", ""]
    if not baseline:
        lines.append("Current-only demonstration. No candidate-vs-main performance verdict.")
    if comparison_switch:
        lines.append(f"Declared configuration difference: `{comparison_switch}` baseline=false, candidate=true.")
    for comparison in comparisons:
        lines.append(f'- {display(comparison["Identity"])}: {comparison["Reason"]}; '
                     f'observed dispatch p99 delta ms: {display(comparison["DispatchP99DeltaMilliseconds"])}; '
                     f'dropped probes delta: {display(comparison["DroppedProbesDelta"])}; '
                     f'rejected probes delta: {display(comparison["RejectedProbesDelta"])}; '
                     f'skipped probes delta: {display(comparison["SkippedProbesDelta"])}; '
                     f'producer-missed probes delta: {display(comparison["ProducerMissedProbesDelta"])}')
    text = "\n".join(lines) + "\n"
    (root / "async-health-summary.md").write_text(text, encoding="utf-8")
    (root / "summary.md").write_text(text, encoding="utf-8")
    return summary


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", required=True)
    args = parser.parse_args()
    count = write_collected_summary(args.results_dir)
    print(f"Async-health diagnostic report: {count} window(s).")
