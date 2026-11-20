"""Verify paired worst-observed burst reports without hiding missing or censored evidence."""

import copy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import benchmark_health_report as report
import compare_perf


def window(side, case="one", phase="Startup", dispatch=100, **changes):
    """Create one schema-2 cohort, with distinct driver identities on the two sides."""
    data = dict(
        SchemaVersion=2, Kind="BenchmarkAsyncHealth", Status="Completed", Error=None,
        CaseId=f"{side}-{case}", Phase=phase,
        Benchmark="Microsoft.Data.SqlClient.PerformanceTests.OpenAsyncBurstRunner.OpenQueryClose",
        Parameters="[State=Empty]", ConfiguredInvocationsPerIteration=1, OperationsPerInvoke=64,
        Configuration=dict(
            UseConnectionPoolV2=False, UseManagedSni=True, UseOptimizedAsync=True,
            Runtime="10.0.15", OS="Unix", ProcessorCount=16, MinimumWorkerThreads=16,
            MaximumWorkerThreads=32767, ProcessId=123, DriverVersion=side,
            DriverSha256=side, DiagnosticRunMode="StartupAndExtraIteration",
            FirstBenchmarkInProcess=True),
        Summary=dict(Health="Insufficient", DispatchP99Ms=dispatch, FirstWorkerQueueP99Ms=20,
                     ThreadPoolThreadsPeak=16, ProcessThreadsPeak=30, Missing=0, Samples=10,
                     OperationEvidence="Complete"),
        Measurement=dict(Probes={"Samples": [{"DispatchDelayMilliseconds": 1}]}, Resources=[]),
        Operations=dict(Failed=0, Cancelled=0, OpenFailures=0, Queued=0, InFlight=0,
                        FirstWorkerOutcomesMissing=0, ObservationsDropped=0))
    return data | changes


class BenchmarkHealthReportTests(unittest.TestCase):
    """Each test gets its own artifact tree, including cleanup after assertion failures."""

    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)

    def write(self, side, data, rep=1):
        """Retain the production side/repetition layout and return the evidence path."""
        folder = self.root / "runs" / f"rep{rep}" / side / "OpenAsyncBurst/async-health"
        folder.mkdir(parents=True, exist_ok=True)
        phase = data["Phase"] if data["Measurement"] is not None else "execution"
        path = folder / f"{data['CaseId']}-{phase}-{data['Status']}.json"
        path.write_text(json.dumps(data), encoding="utf-8")
        return path

    def compare(self):
        """Read both structured and rendered output; only explicit failure tests bypass this."""
        entries = report.build_comparison(self.root)
        markdown, failed = report.render(self.root)
        self.assertFalse(failed)
        return entries, markdown

    def pairs(self, values):
        """Write distinct cases from (name, baseline, current) triples."""
        for name, baseline, current in values:
            for side, value in (("baseline", baseline), ("current", current)):
                self.write(side, window(side, name, dispatch=value, Parameters=name))

    def test_phases_repetitions_and_aliases_reduce_to_one_pair(self):
        """Independent maxima preserve every origin without counting aliases as extra evidence."""
        # Arrange
        for side, samples in (
            ("baseline", [(100, 20, 16, 30), (90, 30, 20, 31),
                          (110, 5, 18, 35), (80, 4, 21, 32)]),
            ("current", [(154, 40, 24, 44), (130, 12, 22, 41),
                         (100, 45, 20, 46), (99, 11, 25, 43)]),
        ):
            for index, values in enumerate(samples):
                data = window(side, str(index // 2), ("Startup", "ExtraIteration")[index % 2])
                data["Configuration"]["ProcessId"] += index // 2
                data["Summary"].update(zip(report.METRICS, values))
                path = self.write(side, data, index // 2 + 1)
                alias = self.root / side / "async-health" / path.name
                alias.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(path, alias)

        # Act
        (entry,), markdown = self.compare()

        # Assert
        self.assertEqual(entry["status"], "unhealthy")
        self.assertEqual(entry["metrics"]["DispatchP99Ms"],
                         dict(baseline=110, current=154, delta=44, deltaPct=40))
        self.assertEqual(entry["metrics"]["FirstWorkerQueueP99Ms"]["delta"], 15)
        self.assertEqual(entry["metrics"]["ThreadPoolThreadsPeak"]["current"], 25)
        self.assertEqual(entry["metrics"]["ProcessThreadsPeak"]["current"], 46)
        self.assertEqual(len(entry["baseline"]["records"]), 4)
        self.assertEqual(len(entry["current"]["records"]), 4)
        origin, = entry["baseline"]["origins"]["DispatchP99Ms"]
        self.assertEqual((origin["data"]["Phase"], origin["repetition"]), ("Startup", 2))
        self.assertEqual(len(origin["sources"]), 2)
        for text in ("154.0000 (+40.00%; +44.0000)", "rep2/baseline", "not pooled", "not best-of-N"):
            self.assertIn(text, markdown)

    def test_current_and_historical_burst_rows_are_reported(self):
        """Current four-row reports and historical eight-row reports exclude unrelated runners."""
        for method in ("OpenQueryClose", "OpenClose"):
            # Arrange: add the retired method only after checking current query-only evidence.
            for side in ("baseline", "current"):
                for state in ("Empty", "Warm", "Oversubscribed", "NonPooled"):
                    for phase in ("Startup", "ExtraIteration"):
                        data = window(side, method + state, phase, Parameters=f"[State={state}]")
                        data["Benchmark"] = data["Benchmark"].rsplit(".", 1)[0] + "." + method
                        self.write(side, data)
                self.write(side, window(side, "existing",
                                       Benchmark="Example.SqlConnectionRunner.OpenAsyncConnection"))

            # Act
            entries, markdown = self.compare()

            # Assert
            self.assertEqual(len(entries), 4 if method == "OpenQueryClose" else 8)
            self.assertNotIn("SqlConnectionRunner", markdown)
            self.assertTrue(all(entry["status"] == "unhealthy" for entry in entries))

    def test_configuration_mismatch_is_not_paired(self):
        """Different runtime, pool, worker floor or workload settings must remain unpaired."""
        # Arrange
        for field, value in (("Runtime", "10.0.16"), ("UseConnectionPoolV2", True),
                             ("MinimumWorkerThreads", 32), ("ConfiguredInvocationsPerIteration", 2)):
            baseline, current = (window(side, field, Parameters=field)
                                 for side in ("baseline", "current"))
            target = current if field in current else current["Configuration"]
            target[field] = value
            self.write("baseline", baseline)
            self.write("current", current)

        # Act
        entries, markdown = self.compare()

        # Assert
        self.assertEqual(len(entries), 8)
        self.assertEqual({e["status"] for e in entries}, {"unknown", "unhealthy"})
        self.assertIn("unmatched configuration", markdown)
        self.assertTrue(all(e["metrics"]["DispatchP99Ms"]["deltaPct"] is None for e in entries))

    def test_incomplete_candidate_remains_visible_despite_healthy_indicator(self):
        """A low observed p99 must not waive incomplete operations or missing phases/probes."""
        # Arrange
        for phase in ("Startup", "ExtraIteration"):
            self.write("baseline", window("baseline", phase=phase))
        current = window("current", dispatch=0.5)
        current["Operations"].update(FirstWorkerOutcomesMissing=3, Cancelled=1, Queued=2, InFlight=3)
        current["Summary"].update(OperationEvidence="Incomplete", Missing=4)
        self.write("current", current)

        # Act
        (entry,), markdown = self.compare()

        # Assert
        self.assertEqual(entry["status"], "healthy")
        self.assertEqual(entry["current"]["missingPhases"], {"current-one": ["ExtraIteration"]})
        for text in ("FirstWorkerOutcomesMissing=3", "Cancelled=1", "Queued=2", "InFlight=3",
                     "missing/censored probe counts by window: `[4]`", "Incomplete", "Insufficient",
                     "not proof of complete or sufficient evidence"):
            self.assertIn(text, markdown)
        self.assertIn("FirstWorkerOutcomesMissing=3", markdown.split("<details>")[0])

    def test_failed_execution_marker_is_retained_and_fails(self):
        """A completed window must not deduplicate away its later execution failure."""
        # Arrange
        data = window("current")
        self.write("current", data)
        self.write("current", data | dict(Status="Failed", Summary=None, Measurement=None))

        # Act
        entry, = report.build_comparison(self.root)
        markdown, failed = report.render(self.root)

        # Assert
        self.assertTrue(failed)
        self.assertEqual(len(entry["current"]["records"]), 2)
        self.assertIn("Failed=1", markdown)
        self.assertIn("execution-Failed.json", markdown)

    def test_zero_and_missing_values_never_fabricate_percentages(self):
        """Unknown percentages do not prevent classification of a known current value."""
        # Arrange
        values = [(0, 0, "0.0000 (N/A; +0.0000)", "healthy"),
                  (0, 5, "5.0000 (N/A; +5.0000)", "warning"),
                  (None, 5, "5.0000 (N/A; N/A)", "warning"),
                  (5, None, "N/A (N/A; N/A)", "unknown")]
        self.pairs((str(i), before, after) for i, (before, after, _, _) in enumerate(values))

        # Act
        entries, markdown = self.compare()

        # Assert
        for entry, (_, _, cell, status) in zip(entries, values):
            self.assertEqual(entry["status"], status)
            self.assertIsNone(entry["metrics"]["DispatchP99Ms"]["deltaPct"])
            self.assertIn(cell, markdown)

    def test_missing_thread_peak_is_not_reported_as_an_improvement(self):
        """Unsampled thread counts remain N/A instead of a false minus-100% change."""
        # Arrange
        for phase in ("Startup", "ExtraIteration"):
            self.write("baseline", window("baseline", phase=phase))
            current = window("current", phase=phase)
            current["Summary"].update(ThreadPoolThreadsPeak=None, ProcessThreadsPeak=25)
            self.write("current", current)

        # Act
        (entry,), markdown = self.compare()

        # Assert
        self.assertEqual(entry["metrics"]["ThreadPoolThreadsPeak"],
                         dict(baseline=16, current=None, delta=None, deltaPct=None))
        self.assertEqual(entry["metrics"]["ProcessThreadsPeak"]["current"], 25)
        self.assertIn("N/A (N/A; N/A)", markdown)
        self.assertIn("ThreadPoolThreadsPeak=2", markdown)
        self.assertNotIn("-100.00%", markdown)

    def test_current_bands_preserve_relative_sorting_and_numeric_ties(self):
        """Both boundaries are yellow; sorting still uses unrounded relative change, not health."""
        # Arrange
        self.pairs([("z", 100, 10.00001), ("a", 100, 10.00001), ("upper", 100, 10),
                    ("below-upper", 100, 9.99999), ("above-lower", 100, 1.00001),
                    ("lower", 100, 1), ("below-lower", 100, 0.99999), ("increase", 0.1, 0.5),
                    ("missing-b", 100, None), ("missing-a", 100, None)])

        # Act
        entries, markdown = self.compare()

        # Assert
        self.assertEqual([e["parameters"] for e in entries],
                         ["increase", "a", "z", "upper", "below-upper", "above-lower", "lower",
                          "below-lower", "missing-a", "missing-b"])
        self.assertEqual([e["status"] for e in entries],
                         ["healthy", "unhealthy", "unhealthy", "warning", "warning",
                          "warning", "warning", "healthy", "unknown", "unknown"])
        self.assertIn("0.5000 (+400.00%; +0.4000)", markdown)
        for status in ("healthy", "warning", "unhealthy"):
            self.assertIn(report.STATUS_LABELS[status] + " |", markdown)

    def test_current_only_health_does_not_require_a_baseline(self):
        """A missing side prevents deltas, not classification of an observed current value."""
        # Arrange
        for name, dispatch in (("low", 0.5), ("medium", 5), ("high", 11), ("missing", None)):
            self.write("current", window("current", name, dispatch=dispatch, Parameters=name))
        self.write("baseline", window("baseline", "removed", dispatch=1, Parameters="removed"))

        # Act
        entries, _ = self.compare()

        # Assert
        self.assertEqual({e["parameters"]: e["status"] for e in entries},
                         {"low": "healthy", "medium": "warning", "high": "unhealthy",
                          "missing": "unknown", "removed": "unknown"})
        self.assertTrue(all(e["metrics"]["DispatchP99Ms"]["deltaPct"] is None for e in entries))

    def test_conflicting_aliases_and_side_origins_are_rejected(self):
        """Copies cannot silently change metric values, repetition or side ownership."""
        for conflict in ("value", "side", "repetition", "cross-phase-side"):
            # Arrange
            data = window("baseline", conflict)
            path = self.write("baseline", data)
            duplicate = copy.deepcopy(data)
            if conflict == "value":
                duplicate["Summary"]["DispatchP99Ms"] = 900
            if conflict == "repetition":
                alias = self.write("baseline", duplicate, rep=2)
            elif conflict == "cross-phase-side":
                duplicate["Phase"] = "ExtraIteration"
                alias = self.write("current", duplicate)
            else:
                side = "current" if conflict == "side" else "baseline"
                alias = self.root / side / "async-health" / path.name
                alias.parent.mkdir(parents=True, exist_ok=True)
                alias.write_text(json.dumps(duplicate))

            # Act / Assert
            with self.subTest(conflict=conflict), self.assertRaisesRegex(ValueError, "Conflicting"):
                report.build_comparison(self.root)
            alias.unlink()

    def test_missing_configuration_and_mixed_driver_identity_are_rejected(self):
        """Unknown settings and mixed driver hashes cannot form one side's maximum."""
        # Arrange
        data = window("current")
        del data["Configuration"]["Runtime"]
        path = self.write("current", data)
        # Act / Assert
        with self.assertRaisesRegex(ValueError, "configuration"):
            report.build_comparison(self.root)
        path.unlink()
        self.write("current", window("current"))
        other = window("current", "two")
        other["Configuration"]["DriverSha256"] = "different"
        self.write("current", other, rep=2)
        with self.assertRaisesRegex(ValueError, "driver"):
            report.build_comparison(self.root)

    def test_regeneration_preserves_historical_report_and_raw_evidence(self):
        """An alternate output labels historical input without overwriting source artifacts."""
        # Arrange
        original = self.root / "benchmark-async-health.md"
        original.write_text("original report")
        raw = self.write("current", window("current"))
        before = raw.read_bytes()
        output = self.root / "preview/regenerated.md"
        # Act
        result = report.main(["--results-dir", str(self.root), "--out-md", str(output),
                              "--source-label", "Historical run 192609"])
        # Assert
        self.assertEqual(result, 0)
        self.assertEqual(original.read_text(), "original report")
        self.assertEqual(raw.read_bytes(), before)
        self.assertIn("Historical run 192609", output.read_text(encoding="utf-8"))
        self.assertIn(report.STATUS_LABELS["unhealthy"], output.read_text(encoding="utf-8"))

    def test_invalid_metrics_fail_explicitly(self):
        """Non-finite or negative metrics cannot produce plausible-looking comparisons."""
        for value in (float("nan"), float("inf"), -1):
            # Arrange
            self.write("current", window("current", dispatch=value))
            # Act / Assert
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, "metric"):
                report.build_comparison(self.root)

    def test_ordinary_comparison_keeps_relative_thresholds(self):
        """Removing async percentage plumbing must not change ordinary timing classifications."""
        # Act
        statuses = [compare_perf.classify_change(delta, 12.5)
                    for delta in (-12.51, -12.5, 12.5, 12.51)]
        # Assert
        self.assertEqual(statuses, ["improvement", "unchanged", "unchanged", "regression"])

    def check_shell_report(self, shell):
        """Execute each real shell's report invocation without a relative threshold argument."""
        executable = shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} unavailable")
        self.write("current", window("current", dispatch=11))
        env = dict(os.environ, TEST_ROOT=self.root.as_posix(), TEST_SCRIPTS=SCRIPTS.as_posix(),
                   TEST_PYTHON=Path(sys.executable).as_posix())
        if shell == "bash":
            text = (SCRIPTS / "run-perf-tests.sh").read_text()
            start = text.index('python3 "${SCRIPT_DIR}/benchmark_health_report.py"')
            invocation = text[start:text.index('\necho "Collected results:', start)]
            script = ('SCRIPT_DIR="$TEST_SCRIPTS"\nRESULTS_DIR="$TEST_ROOT"\n'
                      'python3() { "$TEST_PYTHON" "$@"; }\n') + invocation
            command = [executable, "-c", script]
        else:
            text = (SCRIPTS / "run-perf-tests.ps1").read_text()
            start = text.index('Invoke-Native {\n    python3 (Join-Path $ScriptDir '
                               '"benchmark_health_report.py")')
            invocation = text[start:text.index('\nWrite-Host "Collected results:', start)]
            script = ("$ErrorActionPreference='Stop'\n$ScriptDir=$env:TEST_SCRIPTS\n"
                      "$ResultsDir=$env:TEST_ROOT\n"
                      "function python3 { & $env:TEST_PYTHON @args; "
                      "if ($LASTEXITCODE) { throw 'report failed' } }\n"
                      "function Invoke-Native($Command, $Message) { & $Command }\n") + invocation
            command = [executable, "-NoProfile", "-Command", script]

        # Act
        result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=30)
        # Assert
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        markdown = (self.root / "benchmark-async-health.md").read_text(encoding="utf-8")
        self.assertIn("**10ms**", markdown)
        self.assertIn(report.STATUS_LABELS["unhealthy"], markdown)
        self.assertNotIn(compare_perf.STATUS_LABELS["regression"] + " |", markdown)

    def test_bash_generates_report(self):
        self.check_shell_report("bash")

    def test_powershell_generates_report(self):
        self.check_shell_report("pwsh")


if __name__ == "__main__":
    unittest.main()
