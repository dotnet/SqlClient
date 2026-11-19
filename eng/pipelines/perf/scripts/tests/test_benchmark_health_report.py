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


def window(side, case, phase="Startup", dispatch=100, **changes):
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
    data.update(changes)
    return data


def write_window(root, side, data, rep=1):
    """Retain the production side/repetition layout and return the raw evidence path."""
    folder = root / "runs" / f"rep{rep}" / side / "OpenAsyncBurst/async-health"
    folder.mkdir(parents=True, exist_ok=True)
    suffix = data["Phase"] if data["Measurement"] is not None else "execution"
    path = folder / f"{data['CaseId']}-{suffix}-{data['Status']}.json"
    path.write_text(json.dumps(data), encoding="utf-8")
    return path


class BenchmarkHealthReportTests(unittest.TestCase):
    """Compact comparisons preserve independent maxima and the evidence behind each reduction."""

    def test_phases_repetitions_and_aliases_reduce_to_one_pair(self):
        """Maxima may come from different cohorts; aliases must not inflate coverage."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for side, samples in (
                ("baseline", [(100, 20, 16, 30), (90, 30, 20, 31),
                              (110, 5, 18, 35), (80, 4, 21, 32)]),
                ("current", [(154, 40, 24, 44), (130, 12, 22, 41),
                             (100, 45, 20, 46), (99, 11, 25, 43)]),
            ):
                for index, values in enumerate(samples):
                    data = window(side, str(index // 2),
                                  ("Startup", "ExtraIteration")[index % 2])
                    data["Configuration"]["ProcessId"] += index // 2
                    for metric, value in zip(report.METRICS, values):
                        data["Summary"][metric] = value
                    path = write_window(root, side, data, index // 2 + 1)
                    alias = root / side / "async-health" / path.name
                    alias.parent.mkdir(parents=True, exist_ok=True)
                    shutil.copyfile(path, alias)

            # Act
            entries = report.build_comparison(root)
            markdown, failed = report.render(root)

            # Assert
            self.assertFalse(failed)
            self.assertEqual(len(entries), 1)
            entry = entries[0]
            self.assertEqual(entry["status"], "unhealthy")
            dispatch = entry["metrics"]["DispatchP99Ms"]
            self.assertEqual(dispatch, dict(baseline=110, current=154, delta=44, deltaPct=40))
            self.assertEqual(entry["metrics"]["FirstWorkerQueueP99Ms"]["delta"], 15)
            self.assertEqual(entry["metrics"]["ThreadPoolThreadsPeak"]["current"], 25)
            self.assertEqual(entry["metrics"]["ProcessThreadsPeak"]["current"], 46)
            self.assertEqual(len(entry["baseline"]["records"]), 4)
            self.assertEqual(len(entry["current"]["records"]), 4)
            origins = entry["baseline"]["origins"]["DispatchP99Ms"]
            self.assertEqual([(r["data"]["Phase"], r["repetition"]) for r in origins],
                             [("Startup", 2)])
            self.assertEqual(len(origins[0]["sources"]), 2)
            self.assertIn("154.0000 (+40.00%; +44.0000)", markdown)
            self.assertIn("rep2/baseline", markdown)
            self.assertIn("not pooled", markdown)
            self.assertIn("not best-of-N", markdown)

    def test_current_and_historical_burst_rows_are_reported(self):
        """Current query cases pair correctly without losing retired historical burst evidence."""
        # Arrange
        for methods in (("OpenQueryClose",), ("OpenClose", "OpenQueryClose")):
            with self.subTest(methods=methods), tempfile.TemporaryDirectory() as temp:
                self.check_burst_rows(Path(temp), methods)

    def check_burst_rows(self, root, methods):
        """Build matched phase evidence and check one row per supplied method and pool state."""
        for side in ("baseline", "current"):
            for method in methods:
                for state in ("Empty", "Warm", "Oversubscribed", "NonPooled"):
                    for phase in ("Startup", "ExtraIteration"):
                        data = window(side, method + state, phase)
                        data["Benchmark"] = data["Benchmark"].rsplit(".", 1)[0] + "." + method
                        data["Parameters"] = f"[State={state}]"
                        write_window(root, side, data)
            other = window(side, "existing")
            other["Benchmark"] = "Example.SqlConnectionRunner.OpenAsyncConnection"
            write_window(root, side, other)

        # Act
        entries = report.build_comparison(root)
        markdown, _ = report.render(root)

        # Assert
        self.assertEqual(len(entries), 4 * len(methods))
        self.assertNotIn("SqlConnectionRunner", markdown)
        self.assertTrue(all(entry["status"] == "unhealthy" for entry in entries))

    def test_configuration_mismatch_is_not_paired(self):
        """A different runtime, pool, worker floor or workload size cannot be compared."""
        for field in ("Runtime", "UseConnectionPoolV2", "MinimumWorkerThreads",
                      "ConfiguredInvocationsPerIteration"):
            with self.subTest(field=field), tempfile.TemporaryDirectory() as temp:
                # Arrange
                root = Path(temp)
                baseline, current = window("baseline", "one"), window("current", "one")
                if field == "ConfiguredInvocationsPerIteration":
                    current[field] = 2
                else:
                    current["Configuration"][field] = {
                        "Runtime": "10.0.16", "UseConnectionPoolV2": True,
                        "MinimumWorkerThreads": 32}[field]
                write_window(root, "baseline", baseline)
                write_window(root, "current", current)

                # Act
                entries = report.build_comparison(root)
                markdown, _ = report.render(root)

                # Assert
                self.assertEqual({e["status"] for e in entries},
                                 {"unknown", "unhealthy"})
                self.assertIn("unmatched configuration", markdown)
                self.assertTrue(all(e["metrics"]["DispatchP99Ms"]["deltaPct"] is None
                                    for e in entries))

    def test_incomplete_candidate_is_visible_even_when_both_sides_are_insufficient(self):
        """A low observed dispatch value must not hide missing phases or incomplete work."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for phase in ("Startup", "ExtraIteration"):
                write_window(root, "baseline", window("baseline", "one", phase))
            current = window("current", "one", dispatch=0.5)
            current["Operations"]["FirstWorkerOutcomesMissing"] = 3
            current["Operations"].update(Cancelled=1, Queued=2, InFlight=3)
            current["Summary"]["OperationEvidence"] = "Incomplete"
            current["Summary"]["Missing"] = 4
            write_window(root, "current", current)

            # Act
            entry, = report.build_comparison(root)
            markdown, failed = report.render(root)

            # Assert
            self.assertEqual(entry["status"], "healthy")
            self.assertEqual(entry["current"]["missingPhases"],
                             {"current-one": ["ExtraIteration"]})
            self.assertFalse(failed)  # Health reporting does not introduce a new failure gate.
            self.assertIn("FirstWorkerOutcomesMissing=3", markdown)
            self.assertIn("Cancelled=1", markdown)
            self.assertIn("Queued=2", markdown)
            self.assertIn("InFlight=3", markdown)
            self.assertIn("missing/censored probe counts by window: `[4]`", markdown)
            self.assertIn("Incomplete", markdown)
            self.assertIn("Insufficient", markdown)
            self.assertIn("not proof of complete or sufficient evidence", markdown)
            self.assertIn("FirstWorkerOutcomesMissing=3", markdown.split("<details>")[0])

    def test_failed_execution_marker_is_retained_and_fails(self):
        """A completed window must not deduplicate away its later execution failure."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            data = window("current", "one")
            write_window(root, "current", data)
            failure = dict(data, Status="Failed", Summary=None, Measurement=None)
            write_window(root, "current", failure)

            # Act
            entries = report.build_comparison(root)
            markdown, failed = report.render(root)

            # Assert
            self.assertTrue(failed)
            self.assertEqual(len(entries[0]["current"]["records"]), 2)
            self.assertIn("Failed=1", markdown)
            self.assertIn("execution-Failed.json", markdown)

    def test_zero_and_missing_values_never_fabricate_percentages(self):
        """Zero-to-zero is also undefined here, while signed raw differences remain visible."""
        for baseline, current, expected, status in (
            (0, 0, "0.0000 (N/A; +0.0000)", "healthy"),
            (0, 5, "5.0000 (N/A; +5.0000)", "warning"),
            (None, 5, "5.0000 (N/A; N/A)", "warning"),
            (5, None, "N/A (N/A; N/A)", "unknown"),
        ):
            with self.subTest(baseline=baseline, current=current):
                with tempfile.TemporaryDirectory() as temp:
                    # Arrange
                    root = Path(temp)
                    write_window(root, "baseline", window("baseline", "one", dispatch=baseline))
                    write_window(root, "current", window("current", "one", dispatch=current))

                    # Act
                    entry, = report.build_comparison(root)
                    markdown, _ = report.render(root)

                    # Assert
                    self.assertEqual(entry["status"], status)
                    self.assertIsNone(entry["metrics"]["DispatchP99Ms"]["deltaPct"])
                    self.assertIn(expected, markdown)

    def test_health_status_preserves_sorting_and_regular_comparison_policy(self):
        """Health uses current milliseconds; ordering and ordinary percent thresholds stay unchanged."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name, value in (("down", 87.49), ("lower-boundary", 87.5),
                                ("upper-boundary", 112.5), ("up", 112.51),
                                ("missing-b", None), ("missing-a", None)):
                for side, dispatch in (("baseline", 100), ("current", value)):
                    data = window(side, name, dispatch=dispatch, Parameters=name)
                    write_window(root, side, data)
            regular = root / "regular"
            for side in ("baseline", "current"):
                (regular / side).mkdir(parents=True)
                records = [dict(Type="Runner", Method="Open", Parameters=name,
                                Statistics={"Mean": value})
                           for name, value in (("down", 87.49), ("lower-boundary", 87.5),
                                               ("upper-boundary", 112.5), ("up", 112.51))]
                if side == "baseline":
                    for record in records:
                        record["Statistics"]["Mean"] = 100
                (regular / side / "test-report-full.json").write_text(
                    json.dumps({"Benchmarks": records}))

            # Act
            entries = report.build_comparison(root, 12.5)
            ordinary = compare_perf.build_comparison(
                str(regular / "baseline"), str(regular / "current"), 12.5)

            # Assert
            self.assertEqual([e["parameters"] for e in entries],
                             ["up", "upper-boundary", "lower-boundary", "down",
                              "missing-a", "missing-b"])
            statuses = {e["parameters"]: e["status"] for e in entries}
            self.assertEqual(statuses, {
                "up": "unhealthy", "down": "unhealthy",
                "upper-boundary": "unhealthy", "lower-boundary": "unhealthy",
                "missing-a": "unknown", "missing-b": "unknown"})
            self.assertEqual({e["parameterSignature"]: e["status"] for e in ordinary},
                             {"up": "regression", "down": "improvement",
                              "upper-boundary": "unchanged", "lower-boundary": "unchanged"})
            self.assertIn("12.5%", compare_perf.render_markdown(ordinary, "baseline", 12.5))
            markdown, _ = report.render(root, 12.5)
            self.assertIn("(-12.51%; -12.5100)", markdown)
            self.assertIn("**10ms**", markdown)
            self.assertNotIn("12.5%", markdown)

    def test_conflicting_aliases_and_side_origins_are_rejected(self):
        """Duplicate copies cannot silently replace values or change baseline/current identity."""
        for conflict in ("value", "side", "repetition", "cross-phase-side"):
            with self.subTest(conflict=conflict), tempfile.TemporaryDirectory() as temp:
                # Arrange
                root = Path(temp)
                data = window("baseline", "one")
                path = write_window(root, "baseline", data)
                duplicate = copy.deepcopy(data)
                if conflict == "value":
                    duplicate["Summary"]["DispatchP99Ms"] = 900
                if conflict == "repetition":
                    write_window(root, "baseline", duplicate, rep=2)
                elif conflict == "cross-phase-side":
                    duplicate["Phase"] = "ExtraIteration"
                    write_window(root, "current", duplicate)
                else:
                    side = "current" if conflict == "side" else "baseline"
                    alias = root / side / "async-health" / path.name
                    alias.parent.mkdir(parents=True, exist_ok=True)
                    alias.write_text(json.dumps(duplicate))

                # Act / Assert
                with self.assertRaisesRegex(ValueError, "Conflicting"):
                    report.build_comparison(root)

    def test_missing_configuration_and_mixed_driver_identity_are_rejected(self):
        """Do not combine unknown settings or multiple driver binaries into one side's maximum."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            data = window("current", "one")
            del data["Configuration"]["Runtime"]
            path = write_window(root, "current", data)
            with self.assertRaisesRegex(ValueError, "configuration"):
                report.build_comparison(root)
            path.unlink()
            write_window(root, "current", window("current", "one"))
            other = window("current", "two")
            other["Configuration"]["DriverSha256"] = "different"
            write_window(root, "current", other, rep=2)
            with self.assertRaisesRegex(ValueError, "driver"):
                report.build_comparison(root)

    def test_current_millisecond_boundary_and_numeric_ties(self):
        """Both boundaries are yellow; classification uses unrounded milliseconds, not deltas."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name, value in (("z", 10.00001), ("a", 10.00001),
                                ("upper", 10), ("below-upper", 9.99999),
                                ("above-lower", 1.00001), ("lower", 1),
                                ("below-lower", 0.99999)):
                for side, dispatch in (("baseline", 100), ("current", value)):
                    write_window(root, side, window(side, name, dispatch=dispatch, Parameters=name))

            # Act
            entries = report.build_comparison(root, 99)
            markdown, failed = report.render(root, 99)

            # Assert
            self.assertEqual([e["parameters"] for e in entries],
                             ["a", "z", "upper", "below-upper",
                              "above-lower", "lower", "below-lower"])
            self.assertEqual([e["status"] for e in entries],
                             ["unhealthy", "unhealthy", "warning", "warning",
                              "warning", "warning", "healthy"])
            self.assertFalse(failed)
            self.assertIn("\U0001f534 unhealthy |", markdown)
            self.assertIn("\U0001f7e1 warning |", markdown)
            self.assertIn("\U0001f7e2 healthy |", markdown)

    def test_current_only_health_does_not_require_a_baseline(self):
        """A missing baseline prevents deltas, not classification of an observed current value."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for name, dispatch in (("low", 0.5), ("medium", 5),
                                   ("high", 11), ("missing", None)):
                write_window(root, "current", window(
                    "current", name, dispatch=dispatch, Parameters=name))
            write_window(root, "baseline", window(
                "baseline", "removed", dispatch=1, Parameters="removed"))

            # Act
            entries = report.build_comparison(root)

            # Assert
            self.assertEqual({e["parameters"]: e["status"] for e in entries},
                             {"low": "healthy", "medium": "warning", "high": "unhealthy",
                              "missing": "unknown", "removed": "unknown"})
            self.assertTrue(all(e["metrics"]["DispatchP99Ms"]["deltaPct"] is None
                                for e in entries))

    def test_large_relative_increase_below_one_millisecond_is_healthy(self):
        """A baseline-relative regression must not turn a low current dispatch p99 red."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_window(root, "baseline", window("baseline", "one", dispatch=0.1))
            write_window(root, "current", window("current", "one", dispatch=0.5))

            # Act
            entry, = report.build_comparison(root)
            markdown, failed = report.render(root)

            # Assert
            self.assertEqual(entry["status"], "healthy")
            self.assertEqual(entry["metrics"]["DispatchP99Ms"]["deltaPct"], 400)
            self.assertIn("\U0001f7e2 healthy |", markdown)
            self.assertIn("0.5000 (+400.00%; +0.4000)", markdown)
            self.assertFalse(failed)

    def test_regeneration_preserves_historical_report_and_raw_evidence(self):
        """An alternate output labels historical input without overwriting source artifacts."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            original = root / "benchmark-async-health.md"
            original.write_text("original report")
            raw = write_window(root, "current", window("current", "one"))
            before = raw.read_bytes()
            output = root / "preview/regenerated.md"

            # Act
            result = report.main(["--results-dir", str(root), "--out-md", str(output),
                                  "--threshold", "12.5", "--source-label", "Historical run 192609"])

            # Assert
            self.assertEqual(result, 0)
            self.assertEqual(original.read_text(), "original report")
            self.assertEqual(raw.read_bytes(), before)
            self.assertIn("Historical run 192609", output.read_text(encoding="utf-8"))
            self.assertIn("\U0001f534 unhealthy",
                          output.read_text(encoding="utf-8"))

    def test_invalid_metrics_and_thresholds_fail_explicitly(self):
        """Non-finite data cannot produce plausible-looking relative comparisons."""
        for value in (float("nan"), float("inf"), -1):
            with self.subTest(value=value), tempfile.TemporaryDirectory() as temp:
                # Arrange
                root = Path(temp)
                data = window("current", "one")
                path = write_window(root, "current", data)

                # Act / Assert
                with self.assertRaisesRegex(ValueError, "threshold"):
                    report.build_comparison(root, value)
                data["Summary"]["DispatchP99Ms"] = value
                path.write_text(json.dumps(data))
                with self.assertRaisesRegex(ValueError, "metric"):
                    report.build_comparison(root)

    def check_shell_threshold(self, shell):
        """Existing shell arguments remain compatible without changing the fixed health bands."""
        executable = shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} unavailable")
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            write_window(root, "baseline", window("baseline", "one", dispatch=100))
            write_window(root, "current", window("current", "one", dispatch=11))
            env = dict(os.environ, TEST_ROOT=root.as_posix(), TEST_SCRIPTS=SCRIPTS.as_posix(),
                       TEST_PYTHON=Path(sys.executable).as_posix())
            if shell == "bash":
                text = (SCRIPTS / "run-perf-tests.sh").read_text()
                start = text.index('python3 "${SCRIPT_DIR}/benchmark_health_report.py"')
                invocation = text[start:text.index('\necho "Collected results:', start)]
                script = '''
SCRIPT_DIR="$TEST_SCRIPTS"
RESULTS_DIR="$TEST_ROOT"
regressionThreshold=12.5
python3() { "$TEST_PYTHON" "$@"; }
''' + invocation
                command = [executable, "-c", script]
            else:
                text = (SCRIPTS / "run-perf-tests.ps1").read_text()
                start = text.index('Invoke-Native {\n    python3 (Join-Path $ScriptDir '
                                   '"benchmark_health_report.py")')
                invocation = text[start:text.index('\nWrite-Host "Collected results:', start)]
                script = '''
$ErrorActionPreference='Stop'
$ScriptDir=$env:TEST_SCRIPTS
$ResultsDir=$env:TEST_ROOT
$RegressionThreshold='12.5'
function python3 { & $env:TEST_PYTHON @args; if ($LASTEXITCODE) { throw 'report failed' } }
function Invoke-Native($Command, $Message) { & $Command }
''' + invocation
                command = [executable, "-NoProfile", "-Command", script]

            # Act
            result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=30)

            # Assert
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            markdown = (root / "benchmark-async-health.md").read_text(encoding="utf-8")
            self.assertIn("**10ms**", markdown)
            self.assertIn("\U0001f534 unhealthy", markdown)
            self.assertNotIn("12.5%", markdown)
            self.assertNotIn(compare_perf.STATUS_LABELS["regression"] + " |", markdown)

    def test_bash_forwards_nondefault_threshold(self):
        """Linux callers retain their arguments while health uses fixed millisecond bands."""
        self.check_shell_threshold("bash")

    def test_powershell_forwards_nondefault_threshold(self):
        """Windows callers retain their arguments while health uses fixed millisecond bands."""
        self.check_shell_threshold("pwsh")


if __name__ == "__main__":
    unittest.main()
