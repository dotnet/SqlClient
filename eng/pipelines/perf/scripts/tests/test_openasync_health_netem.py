"""Network ownership/cleanup tests; these never modify the host's actual traffic control."""

import json
from pathlib import Path
import socket
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import netem


class FakeNetwork(netem.NetworkDelay):
    """Model exclusive root ownership and partial setup failure."""

    def __init__(self, root):
        super().__init__(root / "state.json", root / "network.json")
        self.rows = [dict(kind="noqueue", root=True, handle="0:")]
        self.calls = []
        self.fail_child = False
        self.route = "lo"

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
                    raise RuntimeError("child installation failed")
                self.rows.append(dict(kind="netem", handle=handle, packets=100, drops=0))
        if args[:3] == ("tc", "qdisc", "del"):
            self.rows = [dict(kind="noqueue", root=True, handle="0:")]
        return ""


class NetworkTests(unittest.TestCase):
    """Preserve foreign rules and reject unmeasured or packet-dropping RTT evidence."""

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.network = FakeNetwork(Path(self.temp.name))
        resolve = patch.object(netem.socket, "getaddrinfo", return_value=[
            (socket.AF_INET, socket.SOCK_STREAM, 0, "", ("127.0.0.1", 1433))])
        resolve.start()
        self.addCleanup(resolve.stop)

    def test_partial_setup_is_removed(self):
        self.network.fail_child = True
        with self.assertRaisesRegex(RuntimeError, "child"):
            self.network.setup(50, "localhost")
        self.network.cleanup()
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")
        self.assertEqual(json.loads(self.network.output_path.read_text())["Status"], "SetupFailed")

    def test_existing_queue_is_not_changed(self):
        self.network.rows = [dict(kind="fq", root=True, handle="123:")]
        with self.assertRaisesRegex(RuntimeError, "existing"):
            self.network.setup(50, "localhost")
        self.network.cleanup()
        self.assertFalse(any(call[:3] in (("tc", "qdisc", "add"), ("tc", "qdisc", "del"))
                             for call in self.network.calls))

    def test_foreign_replacement_is_not_deleted(self):
        self.network.setup(50, "localhost")
        self.network.rows = [dict(kind="prio", root=True, handle="foreign:")]
        with self.assertRaisesRegex(RuntimeError, "changed"):
            self.network.cleanup()
        self.assertFalse(any(call[:3] == ("tc", "qdisc", "del") for call in self.network.calls))

    def test_only_sql_both_directions_is_shaped(self):
        self.network.setup(50, "localhost")
        filters = [call for call in self.network.calls if call[:3] == ("tc", "filter", "add")]
        self.assertEqual(len(filters), 2)
        self.assertIn("dst_port", filters[0])
        self.assertIn("src_port", filters[1])
        for call in filters:
            self.assertIn("1433", call)
            self.assertIn("tcp", call)
        self.network.cleanup()
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")

    def test_remote_endpoint_is_rejected(self):
        self.network.route = "eth0"
        with self.assertRaisesRegex(RuntimeError, "loopback"):
            self.network.setup(50, "remote")
        self.assertFalse(any(call[0] == "tc" for call in self.network.calls))

    def test_drops_fail_after_queue_removal(self):
        self.network.setup(50, "localhost")
        self.network.rows[1]["drops"] = 1
        with self.assertRaisesRegex(RuntimeError, "dropped"):
            self.network.cleanup()
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")

    def test_zero_packets_is_invalid_after_cleanup(self):
        self.network.setup(50, "localhost")
        self.network.rows[1]["packets"] = 0
        with self.assertRaisesRegex(RuntimeError, "No SQL packets"):
            self.network.cleanup()
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")

    def test_interrupt_after_root_creation_still_removes_owned_queue(self):
        command = self.network.command

        def interrupted(*args, **kwargs):
            result = command(*args, **kwargs)
            if args[:3] == ("tc", "qdisc", "add") and "root" in args:
                raise InterruptedError()
            return result

        with patch.object(self.network, "command", side_effect=interrupted):
            with self.assertRaises(InterruptedError):
                self.network.setup(50, "localhost")
            self.network.cleanup()
        self.assertEqual(self.network.rows[0]["kind"], "noqueue")


if __name__ == "__main__":
    unittest.main()
