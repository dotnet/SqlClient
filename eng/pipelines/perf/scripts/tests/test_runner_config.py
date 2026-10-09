"""Run with python -m unittest discover -s eng/pipelines/perf/scripts/tests.

Exercises the config sections of the actual runners without SQL Server or builds.
Set BASH and PWSH to select shell executables; unavailable shells are skipped.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest


SCRIPTS = Path(__file__).resolve().parents[1]
SWITCHES = ("UseManagedSniOnWindows", "UseOptimizedAsyncBehaviour", "UseConnectionPoolV2")
PASSWORD = 'test;="quoted" and \'single\''


class RunnerConfigTests(unittest.TestCase):
    def check_config(self, shell, switch):
        executable = os.environ.get(shell.upper()) or shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} is unavailable")
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            (root / "results").mkdir()
            source = root / "runnerconfig.jsonc"
            # Include a local tuning value to prove we use the live config, not the default.
            config = {"ConnectionString": "local", "Iterations": 17,
                      **dict.fromkeys(SWITCHES, False)}
            (root / "template.jsonc").write_text(
                "// local settings\n" + json.dumps(config), encoding="utf-8")
            env = dict(os.environ, TEST_ROOT=root.as_posix(), SQL_PASSWORD=PASSWORD,
                       TEST_SWITCH=switch, TEST_PYTHON=Path(sys.executable).as_posix())
            ext = "ps1" if shell == "pwsh" else "sh"
            text = (SCRIPTS / f"run-perf-tests.{ext}").read_text(encoding="utf-8-sig")
            start = "$RunnerConfig =" if shell == "pwsh" else 'RUNNER_CONFIG="'
            section = text[text.index(start):text.index("# 4 & 5.")]
            if shell == "pwsh":
                script = '''$ErrorActionPreference = "Stop"
$RepoRoot = $env:TEST_ROOT
$PerfDir = $RepoRoot
$ResultsDir = Join-Path $RepoRoot "results"
$SqlPassword = $env:SQL_PASSWORD
$SqlServer = "localhost"
$DbName = "perf"
$SwitchUnderTest = $env:TEST_SWITCH
$UseManagedSniOnWindows = "true"
$UseOptimizedAsyncBehaviour = ""
$UseConnectionPoolV2 = "false"
''' + section + '''
if (Test-Path $RunnerConfig) { throw "Config generated before build" }
Copy-Item (Join-Path $RepoRoot "template.jsonc") $SourceRunnerConfig
Prepare-RunnerConfig
Remove-Item $SourceRunnerConfig
Prepare-RunnerConfig
'''
                args = [executable, "-NoProfile", "-File"]
            else:
                script = '''set -u
REPO_ROOT="$TEST_ROOT"
PERF_DIR="$REPO_ROOT"
RESULTS_DIR="$REPO_ROOT/results"
export SQL_SERVER=localhost DB_NAME=perf
switchUnderTest="$TEST_SWITCH"
useManagedSniOnWindows=true
useOptimizedAsyncBehaviour=""
useConnectionPoolV2=false
python3() { "$TEST_PYTHON" "$@"; }
''' + section + '''
[[ ! -f "$RUNNER_CONFIG" ]] || exit 2
cp "$REPO_ROOT/template.jsonc" "$SOURCE_RUNNER_CONFIG" || exit 3
prepare_runner_config || exit 4
rm "$SOURCE_RUNNER_CONFIG" || exit 5
prepare_runner_config || exit 6
'''
                args = [executable]
            harness = root / f"test.{ext}"
            harness.write_text(script, encoding="utf-8", newline="\n")
            result = subprocess.run(args + [harness.as_posix()], env=env,
                                    capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertNotIn(PASSWORD, result.stdout + result.stderr)
            self.assertFalse(source.exists())  # Preparation is idempotent even without its source.
            variants = ("", "-baseline", "-current") if switch else ("",)
            for variant in variants:
                runtime = json.loads((root / f"perf-runnerconfig{variant}.json").read_text(encoding="utf-8-sig"))
                redacted = json.loads((root / "results" / f"runnerconfig{variant}.json").read_text(encoding="utf-8-sig"))
                expected = {**config, "UseManagedSniOnWindows": True}
                if variant:
                    expected[switch] = variant == "-current"
                self.assertEqual(redacted, {**expected, "ConnectionString": "<redacted>"})
                self.assertEqual({**runtime, "ConnectionString": "<redacted>"}, redacted)
                self.assertIn('Password="' + PASSWORD.replace('"', '""') + '";', runtime["ConnectionString"])

    def test_bash(self):
        for switch in ("", *SWITCHES):
            with self.subTest(switch=switch):
                self.check_config("bash", switch)

    def test_powershell(self):
        for switch in ("", *SWITCHES):
            with self.subTest(switch=switch):
                self.check_config("pwsh", switch)

    def test_translation_selects_each_pass_config(self):
        executable = os.environ.get("BASH") or shutil.which("bash")
        if not executable:
            self.skipTest("bash is unavailable")
        for separate in (False, True):
            with self.subTest(separate=separate), tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                for name in ("current", "baseline"):
                    (root / name).mkdir()
                names = ("runnerconfig.json", "runnerconfig-baseline.json", "runnerconfig-current.json")
                for name in names if separate else names[:1]:
                    (root / name).write_text('{}', encoding="utf-8")
                # Capture translator arguments without needing BenchmarkDotNet result fixtures.
                capture = root / "capture.py"
                capture.write_text(
                    'import json, os, sys\n'
                    'with open(os.environ["CAPTURE"], "a") as f: f.write(json.dumps(sys.argv[1:]) + "\\n")\n',
                    encoding="utf-8")
                env = dict(os.environ, REPO_DIR=SCRIPTS.parents[3].as_posix(),
                           RESULTS_DIR=root.as_posix(), KUSTO_OUT=(root / "out").as_posix(),
                           SOURCE_VERSION="HEAD", SOURCE_BRANCH="test", COLLECTION_URI="test",
                           TEAM_PROJECT="test", BUILD_ID="1", AGENT_MACHINE_NAME="test",
                           PLATFORM="test", RUN_MODE="interleaved", BASELINE_VERSION="test",
                           CFG_USE_MANAGED_SNI="", CFG_USE_OPTIMIZED_ASYNC="",
                           CFG_USE_CONNECTION_POOL_V2="true", CAPTURE=(root / "args.jsonl").as_posix(),
                           TEST_PYTHON=Path(sys.executable).as_posix(), CAPTURE_SCRIPT=capture.as_posix(),
                           TRANSLATE=(SCRIPTS / "translate_results_to_kusto.sh").as_posix())
                result = subprocess.run([executable, "-c",
                    'python3() { "$TEST_PYTHON" "$CAPTURE_SCRIPT" "$@"; }; source "$TRANSLATE"'],
                    env=env, capture_output=True, text=True)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                calls = [json.loads(line) for line in (root / "args.jsonl").read_text().splitlines()]
                self.assertEqual(len(calls), 2)
                for call, variant in zip(calls, ("current", "baseline")):
                    name = f"runnerconfig-{variant}.json" if separate else "runnerconfig.json"
                    self.assertEqual(call[call.index("--runner-config") + 1], (root / name).as_posix())
                    self.assertNotIn("--config-override", call)


if __name__ == "__main__":
    unittest.main()
