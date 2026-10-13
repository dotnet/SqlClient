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
from pathlib import Path
import re
import shutil
import signal
import subprocess
import sys
import time
import uuid


SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, SCRIPT_DIR)
import compare_perf  # noqa: E402  (local module, sits next to this script)
import openasync_report  # noqa: E402
import scoreboard_report  # noqa: E402
import scoreboard_observer  # noqa: E402


# --------------------------------------------------------------------------------------------------
# CPU affinity (client pinning, wiki §2.4) — best effort, cross platform.
# --------------------------------------------------------------------------------------------------
def parse_cpus(spec):
    """Parse a CPU spec like "16-31" or "0,2,4" or "0-3,8" into a sorted int list.

    CPU pinning is a best-effort optimisation, never a gate, so a malformed spec must not
    fail the run: on any unparseable segment this warns and returns [] (i.e. no pinning)."""
    if not spec:
        return []
    cpus = set()
    try:
        for part in spec.split(","):
            part = part.strip()
            if not part:
                continue
            if "-" in part:
                lo, hi = part.split("-", 1)
                cpus.update(range(int(lo), int(hi) + 1))
            else:
                cpus.add(int(part))
    except ValueError:
        print(f"WARNING: could not parse CPU spec {spec!r}; running without CPU pinning.",
              file=sys.stderr)
        return []
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
            from ctypes import wintypes

            # The mask is pointer-sized (32 or 64 bits) and cannot span processor groups.
            mask_bits = ctypes.sizeof(ctypes.c_size_t) * 8
            if any(c >= mask_bits for c in cpus):
                print(f"WARNING: CPU index >= {mask_bits} in {cpus}; SetProcessAffinityMask "
                      f"cannot address it with a {mask_bits}-bit mask or span processor groups, "
                      f"so pid {proc.pid} runs without CPU pinning.",
                      file=sys.stderr)
                return
            mask = 0
            for c in cpus:
                mask |= (1 << c)
            kernel32 = ctypes.WinDLL("kernel32", use_last_error=True)
            set_affinity = kernel32.SetProcessAffinityMask
            set_affinity.argtypes = [wintypes.HANDLE, ctypes.c_size_t]
            set_affinity.restype = wintypes.BOOL
            handle = wintypes.HANDLE(int(proc._handle))  # noqa: SLF001 (Popen's OS handle)
            if not set_affinity(handle, ctypes.c_size_t(mask)):
                error = ctypes.get_last_error()
                print(f"WARNING: SetProcessAffinityMask failed for pid {proc.pid}, "
                      f"mask {mask:#x}: [WinError {error}] {ctypes.FormatError(error).strip()}",
                      file=sys.stderr)
    except Exception as exc:  # noqa: BLE001
        print(f"WARNING: could not pin pid {getattr(proc, 'pid', '?')} to CPUs {cpus}: {exc}",
              file=sys.stderr)


# --------------------------------------------------------------------------------------------------
# Running one unit and collecting its artifacts.
# --------------------------------------------------------------------------------------------------
def run_unit_process(exe_dir, assembly, unit, cwd, cpus, log_path, env_overrides=None,
                     timeout_seconds=None, diagnostic_tools=None):
    """Run one benchmark *unit* from the build at *exe_dir* in *cwd*.

    Returns the subprocess return code.  Kept as a small seam so tests can substitute
    a fake runner.  *env_overrides*, when given, is applied on top of the inherited
    environment (e.g. a per-variant RUNNER_CONFIG so baseline and current can run with
    different SqlClient behaviour flags, such as comparing the legacy vs new connection
    pool from the SAME build).
    """
    os.makedirs(cwd, exist_ok=True)
    cmd = ["dotnet", os.path.join(exe_dir, assembly)]
    env = dict(os.environ)
    env["PERF_BENCHMARK"] = unit
    env.pop("PERF_LIST_BENCHMARKS", None)
    if env_overrides:
        env.update(env_overrides)

    with open(log_path, "w", encoding="utf-8") as log:
        proc = subprocess.Popen(cmd, cwd=cwd, env=env, stdout=log,
                                stderr=subprocess.STDOUT,
                                start_new_session=timeout_seconds is not None and os.name != "nt")
        apply_affinity(proc, cpus)
        observer = None
        if diagnostic_tools:
            observer = scoreboard_observer.Observer(cwd, unit, proc.pid, diagnostic_tools)
            observer.thread.start()
        try:
            if observer:
                deadline = time.monotonic() + timeout_seconds
                while proc.poll() is None:
                    if observer.error:
                        raise RuntimeError(observer.error)
                    if time.monotonic() >= deadline:
                        raise subprocess.TimeoutExpired(cmd, timeout_seconds)
                    time.sleep(.05)
                observer.finish()
                return proc.returncode
            return proc.wait(timeout=timeout_seconds)
        except (subprocess.TimeoutExpired, RuntimeError) as error:
            # Kill the BDN child too, not just its launcher, before running another case.
            if observer and proc.poll() is not None:
                pass
            elif os.name == "nt":
                subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                               stdout=log, stderr=subprocess.STDOUT, check=True, timeout=30)
            else:
                os.killpg(proc.pid, signal.SIGKILL)
            proc.wait(timeout=30)
            if observer:
                try:
                    observer.finish()
                except RuntimeError:
                    pass
            if isinstance(error, RuntimeError):
                raise
            raise RuntimeError(f"benchmark unit '{unit}' exceeded {timeout_seconds}s watchdog") from None


def _report_files(root):
    return sorted(glob.glob(os.path.join(root, "**", "*-report-full.json"), recursive=True))


def collect_results(cwd, dest, unit=None, strict=False, expected_warmups=None):
    """Copy the BenchmarkDotNet reports produced under *cwd* into *dest*.

    Returns the set of benchmark ``Type`` names found (used to map a unit to the
    report rows it produced, so best-of-N can re-run the right unit for a flagged key).
    """
    if unit is not None:
        if not re.fullmatch(r"[A-Za-z0-9_-]+", unit):
            raise ValueError("Unsafe benchmark unit name")
        dest = os.path.join(dest, unit)
        if os.path.isdir(dest):
            shutil.rmtree(dest)
    os.makedirs(dest, exist_ok=True)
    types = set()
    artifacts = os.path.join(cwd, "BenchmarkDotNet.Artifacts")
    # Preserve supplements even when BDN reported an exception.
    for path in glob.glob(os.path.join(artifacts, "**", "openasync-*.json"), recursive=True):
        subdir = os.path.join(dest, "warmup") if "warmup" in Path(path).parts else dest
        os.makedirs(subdir, exist_ok=True)
        shutil.copy2(path, os.path.join(subdir, os.path.basename(path)))
    external = os.path.join(artifacts, "external-diagnostics")
    if os.path.isdir(external):
        shutil.copytree(external, os.path.join(dest, "external-diagnostics"), dirs_exist_ok=True)
    reports = _report_files(artifacts)
    if strict and len(reports) != 1:
        raise ValueError(f"{unit}: expected one BDN report for the selected parameter case")
    for path in reports:
        shutil.copy2(path, os.path.join(dest, os.path.basename(path)))
        try:
            with open(path, "r", encoding="utf-8-sig") as fh:
                benchmarks = json.load(fh).get("Benchmarks", [])
                if strict and len(benchmarks) != 1:
                    raise ValueError("Expected a singleton BDN parameter case")
                for bench in benchmarks:
                    if strict:
                        # BDN can truncate Parameters for display; FullName retains the actual value.
                        parameters = bench.get("FullName") or bench.get("Parameters", "")
                        if not isinstance(parameters, str) or not re.search(
                                r"(?<![A-Za-z0-9_-])" + re.escape(unit) + r"(?![A-Za-z0-9_-])",
                                parameters):
                            raise ValueError("BDN parameter case does not match selected unit")
                        stats = bench.get("Statistics") or {}
                        openasync_report.number(stats.get("Mean"), "BDN Mean", positive=True)
                        if stats.get("N") != 3:
                            raise ValueError("Expected all 3 actual BDN iterations, without outlier removal")
                        measurements = bench.get("Measurements")
                        if not isinstance(measurements, list):
                            raise ValueError("Missing BDN measurement records")
                        if expected_warmups is not None:
                            warmup = [item for item in measurements
                                      if item.get("IterationMode") == "Workload"
                                      and item.get("IterationStage") == "Warmup"]
                            if len(warmup) != expected_warmups:
                                raise ValueError("BDN warmup records do not match the selected profile")
                        actual = [item for item in measurements
                                  if item.get("IterationMode") == "Workload"
                                  and item.get("IterationStage") == "Actual"]
                        if len(actual) != 3:
                            raise ValueError("Expected exactly 3 BDN Workload/Actual records")
                        indices = set()
                        for item in actual:
                            launch = openasync_report.number(item.get("LaunchIndex"), "BDN LaunchIndex",
                                                             integer=True)
                            iteration = openasync_report.number(item.get("IterationIndex"),
                                                                "BDN IterationIndex", integer=True)
                            openasync_report.number(item.get("Nanoseconds"), "BDN Nanoseconds", positive=True)
                            if item.get("Operations") != 1:
                                raise ValueError("Expected one BDN invocation per measured iteration")
                            indices.add((launch, iteration))
                        if len(indices) != 3 or len({launch for launch, _ in indices}) != 1:
                            raise ValueError("Expected 3 distinct actual iterations in one BDN launch")
                    if bench.get("Type"):
                        types.add(bench["Type"])
        except (OSError, ValueError):
            if strict:
                raise
    # Also keep the human-readable GitHub markdown reports alongside, best effort.
    for md in glob.glob(os.path.join(artifacts, "**", "*-report-github.md"), recursive=True):
        shutil.copy2(md, os.path.join(dest, os.path.basename(md)))
    return types


# --------------------------------------------------------------------------------------------------
# Interleaved passes.
# --------------------------------------------------------------------------------------------------
class Runner:
    """Holds the invariant run parameters and performs interleaved unit passes."""

    def __init__(self, baseline_dir, current_dir, assembly, work_dir, cpus,
                 baseline_runner_config=None, current_runner_config=None,
                 benchmark_profile="default", unit_timeout_seconds=None, scoreboard_repetitions=3):
        self.baseline_dir = baseline_dir
        self.current_dir = current_dir
        self.assembly = assembly
        self.work_dir = work_dir
        self.cpus = cpus
        # Optional per-variant RUNNER_CONFIG override (e.g. --switch-under-test needs baseline
        # and current to run with different SqlClient behaviour flags even though they share the
        # same build). None means "no override" -> both variants use the ambient RUNNER_CONFIG.
        self.baseline_runner_config = baseline_runner_config
        self.current_runner_config = current_runner_config
        self.benchmark_profile = benchmark_profile
        self.unit_timeout_seconds = unit_timeout_seconds
        self.scoreboard_repetitions = scoreboard_repetitions
        self.diagnostic_tools = None

    def list_units(self, variant="current"):
        exe_dir = self.baseline_dir if variant == "baseline" else self.current_dir
        config = self.baseline_runner_config if variant == "baseline" else self.current_runner_config
        cmd = ["dotnet", os.path.join(exe_dir, self.assembly)]
        env = dict(os.environ)
        env["PERF_LIST_BENCHMARKS"] = "1"
        env.pop("PERF_BENCHMARK", None)
        if config:
            env["RUNNER_CONFIG"] = config
        out = subprocess.check_output(cmd, cwd=self.work_dir, env=env, text=True,
                                      timeout=60 if self.benchmark_profile != "default" else None)
        return [line.strip() for line in out.splitlines() if line.strip()]

    def _run_one(self, variant, exe_dir, unit, rep, agg_dir):
        cwd = os.path.join(self.work_dir, f"rep{rep}", variant, unit)
        if os.path.isdir(cwd):
            shutil.rmtree(cwd)
        os.makedirs(cwd, exist_ok=True)
        log_path = os.path.join(cwd, "run.log")
        runner_config = (self.baseline_runner_config if variant == "baseline"
                         else self.current_runner_config)
        env_overrides = {"RUNNER_CONFIG": runner_config} if runner_config else {}
        full = self.benchmark_profile == openasync_report.FULL_PROFILE
        if self.benchmark_profile in (openasync_report.PROFILE, openasync_report.FULL_PROFILE):
            env_overrides["PERF_RUN_ID"] = str(uuid.uuid4())
            env_overrides["PERF_PROCESS_REPETITION"] = str(rep)
        options = {}
        if full and unit.endswith("-Diagnostic"):
            if (not self.diagnostic_tools or not self.diagnostic_tools.get("stack")
                    or not self.diagnostic_tools.get("sqlcmd")
                    or self.diagnostic_tools.get("sql_capability_verified") is not True):
                raise RuntimeError("Diagnostic case requires initialized external collectors")
            env_overrides["PERF_DIAGNOSTIC_COLLECTOR"] = "1"
            options["diagnostic_tools"] = self.diagnostic_tools
        try:
            rc = run_unit_process(exe_dir, self.assembly, unit, cwd, self.cpus, log_path,
                                  env_overrides=env_overrides or None,
                                  timeout_seconds=self.unit_timeout_seconds, **options)
        except Exception:
            if full:
                collect_results(cwd, agg_dir, unit=unit)
            raise
        if rc != 0:
            if self.benchmark_profile != "default":
                collect_results(cwd, agg_dir, unit=unit)
            _tail(log_path)
            raise RuntimeError(f"benchmark unit '{unit}' ({variant}, rep {rep}) failed (exit {rc}).")
        preliminary = self.benchmark_profile in (openasync_report.PROFILE, openasync_report.FULL_PROFILE)
        types = collect_results(cwd, agg_dir, unit=unit if preliminary else None, strict=preliminary,
                                expected_warmups=2 if full else None)
        if full:
            scoreboard_report.load_case(os.path.join(agg_dir, unit), unit, rep)
        elif preliminary:
            openasync_report.load_case(os.path.join(agg_dir, unit), unit)
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
        full = self.benchmark_profile == openasync_report.FULL_PROFILE
        if full:
            units = counterbalanced_units(units, rep)
        for unit in units:
            print(f"[rep {rep}] {unit}: "
                  f"{'baseline -> ' if self.baseline_dir else ''}candidate", flush=True)
            # Interleaved: measure baseline and candidate for this unit back-to-back.
            current_first = full and rep % 2 == 0
            if current_first:
                types = self._run_one("current", self.current_dir, unit, rep, current_agg)
            if self.baseline_dir:
                self._run_one("baseline", self.baseline_dir, unit, rep, baseline_agg)
            if not current_first:
                types = self._run_one("current", self.current_dir, unit, rep, current_agg)
            unit_types[unit] = types
        return unit_types


def counterbalanced_units(units, repetition):
    def key(unit):
        _, scenario, api, concurrency, *suffix = unit.split("-")
        api_order = ("Sync", "Async") if repetition % 2 else ("Async", "Sync")
        return (scenario, int(concurrency), bool(suffix), api_order.index(api))
    return sorted(units, key=key)


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


def orchestrate(runner, units, results_dir, threshold, reps):
    baseline_agg = os.path.join(results_dir, "baseline")
    current_agg = os.path.join(results_dir, "current")
    comparison_dir = os.path.join(results_dir, "comparison")
    os.makedirs(comparison_dir, exist_ok=True)

    if runner.benchmark_profile == openasync_report.FULL_PROFILE:
        entries = []
        for repetition in range(1, runner.scoreboard_repetitions + 1):
            root = os.path.join(results_dir, "repetitions", f"rep{repetition}")
            runner.interleave(units, repetition, os.path.join(root, "baseline"), os.path.join(root, "current"))
            for unit in units:
                rows = compare_perf.build_comparison(os.path.join(root, "baseline", unit),
                                                     os.path.join(root, "current", unit), threshold)
                for row in rows:
                    row.update(parameterSignature=f"Case={unit}", processRepetition=repetition,
                               key=f"{unit}/rep{repetition}", status="report-only", regressionReps=0,
                               totalReps=runner.scoreboard_repetitions, confirmedRegression=False)
                entries.extend(rows)
        scoreboard_report.write_summary(results_dir, units, bool(runner.baseline_dir),
                                        runner.scoreboard_repetitions)
        return entries, [], []
    # --- Pass 1: interleave every enabled unit -----------------------------------------------------
    unit_types = runner.interleave(units, 1, baseline_agg, current_agg)
    if runner.benchmark_profile == openasync_report.CONTROLS_PROFILE:
        entries = compare_perf.build_comparison(baseline_agg, current_agg, threshold)
        for entry in entries:
            entry.update(status="report-only", regressionReps=0, totalReps=1, confirmedRegression=False)
        return entries, [], []
    if runner.benchmark_profile == openasync_report.PROFILE:
        openasync_report.write_summary(results_dir, units, bool(runner.baseline_dir))
        # BDN batch results remain available, but are not used for a preliminary regression verdict.
        entries = []
        for unit in units:
            unit_entries = compare_perf.build_comparison(
                os.path.join(baseline_agg, unit), os.path.join(current_agg, unit), threshold)
            # Parameter display truncation can hide E1/E2, so never key across case directories.
            for entry in unit_entries:
                entry["parameterSignature"] = f"Case={unit}"
                entry["key"] = f"{entry['benchmarkName']}.{entry['methodName']}(Case={unit})"
            entries.extend(unit_entries)
        for entry in entries:
            entry.update(status="report-only", regressionReps=0, totalReps=1,
                         confirmedRegression=False)
        return entries, [], []
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


def render_markdown(entries, confirmed, unconfirmed, baseline_version, threshold, reps,
                    benchmark_profile="default"):
    if benchmark_profile == openasync_report.CONTROLS_PROFILE:
        lines = ["# OpenAsync controls — existing BDN workloads", "",
                 "Report-only M1/M2 controls: SqlConnection and ConnectionPoolChurn. Existing workload "
                 "and BDN job settings are unchanged. One pass; no regression-triggered reruns.", "",
                 "| Benchmark | Method | Parameters | Baseline mean ms | Current mean ms |",
                 "| --- | --- | --- | ---: | ---: |"]
        for entry in entries:
            lines.append(f"| {entry['benchmarkName']} | {entry['methodName']} | {entry['parameterSignature']} | "
                         f"{compare_perf._fmt_ms(entry['baselineMeanMs'])} | "
                         f"{compare_perf._fmt_ms(entry['currentMeanMs'])} |")
        return "\n".join(lines)
    if benchmark_profile != "default":
        full = benchmark_profile == openasync_report.FULL_PROFILE
        lines = ["# OpenAsync BDN batch results" if full else "# Preliminary OpenAsync BDN batch results",
                 "", scoreboard_report.NOTICE if full else openasync_report.NOTICE, "",
                 "See **openasync-summary.md** and **openasync-summary.json** for per-invocation "
                 "individual-open measurements. BDN means below are instrumented batch means.", "",
                 "| Benchmark | Parameters | Process repetition | Baseline batch ms | Current batch ms |",
                 "| --- | --- | ---: | ---: | ---: |"]
        for entry in entries:
            lines.append(f"| {entry['benchmarkName']} | {entry['parameterSignature']} | "
                         f"{entry.get('processRepetition', 1)} | "
                         f"{compare_perf._fmt_ms(entry['baselineMeanMs'])} | "
                         f"{compare_perf._fmt_ms(entry['currentMeanMs'])} |")
        return "\n".join(lines)
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
    parser.add_argument("--baseline-exe-dir",
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
    parser.add_argument("--baseline-runner-config", default=None,
                        help="Override RUNNER_CONFIG for baseline-variant subprocesses only "
                             "(e.g. to force a different SqlClient behaviour flag for the "
                             "baseline, such as comparing the legacy vs new connection pool "
                             "from the same build). Omit to use the ambient RUNNER_CONFIG for "
                             "both variants (default behaviour).")
    parser.add_argument("--current-runner-config", default=None,
                        help="Override RUNNER_CONFIG for current-variant subprocesses only.")
    parser.add_argument("--client-cpus", default=os.environ.get("PERF_CLIENT_CPUS", ""),
                        help="CPU set to pin the benchmark client to, e.g. '16-31'.")
    parser.add_argument("--fail-on-regression", action="store_true",
                        help="Exit non-zero if any CONFIRMED regression is detected.")
    parser.add_argument("--benchmark-profile", choices=("default", openasync_report.PROFILE,
                                                       openasync_report.FULL_PROFILE, openasync_report.CONTROLS_PROFILE),
                        default="default")
    parser.add_argument("--scoreboard-repetitions", type=int, default=3,
                        help="Predeclared independent process repetitions for the full scoreboard, not best-of-N.")
    parser.add_argument("--unit-timeout-minutes", type=float, default=None,
                        help="Per-unit process-tree watchdog; preliminary profile defaults to 10.")
    args = parser.parse_args(argv)

    if args.reps < 1:
        parser.error("--reps must be >= 1")
    preliminary = args.benchmark_profile != "default"
    full = args.benchmark_profile == openasync_report.FULL_PROFILE
    if args.scoreboard_repetitions < 1:
        parser.error("--scoreboard-repetitions must be positive")
    if preliminary and args.fail_on_regression:
        parser.error("The selected profile is report-only; --fail-on-regression is not allowed")
    if not preliminary and not args.baseline_exe_dir:
        parser.error("--baseline-exe-dir is required for the default interleaved profile")
    if preliminary:
        args.reps = 1
        if args.unit_timeout_minutes is None:
            args.unit_timeout_minutes = 10
        if not 0 < args.unit_timeout_minutes <= 10:
            parser.error("preliminary --unit-timeout-minutes must be in (0, 10]")
    if args.unit_timeout_minutes is not None:
        openasync_report.number(args.unit_timeout_minutes, "unit timeout", positive=True)

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

    runner = Runner(os.path.abspath(args.baseline_exe_dir) if args.baseline_exe_dir else None,
                    os.path.abspath(args.current_exe_dir),
                    args.assembly, work_dir, cpus,
                    baseline_runner_config=args.baseline_runner_config,
                    current_runner_config=args.current_runner_config,
                    benchmark_profile=args.benchmark_profile,
                    scoreboard_repetitions=args.scoreboard_repetitions,
                    unit_timeout_seconds=args.unit_timeout_minutes * 60
                    if args.unit_timeout_minutes is not None else None)

    units = runner.list_units()
    if not units:
        print("ERROR: no enabled benchmark units were reported by the current build.", file=sys.stderr)
        return 1
    print(f"Enabled units ({len(units)}): {', '.join(units)}")
    if preliminary:
        expected = (openasync_report.CONTROL_UNITS if args.benchmark_profile == openasync_report.CONTROLS_PROFILE
                    else openasync_report.FULL_CASES if full else openasync_report.CASES)
        if len(units) != len(expected) or set(units) != set(expected):
            raise RuntimeError("Current build does not discover the exact selected profile's units")
        if runner.baseline_dir:
            baseline_units = runner.list_units("baseline")
            if len(baseline_units) != len(units) or set(baseline_units) != set(units):
                raise RuntimeError("Incompatible baseline: missing selected profile units; "
                                   "use current-only or a source baseline that supports the profile")
    if full:
        runner.diagnostic_tools = scoreboard_observer.prepare_tools(work_dir)

    entries, confirmed, unconfirmed = orchestrate(
        runner, units, results_dir, args.threshold, args.reps)

    # Outputs.
    comparison_dir = os.path.join(results_dir, "comparison")
    md = render_markdown(entries, confirmed, unconfirmed, args.baseline_version, args.threshold, args.reps,
                         args.benchmark_profile)
    with open(os.path.join(comparison_dir, "comparison.md"), "w", encoding="utf-8") as fh:
        fh.write(md + "\n")
    with open(os.path.join(comparison_dir, "comparison.json"), "w", encoding="utf-8") as fh:
        json.dump({
            "benchmarkProfile": args.benchmark_profile,
            "baselineVersion": args.baseline_version,
            "thresholdPct": args.threshold,
            "confirmationRuns": args.reps,
            "scoreboardRepetitions": args.scoreboard_repetitions if full else 1,
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
