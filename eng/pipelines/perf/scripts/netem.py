#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Add round-trip latency to SQL TCP traffic on Linux loopback.

The benchmark runner calls ``start`` before launching the benchmark and ``stop`` afterward
with the same ``--state`` file. This file tracks queue ownership for cleanup, not reporting.
If the runner is killed, use ``python3 netem.py stop --state <path>`` in the same network namespace.

Shaping requires iproute2 and permission to modify traffic control settings (root or
noninteractive sudo). Use a dedicated test host: the filter matches an address and port,
not a process, so any other client hitting the same endpoint also sees the delay.
"""

import argparse
from contextlib import contextmanager
import ipaddress
import json
import math
import os
from pathlib import Path
import re
import signal
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import uuid


def nonnegative(value):
    """Parse an RTT in whole milliseconds, raising ArgumentTypeError for invalid input."""
    if not re.fullmatch(r"[0-9]+", str(value)):
        raise argparse.ArgumentTypeError("RTT must be nonnegative integer milliseconds")
    return int(value)


def write_json(path, data):
    """Replace ownership state atomically so an interrupted write leaves a readable record."""
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


@contextmanager
def locked():
    """Serialize queue inspection and changes, not the benchmark itself.

    Between ``start`` and ``stop``, the installed root prevents a second run from taking over.
    """
    if sys.platform != "linux":
        raise RuntimeError("SQL-only netem requires Linux")
    import fcntl
    lock = Path(tempfile.gettempdir()) / "sqlclient-perf-netem-lo.lock"
    descriptor = os.open(lock, os.O_CREAT | os.O_RDWR | os.O_NOFOLLOW, 0o600)
    try:
        fcntl.flock(descriptor, fcntl.LOCK_EX | fcntl.LOCK_NB)
        yield
    finally:
        os.close(descriptor)


class NetworkDelay:
    """Install, verify, and remove only the queues owned by this run."""

    def __init__(self, state_path):
        """Use state_path (a pathlib.Path) to share ownership between start and stop."""
        self.state_path = state_path
        self.attempted_setup = False

    def command(self, *args, privileged=False):
        """Run an iproute2 command and return its stdout as text.

        The first argument is the executable name. Set privileged for queue mutations;
        non-root users then run the command through noninteractive sudo. Missing tools,
        nonzero exit codes, and timeouts are propagated to the caller.
        """
        prefix = ["sudo", "-n"] if privileged and os.geteuid() != 0 else []
        executable = shutil.which(args[0])
        if executable is None and args[0] in ("tc", "ip"):
            candidate = Path("/usr/sbin") / args[0]
            executable = str(candidate) if candidate.is_file() else None
        if executable is None:
            raise RuntimeError(f"Required networking tool is unavailable: {args[0]}")
        return subprocess.run([*prefix, executable, *args[1:]], check=True,
                              text=True, capture_output=True,
                              timeout=10).stdout

    def queues(self):
        """Return the parsed tc queue records for lo, including packet and drop counts."""
        # Show queueing disciplines on the loopback device (lo), with statistics (-s)
        # in JSON (-j) so the caller can check queue length, traffic, and drops.
        return json.loads(self.command("tc", "-s", "-j", "qdisc", "show", "dev", "lo"))

    def setup(self, rtt, server, port):
        """Install and verify added RTT in milliseconds for server and TCP port.

        A zero RTT leaves queues unchanged. For a positive RTT, every address resolved
        from server must route through lo. Refuse an existing root queue or unrecovered state.
        Call with the loopback lock held.
        """
        if self.state_path.exists():
            previous = json.loads(self.state_path.read_text(encoding="utf-8"))
            if previous.get("RootHandle") and not previous.get("CleanupSucceeded"):
                raise RuntimeError("Unrecovered netem state exists; stop that owned run first")
        if rtt == 0:
            return

        addresses = sorted({str(ipaddress.ip_address(info[4][0]))
                            for info in socket.getaddrinfo(server, port,
                                                          type=socket.SOCK_STREAM)})
        if not addresses:
            raise RuntimeError("SQL endpoint did not resolve")
        for address in addresses:
            family = "-4" if ipaddress.ip_address(address).version == 4 else "-6"
            routes = json.loads(self.command("ip", family, "-j", "route", "get", address))
            if not routes or any(route.get("dev") != "lo" for route in routes):
                raise RuntimeError("Netem supports only local loopback SQL routes")

        before = self.queues()
        if any(queue.get("root") and queue["kind"] != "noqueue" for queue in before):
            raise RuntimeError("Refusing to overwrite an existing root queue on lo")

        number = uuid.uuid4().int % 0x7ffe + 1
        data = dict(AdditionalRttMilliseconds=rtt, RootHandle=f"{number:x}:",
                    NetemHandle=f"{number + 0x8000:x}:", Applied=False, CleanupSucceeded=False)
        self.attempted_setup = True
        # Record the handles before touching lo so cleanup can identify a partial install.
        write_json(self.state_path, data)

        # Add a three-band priority queue at the root of lo, using the saved handle for cleanup.
        # The 16 priomap entries send every default packet priority to the first band (index 0).
        self.command("tc", "qdisc", "add", "dev", "lo", "root", "handle", data["RootHandle"],
                     "prio", "bands", "3", "priomap", *(["0"] * 16), privileged=True)

        # Attach a separately identified netem queue to the root's second band (class :2).
        # Delay each direction by half the RTT (500 us per RTT millisecond); cap the queue
        # at 10,000 packets.
        self.command("tc", "qdisc", "add", "dev", "lo", "parent", f"{number:x}:2",
                     "handle", data["NetemHandle"], "netem", "delay", f"{rtt * 500}us",
                     "limit", "10000", privileged=True)

        for index, address in enumerate(addresses):
            protocol = "ip" if ipaddress.ip_address(address).version == 4 else "ipv6"
            for offset, direction in enumerate(("dst", "src")):
                # Attach a flower filter to the root queue with a distinct priority (pref)
                # for each address and direction. protocol selects IPv4 or IPv6; ip_proto
                # selects TCP. Match the SQL address and port as the destination on requests
                # or source on replies, and send matches to the delayed band (classid :2).
                self.command("tc", "filter", "add", "dev", "lo", "parent", data["RootHandle"],
                             "protocol", protocol, "pref", str(index * 2 + offset + 1),
                             "flower", "ip_proto", "tcp", f"{direction}_ip", address,
                             f"{direction}_port", str(port), "classid", f"{number:x}:2",
                             privileged=True)
        configured = self.queues()
        netem = [queue for queue in configured if queue.get("handle") == data["NetemHandle"]
                 and queue["kind"] == "netem"]
        if len(netem) != 1:
            raise RuntimeError("Netem queue was not installed")
        options = netem[0].get("options", {})
        delay = options.get("delay", {})
        # tc accepts the delay in microseconds above but reports it in seconds in JSON.
        if (not isinstance(delay, dict) or
            not math.isclose(delay.get("delay", -1), rtt / 2000, abs_tol=0.000001) or
            delay.get("jitter", 0) != 0 or delay.get("correlation", 0) != 0):
            raise RuntimeError("Applied netem delay differs from the requested latency-only profile")
        data.update(Applied=True, ConfiguredOptions=options)
        write_json(self.state_path, data)
        print(f"Netem applied: {rtt}ms added RTT for SQL TCP/{port} on lo.", flush=True)

    def cleanup(self, require_traffic=True):
        """Check that the owned queues drained, kept their settings, and dropped no packets.

        Set require_traffic to False when rolling back setup before any SQL traffic can flow.
        Report failed checks after attempting to remove the queues. With no ownership record
        or an already completed cleanup, return without touching lo. Call with the lock held.
        """
        if not self.state_path.exists():
            return
        data = json.loads(self.state_path.read_text(encoding="utf-8"))
        handle = data["RootHandle"]
        if handle is None or data.get("CleanupSucceeded"):
            return
        if not re.fullmatch(r"[0-9a-f]{1,4}:", handle):
            raise ValueError("Invalid owned netem queue handle")

        queues = self.queues()
        owned = any(queue.get("root") and queue.get("handle") == handle
                    and queue["kind"] == "prio" for queue in queues)
        if not owned:
            if data["Applied"]:
                raise RuntimeError("The owned netem root disappeared or changed")
            return

        problems = []
        try:
            deadline = time.monotonic() + min(10, max(1, data["AdditionalRttMilliseconds"] / 250))
            while any(queue.get("qlen", 0) for queue in queues
                      if queue.get("handle") in (handle, data["NetemHandle"])):
                if time.monotonic() >= deadline:
                    problems.append("Netem did not drain before cleanup")
                    break
                time.sleep(0.01)
                queues = self.queues()
            if data["Applied"]:
                netem = [queue for queue in queues if queue.get("handle") == data["NetemHandle"]
                         and queue["kind"] == "netem"]
                if len(netem) != 1 or netem[0].get("options") != data["ConfiguredOptions"]:
                    problems.append("Netem settings changed during execution")
                if any(queue.get("drops", 0) for queue in queues
                       if queue.get("handle") in (handle, data["NetemHandle"])):
                    problems.append("Netem dropped packets; this was not a latency-only run")
                if require_traffic and (not netem or not netem[0].get("packets", 0)):
                    problems.append("No SQL packets traversed netem")
        finally:
            # Delete the root queue on lo using the verified ownership handle. Its child
            # queue and filters are removed with it.
            self.command("tc", "qdisc", "del", "dev", "lo", "root", "handle", handle,
                         privileged=True)
            data["CleanupSucceeded"] = not any(
                queue.get("handle") == handle for queue in self.queues())
            write_json(self.state_path, data)
        if not data["CleanupSucceeded"]:
            problems.append("The owned netem queue was not removed")
        if problems:
            raise RuntimeError("; ".join(problems))
        print("Removed the owned netem queue.", flush=True)


def interrupted(signum, frame):
    """Raise InterruptedError so SIGINT or SIGTERM can unwind through setup rollback."""
    raise InterruptedError("Netem operation interrupted")


def main(argv=None):
    """Run start or stop using argv, or the process arguments when argv is None.

    Return 0 on success and 1 for a networking or state error. Argparse handles usage errors.
    """
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("start", "stop"))
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--rtt-ms", type=nonnegative, default=0)
    parser.add_argument("--server", default="localhost")
    parser.add_argument("--port", type=int, default=1433)
    args = parser.parse_args(argv)
    if not 1 <= args.port <= 65535:
        parser.error("port must be between 1 and 65535")
    controller = NetworkDelay(args.state)
    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    try:
        with locked():
            try:
                if args.action == "start":
                    controller.setup(args.rtt_ms, args.server, args.port)
                else:
                    controller.cleanup()
            except (OSError, ValueError, RuntimeError, subprocess.SubprocessError):
                if args.action == "start" and controller.attempted_setup:
                    # The benchmark has not started yet, so rollback cannot require traffic.
                    controller.cleanup(require_traffic=False)
                raise
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        detail = getattr(error, "stderr", None) or str(error)
        print(f"ERROR: netem {args.action}: {detail}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
