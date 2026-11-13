"""Execute the new sections of both real entrypoints, without SQL or SDK installation."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest

SCRIPTS = Path(__file__).resolve().parents[1]


class HealthRunnerTests(unittest.TestCase):
    """Default execution, baseline routing, and failure propagation agree across shells."""

    def check_runner(self, shell, baseline, health_exit):
        """Stub external work only; parse and execute the checked-in runner integration verbatim."""
        executable = shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} unavailable")
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            env = dict(os.environ, TEST_ROOT=root.as_posix(), TEST_HEALTH_EXIT=str(health_exit))
            env.pop("PERF_CLIENT_CPUS", None)
            if shell == "bash":
                text = (SCRIPTS / "run-perf-tests.sh").read_text()
                section = text[text.index("# Always run production-path health"):
                               text.index('if [[ -n "${baselineLabel}" && "${runMode}"')]
                script = f'''
REPO_ROOT="$TEST_ROOT"
RESULTS_DIR="$TEST_ROOT"
RUNNER_CONFIG="$TEST_ROOT/private.json"
SCRIPT_DIR="$TEST_ROOT"
SQL_SERVER=localhost
baselineLabel=label
baselineSourceRef={"main" if baseline == "source" else ""}
baselineVersion={"6.1.1" if baseline == "package" else ""}
BASELINE_SRC_DIR="$TEST_ROOT/old"
build_current_default_output() {{ :; }}
prepare_runner_config() {{ :; }}
python3() {{ printf '%s\\n' "$@" > "$TEST_ROOT/args.txt"; return "$TEST_HEALTH_EXIT"; }}
''' + section + '\necho BDN_REACHED\nexit "$health_status"\n'
                path = root / "runner.sh"
                path.write_text(script)
                command = [executable, str(path)]
            else:
                text = (SCRIPTS / "run-perf-tests.ps1").read_text()
                section = text[text.index("# Automatic production-path coverage"):
                               text.index('if ((-not [string]::IsNullOrEmpty($BaselineLabel)) -and')]
                script = f'''
$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$RepoRoot = $env:TEST_ROOT
$ResultsDir = $RepoRoot
$RunnerConfig = Join-Path $RepoRoot "private.json"
$ScriptDir = $RepoRoot
$BaselineLabel = ""
$BaselineSourceRef = "{'main' if baseline == 'source' else ''}"
$BaselineVersion = "{'6.1.1' if baseline == 'package' else ''}"
$BaselineSrcDir = Join-Path $RepoRoot "old"
function Build-CurrentDefaultOutput {{ }}
function Prepare-RunnerConfig {{ }}
function python3 {{
    ConvertTo-Json -InputObject @($args) | Set-Content (Join-Path $env:TEST_ROOT "args.json")
    $global:LASTEXITCODE = [int]$env:TEST_HEALTH_EXIT
}}
''' + section + '\nWrite-Host "BDN_REACHED"\nexit $healthStatus\n'
                path = root / "runner.ps1"
                path.write_text(script)
                command = [executable, "-NoProfile", "-File", str(path)]
            run = subprocess.run(command, env=env, capture_output=True, text=True, timeout=30)
            self.assertEqual(run.returncode, health_exit if shell == "bash" or health_exit < 2
                             else 1, run.stdout + run.stderr)
            self.assertEqual("BDN_REACHED" in run.stdout, health_exit < 2)
            args = (root / "args.txt").read_text().splitlines() if shell == "bash" else json.loads(
                (root / "args.json").read_text(encoding="utf-8-sig"))
            self.assertIn("--runner-config", args)
            self.assertEqual("--baseline-source-root" in args, baseline == "source")
            self.assertEqual("--baseline-package-version" in args, baseline == "package")
            if shell == "pwsh":
                self.assertNotIn("", args)

    def test_bash_automatic_success_source_baseline(self):
        self.check_runner("bash", "source", 0)

    def test_powershell_automatic_success_source_baseline(self):
        self.check_runner("pwsh", "source", 0)

    def test_bash_regression_retained_after_bdn(self):
        self.check_runner("bash", "package", 1)

    def test_powershell_regression_retained_after_bdn(self):
        self.check_runner("pwsh", "package", 1)

    def test_bash_invalid_evidence_fails_without_bdn(self):
        self.check_runner("bash", "none", 2)

    def test_powershell_invalid_evidence_fails_without_bdn(self):
        self.check_runner("pwsh", "none", 2)


if __name__ == "__main__":
    unittest.main()
