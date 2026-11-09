#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Own a SQL-only loopback netem rule for one Linux performance run."""

import argparse
import ipaddress
import json
import os
from pathlib import Path
import re
import socket
import subprocess
import sys
import uuid


def delay_milliseconds(value):
    """Accept nonnegative integer milliseconds, without shell arithmetic or rounding."""
    if not re.fullmatch(r"[0-9]+", str(value)):
        raise argparse.ArgumentTypeError("network latency must be nonnegative integer milliseconds")
    return int(value)


def write_json(path, value):
    """Publish complete state before issuing a command that might need cleanup."""
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(json.dumps(value, indent=2) + "\n", encoding="utf-8")
    temporary.replace(path)


class NetworkDelay:
    """Apply and remove only this run's queue, preserving foreign network configuration."""

    def __init__(self, state_path, output_path):
        self.state_path = Path(state_path)
        self.output_path = Path(output_path)

    def save(self, data):
        write_json(self.state_path, data)
        write_json(self.output_path, data)

    def command(self, *args, privileged=False):
        prefix = ["sudo", "-n"] if privileged and os.geteuid() != 0 else []
        result = subprocess.run(
            [*prefix, *args], check=True, text=True, capture_output=True, timeout=30)
        return result.stdout

    def queues(self):
        return json.loads(self.command("tc", "-s", "-j", "qdisc", "show", "dev", "lo"))

    def setup(self, delay, server):
        data = dict(
            SchemaVersion=1, NetworkLatencyMilliseconds=delay, Direction="EachDirection",
            Interface="lo", TcpPort=1433, QueueLimitPackets=10000, ServerAddresses=[],
            Status="Disabled" if delay == 0 else "Preparing", Applied=False,
            RootHandle=None, NetemHandle=None,
        )
        self.save(data)
        if delay == 0:
            return

        addresses = sorted({str(ipaddress.ip_address(info[4][0]))
                            for info in socket.getaddrinfo(server, 1433, type=socket.SOCK_STREAM)})
        if not addresses:
            raise RuntimeError("SQL_SERVER did not resolve to any addresses")
        for address in addresses:
            family = "-4" if ipaddress.ip_address(address).version == 4 else "-6"
            routes = json.loads(self.command("ip", family, "-j", "route", "get", address))
            if not routes or any(route.get("dev") != "lo" for route in routes):
                raise RuntimeError(
                    "Network latency requires SQL_SERVER to resolve only to local loopback "
                    f"routes; {address} is not local. Remote SQL endpoints are unsupported.")

        before = self.queues()
        if any(queue.get("root") and queue["kind"] != "noqueue" for queue in before):
            raise RuntimeError("Refusing to overwrite an existing root queue on lo")

        number = uuid.uuid4().int % 0x7ffe + 1
        data.update(ServerAddresses=addresses, RootHandle=f"{number:x}:",
                    NetemHandle=f"{number + 0x8000:x}:", Before=before, Status="Installing")
        # Record intent before add: cancellation between add and save must still be recoverable.
        self.save(data)
        self.command("tc", "qdisc", "add", "dev", "lo", "root", "handle", data["RootHandle"],
                     "prio", "bands", "3", "priomap", *(["0"] * 16), privileged=True)
        self.command("tc", "qdisc", "add", "dev", "lo", "parent", f"{number:x}:2",
                     "handle", data["NetemHandle"], "netem", "delay", f"{delay}ms",
                     "limit", str(data["QueueLimitPackets"]), privileged=True)

        for index, address in enumerate(addresses):
            protocol = "ip" if ipaddress.ip_address(address).version == 4 else "ipv6"
            for offset, direction in enumerate(("dst", "src")):
                self.command(
                    "tc", "filter", "add", "dev", "lo", "parent", data["RootHandle"],
                    "protocol", protocol, "pref", str(index * 2 + offset + 1),
                    "flower", "ip_proto", "tcp", f"{direction}_ip", address,
                    f"{direction}_port", "1433", "classid", f"{number:x}:2", privileged=True)

        data.update(Status="Applied", Applied=True, Configured=self.queues())
        self.save(data)
        print(f"Netem: {delay} ms per direction for SQL TCP/1433 on lo "
              f"({', '.join(addresses)}); all other traffic uses the undelayed band.")

    def cleanup(self):
        if not self.state_path.exists() or not self.state_path.stat().st_size:
            return
        data = json.loads(self.state_path.read_text(encoding="utf-8"))
        handle = data["RootHandle"]
        if handle is None:
            self.save(data)
            return
        if not re.fullmatch(r"[0-9a-f]{1,4}:", handle):
            raise ValueError("Invalid owned netem queue handle")

        queues = self.queues()
        owned = any(queue.get("root") and queue.get("handle") == handle
                    and queue["kind"] == "prio" for queue in queues)
        if not owned:
            if data["Applied"]:
                raise RuntimeError("The run's netem root queue disappeared or changed during execution")
            return

        try:
            data["After"] = queues
            data["Filters"] = json.loads(self.command(
                "tc", "-s", "-j", "filter", "show", "dev", "lo", "parent", handle))
        finally:
            # Never replace/delete a pre-existing queue; add was exclusive and the handle is ours.
            self.command("tc", "qdisc", "del", "dev", "lo", "root", "handle", handle,
                         privileged=True)

        data["Status"] = "Cleaned" if data["Applied"] else "SetupFailed"
        self.save(data)
        if data["Applied"]:
            netem = [queue for queue in queues if queue.get("handle") == data["NetemHandle"]
                     and queue["kind"] == "netem"]
            if len(netem) != 1:
                raise RuntimeError("The run's netem child queue disappeared during execution")
            configured = next(queue for queue in data["Configured"]
                              if queue.get("handle") == data["NetemHandle"])
            if netem[0].get("options") != configured.get("options"):
                raise RuntimeError("The netem settings changed during execution")
            if any(queue.get("drops", 0) for queue in queues
                   if queue.get("handle") in (handle, data["NetemHandle"])):
                raise RuntimeError("Netem run dropped packets; refusing to report a latency-only run")
            if not netem[0].get("packets", 0):
                print("WARNING: no SQL packets traversed netem; the workload may not use this "
                      "SQL endpoint.", file=sys.stderr)
        print("Removed this run's netem queue from lo.")


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("start", "stop"))
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--delay-ms", type=delay_milliseconds, default=0)
    parser.add_argument("--server", default="localhost")
    args = parser.parse_args(argv)
    controller = NetworkDelay(args.state, args.output)
    try:
        if args.action == "start":
            controller.setup(args.delay_ms, args.server)
        else:
            controller.cleanup()
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        detail = getattr(error, "stderr", None) or str(error)
        print(f"ERROR: netem {args.action}: {detail}", file=sys.stderr)
        if args.state.exists() and args.state.stat().st_size:
            data = json.loads(args.state.read_text(encoding="utf-8"))
            data.update(Status="Failed", Error=str(detail))
            controller.save(data)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
