#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Render BDN startup and extra-iteration evidence separately from timing comparisons/Kusto."""

import argparse
import json
from pathlib import Path


def render(root):
    """Keep side/repetition provenance and explicit failures/insufficient windows in the summary."""
    lines = [
        "# BenchmarkDotNet async health", "",
        "Startup/warmup and the extra diagnostic iteration are separate observations of BDN's "
        "own workloads. Normal timing iterations are not observed.",
        "Startup includes fixture initialization. Short/censored windows are not health passes.",
        "",
        "| Evidence | Benchmark | Parameters | Phase | Status | Scheduler | Dispatch p99 (ms) | "
        "First queue p99 (ms) | First offer-to-open p99 (ms) | Completed/s | Failed/cancelled | "
        "Queued/in-flight | Missing first outcomes | Operation evidence | Peak process threads |",
        "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |",
    ]
    paths = sorted(root.glob("**/async-health/*.json"))
    if not paths:
        raise ValueError("No BDN async-health artifacts were produced.")
    seen = set()
    failed = False
    for path in paths:
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        if data.get("SchemaVersion") != 2 or data.get("Kind") != "BenchmarkAsyncHealth":
            raise ValueError(f"Unknown BDN async-health schema: {path.name}")
        # Sequential collection retains both a full artifacts tree and copied timing reports;
        # identify health windows by their unique case id and result/marker filename.
        identity = (data["CaseId"], path.name)
        if identity in seen:
            continue
        seen.add(identity)
        status = data["Status"]
        if status not in ("Completed", "Failed"):
            raise ValueError("Invalid BDN diagnostic status.")
        failed |= status == "Failed"
        summary = data.get("Summary")
        if status == "Completed" and (not summary or not data.get("Measurement")):
            raise ValueError("Completed BDN diagnostic has no measurement evidence.")
        summary = summary or {}
        operations = data.get("Operations") or {}
        values = [
            str(path.relative_to(root)), data["Benchmark"], data["Parameters"], data["Phase"], status,
            summary.get("Health", "Invalid"), summary.get("DispatchP99Ms"),
            summary.get("FirstWorkerQueueP99Ms"), summary.get("FirstWorkerOfferToOpenP99Ms"),
            summary.get("CompletedOperationsPerSecond"),
            f"{operations.get('Failed', '-')}/{operations.get('Cancelled', '-')}",
            f"{operations.get('Queued', '-')}/{operations.get('InFlight', '-')}",
            operations.get("FirstWorkerOutcomesMissing"),
            summary.get("OperationEvidence", "Unknown"), summary.get("ProcessThreadsPeak")]
        lines.append("| " + " | ".join(
            str(value).replace("|", "\\|").replace("\n", " ") if value is not None else "-"
            for value in values) + " |")
    return "\n".join(lines) + "\n", failed


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--results-dir", type=Path, required=True)
    args = parser.parse_args(argv)
    markdown, failed = render(args.results_dir)
    (args.results_dir / "benchmark-async-health.md").write_text(markdown, encoding="utf-8")
    return 1 if failed else 0


if __name__ == "__main__":
    raise SystemExit(main())
