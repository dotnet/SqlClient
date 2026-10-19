# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Validate this immutable results snapshot without running a benchmark."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
from urllib.parse import unquote, urlsplit
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parent


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(path):
    with path.open("rb") as handle:
        return hashlib.file_digest(handle, "sha256").hexdigest()


def verify():
    expected = {}
    for line in (ROOT / "SHA256SUMS").read_text(encoding="utf-8").splitlines():
        digest, relative = line.split("  ", 1)
        path = ROOT / relative
        require(path.resolve().is_relative_to(ROOT), f"Unsafe manifest path: {relative}")
        require(relative not in expected, f"Duplicate manifest path: {relative}")
        require(path.is_file() and sha256(path) == digest, f"Hash mismatch: {relative}")
        expected[relative] = digest
    actual = {p.relative_to(ROOT).as_posix() for p in ROOT.rglob("*")
              if p.is_file() and p.name != "SHA256SUMS" and "__pycache__" not in p.parts}
    require(actual == set(expected), "Manifest does not exactly cover the result files")

    sources = json.loads((ROOT / "source-artifacts.json").read_text(encoding="utf-8"))
    copied = 0
    for entry in sources["artifacts"]:
        if entry["disposition"] == "copied-byte-for-byte":
            target = ROOT / entry["destination"]
            require(target.stat().st_size == entry["bytes"], f"Source size mismatch: {target}")
            require(sha256(target) == entry["sha256"], f"Source hash mismatch: {target}")
            copied += 1

    figures = 0
    for relative in sorted(expected):
        path = ROOT / relative
        if path.suffix == ".json":
            json.loads(path.read_text(encoding="utf-8-sig"))
        elif path.suffix == ".svg":
            root = ET.parse(path).getroot()
            require(root.tag == "{http://www.w3.org/2000/svg}svg", f"Not an SVG: {path}")
            for element in root.iter():
                require(element.tag.rsplit("}", 1)[-1] not in ("script", "foreignObject"),
                        f"Active SVG content: {path}")
                require(not any(key.rsplit("}", 1)[-1] == "href" for key in element.attrib),
                        f"External SVG reference: {path}")
            figures += 1
        elif path.suffix == ".md":
            text = path.read_text(encoding="utf-8-sig")
            for link in re.findall(r"!?\[[^\]]*\]\(([^)\n]+)\)", text):
                parsed = urlsplit(link)
                if not parsed.scheme and parsed.path:
                    target = path.parent / unquote(parsed.path)
                    require(target.exists(), f"Broken local link in {relative}: {link}")
    require(figures == 26, f"Expected 26 figures, got {figures}")

    data = json.loads((ROOT / "perf-183246-analysis.json").read_text(encoding="utf-8"))
    require(data["stageCounts"] == {"WorkloadActual": 864, "WorkloadWarmup": 576},
            "Main-run invocation accounting mismatch")
    for group in ("sampled", "diagnostics"):
        require(len(data[group]) == 48 and len({r["case"] for r in data[group]}) == 48,
                f"Main-run case accounting mismatch: {group}")

    stages = Counter()
    cases = set()
    with (ROOT / "perf-results-182350" / "invocation-aggregates.jsonl").open(
            encoding="utf-8") as handle:
        for line in handle:
            row = json.loads(line)
            stages[row["MeasurementStage"]] += 1
            cases.add((row["CaseId"], row["Variant"]))
    require(stages == {"WorkloadActual": 255, "WorkloadWarmup": 170} and len(cases) == 85,
            "Partial-run accounting mismatch")
    print(f"Verified {len(expected)} files, {copied} byte-identical copies, {figures} SVGs, "
          "96 main-run cases and 425 partial-run invocation aggregates; all local links resolve.")


if __name__ == "__main__":
    verify()
