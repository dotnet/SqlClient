#!/usr/bin/env python3
"""Compare two sets of BenchmarkDotNet "full" JSON reports and emit a delta.

Reads every ``*-report-full.json`` under a baseline directory and a current
directory, matches benchmarks by (Type, Method, Parameters), and computes the
per-benchmark delta in mean execution time and allocated memory.

Outputs:
  * a GitHub-flavoured markdown table (``--out-md``), and
  * a structured JSON document (``--out-json``)

The script only uses the Python standard library so it can run on the perf VM
without installing any packages.
"""

import argparse
import glob
import json
import os
import sys


NS_PER_MS = 1_000_000.0


def _load_benchmarks(directory):
    """Return {key: record} for every benchmark found under *directory*.

    key = "Type.Method(Parameters)".  record carries the mean (ns), the
    allocated bytes/op, and the display fields used for reporting.
    """
    records = {}
    pattern = os.path.join(directory, "**", "*-report-full.json")
    for path in sorted(glob.glob(pattern, recursive=True)):
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                data = json.load(handle)
        except (OSError, ValueError) as exc:
            print(f"WARNING: could not parse {path}: {exc}", file=sys.stderr)
            continue

        for bench in data.get("Benchmarks", []):
            btype = bench.get("Type", "")
            method = bench.get("Method", "")
            params = bench.get("Parameters", "") or ""
            stats = bench.get("Statistics") or {}
            mean_ns = stats.get("Mean")
            if mean_ns is None:
                continue
            memory = bench.get("Memory") or {}
            alloc = memory.get("BytesAllocatedPerOperation")

            key = f"{btype}.{method}({params})"
            records[key] = {
                "benchmarkName": btype,
                "methodName": method,
                "parameterSignature": params,
                "meanNs": float(mean_ns),
                "allocatedBytes": float(alloc) if alloc is not None else None,
            }
    return records


def _pct(baseline, current):
    if baseline in (None, 0):
        return None
    return (current - baseline) / baseline * 100.0


def build_comparison(baseline_dir, current_dir, threshold_pct):
    baseline = _load_benchmarks(baseline_dir)
    current = _load_benchmarks(current_dir)

    entries = []
    for key in sorted(set(baseline) | set(current)):
        b = baseline.get(key)
        c = current.get(key)
        ref = c or b
        entry = {
            "key": key,
            "benchmarkName": ref["benchmarkName"],
            "methodName": ref["methodName"],
            "parameterSignature": ref["parameterSignature"],
            "baselineMeanMs": (b["meanNs"] / NS_PER_MS) if b else None,
            "currentMeanMs": (c["meanNs"] / NS_PER_MS) if c else None,
            "baselineAllocBytes": b["allocatedBytes"] if b else None,
            "currentAllocBytes": c["allocatedBytes"] if c else None,
        }

        if b and c:
            entry["meanDeltaPct"] = _pct(b["meanNs"], c["meanNs"])
            entry["meanRatio"] = (c["meanNs"] / b["meanNs"]) if b["meanNs"] else None
            if b["allocatedBytes"] and c["allocatedBytes"] is not None:
                entry["allocDeltaPct"] = _pct(b["allocatedBytes"], c["allocatedBytes"])
            else:
                entry["allocDeltaPct"] = None
            delta = entry["meanDeltaPct"]
            if delta is None:
                entry["status"] = "unknown"
            elif delta > threshold_pct:
                entry["status"] = "regression"
            elif delta < -threshold_pct:
                entry["status"] = "improvement"
            else:
                entry["status"] = "unchanged"
        elif c and not b:
            entry["meanDeltaPct"] = None
            entry["meanRatio"] = None
            entry["allocDeltaPct"] = None
            entry["status"] = "current-only"
        else:
            entry["meanDeltaPct"] = None
            entry["meanRatio"] = None
            entry["allocDeltaPct"] = None
            entry["status"] = "baseline-only"

        entries.append(entry)

    # Sort worst-regression first, then by name for stability.
    def _sort_key(e):
        d = e["meanDeltaPct"]
        return (-(d if d is not None else -1e18), e["key"])

    entries.sort(key=_sort_key)
    return entries


def _fmt_ms(value):
    return f"{value:.4f}" if value is not None else "-"


def _fmt_pct(value):
    if value is None:
        return "-"
    return f"{value:+.2f}%"


def _fmt_bytes(value):
    return f"{int(value)}" if value is not None else "-"


def render_markdown(entries, baseline_version, threshold_pct):
    regressions = [e for e in entries if e["status"] == "regression"]
    improvements = [e for e in entries if e["status"] == "improvement"]

    lines = []
    lines.append("# SqlClient Performance Comparison")
    lines.append("")
    lines.append(f"Baseline: **{baseline_version}** &nbsp;|&nbsp; "
                 f"Regression threshold: **{threshold_pct:.0f}%**")
    lines.append("")
    lines.append(f"- Benchmarks compared: **{len(entries)}**")
    lines.append(f"- Regressions (slower > {threshold_pct:.0f}%): **{len(regressions)}**")
    lines.append(f"- Improvements (faster > {threshold_pct:.0f}%): **{len(improvements)}**")
    lines.append("")
    lines.append("| Status | Benchmark | Method | Params | Baseline (ms) | "
                 "Current (ms) | Mean Δ | Alloc Δ |")
    lines.append("| ------ | --------- | ------ | ------ | ------------- | "
                 "------------ | ------ | ------- |")

    icon = {
        "regression": "🔴 regression",
        "improvement": "🟢 improvement",
        "unchanged": "⚪ unchanged",
        "current-only": "🆕 new",
        "baseline-only": "➖ removed",
        "unknown": "❔",
    }
    for e in entries:
        lines.append(
            "| {status} | {name} | {method} | {params} | {base} | {cur} | "
            "{delta} | {alloc} |".format(
                status=icon.get(e["status"], e["status"]),
                name=e["benchmarkName"],
                method=e["methodName"],
                params=e["parameterSignature"] or "-",
                base=_fmt_ms(e["baselineMeanMs"]),
                cur=_fmt_ms(e["currentMeanMs"]),
                delta=_fmt_pct(e["meanDeltaPct"]),
                alloc=_fmt_pct(e["allocDeltaPct"]),
            )
        )
    lines.append("")
    return "\n".join(lines)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--baseline-dir", required=True)
    parser.add_argument("--current-dir", required=True)
    parser.add_argument("--out-md", required=True)
    parser.add_argument("--out-json", required=True)
    parser.add_argument("--baseline-version", default="baseline")
    parser.add_argument("--threshold", type=float, default=10.0,
                        help="Percent slowdown that counts as a regression.")
    parser.add_argument("--fail-on-regression", action="store_true",
                        help="Exit non-zero if any regression is detected.")
    args = parser.parse_args(argv)

    entries = build_comparison(args.baseline_dir, args.current_dir, args.threshold)

    os.makedirs(os.path.dirname(os.path.abspath(args.out_md)), exist_ok=True)
    os.makedirs(os.path.dirname(os.path.abspath(args.out_json)), exist_ok=True)

    markdown = render_markdown(entries, args.baseline_version, args.threshold)
    with open(args.out_md, "w", encoding="utf-8") as handle:
        handle.write(markdown + "\n")

    with open(args.out_json, "w", encoding="utf-8") as handle:
        json.dump(
            {
                "baselineVersion": args.baseline_version,
                "thresholdPct": args.threshold,
                "entries": entries,
            },
            handle,
            indent=2,
        )

    regressions = [e for e in entries if e["status"] == "regression"]
    print(f"Compared {len(entries)} benchmarks; {len(regressions)} regression(s).")
    print(f"Wrote {args.out_md} and {args.out_json}.")

    if args.fail_on_regression and regressions:
        print("Regressions detected; failing as requested.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
