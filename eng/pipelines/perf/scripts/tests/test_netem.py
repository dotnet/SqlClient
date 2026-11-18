"""Verify SQL-only owned shaping and fail-closed cleanup without modifying host networking."""

import copy
import json
from pathlib import Path
import socket
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import netem


class FakeNetwork(netem.NetworkDelay):
    """Model tc mutations and counters while exercising the production ownership logic."""

    def __init__(self, root):
        super().__init__(root / "state.json", root / "network.json")
        self.qdiscs = [{"kind": "noqueue", "root": True}]
        self.calls = []
        self.route = "lo"
        self.fail_filter = False

    def queues(self):
        return copy.deepcopy(self.qdiscs)

    def command(self, *args, privileged=False):
        self.calls.append(args)
        if args[0] == "ip":
            return json.dumps([{"dev": self.route}])
        if args[:3] == ("tc", "qdisc", "add"):
            handle = args[args.index("handle") + 1]
            if "root" in args:
                self.qdiscs = [{"kind": "prio", "root": True, "handle": handle}]
            else:
                self.qdiscs.append(dict(kind="netem", handle=handle, packets=1, drops=0,
                                        options={"delay": {
                                            "delay": int(args[args.index("delay") + 1][:-2]) / 1e6,
                                            "jitter": 0, "correlation": 0}}))
        if args[:3] == ("tc", "filter", "add") and self.fail_filter:
            raise RuntimeError("filter setup failed")
        if args[:3] == ("tc", "qdisc", "del"):
            self.qdiscs = [{"kind": "noqueue", "root": True}]
        return "[]"


class NetemTests(unittest.TestCase):
    """Requested RTT is scoped, observable and never obtained by deleting foreign rules."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.network = FakeNetwork(self.root)
        dns = patch.object(socket, "getaddrinfo", return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("127.0.0.1", 1433))])
        dns.start()
        self.addCleanup(dns.stop)

    def test_round_trip_delay_is_split_and_both_sql_directions_are_filtered(self):
        """Odd RTT milliseconds remain exact through microsecond half-delays."""
        # Act
        self.network.setup(101, "localhost", 1434)

        # Assert
        calls = self.network.calls
        self.assertTrue(any("50500us" in call for call in calls))
        filters = [call for call in calls if call[:3] == ("tc", "filter", "add")]
        self.assertEqual(len(filters), 2)
        self.assertTrue(any("dst_port" in call and "1434" in call for call in filters))
        self.assertTrue(any("src_port" in call and "1434" in call for call in filters))
        evidence = json.loads(self.network.output_path.read_text())
        self.assertEqual(evidence["AdditionalRttMilliseconds"], 101)
        self.assertTrue(evidence["Applied"])

    def test_foreign_queue_is_never_replaced_or_deleted(self):
        """An existing qdisc is a conflict, not permission to reset a shared interface."""
        # Arrange
        self.network.qdiscs = [{"kind": "fq_codel", "root": True, "handle": "abcd:"}]

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "existing root"):
            self.network.setup(100, "localhost", 1433)
        self.network.cleanup(require_traffic=False)
        self.assertEqual(self.network.qdiscs[0]["kind"], "fq_codel")
        self.assertFalse(any(call[:3] == ("tc", "qdisc", "del") for call in self.network.calls))

    def test_remote_endpoint_fails_before_queue_mutation(self):
        """The initial implementation must not silently shape the wrong interface."""
        # Arrange
        self.network.route = "eth0"

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "loopback"):
            self.network.setup(100, "server", 1433)
        self.assertFalse(any(call[:3] == ("tc", "qdisc", "add") for call in self.network.calls))

    def test_partial_setup_can_be_cleaned_without_traffic(self):
        """Failure between root installation and filter completion must leave no owned queue."""
        # Arrange
        self.network.fail_filter = True

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "filter setup"):
            self.network.setup(100, "localhost", 1433)
        self.network.cleanup(require_traffic=False)
        self.assertEqual(self.network.qdiscs, [{"kind": "noqueue", "root": True}])
        self.assertTrue(json.loads(self.network.state_path.read_text())["CleanupSucceeded"])

    def test_missing_traffic_fails_after_cleanup(self):
        """An applied rule without matched SQL traffic cannot masquerade as a shaped benchmark."""
        # Arrange
        self.network.setup(100, "localhost", 1433)
        self.network.qdiscs[1]["packets"] = 0

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "No SQL packets"):
            self.network.cleanup()
        self.assertEqual(self.network.qdiscs[0]["kind"], "noqueue")
        self.assertEqual(json.loads(self.network.output_path.read_text())["Status"], "Failed")

    def test_packet_loss_fails_after_cleanup(self):
        """Latency-only results cannot silently include queue-induced packet loss."""
        # Arrange
        self.network.setup(100, "localhost", 1433)
        self.network.qdiscs[1]["drops"] = 1

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "dropped packets"):
            self.network.cleanup()
        self.assertEqual(self.network.qdiscs[0]["kind"], "noqueue")

    def test_changed_delay_fails_after_cleanup(self):
        """Effective conditions must remain identical through execution."""
        # Arrange
        self.network.setup(100, "localhost", 1433)
        self.network.qdiscs[1]["options"]["delay"]["delay"] = 0.09

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "settings changed"):
            self.network.cleanup()
        self.assertEqual(self.network.qdiscs[0]["kind"], "noqueue")

    def test_replaced_root_is_not_deleted(self):
        """A missing owned handle must not cause deletion of somebody else's replacement."""
        # Arrange
        self.network.setup(100, "localhost", 1433)
        self.network.qdiscs = [{"kind": "prio", "root": True, "handle": "ffff:"}]

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "disappeared or changed"):
            self.network.cleanup()
        self.assertEqual(self.network.qdiscs[0]["handle"], "ffff:")

    def test_unrecovered_state_cannot_be_overwritten(self):
        """A second setup with the same state file cannot erase ownership of an active run."""
        # Arrange
        self.network.setup(100, "localhost", 1433)
        original = self.network.state_path.read_bytes()
        other = FakeNetwork(self.root)

        # Act / Assert
        with self.assertRaisesRegex(RuntimeError, "Unrecovered"):
            other.setup(100, "localhost", 1433)
        self.assertFalse(other.attempted_setup)
        self.assertEqual(self.network.state_path.read_bytes(), original)


if __name__ == "__main__":
    unittest.main()
