#!/usr/bin/env python3
"""Live changes to the lab's own queue; reuse the perf harness's ownership and cleanup."""

import argparse
import contextlib
import json
from pathlib import Path
import subprocess
import sys

import netem


def filters(controller, data):
    return json.loads(controller.command(
        "tc", "-j", "filter", "show", "dev", "lo", "parent", data["RootHandle"]))


def change(controller, delay):
    data = json.loads(controller.state_path.read_text())
    queues = controller.queues()
    root = next((q for q in queues if q.get("root")
                 and q.get("handle") == data["RootHandle"] and q["kind"] == "prio"), None)
    child = next((q for q in queues if q.get("handle") == data["NetemHandle"]
                  and q["kind"] == "netem"), None)
    configured = next((q for q in data["Configured"]
                       if q.get("handle") == data["NetemHandle"]), None)
    if (root is None or child is None or configured is None
            or child.get("options") != configured.get("options")):
        raise RuntimeError("Owned queue disappeared or changed; refusing to alter another owner.")
    if filters(controller, data) != data.get("ConfiguredFilters"):
        raise RuntimeError("SQL traffic filters changed; refusing to report an unverified delay.")
    if root.get("drops", 0) or child.get("drops", 0):
        raise RuntimeError("Owned queue dropped packets; pause and restart the experiment.")

    controller.command(
        "tc", "qdisc", "change", "dev", "lo", "parent", data["RootHandle"] + "2",
        "handle", data["NetemHandle"], "netem", "delay", f"{delay}ms",
        "limit", str(data["QueueLimitPackets"]), privileged=True)
    data.update(NetworkLatencyMilliseconds=delay, Configured=controller.queues())
    controller.save(data)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("set", "stop"))
    parser.add_argument("--state", type=Path, required=True)
    parser.add_argument("--server", default="127.0.0.1")
    parser.add_argument("--delay-ms", type=netem.delay_milliseconds, default=0)
    args = parser.parse_args()
    controller = netem.NetworkDelay(args.state, args.state.with_name("network-latency.json"))
    try:
        with contextlib.redirect_stdout(sys.stderr):
            if args.action == "stop":
                controller.cleanup()
            else:
                if args.state.exists():
                    previous = json.loads(args.state.read_text())
                    if previous["Status"] != "Applied":
                        controller.cleanup()
                        args.state.unlink()
                if not args.state.exists():
                    # Reserve an exclusive queue even at zero, before starting SQL work.
                    controller.setup(max(1, args.delay_ms), args.server)
                    initial = json.loads(args.state.read_text())
                    initial["ConfiguredFilters"] = filters(controller, initial)
                    controller.save(initial)
                change(controller, args.delay_ms)
        print(json.dumps({"ok": True}))
        return 0
    except (OSError, ValueError, RuntimeError, subprocess.SubprocessError) as error:
        print(json.dumps({"ok": False, "error": getattr(error, "stderr", None) or str(error)}))
        return 1


if __name__ == "__main__":
    sys.exit(main())
