#!/usr/bin/env python3
# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

"""Exclusive, SQL-only loopback delay for the OpenAsync health phase."""

import ipaddress
import json
import os
from pathlib import Path
import socket
import subprocess
import uuid

from openasync_health import write_json


class NetworkDelay:
    """Remove only this run's queue, including after partial setup; retain packet/drop evidence."""

    def __init__(self, state_path, output_path):
        self.state_path = Path(state_path)
        self.output_path = Path(output_path)
        self.owned = False

    def save(self, data):
        write_json(self.state_path, data)
        write_json(self.output_path, data)

    def command(self, *args, privileged=False):
        prefix = ["sudo", "-n"] if privileged and os.geteuid() != 0 else []
        return subprocess.run([*prefix, *args], check=True, text=True,
                              capture_output=True, timeout=30).stdout

    def queues(self):
        return json.loads(self.command("tc", "-s", "-j", "qdisc", "show", "dev", "lo"))

    def setup(self, delay, server):
        if delay <= 0:
            raise ValueError("NetworkDelay requires a positive per-direction delay.")
        addresses = sorted({str(ipaddress.ip_address(info[4][0]))
                            for info in socket.getaddrinfo(server, 1433, type=socket.SOCK_STREAM)})
        if not addresses:
            raise RuntimeError("SQL endpoint did not resolve.")
        for address in addresses:
            family = "-4" if ipaddress.ip_address(address).version == 4 else "-6"
            routes = json.loads(self.command("ip", family, "-j", "route", "get", address))
            if not routes or any(route.get("dev") != "lo" for route in routes):
                raise RuntimeError("RTT injection supports only local loopback SQL endpoints.")
        before = self.queues()
        if any(q.get("root") and q["kind"] != "noqueue" for q in before):
            raise RuntimeError("Refusing to overwrite an existing loopback queue.")

        number = uuid.uuid4().int % 0x7ffe + 1
        data = dict(Status="Installing", RootHandle=f"{number:x}:",
                    NetemHandle=f"{number + 0x8000:x}:", Applied=False, Before=before,
                    DelayMillisecondsPerDirection=delay, ServerAddresses=addresses, Port=1433)
        self.save(data)
        # A timeout/interrupt can arrive after tc applied the queue; cleanup must still inspect
        # the recorded handle. A definitive failed add never grants ownership.
        self.owned = True
        try:
            self.command("tc", "qdisc", "add", "dev", "lo", "root", "handle", data["RootHandle"],
                         "prio", "bands", "3", "priomap", *(["0"] * 16), privileged=True)
        except subprocess.CalledProcessError:
            self.owned = False
            raise
        self.command("tc", "qdisc", "add", "dev", "lo", "parent", f"{number:x}:2",
                     "handle", data["NetemHandle"], "netem", "delay", f"{delay}ms",
                     "limit", "10000", privileged=True)
        for index, address in enumerate(addresses):
            protocol = "ip" if ipaddress.ip_address(address).version == 4 else "ipv6"
            for offset, direction in enumerate(("dst", "src")):
                self.command("tc", "filter", "add", "dev", "lo", "parent", data["RootHandle"],
                             "protocol", protocol, "pref", str(index * 2 + offset + 1),
                             "flower", "ip_proto", "tcp", f"{direction}_ip", address,
                             f"{direction}_port", "1433", "classid", f"{number:x}:2",
                             privileged=True)
        data.update(Status="Applied", Applied=True, Configured=self.queues())
        self.save(data)

    def cleanup(self):
        if not self.owned:
            return
        data = json.loads(self.state_path.read_text(encoding="utf-8"))
        queues = self.queues()
        handle = data["RootHandle"]
        if not any(q.get("root") and q.get("handle") == handle and q["kind"] == "prio"
                   for q in queues):
            if not data["Applied"]:
                self.owned = False
                data["Status"] = "SetupFailed"
                self.save(data)
                return
            raise RuntimeError("Owned netem queue disappeared or changed.")
        try:
            data["After"] = queues
            data["Filters"] = json.loads(self.command(
                "tc", "-s", "-j", "filter", "show", "dev", "lo", "parent", handle))
        finally:
            self.command("tc", "qdisc", "del", "dev", "lo", "root", "handle", handle,
                         privileged=True)
            self.owned = False
            data["Status"] = "Cleaned" if data["Applied"] else "SetupFailed"
            self.save(data)
        if data["Applied"]:
            delayed = [q for q in queues if q.get("handle") == data["NetemHandle"]
                       and q["kind"] == "netem"]
            configured = [q for q in data["Configured"]
                          if q.get("handle") == data["NetemHandle"]]
            if (len(delayed) != 1 or len(configured) != 1 or
                    delayed[0].get("options") != configured[0].get("options")):
                raise RuntimeError("Netem delay changed during execution.")
            if any(q.get("drops", 0) for q in queues
                   if q.get("handle") in (handle, data["NetemHandle"])):
                raise RuntimeError("Netem dropped packets; this is not a latency-only run.")
            if not delayed[0].get("packets", 0):
                raise RuntimeError("No SQL packets traversed netem; RTT evidence is invalid.")
