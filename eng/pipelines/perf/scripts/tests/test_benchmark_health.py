"""Verify BDN diagnostic retention, reporting and matching source-baseline plumbing."""

import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import benchmark_health_report as report
import interleave_perf as interleave


class BenchmarkHealthTests(unittest.TestCase):
    """BDN health artifacts remain separate from normal benchmark timing rows."""

    def test_only_new_burst_runner_has_async_health_instrumentation(self):
        """Existing workloads retain ordinary BDN measurements without health wrappers."""
        # Arrange
        runners = (SCRIPTS.parents[3] / "src/Microsoft.Data.SqlClient/tests/"
                   "PerformanceTests/BenchmarkRunners")
        sources = {path.name: path.read_text(encoding="utf-8-sig")
                   for path in runners.rglob("*.cs")}

        # Act
        marked = {name for name, text in sources.items()
                  if re.search(r"\[\s*AsyncHealthDiagnoser(?:Attribute)?\s*\]", text)}
        recorded = {name for name, text in sources.items()
                    if re.search(r"AsyncHealth(?:Diagnoser|OperationRecorder)\s*\.", text)}

        # Assert
        self.assertEqual(marked, {"OpenAsyncBurstRunner.cs"})
        self.assertEqual(recorded, {"OpenAsyncBurstRunner.cs"})

    def test_interleaved_failure_copies_raw_evidence_and_log_before_raising(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            destination = root / "results"

            def child(exe_dir, assembly, unit, cwd, cpus, log_path, env_overrides=None, timeout=None):
                artifact = Path(cwd) / "BenchmarkDotNet.Artifacts/async-health"
                artifact.mkdir(parents=True)
                (artifact / "failed.json").write_text('{"Status":"Failed"}')
                Path(log_path).write_text("diagnostic failed")
                return 1

            runner = interleave.Runner("baseline", "current", "PerformanceTests.dll",
                                       str(root / "work"), [])
            with patch.object(interleave, "run_unit_process", side_effect=child):
                with self.assertRaisesRegex(RuntimeError, "failed"):
                    runner._run_one("current", "current", "OpenAsyncBurst", 1, str(destination))
            self.assertEqual((destination / "async-health/failed.json").read_text(),
                             '{"Status":"Failed"}')
            self.assertEqual((destination / "OpenAsyncBurst-rep1.log").read_text(),
                             "diagnostic failed")

    def test_collector_does_not_count_diagnostics_as_timing_results(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            source = root / "work/BenchmarkDotNet.Artifacts"
            (source / "async-health").mkdir(parents=True)
            (source / "async-health/one.json").write_text('{"Kind":"BenchmarkAsyncHealth"}')
            (source / "Runner-report-full.json").write_text(json.dumps({
                "Benchmarks": [{"Type": "Runner"}]}))
            types = interleave.collect_results(str(root / "work"), str(root / "results"))
            self.assertEqual(types, {"Runner"})
            self.assertTrue((root / "results/async-health/one.json").is_file())
            self.assertEqual(len(list((root / "results").glob("*-report-full.json"))), 1)

    def test_collector_preserves_each_isolated_case_report(self):
        """Per-case processes emit the same report filename; none may overwrite another case."""
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            for case in ("first", "second"):
                artifact = (root / "work/BenchmarkDotNet.Artifacts/processes" /
                            case / "BenchmarkDotNet.Artifacts")
                (artifact / "async-health").mkdir(parents=True)
                (artifact / f"async-health/{case}.json").write_text('{"Status":"Completed"}')
                (artifact / "Runner-report-full.json").write_text(json.dumps({
                    "Benchmarks": [{"Type": "Runner", "Parameters": case}]}))
            types = interleave.collect_results(str(root / "work"), str(root / "results"))
            self.assertEqual(types, {"Runner"})
            reports = list((root / "results").glob("**/*-report-full.json"))
            self.assertEqual(len(reports), 2)
            self.assertEqual({json.loads(path.read_text())["Benchmarks"][0]["Parameters"]
                              for path in reports}, {"first", "second"})
            self.assertEqual(len(list((root / "results/async-health").glob("*.json"))), 2)

    def test_missing_diagnostic_evidence_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(ValueError, "No BDN"):
                report.render(Path(temp))

    def test_execution_metadata_selects_repetition_policy_without_unit_name_rules(self):
        """A runner's declared extended sampling policy drives repetitions, not a Python allowlist."""
        runner = interleave.Runner("baseline", "current", "app.dll", "work", [])
        metadata = [
            {"Name": "FutureRunner", "RepeatAll": True},
            {"Name": "Ordinary", "RepeatAll": False}]
        with patch.object(interleave.subprocess, "check_output",
                          return_value=json.dumps(metadata)) as command:
            units = runner.list_units()
        self.assertEqual(units, ["FutureRunner", "Ordinary"])
        self.assertEqual(runner.repeat_all_units, {"FutureRunner"})
        self.assertEqual(command.call_args.kwargs["env"]["PERF_LIST_BENCHMARKS"], "json")

    def test_extended_unit_runs_requested_repetitions_without_nested_confirmation(self):
        """Adequate sampling repeats even an unchanged unit, exactly N times."""
        class FakeRunner:
            repeat_all_units = {"Extended"}

            def __init__(self):
                self.calls = []

            def interleave(self, units, rep, baseline, current):
                self.calls.append((list(units), rep))
                for directory in (baseline, current):
                    Path(directory).mkdir(parents=True, exist_ok=True)
                    for unit in units:
                        (Path(directory) / f"{unit}-report-full.json").write_text(json.dumps({
                            "Benchmarks": [{"Type": unit, "Method": "Run",
                                            "Statistics": {"Mean": 100}}]}))
                return {unit: {unit} for unit in units}

        with tempfile.TemporaryDirectory() as temp:
            runner = FakeRunner()
            entries, confirmed, unconfirmed = interleave.orchestrate(
                runner, ["Extended", "Ordinary"], temp, 10, 3)
            self.assertEqual(runner.calls,
                             [(["Extended", "Ordinary"], 1), (["Extended"], 2), (["Extended"], 3)])
            self.assertFalse(confirmed)
            self.assertFalse(unconfirmed)
            self.assertTrue(all(entry["status"] == "unchanged" for entry in entries))

    def check_sequential_rebuild(self, shell):
        """Execute each real runner's build guard, proving baseline cannot leave candidate stale."""
        executable = shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} unavailable")
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            env = dict(os.environ, TEST_ROOT=root.as_posix())
            if shell == "bash":
                text = (SCRIPTS / "run-perf-tests.sh").read_text()
                start = text.index('    if [[ "${label}" == "current"')
                section = text[start:text.index("    prepare_runner_config", start)]
                script = '''
PERF_PROJECT=candidate
configuration=Release
framework=net10.0
CURRENT_DEFAULT_OUTPUT_BUILT=true
dotnet() { printf '%s\\n' "$*" >> "$TEST_ROOT/builds"; }
build_part() {
    local label="$1"
    local project="$PERF_PROJECT"
    local extra_args=()
    if [[ "$label" == "baseline" ]]; then extra_args=(-p:MdsSourceRoot=old); fi
''' + section + '''
}
build_part baseline
[[ "$CURRENT_DEFAULT_OUTPUT_BUILT" == false ]] || exit 3
build_part current
[[ "$CURRENT_DEFAULT_OUTPUT_BUILT" == true ]] || exit 4
'''
                path = root / "test.sh"
                path.write_text(script)
                command = [executable, str(path)]
            else:
                text = (SCRIPTS / "run-perf-tests.ps1").read_text()
                start = text.index('    if (($Label -eq "current")')
                section = text[start:text.index("    Prepare-RunnerConfig", start)]
                script = '''
$ErrorActionPreference = 'Stop'
$PerfProject = 'candidate'
$Configuration = 'Release'
$Framework = 'net10.0'
$script:CurrentDefaultOutputBuilt = $true
function dotnet { "$args" | Add-Content (Join-Path $env:TEST_ROOT 'builds') }
function Invoke-Native($command, $message) { & $command }
function Build-Part($Label, [string[]]$ExtraArgs) {
    $Project = $PerfProject
''' + section + '''
}
Build-Part 'baseline' @('-p:MdsSourceRoot=old')
if ($script:CurrentDefaultOutputBuilt) { throw 'Baseline did not invalidate candidate' }
Build-Part 'current' @()
if (-not $script:CurrentDefaultOutputBuilt) { throw 'Candidate did not rebuild' }
'''
                path = root / "test.ps1"
                path.write_text(script)
                command = [executable, "-NoProfile", "-File", str(path)]
            result = subprocess.run(command, env=env, capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            builds = (root / "builds").read_text(encoding="utf-8-sig").splitlines()
            self.assertEqual(len(builds), 2)
            self.assertIn("MdsSourceRoot=old", builds[0])
            self.assertNotIn("MdsSourceRoot", builds[1])

    def test_bash_sequential_baseline_forces_candidate_rebuild(self):
        self.check_sequential_rebuild("bash")

    def test_powershell_sequential_baseline_forces_candidate_rebuild(self):
        self.check_sequential_rebuild("pwsh")


if __name__ == "__main__":
    unittest.main()
