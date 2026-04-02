#!/usr/bin/env python3
"""
Compare two BenchmarkDotNet JSON result files and produce a regression report.

Usage:
    python Compare-BenchmarkResults.py --baseline baseline.json --current current.json [--threshold 10] [--output benchmark-comparison.md]

Exit codes:
    0 - No regressions detected
    1 - One or more regressions exceed the threshold
"""

import argparse
import json
import sys
from pathlib import Path


def parse_benchmark_json(path: str) -> dict:
    """Parse a BenchmarkDotNet JSON file and return benchmark name -> stats."""
    with open(path, "r", encoding="utf-8") as f:
        data = json.load(f)

    results = {}
    benchmarks = data.get("Benchmarks", [])
    for bench in benchmarks:
        name = bench.get("FullName") or bench.get("Method", "Unknown")
        stats = bench.get("Statistics", {})
        mean = stats.get("Mean")
        memory = bench.get("Memory", {})
        allocated = memory.get("BytesAllocatedPerOperation")

        if mean is not None:
            results[name] = {"Mean": mean, "Allocated": allocated}

    return results


def compare(baseline: dict, current: dict, threshold: float) -> tuple:
    """Compare baseline and current results. Returns (comparisons, has_regression)."""
    comparisons = []
    has_regression = False

    for name, cur_stats in current.items():
        if name in baseline:
            base_mean = baseline[name]["Mean"]
            cur_mean = cur_stats["Mean"]
            change_pct = ((cur_mean - base_mean) / base_mean * 100) if base_mean > 0 else 0

            status = "pass"
            if change_pct > threshold:
                status = "REGRESSION"
                has_regression = True
            elif change_pct < -threshold:
                status = "improvement"

            comparisons.append({
                "name": name,
                "baseline_ms": round(base_mean / 1_000_000, 3),
                "current_ms": round(cur_mean / 1_000_000, 3),
                "change_pct": round(change_pct, 2),
                "status": status,
            })
        else:
            comparisons.append({
                "name": name,
                "baseline_ms": "N/A",
                "current_ms": round(cur_stats["Mean"] / 1_000_000, 3),
                "change_pct": "NEW",
                "status": "new",
            })

    return comparisons, has_regression


def generate_markdown(comparisons: list, threshold: float, has_regression: bool) -> str:
    """Generate a Markdown comparison table."""
    lines = [
        "# Benchmark Comparison Report",
        "",
        "| Benchmark | Baseline (ms) | Current (ms) | Change (%) | Status |",
        "|-----------|--------------|-------------|-----------|--------|",
    ]

    status_icons = {
        "REGRESSION": "❌",
        "improvement": "✅",
        "new": "🆕",
        "pass": "✅",
    }

    # Sort regressions first
    sorted_comparisons = sorted(comparisons, key=lambda c: c["status"] == "REGRESSION", reverse=True)

    for c in sorted_comparisons:
        icon = status_icons.get(c["status"], "✅")
        lines.append(
            f"| {c['name']} | {c['baseline_ms']} | {c['current_ms']} | {c['change_pct']} | {icon} |"
        )

    lines.append("")
    if has_regression:
        lines.append(f"**⚠️ Regressions detected!** One or more benchmarks exceeded the {threshold}% threshold.")
    else:
        lines.append(f"**✅ No regressions detected.** All benchmarks within {threshold}% threshold.")

    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description="Compare BenchmarkDotNet JSON results")
    parser.add_argument("--baseline", required=True, help="Path to baseline JSON file")
    parser.add_argument("--current", required=True, help="Path to current JSON file")
    parser.add_argument("--threshold", type=float, default=10.0, help="Regression threshold percentage (default: 10)")
    parser.add_argument("--output", default="benchmark-comparison.md", help="Output Markdown file path")
    args = parser.parse_args()

    print(f"Loading baseline: {args.baseline}")
    baseline = parse_benchmark_json(args.baseline)

    print(f"Loading current: {args.current}")
    current = parse_benchmark_json(args.current)

    comparisons, has_regression = compare(baseline, current, args.threshold)
    markdown = generate_markdown(comparisons, args.threshold, has_regression)

    Path(args.output).write_text(markdown, encoding="utf-8")
    print(f"Comparison written to: {args.output}")
    print()
    print(markdown)

    if has_regression:
        print("\nFAIL: Performance regressions detected.", file=sys.stderr)
        sys.exit(1)
    else:
        print("\nPASS: No performance regressions.")
        sys.exit(0)


if __name__ == "__main__":
    main()
