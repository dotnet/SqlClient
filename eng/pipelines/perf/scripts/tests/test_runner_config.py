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
    """Exercise authentication and per-pass configs without real SQL commands or builds."""

    def check_config(self, shell, switch, local=False, sql_failure=""):
        """Run isolated runner sections with captured SQL calls and temporary config files."""
        if local and os.name != "nt":
            self.skipTest("Local Windows authentication requires Windows")
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
                       TEST_SWITCH=switch, TEST_PYTHON=Path(sys.executable).as_posix(),
                       TEST_SQL_SERVER="127.0.0.1" if local else "192.0.2.1",
                       TEST_SQL_FAILURE=sql_failure)
            ext = "ps1" if shell == "pwsh" else "sh"
            text = (SCRIPTS / f"run-perf-tests.{ext}").read_text(encoding="utf-8-sig")
            start = "$RunnerConfig =" if shell == "pwsh" else 'RUNNER_CONFIG="'
            section = text[text.index(start):text.index("# 4 & 5.")]
            if shell == "pwsh":
                native = text[text.index("function Invoke-Native {"):text.index("# Resolve paths")]
                authentication = text[text.index("function Test-IsLocalSqlServer {"):
                                      text.index("# Noise-reduction controls")]
                script = '''$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$RepoRoot = $env:TEST_ROOT
$PerfDir = $RepoRoot
$ResultsDir = Join-Path $RepoRoot "results"
$SqlPassword = $env:SQL_PASSWORD
$SqlServer = $env:TEST_SQL_SERVER
$DbName = "perf"
$SwitchUnderTest = $env:TEST_SWITCH
$UseManagedSniOnWindows = "true"
$UseOptimizedAsyncBehaviour = ""
$UseConnectionPoolV2 = "false"
$script:SqlCalls = @()
function Invoke-TestSqlcmd {
    $script:SqlCalls += ,@($args)
    ConvertTo-Json -InputObject $script:SqlCalls -Depth 5 |
        Set-Content (Join-Path $RepoRoot "sql-calls.json")
    $phase = if ($args -contains "-E") { "preflight" } else { "setup" }
    $global:LASTEXITCODE = if ($env:TEST_SQL_FAILURE -eq $phase) { 42 } else { 0 }
}
$sqlcmd = @{ Source = "Invoke-TestSqlcmd" }
''' + native + authentication + section + '''
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
            self.assertNotIn(PASSWORD, result.stdout + result.stderr)
            calls_path = root / "sql-calls.json"
            if shell == "pwsh" and local:
                self.assertTrue(calls_path.exists(), result.stdout + result.stderr)
                calls = json.loads(calls_path.read_text(encoding="utf-8-sig"))
                self.assertEqual(len(calls), 1 if sql_failure == "setup" else 2)
                setup = calls[0]
                self.assertEqual(setup[setup.index("-S") + 1], "127.0.0.1")
                self.assertEqual(setup[setup.index("-P") + 1], PASSWORD)
                ddl = setup[setup.index("-Q") + 1]
                self.assertIn("CREATE LOGIN ", ddl)
                self.assertIn(" FROM WINDOWS", ddl)
                self.assertIn("ALTER ROLE [db_owner] ADD MEMBER ", ddl)
                if len(calls) == 2:
                    preflight = calls[1]
                    self.assertEqual(preflight[preflight.index("-S") + 1], "tcp:localhost,1433")
                    self.assertIn("-E", preflight)
                    self.assertNotIn("-P", preflight)
            else:
                self.assertFalse(calls_path.exists())
            if sql_failure:
                self.assertNotEqual(result.returncode, 0)
                message = ("sqlcmd failed to configure Windows benchmark identity" if
                           sql_failure == "setup" else "Loopback integrated-authentication preflight failed")
                self.assertIn(message, result.stdout + result.stderr)
                self.assertFalse((root / "perf-runnerconfig.json").exists())
                return
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
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
                connection = runtime["ConnectionString"]
                if shell == "pwsh":
                    server = "localhost" if local else "192.0.2.1"
                    self.assertIn(f"Server=tcp:{server},1433;", connection)
                    self.assertIn("Encrypt=True;", connection)
                if local:
                    self.assertIn("Integrated Security=True;", connection)
                    self.assertNotIn("Password=", connection)
                    self.assertNotIn("User ID=", connection)
                else:
                    self.assertIn('Password="' + PASSWORD.replace('"', '""') + '";', connection)
                    self.assertNotIn("Integrated Security=", connection)

    def test_bash(self):
        for switch in ("", *SWITCHES):
            with self.subTest(switch=switch):
                self.check_config("bash", switch)

    def test_powershell(self):
        """Both authentication modes retain per-pass switches, quoting, and redaction."""
        for switch in ("", *SWITCHES):
            for local in (False, True):
                with self.subTest(switch=switch, local=local):
                    self.check_config("pwsh", switch, local=local)

    def test_powershell_authentication_failure_stops_config_generation(self):
        """A failed identity setup or integrated preflight must stop the benchmark run."""
        for phase in ("setup", "preflight"):
            with self.subTest(phase=phase):
                self.check_config("pwsh", "", local=True, sql_failure=phase)

    def test_powershell_local_endpoint_classification(self):
        """Only loopback or local-interface addresses qualify for Windows authentication."""
        executable = os.environ.get("PWSH") or shutil.which("pwsh")
        if not executable:
            self.skipTest("pwsh is unavailable")
        text = (SCRIPTS / "run-perf-tests.ps1").read_text(encoding="utf-8-sig")
        function = text[text.index("function Test-IsLocalSqlServer {"):
                        text.index("if (Test-IsLocalSqlServer $SqlServer)")]
        script = function + '''
foreach ($server in @("localhost", "127.0.0.1", "::1")) {
    if (-not (Test-IsLocalSqlServer $server)) { throw "Local endpoint not recognized: $server" }
}
foreach ($server in @("192.0.2.1", "2001:db8::1")) {
    if (Test-IsLocalSqlServer $server) { throw "External endpoint classified as local: $server" }
}
'''
        result = subprocess.run([executable, "-NoProfile", "-Command", script],
                                capture_output=True, text=True, timeout=30)
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)

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
