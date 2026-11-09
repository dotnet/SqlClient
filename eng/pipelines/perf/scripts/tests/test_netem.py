"""Network-delay lifecycle tests, plus opt-in Linux tests in disposable network namespaces."""

import argparse
from contextlib import redirect_stderr
import io
import json
import os
from pathlib import Path
import shutil
import shlex
import socket
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import uuid

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import netem
import perf_to_kusto


class FakeNetwork(netem.NetworkDelay):
    """Model exclusive root-queue ownership and command failures without touching the host."""

    def __init__(self, root):
        super().__init__(root / "state.json", root / "network-latency.json")
        self.rows = [dict(kind="noqueue", root=True, handle="0:")]
        self.calls = []
        self.route = "lo"
        self.fail_child = False

    def command(self, *args, privileged=False):
        self.calls.append(args)
        if args[0] == "ip":
            return json.dumps([dict(dev=self.route)])
        if args[:5] == ("tc", "-s", "-j", "qdisc", "show"):
            return json.dumps(self.rows)
        if args[:5] == ("tc", "-s", "-j", "filter", "show"):
            return "[]"
        if args[:3] == ("tc", "qdisc", "add"):
            handle = args[args.index("handle") + 1]
            if "root" in args:
                self.rows = [dict(kind="prio", root=True, handle=handle, drops=0)]
            else:
                if self.fail_child:
                    raise RuntimeError("Injected netem installation failure")
                self.rows.append(dict(kind="netem", handle=handle, packets=100, drops=0))
        if args[:3] == ("tc", "qdisc", "del"):
            self.rows = [dict(kind="noqueue", root=True, handle="0:")]
        return ""


class NetemTests(unittest.TestCase):
    """Verify opt-in behavior, safe ownership, partial failure cleanup and typed provenance."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.network = FakeNetwork(self.root)
        addresses = [(socket.AF_INET, socket.SOCK_STREAM, 0, "", ("127.0.0.1", 1433)),
                     (socket.AF_INET6, socket.SOCK_STREAM, 0, "", ("::1", 1433, 0, 0))]
        resolver = patch.object(netem.socket, "getaddrinfo", return_value=addresses)
        resolver.start()
        self.addCleanup(resolver.stop)

    def test_zero_delay_does_not_inspect_or_modify_network(self):
        """Default runs need neither traffic-control tooling nor privilege."""
        # Act
        self.network.setup(0, "localhost")
        self.network.cleanup()

        # Assert
        self.assertEqual(self.network.calls, [])
        self.assertEqual(perf_to_kusto._load_network_config(self.network.output_path), {
            "NetworkLatencyMilliseconds": 0, "NetworkLatencyScope": "Disabled"})

    def test_invalid_delay_is_rejected_without_rounding(self):
        """Negative, fractional and nonnumeric queue-time values cannot silently change meaning."""
        # Act / Assert
        for value in ("-1", "0.5", "nan", "", "1e2"):
            with self.subTest(value=value), self.assertRaises(argparse.ArgumentTypeError):
                netem.delay_milliseconds(value)
        self.assertEqual(netem.delay_milliseconds("025"), 25)

    def test_filters_cover_sql_both_directions_and_address_families(self):
        """Only SQL TCP traffic to resolved local endpoints enters the delayed band."""
        # Act
        self.network.setup(25, "localhost")

        # Assert
        filters = [args for args in self.network.calls if args[:3] == ("tc", "filter", "add")]
        self.assertEqual(len(filters), 4)
        for address, protocol in (("127.0.0.1", "ip"), ("::1", "ipv6")):
            for direction in ("src", "dst"):
                self.assertTrue(any(
                    address in args and protocol in args and
                    f"{direction}_ip" in args and f"{direction}_port" in args and
                    args[args.index(f"{direction}_port") + 1] == "1433" and
                    args[args.index("ip_proto") + 1] == "tcp" for args in filters))

    def test_existing_queue_is_not_overwritten_or_removed(self):
        """An existing queue causes failure without replacing somebody else's network rules."""
        # Arrange
        self.network.rows = [dict(kind="fq_codel", root=True, handle="123:")]

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "existing root queue"):
            self.network.setup(25, "localhost")
        self.network.cleanup()
        self.assertFalse(any(args[:3] in (("tc", "qdisc", "add"), ("tc", "qdisc", "del"))
                             for args in self.network.calls))

    def test_remote_endpoint_is_rejected_before_network_changes(self):
        """A remote endpoint cannot be reported as receiving bidirectional loopback delay."""
        # Arrange
        self.network.route = "eth0"

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "Remote SQL endpoints"):
            self.network.setup(25, "remote")
        self.assertFalse(any(args[0] == "tc" for args in self.network.calls))

    def test_partial_installation_is_removed(self):
        """Cleanup owns the parent even when child installation fails before Applied is saved."""
        # Arrange
        self.network.fail_child = True

        # Act
        with self.assertRaisesRegex(RuntimeError, "Injected"):
            self.network.setup(25, "localhost")
        self.network.cleanup()

        # Assert
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")
        self.assertEqual(json.loads(self.network.output_path.read_text())["Status"], "SetupFailed")

    def test_changed_queue_is_not_deleted(self):
        """Replacing the owned root externally fails verification but preserves the new owner."""
        # Arrange
        self.network.setup(25, "localhost")
        self.network.rows = [dict(kind="prio", root=True, handle="foreign:")]

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "disappeared or changed"):
            self.network.cleanup()
        self.assertFalse(any(args[:3] == ("tc", "qdisc", "del")
                             for args in self.network.calls))

    def test_cleanup_publishes_typed_latency_and_packet_statistics(self):
        """Successful cleanup preserves the applied condition and counters for Kusto ingestion."""
        # Act
        self.network.setup(25, "localhost")
        self.network.cleanup()

        # Assert
        data = json.loads(self.network.output_path.read_text())
        self.assertEqual(data["Status"], "Cleaned")
        self.assertEqual(data["After"][1]["packets"], 100)
        self.assertEqual(perf_to_kusto._load_network_config(self.network.output_path), {
            "NetworkLatencyMilliseconds": 25, "NetworkLatencyScope": "SqlLoopbackTcp1433"})

    def test_packet_drops_fail_after_removing_the_queue(self):
        """An overflowing delay queue cannot masquerade as a latency-only run."""
        # Arrange
        self.network.setup(25, "localhost")
        self.network.rows[1]["drops"] = 1

        # Act
        with patch.object(netem, "NetworkDelay", return_value=self.network), \
                redirect_stderr(io.StringIO()) as errors:
            result = netem.main(["stop", "--state", str(self.network.state_path),
                                 "--output", str(self.network.output_path)])

        # Assert
        self.assertEqual(result, 1)
        self.assertIn("dropped packets", errors.getvalue())
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")
        with self.assertRaisesRegex(ValueError, "successfully applied"):
            perf_to_kusto._load_network_config(self.network.output_path)

    def test_failed_or_unfinished_metadata_is_not_ingested(self):
        """Translation must not infer that a requested condition was actually maintained."""
        # Act
        self.network.setup(25, "localhost")

        # Assert
        with self.assertRaisesRegex(ValueError, "successfully applied"):
            perf_to_kusto._load_network_config(self.network.output_path)

    def test_translation_records_environment_without_changing_benchmark_identity(self):
        """Latency provenance belongs to the run, not to the BDN parameter matrix."""
        # Arrange
        self.network.setup(25, "localhost")
        self.network.cleanup()
        (self.root / "Runner-report-full.json").write_text(json.dumps({
            "Benchmarks": [dict(Type="Runner", Method="Open", Statistics=dict(Mean=1000))]}))

        # Act
        perf_to_kusto.main([
            "--input-dir", str(self.root), "--out-run", str(self.root / "run.ndjson"),
            "--out-results", str(self.root / "benchmarks.ndjson"),
            "--pipeline-run-id", "123", "--commit-hash", "test",
            "--driver-name", "SqlClient", "--network-config", str(self.network.output_path)])

        # Assert
        run = json.loads((self.root / "run.ndjson").read_text())
        benchmark = json.loads((self.root / "benchmarks.ndjson").read_text())
        self.assertEqual(run["Config"]["NetworkLatencyMilliseconds"], 25)
        self.assertEqual(benchmark["MethodName"], "Open")
        self.assertNotIn("NetworkLatencyMilliseconds", benchmark)

    def test_linux_rejects_invalid_latency_before_build_or_database_setup(self):
        """The shell entrypoint cannot round fractional or accept negative delay values."""
        # Arrange
        executable = os.environ.get("BASH") or shutil.which("bash")
        if not executable:
            self.skipTest("bash unavailable")

        # Act / Assert
        for value in ("-1", "0.5"):
            result = subprocess.run(
                [executable, str(SCRIPTS / "run-perf-tests.sh"), "--network-latency-ms", value],
                capture_output=True, text=True, timeout=30)
            self.assertEqual(result.returncode, 2, result.stdout + result.stderr)
            self.assertIn("nonnegative integer", result.stderr)

    def test_every_pipeline_wires_default_zero_to_both_entrypoints(self):
        """Nightly, PR and switch experiments expose the same run-wide condition."""
        # Act / Assert
        for path in SCRIPTS.parent.glob("sqlclient-perf-*.yml"):
            with self.subTest(path=path.name):
                text = path.read_text()
                self.assertRegex(text, r"name: networkLatencyMilliseconds\s+"
                                 r"displayName: [^\n]+\s+type: number\s+default: 0")
                self.assertIn("--network-latency-ms ${{ parameters.networkLatencyMilliseconds }}",
                              text)
                self.assertIn("-NetworkLatencyMilliseconds "
                              "${{ parameters.networkLatencyMilliseconds }}", text)
                self.assertIn("$(PerfArgsNetwork)'", text)

    def test_windows_rejects_nonzero_before_build_or_database_setup(self):
        """The Windows entrypoint fails explicitly instead of silently ignoring Linux latency."""
        # Arrange
        executable = shutil.which("pwsh")
        if not executable:
            self.skipTest("pwsh unavailable")

        # Act
        result = subprocess.run(
            [executable, "-NoProfile", "-File", str(SCRIPTS / "run-perf-tests.ps1"),
             "-NetworkLatencyMilliseconds", "25"], capture_output=True, text=True, timeout=30)

        # Assert
        self.assertNotEqual(result.returncode, 0)
        self.assertIn("Linux-only", result.stdout + result.stderr)


@unittest.skipUnless(sys.platform == "linux" and os.environ.get("PERF_NETEM_TESTS") == "1",
                     "Set PERF_NETEM_TESTS=1 on Linux with root/CAP_NET_ADMIN")
class NetemLinuxTests(unittest.TestCase):
    """Measure actual TCP delay and isolation without touching the host's network namespace."""

    def check_exit_cleanup(self, ending, expected_status):
        """Exercise the actual runner's traps on an isolated interface."""
        name = "perf-netem-" + uuid.uuid4().hex[:12]
        subprocess.run(["ip", "netns", "add", name], check=True)
        try:
            subprocess.run(["ip", "-n", name, "link", "set", "lo", "up"], check=True)
            with tempfile.TemporaryDirectory() as temp:
                root = Path(temp)
                text = (SCRIPTS / "run-perf-tests.sh").read_text()
                section = text[text.index('NETEM_STATE="$(mktemp'):
                               text.index("# NuGet.config on the VM")]
                script = root / "runner.sh"
                script.write_text(
                    f"SCRIPT_DIR={shlex.quote(str(SCRIPTS))}\n"
                    f"DIAG_DIR={shlex.quote(temp)}\nRESULTS_DIR={shlex.quote(temp)}\n"
                    "SQL_SERVER=localhost\nnetworkLatencyMilliseconds=1\n"
                    + section + f"\n{ending}\n")

                result = subprocess.run(
                    ["ip", "netns", "exec", name, "bash", str(script)],
                    capture_output=True, text=True, timeout=30)
                self.assertEqual(result.returncode, expected_status, result.stdout + result.stderr)
                self.assertEqual(json.loads((root / "network-latency.json").read_text())["Status"],
                                 "Cleaned")
                self.assertEqual(list(root.glob("netem-state.*")), [])
                queues = subprocess.check_output(
                    ["ip", "netns", "exec", name, "tc", "-j", "qdisc", "show", "dev", "lo"],
                    text=True)
                self.assertTrue(all(row["kind"] == "noqueue" for row in json.loads(queues)))
        finally:
            subprocess.run(["ip", "netns", "del", name], check=True)

    def test_runner_failure_removes_delay_and_preserves_exit_code(self):
        """A failed benchmark must clean up its queue without turning the run green."""
        # Act / Assert
        self.check_exit_cleanup("exit 23", 23)

    def test_runner_sigterm_removes_delay_and_preserves_cancellation(self):
        """Normal cancellation runs the same cleanup path as a failed benchmark."""
        # Act / Assert
        self.check_exit_cleanup("kill -TERM $$", 143)

    def test_tcp_delay_filters_and_cleanup(self):
        """IPv4/IPv6 SQL echo is delayed both ways; another port and post-cleanup SQL are not."""
        # Arrange
        name = "perf-netem-" + uuid.uuid4().hex[:12]
        subprocess.run(["ip", "netns", "add", name], check=True)
        try:
            subprocess.run(["ip", "-n", name, "link", "set", "lo", "up"], check=True)
            code = r'''
import json, socket, statistics, sys, tempfile, threading, time
from pathlib import Path
sys.path.insert(0, sys.argv[1])
import netem

def measure(family, port):
    address = "::1" if family == socket.AF_INET6 else "127.0.0.1"
    with socket.socket(family) as listener:
        listener.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        listener.bind((address, port))
        listener.listen()
        listener.settimeout(5)
        failures = []
        def echo():
            try:
                with listener.accept()[0] as peer:
                    peer.settimeout(5)
                    for _ in range(5):
                        peer.sendall(peer.recv(1))
            except Exception as error:
                failures.append(error)
        worker = threading.Thread(target=echo)
        worker.start()
        times = []
        try:
            with socket.socket(family) as client:
                client.settimeout(5)
                client.connect((address, port))
                for _ in range(5):
                    start = time.perf_counter()
                    client.sendall(b"x")
                    assert client.recv(1) == b"x"
                    times.append((time.perf_counter() - start) * 1000)
        finally:
            worker.join(6)
        assert not worker.is_alive() and not failures, failures
        return statistics.median(times)

with tempfile.TemporaryDirectory() as temp:
    root = Path(temp)
    controller = netem.NetworkDelay(root / "state.json", root / "network.json")
    for family, server in ((socket.AF_INET, "127.0.0.1"), (socket.AF_INET6, "::1")):
        before = measure(family, 1433)
        try:
            controller.setup(30, server)
            delayed = measure(family, 1433)
            other = measure(family, 1434)
        finally:
            controller.cleanup()
        after = measure(family, 1433)
        assert 50 <= delayed <= 250, delayed
        assert max(before, other, after) < delayed / 2, (before, other, after)
        assert json.loads((root / "network.json").read_text())["Status"] == "Cleaned"
        assert all(row["kind"] == "noqueue" for row in controller.queues())
        print(json.dumps(dict(server=server, before_ms=before, sql_ms=delayed,
                              other_port_ms=other, after_ms=after)))
'''
            # Act
            result = subprocess.run(
                ["ip", "netns", "exec", name, sys.executable, "-B", "-c", code, str(SCRIPTS)],
                capture_output=True, text=True, timeout=60)

            # Assert
            self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
            print(result.stdout)
        finally:
            # Cleanup
            subprocess.run(["ip", "netns", "del", name], check=True)


if __name__ == "__main__":
    unittest.main()
