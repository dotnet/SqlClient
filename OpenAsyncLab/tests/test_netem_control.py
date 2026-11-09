"""Exercise the actual live netem bridge only inside disposable Linux network namespaces."""

import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import uuid


ROOT = Path(__file__).resolve().parents[2]
BRIDGE = ROOT / "OpenAsyncLab/netem_control.py"
SCRIPTS = ROOT / "eng/pipelines/perf/scripts"


@unittest.skipUnless(
    sys.platform == "linux" and os.environ.get("PERF_NETEM_TESTS") == "1",
    "Set PERF_NETEM_TESTS=1 in Linux with root/CAP_NET_ADMIN")
class NetworkControlTests(unittest.TestCase):
    """Live updates must preserve ownership and remove only the tool's own queue."""

    def test_live_zero_high_delay_zero_and_cleanup(self):
        """A reserved zero queue supports live 300ms RTT and returns to an unshaped interface."""
        name = "openasync-lab-" + uuid.uuid4().hex[:12]
        subprocess.run(["ip", "netns", "add", name], check=True)
        try:
            subprocess.run(["ip", "-n", name, "link", "set", "lo", "up"], check=True)
            with tempfile.TemporaryDirectory() as temporary:
                state = Path(temporary) / "state.json"
                env = dict(os.environ, PYTHONPATH=str(SCRIPTS))

                def bridge(action, delay=0, success=True):
                    result = subprocess.run(
                        ["ip", "netns", "exec", name, sys.executable, "-B", str(BRIDGE),
                         action, "--state", str(state), "--delay-ms", str(delay)],
                        env=env, text=True, capture_output=True, timeout=30)
                    self.assertEqual(result.returncode == 0, success, result.stdout + result.stderr)
                    return result

                bridge("set")
                original = json.loads(state.read_text())
                self.assertTrue(original["Applied"])
                self.assertEqual(original["NetworkLatencyMilliseconds"], 0)
                bridge("set", 150)
                changed = json.loads(state.read_text())
                self.assertEqual(changed["RootHandle"], original["RootHandle"])
                self.assertEqual(changed["NetworkLatencyMilliseconds"], 150)

                echo = """
import socket, threading, time
with socket.socket() as server:
    server.bind(('127.0.0.1',1433))
    server.listen()
    def serve():
        with server.accept()[0] as peer:
            peer.sendall(peer.recv(1))
    worker=threading.Thread(target=serve)
    worker.start()
    with socket.create_connection(('127.0.0.1',1433),timeout=5) as client:
        start=time.monotonic()
        client.sendall(b'x')
        assert client.recv(1)==b'x'
        print(time.monotonic()-start)
    worker.join()
"""
                result = subprocess.run(
                    ["ip", "netns", "exec", name, sys.executable, "-c", echo],
                    check=True, text=True, capture_output=True, timeout=10)
                elapsed = float(result.stdout)
                self.assertGreaterEqual(elapsed, 0.25)
                self.assertLess(elapsed, 2)
                print(f"Isolated live netem 300ms RTT: observed {elapsed * 1000:.1f} ms")

                bridge("set", 0)
                self.assertEqual(json.loads(state.read_text())["NetworkLatencyMilliseconds"], 0)

                # Removing the SQL filters must not be reported as a successful delay update.
                subprocess.run(
                    ["ip", "netns", "exec", name, "tc", "filter", "del", "dev", "lo",
                     "parent", original["RootHandle"]],
                    check=True, capture_output=True)
                rejected = bridge("set", 10, success=False)
                self.assertIn("SQL traffic filters changed", rejected.stdout)

                bridge("stop")
                queues = json.loads(subprocess.check_output(
                    ["ip", "netns", "exec", name, "tc", "-j", "qdisc", "show", "dev", "lo"]))
                self.assertFalse(any(q["kind"] in ("prio", "netem") for q in queues))
        finally:
            subprocess.run(["ip", "netns", "del", name], check=True)

    def test_foreign_root_survives_failed_acquisition_and_cleanup(self):
        """An existing rule is not replaced even when requesting zero delay."""
        name = "openasync-lab-" + uuid.uuid4().hex[:12]
        subprocess.run(["ip", "netns", "add", name], check=True)
        try:
            subprocess.run(["ip", "-n", name, "link", "set", "lo", "up"], check=True)
            prefix = ["ip", "netns", "exec", name]
            subprocess.run([*prefix, "tc", "qdisc", "add", "dev", "lo", "root",
                            "handle", "1234:", "netem", "delay", "1ms"], check=True)
            with tempfile.TemporaryDirectory() as temporary:
                args = ["--state", str(Path(temporary) / "state.json")]
                env = dict(os.environ, PYTHONPATH=str(SCRIPTS))
                result = subprocess.run(
                    [*prefix, sys.executable, "-B", str(BRIDGE), "set", *args],
                    env=env, text=True, capture_output=True, timeout=30)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("existing root queue", result.stdout)
                subprocess.run([*prefix, sys.executable, "-B", str(BRIDGE), "stop", *args],
                               env=env, check=True, capture_output=True, timeout=30)
                queues = json.loads(subprocess.check_output(
                    [*prefix, "tc", "-j", "qdisc", "show", "dev", "lo"]))
                self.assertTrue(any(q.get("handle") == "1234:" for q in queues))
        finally:
            subprocess.run(["ip", "netns", "del", name], check=True)


if __name__ == "__main__":
    unittest.main()
