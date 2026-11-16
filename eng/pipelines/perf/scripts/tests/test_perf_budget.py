"""Verify bounded execution leaves time and evidence for pipeline artifact collection."""

import contextlib
import io
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
import interleave_perf as interleave


class PerfBudgetTests(unittest.TestCase):
    """Budgets terminate owned work, preserve evidence and never turn expiry into success."""

    def test_deadline_is_shared_across_units(self):
        """A later unit receives only the remaining budget, not a fresh full timeout."""
        # Arrange
        with patch.object(interleave.time, "monotonic", side_effect=[100, 105, 111]):
            runner = interleave.Runner("base", "current", "app.dll", "work", [],
                                       max_run_seconds=10)

            # Act / Assert
            self.assertEqual(runner.remaining_seconds(), 5)
            with self.assertRaisesRegex(TimeoutError, "budget exhausted"):
                runner.remaining_seconds()

    def test_timed_out_process_is_stopped_and_log_retained(self):
        """Exercise actual process termination without requiring dotnet or a SQL server."""
        # Arrange
        original_popen = subprocess.Popen
        started = []

        def launch(command, **kwargs):
            process = original_popen(
                [sys.executable, "-c", "import time; print('started', flush=True); time.sleep(30)"],
                **kwargs)
            started.append(process)
            return process

        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with patch.object(interleave.subprocess, "Popen", side_effect=launch):
                # stop_process_tree invokes taskkill through subprocess.run on Windows.
                # Restore Popen for that native termination command, rather than substituting it.
                stop = interleave.stop_process_tree

                def stop_real(process):
                    with patch.object(interleave.subprocess, "Popen", original_popen):
                        stop(process)

                # Act
                with patch.object(interleave, "stop_process_tree", side_effect=stop_real):
                    code = interleave.run_unit_process(
                        temp, "unused.dll", "Unit", temp, [], str(root / "run.log"), timeout=2)

            # Assert
            self.assertEqual(code, 124)
            self.assertIsNotNone(started[0].poll())
            self.assertIn("remaining run budget", (root / "run.log").read_text())

    def test_exception_still_collects_partial_evidence(self):
        """Launch/execution exceptions cannot bypass the diagnostic collector."""
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            runner = interleave.Runner("base", "current", "app.dll", str(root / "runs"), [])

            def failing_run(exe, assembly, unit, cwd, cpus, log_path, **kwargs):
                folder = Path(cwd) / "BenchmarkDotNet.Artifacts/async-health"
                folder.mkdir(parents=True)
                (folder / "partial.json").write_text(json.dumps({"Status": "Failed"}))
                Path(log_path).write_text("deadline reached")
                raise TimeoutError("deadline reached")

            # Act / Assert
            with patch.object(interleave, "run_unit_process", side_effect=failing_run):
                with self.assertRaises(TimeoutError):
                    runner._run_one("current", "current", "Unit", 1, str(root / "current"))
            self.assertTrue((root / "current/async-health/partial.json").is_file())
            self.assertEqual((root / "current/Unit-rep1.log").read_text(), "deadline reached")

    def test_invalid_budget_is_rejected(self):
        """Non-finite or nonpositive inputs must fail before invoking any benchmark."""
        # Act / Assert
        for value in ("0", "-1", "nan", "inf"):
            with self.subTest(value=value), contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit) as result:
                    interleave.main(["--baseline-exe-dir", "base", "--current-exe-dir", "current",
                                     "--results-dir", "results", "--max-run-seconds", value])
                self.assertEqual(result.exception.code, 2)

    def check_shell_budget(self, shell, elapsed, expired=False):
        """Execute the actual runner budget function with a controlled elapsed clock."""
        executable = shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} unavailable")
        if shell == "bash":
            text = (SCRIPTS / "run-perf-tests.sh").read_text()
            start = text.index("remaining_perf_seconds() {")
            function = text[start:text.index("# Validate queue-time inputs", start)]
            script = (f"testTimeoutMinutes=180\nrunStartedSeconds=0\nSECONDS={elapsed}\n" +
                      function + "\nremaining_perf_seconds\n")
            command = [executable, "-c", script]
        else:
            text = (SCRIPTS / "run-perf-tests.ps1").read_text()
            start = text.index("function Get-RemainingPerfSeconds {")
            function = text[start:text.index("# Native-command error handling", start)]
            script = (f"$ErrorActionPreference='Stop'\n$TestTimeoutMinutes=180\n"
                      f"$RunClock=@{{Elapsed=@{{TotalSeconds={elapsed}}}}}\n" +
                      function + "\nGet-RemainingPerfSeconds\n")
            command = [executable, "-NoProfile", "-Command", script]

        # Act
        result = subprocess.run(command, capture_output=True, text=True, timeout=30)

        # Assert
        if expired:
            self.assertNotEqual(result.returncode, 0)
            self.assertIn("budget exhausted", result.stderr)
        else:
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(int(result.stdout.strip()), 170 * 60 - elapsed)

    def test_bash_reserves_collection_grace(self):
        """Build/setup elapsed time and ten minutes of collection grace are excluded."""
        self.check_shell_budget("bash", 30)

    def test_powershell_reserves_collection_grace(self):
        """Windows applies the same remaining-budget calculation."""
        self.check_shell_budget("pwsh", 30)

    def test_bash_expired_budget_fails(self):
        """An exhausted budget must stop before another unit is launched."""
        self.check_shell_budget("bash", 10201, expired=True)

    def test_powershell_expired_budget_fails(self):
        """Windows must also fail rather than starting work with a negative timeout."""
        self.check_shell_budget("pwsh", 10201, expired=True)

    def test_pipeline_budgets_reach_both_entrypoints(self):
        """All consumers forward their own outer timeout instead of using a mismatched default."""
        # Act / Assert
        for path in SCRIPTS.parent.glob("sqlclient-perf-*.yml"):
            with self.subTest(pipeline=path.name):
                text = path.read_text()
                self.assertIn("-TestTimeoutMinutes ${{ parameters.testTimeoutMinutes }}", text)
                self.assertIn("--test-timeout-minutes ${{ parameters.testTimeoutMinutes }}", text)


if __name__ == "__main__":
    unittest.main()
