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


def write_report(root, side, rtt, isolation="PerCase", *, manifest=True):
    """Create one isolated child report and the execution evidence emitted by its parent."""
    process = root / side / "processes/case"
    reports = process / "BenchmarkDotNet.Artifacts/results"
    reports.mkdir(parents=True, exist_ok=True)
    (reports / "Runner-report-full.json").write_text(json.dumps({
        "Benchmarks": [{"Type": "Runner", "Method": "Run", "Parameters": "Value=1",
                        "Statistics": {"Mean": 100 if side == "baseline" else 105}}]}))
    if manifest:
        (process / "process.json").write_text(json.dumps({
            "ProcessId": 10 if side == "baseline" else 20,
            "EffectiveProcessIsolation": isolation,
            "AppliedAdditionalRttMilliseconds": rtt,
            "ExecutionConditions": {"ProcessIsolation": isolation,
                                    "AdditionalRttMilliseconds": rtt, "InvocationCount": 1}}))
    return process


class ExecutionConditionsTests(unittest.TestCase):
    """PID differences are expected, but mismatched RTT/isolation must not produce a delta."""

    def test_matching_conditions_pair_across_different_processes(self):
        """Volatile PIDs do not prevent a valid matched comparison."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for side in ("baseline", "current"):
                write_report(root, side, 100)
            entries = compare_perf.build_comparison(root / "baseline", root / "current", 10)
            self.assertEqual(len(entries), 1)
            self.assertEqual(entries[0]["status"], "unchanged")
            self.assertEqual(entries[0]["meanDeltaPct"], 5)

    def test_different_rtt_or_isolation_remains_unpaired(self):
        """Changed execution conditions produce new/removed rows rather than a false delta."""
        for rtt, isolation in ((0, "PerCase"), (100, "PerRunner")):
            with self.subTest(rtt=rtt, isolation=isolation), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                write_report(root, "baseline", 100)
                write_report(root, "current", rtt, isolation)
                entries = compare_perf.build_comparison(root / "baseline", root / "current", 10)
                self.assertEqual({e["status"] for e in entries}, {"current-only", "baseline-only"})
                self.assertTrue(all(e["meanDeltaPct"] is None for e in entries))
                self.assertIn("[conditions=", compare_perf.render_markdown(entries, "baseline", 10))

    def test_missing_isolated_manifest_fails_explicitly(self):
        """Lost condition evidence cannot silently fall back to legacy matching."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_report(root, "baseline", 100)
            write_report(root, "current", 100, manifest=False)
            with self.assertRaisesRegex(ValueError, "Missing process manifest"):
                compare_perf.build_comparison(root / "baseline", root / "current", 10)

    def test_unapplied_requested_rtt_fails_explicitly(self):
        """Requested latency alone is insufficient evidence of the measured conditions."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_report(root, "baseline", 100)
            process = write_report(root, "current", 100)
            path = process / "process.json"
            data = json.loads(path.read_text())
            data["AppliedAdditionalRttMilliseconds"] = 0
            path.write_text(json.dumps(data))
            with self.assertRaisesRegex(ValueError, "requested/applied RTT"):
                compare_perf.build_comparison(root / "baseline", root / "current", 10)

    def test_collector_preserves_manifest_next_to_case_reports(self):
        """Artifact collection must retain the conditions needed for subsequent comparison."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            work = root / "work"
            process = write_report(work / "BenchmarkDotNet.Artifacts", "ignored", 100)
            # Move the case beneath the production artifacts/processes layout.
            destination = work / "BenchmarkDotNet.Artifacts/processes"
            destination.mkdir()
            process.rename(destination / "case")
            interleave.collect_results(str(work), str(root / "collected"))
            records = compare_perf._load_benchmarks(str(root / "collected"))
            self.assertEqual(len(records), 1)
            self.assertEqual(next(iter(records.values()))["executionConditions"]
                             ["AdditionalRttMilliseconds"], 100)


if __name__ == "__main__":
    unittest.main()
