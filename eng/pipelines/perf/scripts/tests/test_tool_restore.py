"""Verify VM runners restore local tools before building either source tree."""

import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import unittest


SCRIPTS = Path(__file__).resolve().parents[1]


class ToolRestoreTests(unittest.TestCase):
    """Exercise tool restoration in both runners without restoring real packages."""

    def check_restore(self, shell, failing_root=""):
        """Run the actual restore helper and call sites with a recording dotnet stub."""
        executable = os.environ.get(shell.upper()) or shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} is unavailable")
        extension = "ps1" if shell == "pwsh" else "sh"
        text = (SCRIPTS / f"run-perf-tests.{extension}").read_text(encoding="utf-8-sig")
        start = text.index("# Restore repository-local build tools")
        end = text.index("################################################################", start)
        restore = text[start:end]
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            current, baseline = root / "current source", root / "baseline source"
            current.mkdir()
            baseline.mkdir()
            log = root / "restore.log"
            env = dict(os.environ, TEST_CURRENT=str(current), TEST_BASELINE=str(baseline),
                       TEST_LOG=str(log), TEST_FAIL_ROOT=failing_root)
            if shell == "pwsh":
                native = text[text.index("function Invoke-Native {"):text.index("# Resolve paths")]
                baseline_call = re.findall(r"(?m)^\s*Restore-LocalTools \$BaselineSrcDir\s*$", text)
                self.assertEqual(len(baseline_call), 1)
                script = '''$ErrorActionPreference = "Stop"
$RepoRoot = $env:TEST_CURRENT
$BaselineSrcDir = $env:TEST_BASELINE
function dotnet {
    "$((Get-Location).Path)|$($args -join ' ')" | Add-Content $env:TEST_LOG
    $name = if ((Get-Location).Path -eq $RepoRoot) { "current" } else { "baseline" }
    $global:LASTEXITCODE = if ($env:TEST_FAIL_ROOT -eq $name) { 42 } else { 0 }
}
''' + native + restore + baseline_call[0] + '\nWrite-Output "BUILD"\n'
                args = [executable, "-NoProfile", "-Command", script]
            else:
                baseline_call = re.findall(
                    r'(?m)^\s*restore_local_tools "\$\{BASELINE_SRC_DIR\}"\s*$', text)
                self.assertEqual(len(baseline_call), 1)
                script = '''set -euo pipefail
REPO_ROOT="$TEST_CURRENT"
BASELINE_SRC_DIR="$TEST_BASELINE"
dotnet() {
    printf '%s|%s\\n' "$PWD" "$*" >> "$TEST_LOG"
    local name=baseline
    [[ "$PWD" != "$REPO_ROOT" ]] || name=current
    [[ "$TEST_FAIL_ROOT" != "$name" ]] || return 42
}
''' + restore + baseline_call[0] + '\necho BUILD\n'
                args = [executable, "-c", script]

            # Act
            result = subprocess.run(args, cwd=root, env=env, capture_output=True, text=True)

            # Assert
            calls = log.read_text(encoding="utf-8-sig").replace("\\", "/").splitlines()
            expected = [f"{current.as_posix()}|tool restore"]
            if failing_root != "current":
                expected.append(f"{baseline.as_posix()}|tool restore")
            self.assertEqual(calls, expected)
            if failing_root:
                self.assertNotEqual(result.returncode, 0)
                self.assertNotIn("BUILD", result.stdout)
            else:
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn("BUILD", result.stdout)
            self.assertLess(start, text.index("# 2. Create the perf database"))

    def test_bash_restores_both_source_trees(self):
        self.check_restore("bash")

    def test_powershell_restores_both_source_trees(self):
        self.check_restore("pwsh")

    def test_bash_current_restore_failure_stops_build(self):
        self.check_restore("bash", "current")

    def test_powershell_current_restore_failure_stops_build(self):
        self.check_restore("pwsh", "current")

    def test_bash_baseline_restore_failure_stops_build(self):
        self.check_restore("bash", "baseline")

    def test_powershell_baseline_restore_failure_stops_build(self):
        self.check_restore("pwsh", "baseline")
