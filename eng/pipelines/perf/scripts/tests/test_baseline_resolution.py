"""Exercise governed baseline resolution and both runners without builds or SQL Server."""

import contextlib
import importlib.util
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap
import unittest
from unittest.mock import call, patch
from urllib.error import URLError


SCRIPTS = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("resolve_baseline", SCRIPTS / "resolve_baseline.py")
resolver = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(resolver)


class BaselineResolutionTests(unittest.TestCase):
    """Verify stable selection, error handling, and forwarding to real runner build arguments."""

    def test_selects_highest_stable_numerically(self):
        # Act
        version = resolver.latest_stable({"versions": [
            "6.1.10", "7.0.2", "7.0.10", "7.0.9", "8.0.0-preview1", "8.0.0-rc.1",
        ]})

        # Assert
        self.assertEqual(version, "7.0.10")

    def test_rejects_missing_stable_version(self):
        # Act / Assert
        with self.assertRaisesRegex(ValueError, "no stable"):
            resolver.latest_stable({"versions": ["8.0.0-preview1"]})

    def test_rejects_malformed_index(self):
        # Act / Assert
        for index in ([], {}, {"versions": "7.0.2"}, {"versions": [None]}):
            with self.subTest(index=index), self.assertRaisesRegex(ValueError, "versions array"):
                resolver.latest_stable(index)

    def test_fetches_index_with_timeout(self):
        # Arrange
        service_url = "https://example.test/governed/index.json"
        package_url = "https://example.test/governed/flat2/"
        responses = [
            io.BytesIO(json.dumps({"resources": [{
                "@type": "PackageBaseAddress/3.0.0", "@id": package_url,
            }]}).encode()),
            io.BytesIO(json.dumps({"versions": ["7.0.10"]}).encode()),
        ]
        stdout = io.StringIO()
        with tempfile.TemporaryDirectory() as temp:
            config = Path(temp) / "NuGet.config"
            config.write_text(f'''<configuration><packageSources>
<add key="local" value="packages/" />
<add key="governed" value="{service_url}" />
</packageSources></configuration>''')

            # Act
            with patch.object(resolver, "NUGET_CONFIG", config):
                with patch.object(resolver, "urlopen", side_effect=responses) as fetch:
                    with contextlib.redirect_stdout(stdout):
                        result = resolver.main()

        # Assert
        self.assertEqual(result, 0)
        self.assertEqual(stdout.getvalue(), "7.0.10\n")
        self.assertEqual(fetch.call_args_list, [
            call(service_url, timeout=30),
            call(package_url + "microsoft.data.sqlclient/index.json", timeout=30),
        ])

    def test_missing_governed_source_fails_without_network_lookup(self):
        # Arrange
        with tempfile.TemporaryDirectory() as temp:
            config = Path(temp) / "NuGet.config"
            config.write_text("<configuration><packageSources /></configuration>")

            # Act / Assert
            with patch.object(resolver, "urlopen") as fetch:
                with self.assertRaisesRegex(ValueError, "governed package source"):
                    resolver.governed_package_index(config)
                fetch.assert_not_called()

    def test_missing_package_endpoint_fails(self):
        # Act / Assert
        with patch.object(resolver, "read_json", return_value={"resources": []}):
            with self.assertRaisesRegex(ValueError, "PackageBaseAddress"):
                resolver.governed_package_index(resolver.NUGET_CONFIG)

    def test_lookup_failure_has_no_success_output(self):
        # Arrange
        stdout, stderr = io.StringIO(), io.StringIO()

        # Act
        with patch.object(resolver, "urlopen", side_effect=URLError("unavailable")):
            with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
                result = resolver.main()

        # Assert
        self.assertEqual(result, 1)
        self.assertEqual(stdout.getvalue(), "")
        self.assertIn("Failed to resolve latest stable", stderr.getvalue())

    def check_runner(self, shell, requested, failure=False):
        """Execute the actual baseline argument section with only network lookup stubbed."""
        executable = os.environ.get(shell.upper()) or shutil.which(shell)
        if not executable:
            self.skipTest(f"{shell} is unavailable")
        text = (SCRIPTS / f"run-perf-tests.{'ps1' if shell == 'pwsh' else 'sh'}").read_text()
        with tempfile.TemporaryDirectory() as temp:
            env = dict(os.environ, TEST_VERSION=requested, TEST_ROOT=temp,
                       TEST_FAILURE="1" if failure else "0")
            if shell == "pwsh":
                native = text[text.index("function Invoke-Native {"):text.index("# Resolve paths")]
                config = text[text.index('$BaselineNuGetConfig ='):
                              text.index("# --- Source baseline (-BaselineSourceRef)")]
                section = text[text.index('if (-not [string]::IsNullOrEmpty($BaselineVersion)) {',
                                          text.index("# --- Resolve how")):
                               text.index('} elseif (-not [string]::IsNullOrEmpty($BaselineSourceRef))',
                                          text.index("# --- Resolve how"))] + "}"
                script = '''$ErrorActionPreference = "Stop"
$BaselineVersion = $env:TEST_VERSION
$RepoRoot = $env:TEST_ROOT
$ScriptDir = $env:TEST_ROOT
function python3 {
    if ($args[0] -notlike "*resolve_baseline.py") { throw "Wrong resolver" }
    $global:LASTEXITCODE = [int]$env:TEST_FAILURE
    if ($LASTEXITCODE -eq 0) { "7.0.10" }
}
''' + native + config + section + '''
Write-Output "ARG:$BaselineLabel"
$BaselineBuildArgs | ForEach-Object { Write-Output "ARG:$_" }
'''
                args = [executable, "-NoProfile", "-Command", script]
            else:
                config = text[text.index('BASELINE_NUGET_CONFIG='):
                              text.index("# --- Source baseline (--baseline-source-ref)")]
                section = text[text.index('if [[ -n "${baselineVersion}" ]]; then',
                                          text.index("# --- Resolve how")):
                               text.index('elif [[ -n "${baselineSourceRef}" ]]',
                                          text.index("# --- Resolve how"))] + "fi"
                script = '''set -euo pipefail
baselineVersion="$TEST_VERSION"
REPO_ROOT="$TEST_ROOT"
SCRIPT_DIR="$TEST_ROOT"
python3() {
    [[ "$1" == */resolve_baseline.py ]] || exit 2
    [[ "$TEST_FAILURE" == 0 ]] || return 1
    echo 7.0.10
}
''' + config + section + '''
printf 'ARG:%s\\n' "$baselineLabel" "${baselineBuildArgs[@]}"
'''
                args = [executable, "-c", script]

            # Act
            result = subprocess.run(args, env=env, capture_output=True, text=True)

            # Assert
            if failure:
                self.assertNotEqual(result.returncode, 0)
                self.assertNotIn("-p:MdsPackageVersion=", result.stdout)
            else:
                expected = "7.0.10" if requested == "latest" else requested
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                arguments = [line[4:].replace("\\", "/") for line in result.stdout.splitlines()
                             if line.startswith("ARG:")]
                self.assertEqual(arguments[0], expected)
                self.assertIn(f"-p:MdsPackageVersion={expected}", arguments)
                self.assertIn("-p:ReferenceType=Package", arguments)
                config_path = (Path(temp) / "NuGet.config").as_posix()
                self.assertIn(f"-p:RestoreConfigFile={config_path}", arguments)
                self.assertNotIn("-p:MdsPackageVersion=latest", arguments)

    def test_bash_forwards_resolved_version(self):
        self.check_runner("bash", "latest")

    def test_powershell_forwards_resolved_version(self):
        self.check_runner("pwsh", "latest")

    def test_bash_preserves_explicit_version(self):
        self.check_runner("bash", "6.1.7")

    def test_powershell_preserves_explicit_version(self):
        self.check_runner("pwsh", "6.1.7")

    def test_bash_stops_on_resolution_failure(self):
        self.check_runner("bash", "latest", failure=True)

    def test_powershell_stops_on_resolution_failure(self):
        self.check_runner("pwsh", "latest", failure=True)

    def test_pipeline_tags_resolved_version(self):
        # Arrange
        executable = os.environ.get("BASH") or shutil.which("bash")
        if not executable:
            self.skipTest("bash is unavailable")
        pipeline = (SCRIPTS.parent / "sqlclient-perf-pipeline.yml").read_text()
        start = pipeline.index("            labelFile=")
        end = pipeline.index("          displayName: 'Tag build with baseline version'", start)
        script = textwrap.dedent(pipeline[start:end])
        with tempfile.TemporaryDirectory() as temp:
            (Path(temp) / "baseline-label.txt").write_bytes(b"7.0.10\r\n")
            env = dict(os.environ, RESULTS_DIR=temp, BASELINE_VERSION="latest")

            # Act
            result = subprocess.run([executable, "-c", script], env=env,
                                    capture_output=True, text=True)

            # Assert
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            self.assertEqual(result.stdout, "##vso[build.addbuildtag]Baseline 7.0.10\n")


if __name__ == "__main__":
    unittest.main()
