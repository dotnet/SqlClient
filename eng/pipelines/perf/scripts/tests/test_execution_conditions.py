"""Verify ordinary timing comparisons honor shared runner conditions, not just health reports."""

import json
from pathlib import Path
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import compare_perf
import interleave_perf as interleave


class ExecutionConditionsTests(unittest.TestCase):
    """PID differences are expected, but mismatched RTT/isolation must not produce a delta."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        for side in ("baseline", "current"):
            process = self.root / side / "processes/case"
            reports = process / "BenchmarkDotNet.Artifacts/results"
            reports.mkdir(parents=True)
            (reports / "Runner-report-full.json").write_text(json.dumps({
                "Benchmarks": [{"Type": "Runner", "Method": "Run", "Parameters": "Value=1",
                                "Statistics": {"Mean": 100 if side == "baseline" else 105}}]}))
            (process / "process.json").write_text(json.dumps({
                "ProcessId": 10 if side == "baseline" else 20,
                "EffectiveProcessIsolation": "PerCase", "AppliedAdditionalRttMilliseconds": 100,
                "ExecutionConditions": {"ProcessIsolation": "PerCase",
                                        "AdditionalRttMilliseconds": 100, "InvocationCount": 1}}))
        self.current_manifest = self.root / "current/processes/case/process.json"

    def compare(self):
        return compare_perf.build_comparison(self.root / "baseline", self.root / "current", 10)

    def test_matching_conditions_pair_across_different_processes(self):
        """Volatile PIDs do not prevent a valid matched comparison."""
        path, = (self.current_manifest.parent / "BenchmarkDotNet.Artifacts/results").glob(
            "*-report-full.json")
        data = json.loads(path.read_text())
        for value, status in ((105, "unchanged"), (110, "unchanged"), (90, "unchanged"),
                              (110.01, "regression"), (89.99, "improvement")):
            with self.subTest(value=value):
                data["Benchmarks"][0]["Statistics"]["Mean"] = value
                path.write_text(json.dumps(data))
                entry, = self.compare()
                self.assertEqual(entry["status"], status)
                self.assertAlmostEqual(entry["meanDeltaPct"], value - 100)

    def test_different_rtt_or_isolation_remains_unpaired(self):
        """Changed execution conditions produce new/removed rows rather than a false delta."""
        for rtt, isolation in ((0, "PerCase"), (100, "PerRunner")):
            with self.subTest(rtt=rtt, isolation=isolation):
                data = json.loads(self.current_manifest.read_text())
                data.update(AppliedAdditionalRttMilliseconds=rtt, EffectiveProcessIsolation=isolation)
                data["ExecutionConditions"].update(
                    AdditionalRttMilliseconds=rtt, ProcessIsolation=isolation)
                self.current_manifest.write_text(json.dumps(data))
                entries = self.compare()
                self.assertEqual({e["status"] for e in entries}, {"current-only", "baseline-only"})
                self.assertTrue(all(e["meanDeltaPct"] is None for e in entries))
                self.assertIn("[conditions=", compare_perf.render_markdown(entries, "baseline", 10))

    def test_missing_isolated_manifest_fails_explicitly(self):
        """Lost condition evidence cannot silently fall back to legacy matching."""
        self.current_manifest.unlink()
        with self.assertRaisesRegex(ValueError, "Missing process manifest"):
            self.compare()

    def test_unapplied_requested_rtt_fails_explicitly(self):
        """Requested latency alone is insufficient evidence of the measured conditions."""
        data = json.loads(self.current_manifest.read_text())
        data["AppliedAdditionalRttMilliseconds"] = 0
        self.current_manifest.write_text(json.dumps(data))
        with self.assertRaisesRegex(ValueError, "requested/applied RTT"):
            self.compare()

    def test_collector_preserves_manifest_next_to_case_reports(self):
        """Artifact collection must retain the conditions needed for subsequent comparison."""
        work = self.root / "work"
        work.mkdir()
        (self.root / "current").rename(work / "BenchmarkDotNet.Artifacts")
        interleave.collect_results(str(work), str(self.root / "collected"))
        records = compare_perf._load_benchmarks(str(self.root / "collected"))
        self.assertEqual(len(records), 1)
        self.assertEqual(next(iter(records.values()))["executionConditions"]
                         ["AdditionalRttMilliseconds"], 100)


if __name__ == "__main__":
    unittest.main()
