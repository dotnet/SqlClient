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
import hashlib
import json
import os
import sys
from pathlib import Path


NS_PER_MS = 1_000_000.0
DEFAULT_THRESHOLD_PCT = 10.0
STATUS_LABELS = {
    "regression": "\U0001f534 regression",
    "improvement": "\U0001f7e2 improvement",
    "unchanged": "\u26aa unchanged",
    "current-only": "\U0001f195 new",
    "baseline-only": "\u2796 removed",
    "unknown": "\u2754",
}


def classify_change(delta, threshold_pct):
    """Classify an unrounded relative change; equality with either boundary is neutral."""
    if delta is None:
        return "unknown"
    if delta > threshold_pct:
        return "regression"
    if delta < -threshold_pct:
        return "improvement"
    return "unchanged"


def _execution_conditions(path, directory):
    """Keep new runner outputs from silently pairing across different execution conditions."""
    root = Path(directory).resolve()
    parent = Path(path).resolve().parent
    while parent == root or root in parent.parents:
        manifest = parent / "process.json"
        if manifest.is_file():
            data = json.loads(manifest.read_text(encoding="utf-8-sig"))
            conditions = data.get("ExecutionConditions")
            if not isinstance(conditions, dict):
                raise ValueError(f"Missing execution conditions: {manifest}")
            requested = conditions.get("AdditionalRttMilliseconds")
            applied = data.get("AppliedAdditionalRttMilliseconds")
            if requested is None or applied != requested:
                raise ValueError(f"Mismatched requested/applied RTT: {manifest}")
            if data.get("EffectiveProcessIsolation") != conditions.get("ProcessIsolation"):
                raise ValueError(f"Mismatched process isolation: {manifest}")
            return conditions
        if parent == root:
            break
        parent = parent.parent
    if "processes" in Path(path).resolve().relative_to(root).parts:
        raise ValueError(f"Missing process manifest for isolated benchmark report: {path}")
    return None


def _load_benchmarks(directory):
    """Return {key: record} for every benchmark found under *directory*.

    key = "Type.Method(Parameters)".  record carries the mean (ns), the
    allocated bytes/op, and the display fields used for reporting.
    """
    records = {}
    pattern = os.path.join(directory, "**", "*-report-full.json")
    for path in sorted(glob.glob(pattern, recursive=True)):
        conditions = _execution_conditions(path, directory)
        signature = (hashlib.sha256(json.dumps(conditions, sort_keys=True).encode()).hexdigest()[:16]
                     if conditions is not None else None)
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
            if signature is not None:
                key += f"[conditions={signature}]"
            records[key] = {
                "benchmarkName": btype,
                "methodName": method,
                "parameterSignature": params,
                "meanNs": float(mean_ns),
                "allocatedBytes": float(alloc) if alloc is not None else None,
                "executionConditions": conditions,
                "conditionSignature": signature,
            }
    return records


def _pct(baseline, current):
    if baseline is None or current is None:
        return None
    if baseline == 0:
        # A percentage change from a zero baseline is undefined (0 -> 0 is no change; 0 -> X is an
        # infinite increase).  Return 0.0 only for the genuine no-change case; otherwise None, and let
        # callers surface the raw before/after values so a 0 -> X regression is still visible.
        return 0.0 if current == 0 else None
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
            "executionConditions": ref["executionConditions"],
            "conditionSignature": ref["conditionSignature"],
            "baselineMeanMs": (b["meanNs"] / NS_PER_MS) if b else None,
            "currentMeanMs": (c["meanNs"] / NS_PER_MS) if c else None,
            "baselineAllocBytes": b["allocatedBytes"] if b else None,
            "currentAllocBytes": c["allocatedBytes"] if c else None,
        }

        if b and c:
            entry["meanDeltaPct"] = _pct(b["meanNs"], c["meanNs"])
            entry["meanRatio"] = (c["meanNs"] / b["meanNs"]) if b["meanNs"] else None
            # Compute the allocation delta whenever both sides report a value.  A 0-byte baseline is
            # valid, so gate on 'is not None' rather than truthiness -- gating on truthiness would drop
            # a real 0 -> X allocation regression.  The percentage itself is undefined for a 0 baseline
            # (see _pct); the raw baseline/current byte counts on the entry keep 0 -> X visible.
            if b["allocatedBytes"] is not None and c["allocatedBytes"] is not None:
                entry["allocDeltaPct"] = _pct(b["allocatedBytes"], c["allocatedBytes"])
            else:
                entry["allocDeltaPct"] = None
            entry["status"] = classify_change(entry["meanDeltaPct"], threshold_pct)
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


def _fmt_alloc(entry):
    """Alloc column: a percentage when it is defined, otherwise the raw byte transition so a
    0 -> X regression (undefined as a percentage) is still shown rather than collapsing to '-'."""
    pct = entry.get("allocDeltaPct")
    if pct is not None:
        return f"{pct:+.2f}%"
    base = entry.get("baselineAllocBytes")
    cur = entry.get("currentAllocBytes")
    if base is not None and cur is not None and base != cur:
        return f"{int(base)} → {int(cur)} B"
    return "-"


def _fmt_parameters(entry):
    parameters = entry["parameterSignature"] or "-"
    if entry["status"] in ("current-only", "baseline-only") and entry.get("conditionSignature"):
        parameters += f" [conditions={entry['conditionSignature']}]"
    return parameters


def render_markdown(entries, baseline_version, threshold_pct):
    regressions = [e for e in entries if e["status"] == "regression"]
    improvements = [e for e in entries if e["status"] == "improvement"]

    lines = []
    lines.append("# SqlClient Performance Comparison")
    lines.append("")
    lines.append(f"Baseline: **{baseline_version}** &nbsp;|&nbsp; "
                 f"Regression threshold: **{threshold_pct:g}%**")
    lines.append("")
    lines.append(f"- Benchmarks compared: **{len(entries)}**")
    lines.append(f"- Regressions (slower > {threshold_pct:g}%): **{len(regressions)}**")
    lines.append(f"- Improvements (faster > {threshold_pct:g}%): **{len(improvements)}**")
    lines.append("")
    lines.append("| Status | Benchmark | Method | Params | Baseline (ms) | "
                 "Current (ms) | Mean Δ | Alloc Δ |")
    lines.append("| ------ | --------- | ------ | ------ | ------------- | "
                 "------------ | ------ | ------- |")

    for e in entries:
        lines.append(
            "| {status} | {name} | {method} | {params} | {base} | {cur} | "
            "{delta} | {alloc} |".format(
                status=STATUS_LABELS.get(e["status"], e["status"]),
                name=e["benchmarkName"],
                method=e["methodName"],
                params=_fmt_parameters(e),
                base=_fmt_ms(e["baselineMeanMs"]),
                cur=_fmt_ms(e["currentMeanMs"]),
                delta=_fmt_pct(e["meanDeltaPct"]),
                alloc=_fmt_alloc(e),
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
    parser.add_argument("--threshold", type=float, default=DEFAULT_THRESHOLD_PCT,
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
