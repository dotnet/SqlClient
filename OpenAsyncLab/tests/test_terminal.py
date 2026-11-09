"""Drive the real Linux TUI in a PTY without touching the host's network or SQL instance."""

import json
import os
from pathlib import Path
import select
import shutil
import subprocess
import sys
import time
import unittest
import uuid


ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / "OpenAsyncLab/src/bin/Release/net10.0/OpenAsyncLab.dll"


@unittest.skipUnless(
    sys.platform == "linux" and os.environ.get("PERF_NETEM_TESTS") == "1",
    "Set PERF_NETEM_TESTS=1 in Linux with root/CAP_NET_ADMIN")
class TerminalTests(unittest.TestCase):
    """Keyboard controls and shutdown must work with actual netem and real OpenAsync attempts."""

    def test_live_controls_and_sigterm_cleanup(self):
        """A private namespace permits live updates and SIGTERM cleanup without a SQL credential."""
        import fcntl
        import pty
        import struct
        import termios

        # Arrange: no SQL server exists in this namespace; opens must report errors, not succeed.
        self.assertTrue(APP.exists(), "Build the Release tool before running terminal checks.")
        dotnet = shutil.which("dotnet")
        self.assertIsNotNone(dotnet, "Place the .NET 10 host on PATH.")
        name = "openasync-lab-" + uuid.uuid4().hex[:12]
        prefix = ["ip", "netns", "exec", name]
        subprocess.run(["ip", "netns", "add", name], check=True)
        master, slave = pty.openpty()
        process = None
        output = ""
        try:
            subprocess.run(["ip", "-n", name, "link", "set", "lo", "up"], check=True)
            fcntl.ioctl(slave, termios.TIOCSWINSZ, struct.pack("HHHH", 40, 120, 0, 0))
            env = dict(os.environ, TERM="xterm-256color",
                       SQLCLIENT_TUI_CONNECTION_STRING=(
                           "Server=tcp:127.0.0.1,1433;Integrated Security=true;"
                           "Connect Timeout=1;Encrypt=true;TrustServerCertificate=true"))
            process = subprocess.Popen(
                [*prefix, dotnet, str(APP), "--netem", "--pool-v2"],
                stdin=slave, stdout=slave, stderr=slave, env=env)
            os.close(slave)
            slave = None

            def wait_for(text, timeout=15):
                nonlocal output
                deadline = time.monotonic() + timeout
                while time.monotonic() < deadline:
                    if text in output:
                        # Preserve the unread tail: incremental redraw does not repeat unchanged rows.
                        output = output.split(text, 1)[1]
                        return
                    if select.select([master], [], [], 0.1)[0]:
                        try:
                            output += os.read(master, 65536).decode("utf-8", errors="replace")
                        except OSError:
                            break
                    if process.poll() is not None and text not in output:
                        break
                self.fail(f"Did not observe {text!r}. Terminal tail:\n{output[-8000:]}")

            # Act: start, edit workers, and apply 150ms per direction through real keyboard input.
            wait_for("PAUSED")
            os.write(master, b" ")
            wait_for("RUNNING")
            os.write(master, b"l")
            wait_for("V2 dispatch=LongRunning")
            os.write(master, b"l")
            wait_for("V2 dispatch=ThreadPool")
            os.write(master, b"\r2\r")
            wait_for("Workers=2")
            os.write(master, b"\x1b[B\x1b[B\x1b[B\x1b[B\r150\r")
            wait_for("Delay=150 ms/direction")
            wait_for("Last error: SQL error", timeout=20)

            # Assert: kernel shaping is actually installed, and pause does not remove it.
            queues = json.loads(subprocess.check_output(
                [*prefix, "tc", "-j", "qdisc", "show", "dev", "lo"]))
            self.assertTrue(any(q["kind"] == "netem" for q in queues))
            os.write(master, b" ")
            wait_for("PAUSED")
            os.write(master, b"w")
            wait_for("Workload=Open/query/close (SELECT 1)")
            os.write(master, b"q")
            wait_for("Workload=Open/close")

            # Cleanup: SIGTERM follows the app's normal owned-queue cleanup path.
            process.terminate()
            deadline = time.monotonic() + 20
            while process.poll() is None and time.monotonic() < deadline:
                if select.select([master], [], [], 0.1)[0]:
                    try:
                        os.read(master, 65536)
                    except OSError:
                        break
            self.assertEqual(process.wait(timeout=5), 0)
            queues = json.loads(subprocess.check_output(
                [*prefix, "tc", "-j", "qdisc", "show", "dev", "lo"]))
            self.assertFalse(any(q["kind"] in ("prio", "netem") for q in queues))
        finally:
            if process is not None and process.poll() is None:
                process.kill()
                process.wait(timeout=5)
            os.close(master)
            if slave is not None:
                os.close(slave)
            subprocess.run(["ip", "netns", "del", name], check=True)


if __name__ == "__main__":
    unittest.main()
