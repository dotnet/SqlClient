#!/usr/bin/env python3
"""Graph one homogeneous authenticated RTT sweep without mixing platforms or control workloads."""

import argparse
import csv
import html
import json
from pathlib import Path
import statistics


def observations(rows, key):
    return [float(r[key]) for r in rows if r.get(key) not in ("", None)]


def median(rows, key):
    values = observations(rows, key)
    return statistics.median(values) if values else None


METRICS = {
    "dispatch_p99_ms": "ThreadPool dispatch p99 (ms)",
    "first_open_success_p99_ms": "First successful OpenAsync p99 (ms, inside worker)",
    "missing_probes": "Missing / dropped / producer-missed probes (count)",
}


def case_labels(manifest):
    """Derive presentation names from actual settings, not abbreviated identifier conventions."""
    labels = {}
    for name, config in manifest["cases"]:
        mode = ("Pooled V2" if config["PoolV2"] else "Pooled V1") if config["Pooling"] else "Non-pooled"
        state = ("Prewarmed pool" if config["WarmPool"] else
                 "Initialized driver, empty pool" if config.get("WarmDriver") and config["Pooling"]
                 else "Initialized driver" if config.get("WarmDriver")
                 else "Cold process / empty pool" if config["Pooling"] else "Cold process")
        capacity = f" | Pool max {config['PoolSize']}" if config["Pooling"] else ""
        operation = "Open/query/close" if config["Query"] else "Open/close (no query)"
        dispatch = " | LongRunning" if config["PoolV2"] and config["Pooling"] and config["LongRunning"] else ""
        labels[name] = [
            f"{mode} | {config['Workers']} workers{dispatch}",
            state + capacity,
            f"TP min/max {config['MinimumThreads']}/{config['MaximumThreads']} | {operation}",
        ]
    return labels


def label_svg(labels, case, x, y, spacing=17):
    lines = (labels or {}).get(case, [case])
    return "".join(f'<text x="{x}" y="{y+i*spacing}">{html.escape(line)}</text>'
                   for i, line in enumerate(lines))


def heatmap(grouped, cases, rtts, metric, title, unit, health=False, labels=None):
    left, top, cell, step = 450, 160, 165, 72
    width, height = max(1300, left + cell * len(rtts) + 20), top + step * len(cases) + 45
    result = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" '
              f'role="img" aria-label="{html.escape(title)}">',
              '<rect width="100%" height="100%" fill="white"/>',
              f'<text x="12" y="25" font-size="18">{html.escape(title)}</text>',
              f'<text x="12" y="48">Cell value: {html.escape(METRICS.get(metric, metric))}'
              ' - median of the per-run values across repetitions.</text>',
              '<text x="12" y="68">* = incomplete probe coverage in at least one run; n = repetitions.</text>']
    legend = [
        ("#fecaca", "Red: at least one run has dispatch p99 > 100 ms"),
        ("#dcfce7", "Green: every run passes scheduler budget and coverage"),
        ("#fef3c7", "Amber: no observed failure, but coverage is insufficient"),
        ("#eef2f7", "Gray: no value / not a scheduler-health verdict"),
    ] if health else [
        ("#eef2f7", "Gray: descriptive metric only; not a scheduler-health verdict"),
    ]
    for index, (color, description) in enumerate(legend):
        x, y = 12 + (index % 2) * 625, 91 + (index // 2) * 23
        result.append(f'<rect x="{x}" y="{y-11}" width="14" height="14" fill="{color}" stroke="#999"/>')
        result.append(f'<text x="{x+22}" y="{y}">{description}</text>')
    for col, rtt in enumerate(rtts):
        result.append(f'<text x="{left+col*cell+10}" y="{top-8}">{rtt} ms RTT</text>')
    for row, case in enumerate(cases):
        y = top + row * step
        result.append(label_svg(labels, case, 10, y + 20))
        for col, rtt in enumerate(rtts):
            items = grouped.get((case, rtt), [])
            value = median(items, metric)
            gaps = any(float(r["missing_probes"]) > 0 or r["status"] != "Completed" for r in items)
            fill = "#eef2f7"
            if health and value is not None:
                verdicts = {r["scheduler_health"] for r in items}
                fill = ("#fecaca" if "Fail" in verdicts else
                        "#dcfce7" if verdicts == {"Pass"} else "#fef3c7")
            x = left + col * cell
            label = "N/A" if value is None else f"{value:.1f}" + (" *" if gaps else "")
            result.extend([
                f'<rect class="metric-cell" x="{x}" y="{y}" width="{cell-4}" height="{step-4}" fill="{fill}"/>',
                f'<text x="{x+12}" y="{y+29}" font-size="18">{label}</text>',
                f'<text x="{x+12}" y="{y+50}" font-size="11">n={len(items)}</text>',
            ])
    result.append(f'<text x="12" y="{height-12}">RTT is added delay; 100 ms RTT = 50 ms per direction.</text>')
    result.append("</svg>")
    return "\n".join(result)


def linechart(grouped, cases, rtts, metric, title, unit, labels=None):
    width, height, left, top, plotwidth, plotheight = 1300, 590, 85, 70, 670, 440
    values = [v for (case, rtt), rows in grouped.items() if case in cases and rtt in rtts
              for v in observations(rows, metric)]
    ymax = max(values + [1]) * 1.1
    xmax = max(rtts + [1])
    colors = ["#dc2626", "#2563eb", "#059669", "#d97706", "#9333ea", "#475569", "#be185d"]
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" '
           f'role="img" aria-label="{html.escape(title)}">',
           '<rect width="100%" height="100%" fill="white"/>',
           f'<text x="12" y="25" font-size="18">{html.escape(title)}</text>',
           f'<text x="12" y="47">Markers = medians; vertical whiskers = observed min/max ({unit})</text>']
    for tick in range(5):
        value = ymax * tick / 4
        y = top + plotheight * (1 - tick / 4)
        svg.append(f'<path d="M{left} {y} H{left+plotwidth}" stroke="#ddd"/>')
        svg.append(f'<text x="8" y="{y+4}">{value:.0f}</text>')
    for rtt in rtts:
        x = left + plotwidth * rtt / xmax
        svg.append(f'<text x="{x-10}" y="{top+plotheight+25}">{rtt}</text>')
    for index, case in enumerate(cases):
        color = colors[index % len(colors)]
        points = []
        for rtt in rtts:
            vals = observations(grouped.get((case, rtt), []), metric)
            if not vals:
                continue
            x = left + plotwidth * rtt / xmax
            y = top + plotheight * (1 - statistics.median(vals) / ymax)
            points.append(f"{x},{y}")
            lo = top + plotheight * (1 - min(vals) / ymax)
            hi = top + plotheight * (1 - max(vals) / ymax)
            svg.append(f'<path d="M{x} {lo} V{hi}" stroke="{color}" stroke-width="2"/>')
            svg.append(f'<circle cx="{x}" cy="{y}" r="4" fill="{color}"/>')
        svg.append(f'<polyline points="{" ".join(points)}" fill="none" stroke="{color}" stroke-width="2"/>')
        svg.append(f'<g fill="{color}">' + label_svg(labels, case, 790, top + index * 68) + '</g>')
    svg.append(f'<text x="250" y="{height-15}">Added RTT (ms); missing probes require separate interpretation.</text>')
    svg.append("</svg>")
    return "\n".join(svg)


def validate_results(rows, manifest):
    """Require every requested case/RTT/repetition exactly once before drawing final boundaries."""
    expected = {(case, int(rtt), rep) for case, _ in manifest["cases"]
                for rtt in manifest["rtt"] for rep in range(1, manifest["repetitions"] + 1)}
    actual = [(r["case"], int(r["rtt_ms"]), int(r["repetition"])) for r in rows]
    if (manifest["status"] != "Completed" or len(actual) != len(expected)
            or set(actual) != expected):
        raise ValueError("Incomplete or duplicate case/RTT/repetition results.")
    if any(r["source"] != "AuthenticatedSql" for r in rows):
        raise ValueError("Do not mix synthetic controls into the authenticated RTT report.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("summary", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--findings", type=Path, help="Optional study narrative to embed as text.")
    args = parser.parse_args()
    with args.summary.open(newline="") as stream:
        rows = list(csv.DictReader(stream))
    manifest = json.loads(args.summary.with_name("manifest.json").read_text())
    validate_results(rows, manifest)
    labels = case_labels(manifest)
    platforms = sorted({r.get("platform") for r in rows if r.get("platform")})
    platform_description = ", ".join(platforms) if platforms else "Linux/WSL (original study)"
    managed = {config.get("UseManagedSni", True) for _, config in manifest["cases"]}
    durations = ", ".join(str(v) for v in sorted({
        config["DurationSeconds"] for _, config in manifest["cases"]}))
    timeouts = ", ".join(str(v) for v in sorted({
        config["ConnectTimeoutSeconds"] for _, config in manifest["cases"]}))
    sni_description = ("managed SNI" if managed == {True} else
                       "native SNI" if managed == {False} else "SNI selected per case")
    grouped = {}
    for row in rows:
        grouped.setdefault((row["case"], int(row["rtt_ms"])), []).append(row)
    rtts = sorted({rtt for _, rtt in grouped})
    workers = ["w16", "w32", "w64", "w128", "w256"]
    policies = ["w64", "w64-long", "w64-min64", "w64-max16", "w64-v1", "w64-warm",
                "w64-nonpooled"]
    graphs = [
        heatmap(grouped, workers, rtts, "dispatch_p99_ms",
                "V2 cold starts: ThreadPool dispatch p99 by worker count and RTT", "ms", True, labels),
        heatmap(grouped, policies, rtts, "dispatch_p99_ms",
                "64 workers: ThreadPool dispatch p99 by configuration", "ms", True, labels),
        linechart(grouped, policies, rtts, "completed_per_second",
                  "Completed query cycles across the observation window", "cycles/s", labels),
        linechart(grouped, policies, rtts, "process_peak",
                  "Total process-thread cost, not just ThreadPool threads", "threads", labels),
        linechart(grouped, ["w64", "w64-v1", "w64-nonpooled", "w64-long", "w64-warm"],
                  [rtt for rtt in rtts if rtt > 0], "completed_per_second",
                  "Connection modes at nonzero RTT: cold-window completed work", "cycles/s", labels),
        heatmap(grouped, ["w64", "w256", "w64-long", "w256-long", "w64-nonpooled"],
                rtts, "first_open_success_p99_ms", "First open per worker: successful samples", "ms",
                labels=labels),
        heatmap(grouped, ["w64", "w256", "w64-max16", "w64-long", "w256-long"],
                rtts, "missing_probes", "Missing, dropped, or producer-missed probe opportunities",
                "opportunities", labels=labels),
    ]
    if not set(workers).intersection(name for name, _ in manifest["cases"]):
        graphs = [
            heatmap(grouped, [name for name, config in manifest["cases"]
                             if (("V2" if config["PoolV2"] else "V1") if config["Pooling"]
                                 else "Non-pooled") == mode],
                    rtts, "dispatch_p99_ms", f"{mode}: OpenAsync path coverage - dispatch p99",
                    "ms", True, labels)
            for mode in ("V1", "V2", "Non-pooled")
        ]
    content = """<!doctype html><html lang="en"><meta charset="utf-8">
<title>Authenticated OpenAsync RTT study</title>
<style>body{font:15px system-ui;margin:32px;color:#172033;max-width:1400px}
p,li{line-height:1.5;max-width:1100px}svg{width:100%;font:13px system-ui;margin:16px 0;border:1px solid #ddd}
table{border-collapse:collapse;font-size:12px}td,th{padding:7px;border:1px solid #ddd;white-space:nowrap}
th{background:#e8eff7}.scroll{overflow:auto}.warning{background:#fff4d6;padding:15px}</style>
<h1>Authenticated OpenAsync RTT study</h1>
<p>PLATFORM_DESCRIPTION, real authenticated SQL Server, SNI_DESCRIPTION;
fresh process per case and repetition.
100 ms added RTT means 50 ms netem delay in each direction. Windows and synthetic-control
observations are deliberately not mixed into these graphs.</p>
<div class="warning">Observation duration: DURATION_SECONDS seconds. SQL connect timeout:
TIMEOUT_SECONDS seconds. In-flight work is cancelled at the deadline. A window shorter than the
connection timeout cannot establish that all requests finish within that timeout.
Missing probes can make a low completed
percentile misleading. Green cells are not an overall workload-health certification.</div>
<p>Every row states the connection mode, worker count, initialization, pool capacity, ThreadPool
limits, and whether SELECT 1 is executed. Prewarmed pool also warms driver initialization.
Non-pooled initialized-driver cases still create a physical connection on every open.
All cases disable pool blocking-period
error caching. Red means at least one repetition fails the investigative 100-ms dispatch p99 budget;
green means all repetitions pass scheduler coverage checks; amber means insufficient coverage.
This is not a product SLA.
Whiskers are repetition ranges, not confidence intervals.</p>"""
    content = content.replace("PLATFORM_DESCRIPTION", html.escape(platform_description))
    content = content.replace("SNI_DESCRIPTION", html.escape(sni_description))
    content = content.replace("DURATION_SECONDS", durations).replace("TIMEOUT_SECONDS", timeouts)
    content += ("<p>Open/acquisition percentiles include successful opens only; first opens cancelled "
                "at the deadline are excluded. The original operation sample cap also omitted some "
                "first-worker records in fast Open/close-only runs; those cold percentiles are N/A. "
                "General open percentiles may use a truncated sample. Counts and scheduler probes "
                "remain independent of that sample cap.</p>")
    content += f"<p>{len(rows)} runs; {manifest['repetitions']} repetitions per case; source revision "
    content += html.escape(manifest["revision"]) + " plus the diagnostic harness additions.</p>"
    if args.findings:
        content += ("<details><summary>Read findings, tradeoffs, and limits</summary>"
                    "<pre style='white-space:pre-wrap;font:14px system-ui;line-height:1.6'>"
                    + html.escape(args.findings.read_text(encoding="utf-8")) + "</pre></details>")
    content += "\n".join(graphs)
    keys = ["case", "repetition", "rtt_ms", "scheduler_health", "dispatch_p99_ms",
            "worst_1s_completion_p99_ms", "producer_p99_ms", "tp_peak", "process_peak",
            "queued_peak", "completed_per_second", "first_open_success_p99_ms",
            "first_acquisition_from_offer_p99_ms", "failures",
            "inflight_at_end", "unstarted_workers", "missing_probes", "observations_dropped",
            "first_worker_samples_missing", "status"]
    content += "<h2>Case definitions</h2><table><tr><th>Stable ID</th><th>Full configuration</th></tr>"
    for case, lines in labels.items():
        content += f"<tr><td>{html.escape(case)}</td><td>{html.escape('; '.join(lines))}</td></tr>"
    content += "</table><h2>Every repetition</h2><div class='scroll'><table><tr>"
    content += "".join(f"<th>{key}</th>" for key in keys) + "</tr>"
    for row in sorted(rows, key=lambda r: (int(r["rtt_ms"]), r["case"], int(r["repetition"]))):
        content += "<tr>" + "".join(
            f"<td>{html.escape('; '.join(labels[row['case']]) if key == 'case' else row.get(key, ''))}</td>"
            for key in keys) + "</tr>"
    content += "</table></div><h2>Provenance</h2><pre>"
    safe_manifest = {k: v for k, v in manifest.items() if k != "runtime"}
    content += html.escape(json.dumps(safe_manifest, indent=2)) + "</pre></html>"
    args.output.write_text(content, encoding="utf-8")
    for index, graph in enumerate(graphs, 1):
        args.output.with_name(f"rtt-chart-{index}.svg").write_text(graph, encoding="utf-8")


if __name__ == "__main__":
    main()
