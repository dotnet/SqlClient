"""Verify BDN diagnostic retention, reporting and matching source-baseline plumbing."""

import json
import os
from pathlib import Path
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

    def test_report_preserves_insufficient_windows_and_failure_status(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            target = root / "current/async-health"
            target.mkdir(parents=True)
            data = dict(SchemaVersion=2, Kind="BenchmarkAsyncHealth", CaseId="case",
                        Benchmark="Burst.OpenClose", Parameters="State=Warm",
                        Status="Completed", CompletedInvocations=1, Phase="ExtraIteration",
                        Operations=dict(Failed=0, Cancelled=1, Queued=2, InFlight=3,
                                        FirstWorkerOutcomesMissing=5),
                        Measurement={"Probes": {}},
                        Summary=dict(Health="Insufficient", DispatchP99Ms=1, Samples=5,
                                     Missing=0, ProcessThreadsPeak=50, OperationEvidence="Incomplete"))
            (target / "case-Completed.json").write_text(json.dumps(data))
            markdown, failed = report.render(root)
            self.assertFalse(failed)
            self.assertIn("Insufficient", markdown)
            self.assertIn("current", markdown)
            self.assertIn("State=Warm", markdown)
            self.assertIn("ExtraIteration", markdown)
            self.assertIn("0/1 | 2/3 | 5 | Incomplete", markdown)
            data.update(Status="Failed", Summary=None, Measurement=None)
            (target / "case-execution-Failed.json").write_text(json.dumps(data))
            markdown, failed = report.render(root)
            self.assertTrue(failed)
            self.assertIn("Failed | Invalid", markdown)

    def test_missing_diagnostic_evidence_fails(self):
        with tempfile.TemporaryDirectory() as temp:
            with self.assertRaisesRegex(ValueError, "No BDN"):
                report.render(Path(temp))

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
