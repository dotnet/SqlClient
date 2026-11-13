"""Opt-in smoke of the real built .NET harness against its credential-free stalled endpoint."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import openasync_health as health


@unittest.skipUnless(os.environ.get("OPENASYNC_HEALTH_BINARY"),
                     "Set OPENASYNC_HEALTH_BINARY to a built OpenAsyncHealth.dll")
class HarnessProcessTests(unittest.TestCase):
    """Prove real CLI dispatch, cancellation, timeout and unknown-setting rejection."""

    def run_harness(self, **overrides):
        """Run one fresh process without credentials and return its raw result and exit code."""
        settings = dict(Workers=4, PoolSize=4, DurationSeconds=1, ConnectTimeoutSeconds=15,
                        StalledPrelogin=True, Query=False)
        settings.update(overrides)
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            case, result = root / "case.json", root / "result.json"
            case.write_text(json.dumps(settings))
            env = dict(os.environ)
            env.pop("SQLCLIENT_HEALTH_CONNECTION_STRING", None)
            process = subprocess.run(
                ["dotnet", os.environ["OPENASYNC_HEALTH_BINARY"], "--openasync-health",
                 str(case), str(result)], capture_output=True, text=True, env=env, timeout=30)
            data = json.loads(result.read_text()) if result.exists() else None
        return process, data

    def test_cancellation_preserves_every_first_worker(self):
        process, data = self.run_harness()
        self.assertEqual(process.returncode, 0, process.stdout + process.stderr)
        self.assertEqual(data["Source"], "SyntheticStalledPrelogin")
        self.assertEqual(data["Status"], "Completed")
        self.assertEqual(data["Final"]["Completed"], 0)
        self.assertEqual(data["Final"]["Cancelled"], 4)
        self.assertEqual(data["Final"]["InFlight"], 0)
        self.assertEqual({o["Worker"] for o in data["Observations"]}, set(range(4)))
        self.assertTrue(all(o["OpenEndMs"] is None for o in data["Observations"]))
        self.assertGreater(len(data["Measurement"]["Probes"]["Samples"]), 0)

    def test_login_timeout_is_not_a_successful_open(self):
        process, data = self.run_harness(DurationSeconds=3, ConnectTimeoutSeconds=1)
        self.assertEqual(process.returncode, 0, process.stdout + process.stderr)
        self.assertGreater(data["Final"]["Failed"], 0)
        self.assertEqual(data["Final"]["Completed"], 0)
        self.assertTrue(data["Errors"])

    @unittest.skipUnless(sys.platform == "win32", "Native SNI requires Windows")
    def test_native_sni_cancellation_drains_without_successful_work(self):
        process, data = self.run_harness(UseManagedSni=False)
        self.assertEqual(process.returncode, 0, process.stdout + process.stderr)
        self.assertEqual(data["Status"], "Completed")
        self.assertFalse(data["Settings"]["UseManagedSni"])
        self.assertEqual(data["Final"]["Completed"], 0)
        self.assertEqual(data["Final"]["Cancelled"], 4)

    def test_experimental_toggle_is_rejected_not_ignored(self):
        process, data = self.run_harness(LongRunning=True)
        self.assertEqual(process.returncode, 2)
        self.assertIsNone(data)
        self.assertIn("JsonException", process.stderr)

    def test_v2_is_verified_or_explicitly_unsupported(self):
        process, data = self.run_harness(PoolV2=True)
        self.assertEqual(process.returncode, 0, process.stdout + process.stderr)
        self.assertEqual(len(data["DriverSha256"]), 64)
        if data["Status"] == "Unsupported":
            self.assertEqual(data["Reason"], "PoolV2Unavailable")
        else:
            self.assertEqual(data["Status"], "Completed")
            self.assertTrue(data["SupportsPoolV2"])
            self.assertTrue(data["Settings"]["PoolV2"])


if __name__ == "__main__":
    unittest.main()
