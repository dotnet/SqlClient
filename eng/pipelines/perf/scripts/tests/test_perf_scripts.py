"""Unit tests for the perf pipeline's comparison and best-of-N confirmation logic.

These cover the decision-making code that the regression gate depends on
(``compare_perf.build_comparison`` and ``interleave_perf.orchestrate``).  That logic decides
whether a pipeline run passes or fails once ``failOnRegression`` is turned on, so it needs to
be exercised without a SQL Server, without BenchmarkDotNet and without an ADX cluster.

Run from anywhere:

    python3 -m unittest discover -s eng/pipelines/perf/scripts/tests -v

Only the standard library is used, so this runs on any agent that already has Python 3.
"""

import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import compare_perf  # noqa: E402
import interleave_perf  # noqa: E402


NS_PER_MS = 1_000_000.0


def write_report(directory, type_name, mean_ms, alloc=None, method="Run", params=""):
    """Write a minimal BenchmarkDotNet ``*-report-full.json`` for one benchmark."""
    os.makedirs(directory, exist_ok=True)
    bench = {
        "Type": type_name,
        "Method": method,
        "Parameters": params,
        "Statistics": {"Mean": mean_ms * NS_PER_MS},
    }
    if alloc is not None:
        bench["Memory"] = {"BytesAllocatedPerOperation": alloc}
    path = os.path.join(directory, f"{type_name}.{method}-report-full.json")
    with open(path, "w", encoding="utf-8") as handle:
        json.dump({"Benchmarks": [bench]}, handle)
    return path


class TempDirTestCase(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp(prefix="perf-tests-")
        self.addCleanup(shutil.rmtree, self.tmp, True)

    def path(self, *parts):
        return os.path.join(self.tmp, *parts)


class PctTests(unittest.TestCase):
    """``_pct`` guards the zero-baseline case that would otherwise divide by zero."""

    def test_regular_change(self):
        self.assertAlmostEqual(compare_perf._pct(100.0, 110.0), 10.0)
        self.assertAlmostEqual(compare_perf._pct(100.0, 90.0), -10.0)

    def test_zero_to_zero_is_no_change(self):
        self.assertEqual(compare_perf._pct(0, 0), 0.0)

    def test_zero_to_nonzero_is_undefined_not_infinite(self):
        # Undefined rather than a bogus percentage; the raw byte counts keep it visible.
        self.assertIsNone(compare_perf._pct(0, 512))

    def test_missing_side_is_undefined(self):
        self.assertIsNone(compare_perf._pct(None, 1.0))
        self.assertIsNone(compare_perf._pct(1.0, None))


class BuildComparisonTests(TempDirTestCase):
    """Classification is what the gate reads, so pin down each status boundary."""

    def _compare(self, threshold=5.0):
        return compare_perf.build_comparison(self.path("baseline"), self.path("current"), threshold)

    def _by_type(self, entries):
        return {e["benchmarkName"]: e for e in entries}

    def test_status_classification(self):
        write_report(self.path("baseline"), "Slower", 100.0)
        write_report(self.path("current"), "Slower", 120.0)      # +20% -> regression
        write_report(self.path("baseline"), "Faster", 100.0)
        write_report(self.path("current"), "Faster", 80.0)       # -20% -> improvement
        write_report(self.path("baseline"), "Same", 100.0)
        write_report(self.path("current"), "Same", 102.0)        # +2% -> within threshold

        entries = self._by_type(self._compare(threshold=5.0))
        self.assertEqual(entries["Slower"]["status"], "regression")
        self.assertEqual(entries["Faster"]["status"], "improvement")
        self.assertEqual(entries["Same"]["status"], "unchanged")

    def test_threshold_is_exclusive(self):
        # Exactly at the threshold must NOT trip the gate; just past it must.
        write_report(self.path("baseline"), "AtThreshold", 100.0)
        write_report(self.path("current"), "AtThreshold", 105.0)
        self.assertEqual(self._by_type(self._compare(5.0))["AtThreshold"]["status"], "unchanged")

        write_report(self.path("current"), "AtThreshold", 105.1)
        self.assertEqual(self._by_type(self._compare(5.0))["AtThreshold"]["status"], "regression")

    def test_one_sided_benchmarks(self):
        write_report(self.path("baseline"), "Removed", 100.0)
        write_report(self.path("current"), "Added", 100.0)
        entries = self._by_type(self._compare())
        self.assertEqual(entries["Removed"]["status"], "baseline-only")
        self.assertEqual(entries["Added"]["status"], "current-only")
        # Neither side can be a regression: there is nothing to compare against.
        self.assertIsNone(entries["Removed"]["meanDeltaPct"])
        self.assertIsNone(entries["Added"]["meanDeltaPct"])

    def test_zero_byte_baseline_allocation_is_not_dropped(self):
        # A 0-byte baseline is a legitimate value; gating on truthiness would hide a 0 -> X
        # allocation regression entirely.
        write_report(self.path("baseline"), "Alloc", 100.0, alloc=0)
        write_report(self.path("current"), "Alloc", 100.0, alloc=4096)
        entry = self._by_type(self._compare())["Alloc"]
        self.assertEqual(entry["baselineAllocBytes"], 0)
        self.assertEqual(entry["currentAllocBytes"], 4096)
        self.assertIsNone(entry["allocDeltaPct"])  # undefined percentage, raw values retained

    def test_worst_regression_sorts_first(self):
        write_report(self.path("baseline"), "Mild", 100.0)
        write_report(self.path("current"), "Mild", 110.0)
        write_report(self.path("baseline"), "Severe", 100.0)
        write_report(self.path("current"), "Severe", 200.0)
        entries = self._compare()
        self.assertEqual(entries[0]["benchmarkName"], "Severe")

    def test_unparseable_report_is_skipped_not_fatal(self):
        write_report(self.path("baseline"), "Good", 100.0)
        write_report(self.path("current"), "Good", 100.0)
        os.makedirs(self.path("current"), exist_ok=True)
        with open(self.path("current", "broken-report-full.json"), "w", encoding="utf-8") as fh:
            fh.write("{ not json")
        entries = self._by_type(self._compare())
        self.assertEqual(entries["Good"]["status"], "unchanged")


class FakeRunner:
    """Stands in for the real benchmark runner in ``orchestrate``.

    *plan* maps unit -> {type_name: {rep: (baseline_ms, current_ms)}}.  *reported_types* lets a
    test simulate the unit list and the reported benchmark Type names drifting apart.
    """

    def __init__(self, plan, reported_types=None):
        self.plan = plan
        self.reported_types = reported_types
        self.calls = []

    def interleave(self, units, rep, baseline_dir, current_dir):
        self.calls.append((tuple(units), rep))
        result = {}
        for unit in units:
            types = self.plan[unit]
            for type_name, per_rep in types.items():
                baseline_ms, current_ms = per_rep.get(rep, per_rep[max(per_rep)])
                write_report(baseline_dir, type_name, baseline_ms)
                write_report(current_dir, type_name, current_ms)
            if self.reported_types is not None:
                result[unit] = set(self.reported_types.get(unit, []))
            else:
                result[unit] = set(types)
        return result


class OrchestrateTests(TempDirTestCase):
    """Best-of-N confirmation decides whether a flagged regression fails the pipeline."""

    def _run(self, runner, units, reps, threshold=5.0):
        return interleave_perf.orchestrate(runner, units, self.tmp, threshold, reps)

    def test_persistent_regression_is_confirmed(self):
        runner = FakeRunner({"UnitA": {"Bench": {1: (100.0, 130.0),
                                                2: (100.0, 130.0),
                                                3: (100.0, 130.0)}}})
        entries, confirmed, unconfirmed = self._run(runner, ["UnitA"], reps=3)
        self.assertEqual([e["benchmarkName"] for e in confirmed], ["Bench"])
        self.assertEqual(unconfirmed, [])
        self.assertEqual(entries[0]["regressionReps"], 3)
        self.assertTrue(entries[0]["confirmedRegression"])

    def test_flaky_regression_is_not_confirmed(self):
        # Slow on rep 1 only: noise, not a regression. Must not fail the gate.
        runner = FakeRunner({"UnitA": {"Bench": {1: (100.0, 130.0),
                                                2: (100.0, 100.0),
                                                3: (100.0, 100.0)}}})
        entries, confirmed, unconfirmed = self._run(runner, ["UnitA"], reps=3)
        self.assertEqual(confirmed, [])
        self.assertEqual([e["benchmarkName"] for e in unconfirmed], ["Bench"])
        self.assertEqual(entries[0]["status"], "regression-unconfirmed")
        self.assertEqual(entries[0]["regressionReps"], 1)

    def test_strict_majority_two_of_three(self):
        runner = FakeRunner({"UnitA": {"Bench": {1: (100.0, 130.0),
                                                2: (100.0, 130.0),
                                                3: (100.0, 100.0)}}})
        _, confirmed, _ = self._run(runner, ["UnitA"], reps=3)
        self.assertEqual(len(confirmed), 1)

    def test_only_regressed_units_are_rerun(self):
        runner = FakeRunner({
            "Slow": {"SlowBench": {1: (100.0, 130.0)}},
            "Fine": {"FineBench": {1: (100.0, 100.0)}},
        })
        self._run(runner, ["Slow", "Fine"], reps=3)
        # Pass 1 covers everything; passes 2..N only the candidates.
        self.assertEqual(runner.calls[0], (("Slow", "Fine"), 1))
        self.assertEqual([c[0] for c in runner.calls[1:]], [("Slow",), ("Slow",)])

    def test_single_rep_confirms_immediately(self):
        runner = FakeRunner({"UnitA": {"Bench": {1: (100.0, 130.0)}}})
        _, confirmed, _ = self._run(runner, ["UnitA"], reps=1)
        self.assertEqual(len(confirmed), 1)
        self.assertEqual(len(runner.calls), 1)

    def test_unmappable_regression_is_reported_not_silently_cleared(self):
        # The runner reports no Type names, so the regression cannot be traced back to a unit and
        # can never be re-run.  Its tally is therefore stuck at 1; scoring that against reps=3
        # would silently downgrade a real regression to "unconfirmed" and let it through the gate.
        runner = FakeRunner({"UnitA": {"Ghost": {1: (100.0, 130.0)}}},
                            reported_types={"UnitA": []})
        entries, confirmed, unconfirmed = self._run(runner, ["UnitA"], reps=3)
        self.assertEqual([e["benchmarkName"] for e in confirmed], ["Ghost"])
        self.assertEqual(unconfirmed, [])
        self.assertTrue(entries[0]["confirmationSkipped"])
        self.assertEqual(entries[0]["totalReps"], 1)
        # It was never eligible for a re-run.
        self.assertEqual(len(runner.calls), 1)

    def test_improvement_never_counts_as_a_regression(self):
        runner = FakeRunner({"UnitA": {"Bench": {1: (130.0, 100.0)}}})
        entries, confirmed, unconfirmed = self._run(runner, ["UnitA"], reps=3)
        self.assertEqual(entries[0]["status"], "improvement")
        self.assertFalse(entries[0]["confirmedRegression"])
        self.assertEqual(confirmed, [])
        self.assertEqual(unconfirmed, [])


class _FakeProc:
    pid = 4242

    def wait(self):
        return 0


class AffinityTests(unittest.TestCase):
    """The benchmark child must be pinned before it starts executing, not after."""

    def _capture_popen_kwargs(self, cpus):
        captured = {}
        real_popen = subprocess.Popen

        def fake_popen(cmd, **kwargs):
            captured.update(kwargs)
            return _FakeProc()

        subprocess.Popen = fake_popen
        try:
            with tempfile.TemporaryDirectory() as tmp:
                rc = interleave_perf.run_unit_process(
                    tmp, "PerformanceTests.dll", "UnitA", tmp, cpus,
                    os.path.join(tmp, "log.txt"))
        finally:
            subprocess.Popen = real_popen
        return rc, captured

    @unittest.skipUnless(os.name == "posix", "preexec_fn is POSIX-only")
    def test_cpus_are_pinned_via_preexec_fn(self):
        # macOS is POSIX but has no sched_setaffinity, so inject one to exercise the pinning path
        # that matters on the Linux perf VM.
        injected = not hasattr(os, "sched_setaffinity")
        if injected:
            os.sched_setaffinity = lambda pid, mask: None
        try:
            rc, captured = self._capture_popen_kwargs([0, 1])
        finally:
            if injected:
                del os.sched_setaffinity

        self.assertEqual(rc, 0)
        preexec = captured.get("preexec_fn")
        self.assertIsNotNone(preexec,
                             "affinity must be applied between fork() and exec() so that process "
                             "startup and JIT are pinned too")

        # The hook must actually request the CPUs it was given.
        requested = {}
        real = getattr(os, "sched_setaffinity", None)
        os.sched_setaffinity = lambda pid, mask: requested.update(pid=pid, mask=set(mask))
        try:
            preexec()
        finally:
            if real is None:
                del os.sched_setaffinity
            else:
                os.sched_setaffinity = real
        self.assertEqual(requested, {"pid": 0, "mask": {0, 1}})

    @unittest.skipUnless(os.name == "posix", "preexec_fn is POSIX-only")
    def test_falls_back_to_post_start_pinning_when_platform_cannot_pin(self):
        # No sched_setaffinity (e.g. macOS): must still start the process, just unpinned here.
        real = getattr(os, "sched_setaffinity", None)
        if real is not None:
            del os.sched_setaffinity
        try:
            rc, captured = self._capture_popen_kwargs([0, 1])
        finally:
            if real is not None:
                os.sched_setaffinity = real
        self.assertEqual(rc, 0)
        self.assertIsNone(captured.get("preexec_fn"))

    def test_no_preexec_when_no_cpus_requested(self):
        _, captured = self._capture_popen_kwargs([])
        self.assertIsNone(captured.get("preexec_fn"))


if __name__ == "__main__":
    unittest.main()
