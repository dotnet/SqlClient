#!/usr/bin/env python3
"""Interleaved, best-of-N benchmark orchestrator for the SqlClient perf pipeline.

Implements the two structural noise-reduction controls from InternalDriverTools
wiki 339 ("Reducing Noise in Performance Tests"):

* **Interleaving (§2.2 / §2.3)** — instead of running the whole baseline suite and
  then the whole candidate suite (so a given benchmark is measured tens of minutes
  apart), this runs *one benchmark unit at a time*, executing the baseline build and
  the candidate build back-to-back for that unit before moving on.  Any slow host
  drift then affects both sides of each comparison roughly equally.

* **Best-of-N confirmation (§2.6)** — a single measurement can flag a regression
  purely from noise.  After the first interleaved pass, only the units that showed a
  regression are re-run N-1 more times (still interleaved); a regression is
  *confirmed* only when a strict majority of the N passes agree.  This is what makes
  the ``--fail-on-regression`` gate trustworthy.

The benchmark executable is the ``PerformanceTests`` app built two ways (same test
sources, different Microsoft.Data.SqlClient reference).  It understands two env vars
added for this orchestrator:

* ``PERF_LIST_BENCHMARKS`` — print the enabled unit names and exit.
* ``PERF_BENCHMARK=<Name>`` — run only that unit.

Only the Python standard library is used so it runs on the perf VM unchanged.
"""

import argparse
import glob
import json
import os
import shutil
import subprocess
import sys


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import compare_perf  # noqa: E402  (local module, sits next to this script)


# --------------------------------------------------------------------------------------------------
# CPU affinity (client pinning, wiki §2.4) — best effort, cross platform.
# --------------------------------------------------------------------------------------------------
def parse_cpus(spec):
    """Parse a CPU spec like "16-31" or "0,2,4" or "0-3,8" into a sorted int list."""
    if not spec:
        return []
    cpus = set()
    for part in spec.split(","):
        part = part.strip()
        if not part:
            continue
        if "-" in part:
            lo, hi = part.split("-", 1)
            cpus.update(range(int(lo), int(hi) + 1))
        else:
            cpus.add(int(part))
    return sorted(cpus)


def apply_affinity(proc, cpus):
    """Pin *proc* to *cpus*.  Never raises — pinning is an optimisation, not a gate."""
    if not cpus:
        return
    try:
        if hasattr(os, "sched_setaffinity"):  # Linux
            os.sched_setaffinity(proc.pid, set(cpus))
            return
        if os.name == "nt":  # Windows
            import ctypes

            mask = 0
            for c in cpus:
                mask |= (1 << c)
            handle = int(proc._handle)  # noqa: SLF001  (Popen exposes the OS handle here)
            if ctypes.windll.kernel32.SetProcessAffinityMask(handle, ctypes.c_size_t(mask)) == 0:
                print(f"WARNING: SetProcessAffinityMask failed for pid {proc.pid}.", file=sys.stderr)
    except Exception as exc:  # noqa: BLE001
        print(f"WARNING: could not pin pid {getattr(proc, 'pid', '?')} to CPUs {cpus}: {exc}",
              file=sys.stderr)


# --------------------------------------------------------------------------------------------------
# Running one unit and collecting its artifacts.
# --------------------------------------------------------------------------------------------------
def run_unit_process(exe_dir, assembly, unit, cwd, cpus, log_path):
    """Run one benchmark *unit* from the build at *exe_dir* in *cwd*.

    Returns the subprocess return code.  Kept as a small seam so tests can substitute
    a fake runner.
    """
    os.makedirs(cwd, exist_ok=True)
    cmd = ["dotnet", os.path.join(exe_dir, assembly)]
    env = dict(os.environ)
    env["PERF_BENCHMARK"] = unit
    env.pop("PERF_LIST_BENCHMARKS", None)

    with open(log_path, "w", encoding="utf-8") as log:
        proc = subprocess.Popen(cmd, cwd=cwd, env=env, stdout=log,
                                stderr=subprocess.STDOUT)
        apply_affinity(proc, cpus)
        return proc.wait()


def _report_files(root):
    return sorted(glob.glob(os.path.join(root, "**", "*-report-full.json"), recursive=True))


def collect_results(cwd, dest):
    """Copy the BenchmarkDotNet reports produced under *cwd* into *dest*.

    Returns the set of benchmark ``Type`` names found (used to map a unit to the
    report rows it produced, so best-of-N can re-run the right unit for a flagged key).
    """
    os.makedirs(dest, exist_ok=True)
    types = set()
    artifacts = os.path.join(cwd, "BenchmarkDotNet.Artifacts")
    for path in _report_files(artifacts):
        shutil.copy2(path, os.path.join(dest, os.path.basename(path)))
        try:
            with open(path, "r", encoding="utf-8-sig") as fh:
                for bench in json.load(fh).get("Benchmarks", []):
                    if bench.get("Type"):
                        types.add(bench["Type"])
        except (OSError, ValueError):
            pass
    # Also keep the human-readable GitHub markdown reports alongside, best effort.
    for md in glob.glob(os.path.join(artifacts, "**", "*-report-github.md"), recursive=True):
        shutil.copy2(md, os.path.join(dest, os.path.basename(md)))
    return types


# --------------------------------------------------------------------------------------------------
# Interleaved passes.
# --------------------------------------------------------------------------------------------------
class Runner:
    """Holds the invariant run parameters and performs interleaved unit passes."""

    def __init__(self, baseline_dir, current_dir, assembly, work_dir, cpus):
        self.baseline_dir = baseline_dir
        self.current_dir = current_dir
        self.assembly = assembly
        self.work_dir = work_dir
        self.cpus = cpus

    def list_units(self):
        cmd = ["dotnet", os.path.join(self.current_dir, self.assembly)]
        env = dict(os.environ)
        env["PERF_LIST_BENCHMARKS"] = "1"
        env.pop("PERF_BENCHMARK", None)
        out = subprocess.check_output(cmd, cwd=self.work_dir, env=env, text=True)
        return [line.strip() for line in out.splitlines() if line.strip()]

    def _run_one(self, variant, exe_dir, unit, rep, agg_dir):
        cwd = os.path.join(self.work_dir, f"rep{rep}", variant, unit)
        if os.path.isdir(cwd):
            shutil.rmtree(cwd)
        os.makedirs(cwd, exist_ok=True)
        log_path = os.path.join(cwd, "run.log")
        rc = run_unit_process(exe_dir, self.assembly, unit, cwd, self.cpus, log_path)
        if rc != 0:
            _tail(log_path)
            raise RuntimeError(f"benchmark unit '{unit}' ({variant}, rep {rep}) failed (exit {rc}).")
        types = collect_results(cwd, agg_dir)
        if not types:
            _tail(log_path)
            raise RuntimeError(
                f"benchmark unit '{unit}' ({variant}, rep {rep}) produced no results "
                f"(a broken benchmark must not be reported as a pass).")
        return types

    def interleave(self, units, rep, baseline_agg, current_agg):
        """Run each unit baseline-then-candidate; aggregate reports per variant.

        Returns a dict mapping unit name -> set of report Type names it produced.
        """
        unit_types = {}
        for unit in units:
            print(f"[rep {rep}] {unit}: baseline -> candidate", flush=True)
            # Interleaved: measure baseline and candidate for this unit back-to-back.
            self._run_one("baseline", self.baseline_dir, unit, rep, baseline_agg)
            types = self._run_one("current", self.current_dir, unit, rep, current_agg)
            unit_types[unit] = types
        return unit_types


def _tail(path, n=40):
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as fh:
            lines = fh.readlines()
        sys.stderr.write("".join(lines[-n:]))
    except OSError:
        pass


# --------------------------------------------------------------------------------------------------
# Comparison + confirmation.
# --------------------------------------------------------------------------------------------------
def _regression_keys(baseline_dir, current_dir, threshold):
    entries = compare_perf.build_comparison(baseline_dir, current_dir, threshold)
    return entries, {e["key"] for e in entries if e["status"] == "regression"}


def orchestrate(runner, units, results_dir, threshold, reps, fail_on_regression):
    baseline_agg = os.path.join(results_dir, "baseline")
    current_agg = os.path.join(results_dir, "current")
    comparison_dir = os.path.join(results_dir, "comparison")
    os.makedirs(comparison_dir, exist_ok=True)

    # --- Pass 1: interleave every enabled unit -----------------------------------------------------
    unit_types = runner.interleave(units, 1, baseline_agg, current_agg)
    type_to_unit = {}
    for unit, types in unit_types.items():
        for t in types:
            type_to_unit[t] = unit

    entries, reg_keys_1 = _regression_keys(baseline_agg, current_agg, threshold)

    # Which units contain a rep-1 regression?  Those are the best-of-N candidates.
    candidate_units = []
    for e in entries:
        if e["status"] == "regression":
            unit = type_to_unit.get(e["benchmarkName"])
            if unit and unit not in candidate_units:
                candidate_units.append(unit)

    # Per-key regression tally across reps (rep 1 counts once).
    reg_counts = {k: 1 for k in reg_keys_1}

    # --- Passes 2..N: re-run only the candidate units, still interleaved ---------------------------
    if reps > 1 and candidate_units:
        print(f"Best-of-{reps}: confirming {len(candidate_units)} candidate unit(s): "
              f"{', '.join(candidate_units)}", flush=True)
        for rep in range(2, reps + 1):
            b_dir = os.path.join(results_dir, "reps", f"rep{rep}", "baseline")
            c_dir = os.path.join(results_dir, "reps", f"rep{rep}", "current")
            runner.interleave(candidate_units, rep, b_dir, c_dir)
            _, reg_keys_r = _regression_keys(b_dir, c_dir, threshold)
            for k in reg_keys_1:
                if k in reg_keys_r:
                    reg_counts[k] = reg_counts.get(k, 0) + 1

    # --- Confirmation verdict (strict majority of N) -----------------------------------------------
    total_reps = reps
    for e in entries:
        key = e["key"]
        if e["status"] == "regression":
            count = reg_counts.get(key, 0)
            confirmed = (count * 2) > total_reps
            e["regressionReps"] = count
            e["totalReps"] = total_reps
            e["confirmedRegression"] = confirmed
            if not confirmed:
                e["status"] = "regression-unconfirmed"
        else:
            e["regressionReps"] = 0
            e["totalReps"] = total_reps
            e["confirmedRegression"] = False

    confirmed = [e for e in entries if e.get("confirmedRegression")]
    unconfirmed = [e for e in entries if e["status"] == "regression-unconfirmed"]
    return entries, confirmed, unconfirmed


# --------------------------------------------------------------------------------------------------
# Rendering.
# --------------------------------------------------------------------------------------------------
_ICON = {
    "regression": "🔴 regression",
    "regression-unconfirmed": "🟠 regression (unconfirmed)",
    "improvement": "🟢 improvement",
    "unchanged": "⚪ unchanged",
    "current-only": "🆕 new",
    "baseline-only": "➖ removed",
    "unknown": "❔",
}


def render_markdown(entries, confirmed, unconfirmed, baseline_version, threshold, reps):
    improvements = [e for e in entries if e["status"] == "improvement"]
    lines = []
    lines.append("# SqlClient Performance Comparison (interleaved, best-of-N)")
    lines.append("")
    lines.append(f"Baseline: **{baseline_version}** &nbsp;|&nbsp; "
                 f"Regression threshold: **{threshold:.0f}%** &nbsp;|&nbsp; "
                 f"Confirmation runs: **N = {reps}**")
    lines.append("")
    lines.append(f"- Benchmarks compared: **{len(entries)}**")
    lines.append(f"- **Confirmed** regressions (majority of {reps}): **{len(confirmed)}**")
    lines.append(f"- Unconfirmed (flagged once, not reproduced): **{len(unconfirmed)}**")
    lines.append(f"- Improvements (faster > {threshold:.0f}%): **{len(improvements)}**")
    lines.append("")
    lines.append("| Status | Benchmark | Method | Params | Baseline (ms) | "
                 "Current (ms) | Mean Δ | Alloc Δ | Confirm |")
    lines.append("| ------ | --------- | ------ | ------ | ------------- | "
                 "------------ | ------ | ------- | ------- |")
    for e in entries:
        if e["totalReps"] > 1 and (e["confirmedRegression"] or e["status"] == "regression-unconfirmed"):
            confirm = f"{e['regressionReps']}/{e['totalReps']}"
        else:
            confirm = "-"
        lines.append(
            "| {status} | {name} | {method} | {params} | {base} | {cur} | "
            "{delta} | {alloc} | {confirm} |".format(
                status=_ICON.get(e["status"], e["status"]),
                name=e["benchmarkName"],
                method=e["methodName"],
                params=e["parameterSignature"] or "-",
                base=compare_perf._fmt_ms(e["baselineMeanMs"]),
                cur=compare_perf._fmt_ms(e["currentMeanMs"]),
                delta=compare_perf._fmt_pct(e["meanDeltaPct"]),
                alloc=compare_perf._fmt_pct(e["allocDeltaPct"]),
                confirm=confirm,
            )
        )
    lines.append("")
    return "\n".join(lines)


# --------------------------------------------------------------------------------------------------
# Entry point.
# --------------------------------------------------------------------------------------------------
def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--baseline-exe-dir", required=True,
                        help="Directory of the baseline (released package) build.")
    parser.add_argument("--current-exe-dir", required=True,
                        help="Directory of the candidate (from-source) build.")
    parser.add_argument("--assembly", default="PerformanceTests.dll")
    parser.add_argument("--results-dir", required=True)
    parser.add_argument("--work-dir", default=None,
                        help="Scratch directory for per-unit run dirs (default: <results>/../perf-interleave-work).")
    parser.add_argument("--threshold", type=float, default=10.0)
    parser.add_argument("--reps", type=int, default=3,
                        help="Total interleaved passes for a flagged unit (best-of-N). 1 disables confirmation.")
    parser.add_argument("--baseline-version", default="baseline")
    parser.add_argument("--client-cpus", default=os.environ.get("PERF_CLIENT_CPUS", ""),
                        help="CPU set to pin the benchmark client to, e.g. '16-31'.")
    parser.add_argument("--fail-on-regression", action="store_true",
                        help="Exit non-zero if any CONFIRMED regression is detected.")
    args = parser.parse_args(argv)

    if args.reps < 1:
        parser.error("--reps must be >= 1")

    results_dir = os.path.abspath(args.results_dir)
    work_dir = os.path.abspath(args.work_dir) if args.work_dir \
        else os.path.join(os.path.dirname(results_dir), "perf-interleave-work")
    os.makedirs(work_dir, exist_ok=True)
    os.makedirs(results_dir, exist_ok=True)

    cpus = parse_cpus(args.client_cpus)
    if cpus:
        print(f"Pinning benchmark client to CPUs {args.client_cpus} ({len(cpus)} core(s)).")
    else:
        print("PERF_CLIENT_CPUS not set; running without CPU pinning.", file=sys.stderr)

    runner = Runner(os.path.abspath(args.baseline_exe_dir),
                    os.path.abspath(args.current_exe_dir),
                    args.assembly, work_dir, cpus)

    units = runner.list_units()
    if not units:
        print("ERROR: no enabled benchmark units were reported by the current build.", file=sys.stderr)
        return 1
    print(f"Enabled units ({len(units)}): {', '.join(units)}")

    entries, confirmed, unconfirmed = orchestrate(
        runner, units, results_dir, args.threshold, args.reps, args.fail_on_regression)

    # Outputs.
    comparison_dir = os.path.join(results_dir, "comparison")
    md = render_markdown(entries, confirmed, unconfirmed, args.baseline_version, args.threshold, args.reps)
    with open(os.path.join(comparison_dir, "comparison.md"), "w", encoding="utf-8") as fh:
        fh.write(md + "\n")
    with open(os.path.join(comparison_dir, "comparison.json"), "w", encoding="utf-8") as fh:
        json.dump({
            "baselineVersion": args.baseline_version,
            "thresholdPct": args.threshold,
            "confirmationRuns": args.reps,
            "confirmedRegressions": len(confirmed),
            "unconfirmedRegressions": len(unconfirmed),
            "entries": entries,
        }, fh, indent=2)
    # Surface the comparison as the top-level run summary (collect-results attaches results/*.md).
    shutil.copyfile(os.path.join(comparison_dir, "comparison.md"),
                    os.path.join(results_dir, "summary.md"))

    print(f"Interleaved comparison complete: {len(confirmed)} confirmed regression(s), "
          f"{len(unconfirmed)} unconfirmed.")

    if args.fail_on_regression and confirmed:
        print("Confirmed regressions detected; failing as requested.", file=sys.stderr)
        for e in confirmed:
            print(f"  {e['key']}: {compare_perf._fmt_pct(e['meanDeltaPct'])} "
                  f"({e['regressionReps']}/{e['totalReps']} reps)", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
