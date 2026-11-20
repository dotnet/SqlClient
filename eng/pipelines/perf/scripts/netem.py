#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Own a latency-only SQL TCP loopback queue for one benchmark process."""

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
    if not re.fullmatch(r"[0-9]+", str(value)):
        raise argparse.ArgumentTypeError("RTT must be nonnegative integer milliseconds")
    return int(value)


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


@contextmanager
def locked():
    """Serialize inspection/mutation; an installed owned root reserves the run's network scope."""
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
    """Never replace a foreign queue; persist ownership before any mutation."""

    def __init__(self, state_path, output_path):
        self.state_path = Path(state_path)
        self.output_path = Path(output_path)
        self.attempted_setup = False

    def save(self, data):
        write_json(self.state_path, data)
        write_json(self.output_path, data)

    def command(self, *args, privileged=False):
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
        return json.loads(self.command("tc", "-s", "-j", "qdisc", "show", "dev", "lo"))

    def setup(self, rtt, server, port):
        if self.state_path.exists():
            previous = json.loads(self.state_path.read_text(encoding="utf-8"))
            if previous.get("RootHandle") and not previous.get("CleanupSucceeded"):
                raise RuntimeError("Unrecovered netem state exists; stop that owned run first")
        data = dict(
            SchemaVersion=1, AdditionalRttMilliseconds=rtt,
            DelayMicrosecondsPerDirection=rtt * 500, Interface="lo", TcpPort=port,
            QueueLimitPackets=10000, ServerAddresses=[], Applied=False,
            Status="Disabled" if rtt == 0 else "Preparing",
            RootHandle=None, NetemHandle=None, CleanupSucceeded=False)
        self.attempted_setup = True
        self.save(data)
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
        data.update(ServerAddresses=addresses, RootHandle=f"{number:x}:",
                    NetemHandle=f"{number + 0x8000:x}:", Before=before, Status="Installing")
        self.save(data)
        self.command("tc", "qdisc", "add", "dev", "lo", "root", "handle", data["RootHandle"],
                     "prio", "bands", "3", "priomap", *(["0"] * 16), privileged=True)
        self.command("tc", "qdisc", "add", "dev", "lo", "parent", f"{number:x}:2",
                     "handle", data["NetemHandle"], "netem", "delay", f"{rtt * 500}us",
                     "limit", str(data["QueueLimitPackets"]), privileged=True)
        for index, address in enumerate(addresses):
            protocol = "ip" if ipaddress.ip_address(address).version == 4 else "ipv6"
            for offset, direction in enumerate(("dst", "src")):
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
        if (not isinstance(delay, dict) or
            not math.isclose(delay.get("delay", -1), rtt / 2000, abs_tol=0.000001) or
            delay.get("jitter", 0) != 0 or delay.get("correlation", 0) != 0):
            raise RuntimeError("Applied netem delay differs from the requested latency-only profile")
        data.update(Status="Applied", Applied=True, Configured=configured)
        self.save(data)
        print(f"Netem applied: {rtt}ms added RTT for SQL TCP/{port} on lo.", flush=True)

    def cleanup(self, require_traffic=True):
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
            data["After"] = queues
            data["Filters"] = json.loads(self.command(
                "tc", "-s", "-j", "filter", "show", "dev", "lo", "parent", handle))
            if data["Applied"]:
                netem = [queue for queue in queues if queue.get("handle") == data["NetemHandle"]
                         and queue["kind"] == "netem"]
                configured = next(queue for queue in data["Configured"]
                                  if queue.get("handle") == data["NetemHandle"])
                if len(netem) != 1 or netem[0].get("options") != configured.get("options"):
                    problems.append("Netem settings changed during execution")
                if any(queue.get("drops", 0) for queue in queues
                       if queue.get("handle") in (handle, data["NetemHandle"])):
                    problems.append("Netem dropped packets; this was not a latency-only run")
                if require_traffic and (not netem or not netem[0].get("packets", 0)):
                    problems.append("No SQL packets traversed netem")
        finally:
            self.command("tc", "qdisc", "del", "dev", "lo", "root", "handle", handle,
                         privileged=True)
            data["AfterCleanup"] = self.queues()
            data["CleanupSucceeded"] = not any(
                queue.get("handle") == handle for queue in data["AfterCleanup"])
            data["Status"] = "Cleaned" if data["CleanupSucceeded"] and not problems else "Failed"
            self.save(data)
        if not data["CleanupSucceeded"]:
            problems.append("The owned netem queue was not removed")
        if problems:
            raise RuntimeError("; ".join(problems))
        print("Removed the owned netem queue.", flush=True)


def interrupted(signum, frame):
    raise InterruptedError("Netem operation interrupted")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("start", "stop"))
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--rtt-ms", type=nonnegative, default=0)
    parser.add_argument("--server", default="localhost")
    parser.add_argument("--port", type=int, default=1433)
    args = parser.parse_args(argv)
    if not 1 <= args.port <= 65535:
        parser.error("port must be between 1 and 65535")
    controller = NetworkDelay(args.state, args.output)
    signal.signal(signal.SIGTERM, interrupted)
    signal.signal(signal.SIGINT, interrupted)
    entered = False
    try:
        with locked():
            entered = True
            try:
                if args.action == "start":
                    controller.setup(args.rtt_ms, args.server, args.port)
                else:
                    controller.cleanup()
            except (OSError, ValueError, RuntimeError, subprocess.SubprocessError):
                if args.action == "start" and controller.attempted_setup:
                    controller.cleanup(require_traffic=False)
                raise
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        detail = getattr(error, "stderr", None) or str(error)
        print(f"ERROR: netem {args.action}: {detail}", file=sys.stderr)
        if entered and args.state.exists() and (
                args.action == "stop" or controller.attempted_setup):
            data = json.loads(args.state.read_text(encoding="utf-8"))
            data.update(Status="Failed", Error=str(detail))
            controller.save(data)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
