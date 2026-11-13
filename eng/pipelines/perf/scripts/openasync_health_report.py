#!/usr/bin/env python3
"""Render replicated open-health CSVs as a self-contained HTML report with inline SVG graphs."""

import argparse
import csv
import html
from pathlib import Path
import statistics


def groups(rows):
    result = {}
    for row in rows:
        result.setdefault((row["source"], int(row["rtt_ms"]), row["case"]), []).append(row)
    return result


def values(rows, key):
    return [float(r[key]) for r in rows if r.get(key) not in (None, "")]


def interval(rows, key):
    v = values(rows, key)
    return "N/A" if not v else f"{statistics.median(v):.2f} [{min(v):.2f}, {max(v):.2f}]"


def chart(grouped, source, keys, title, unit, budget=None):
    selected = [(f"{case}" + (f" / RTT {rtt}" if source == "AuthenticatedSql" else "") +
                 (" *" if any(float(r["missing_probes"]) > 0 for r in rows) else ""), rows)
                for (kind, rtt, case), rows in grouped.items()
                if kind == source]
    if not selected:
        return ""
    width, left, right = 1060, 210, 170
    step = 28 * len(keys) + 10
    height = 90 + len(selected) * step
    maximum = max([v for _, rows in selected for key, _ in keys for v in values(rows, key)]
                  + [budget or 1]) * 1.05
    scale = (width - left - right) / maximum
    colors = ["#2563eb", "#ea580c"]
    svg = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}" '
           f'role="img" aria-label="{html.escape(title)}">',
           '<rect width="100%" height="100%" fill="white"/>',
           f'<text x="15" y="23" font-size="18">{html.escape(title)}</text>']
    for i, (_, label) in enumerate(keys):
        svg.append(f'<text x="{left + i * 320}" y="48" fill="{colors[i]}">'
                   f'{html.escape(label)} ({unit})</text>')
    if budget:
        x = left + budget * scale
        svg += [f'<path d="M{x} 57 V{height - 10}" stroke="#dc2626" stroke-dasharray="4 4"/>',
                f'<text x="{x + 4}" y="62" fill="#dc2626">{budget:g} budget</text>']
    for n, (label, rows) in enumerate(selected):
        y = 82 + n * step
        svg.append(f'<text x="10" y="{y + 3}">{html.escape(label)}</text>')
        for i, (key, _) in enumerate(keys):
            v = values(rows, key)
            if not v:
                continue
            median = statistics.median(v)
            cy = y + i * 28
            svg += [
                f'<rect x="{left}" y="{cy - 9}" width="{median * scale}" height="16" '
                f'fill="{colors[i]}" opacity=".75"/>',
                f'<path d="M{left + min(v) * scale} {cy} H{left + max(v) * scale}" '
                'stroke="#111" stroke-width="2"/>',
                f'<text x="{width-right+12}" y="{cy+4}">{median:.1f} '
                f'[{min(v):.1f}, {max(v):.1f}]</text>',
            ]
    svg.append(f'<text x="10" y="{height - 4}" font-size="11">'
               '* Missing/missed probes in one or more runs: a low bar alone is not a pass.</text>')
    svg.append("</svg>")
    return "\n".join(svg)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("summaries", type=Path, nargs="+")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--scope-note", default=(
        "Check the manifests for runtime, platform and network provenance. "
        "SyntheticStalledPrelogin is a negative control, not a SQL RTT simulation."))
    parser.add_argument("--findings", type=Path,
                        help="Optional narrative Markdown, embedded as readable source text.")
    args = parser.parse_args()
    rows = []
    for source in args.summaries:
        with source.open(newline="") as stream:
            rows.extend(csv.DictReader(stream))
    grouped = dict(sorted(groups(rows).items()))
    charts = [
        chart(grouped, "AuthenticatedSql", [("dispatch_p99_ms", "Dispatch p99")],
              "Authenticated SQL: fresh-process scheduler tail", "ms", 100),
        chart(grouped, "SyntheticStalledPrelogin", [("dispatch_p99_ms", "Dispatch p99")],
              "Stalled prelogin negative control: scheduler tail (not an RTT test)", "ms", 100),
        chart(grouped, "SyntheticStalledPrelogin",
              [("tp_peak", "ThreadPool peak"), ("process_peak", "Process threads peak")],
              "Stalled prelogin: where the threads went", "threads"),
        chart(grouped, "AuthenticatedSql",
              [("first_open_success_p99_ms", "First open per worker p99"),
               ("open_success_p99_ms", "Retained successful opens p99")],
              "Cold-open latency versus warm-reuse dominated latency", "ms"),
    ]
    columns = [
        ("dispatch_p99_ms", "Dispatch p99 ms"), ("worst_1s_completion_p99_ms", "Worst 1s p99 ms"),
        ("producer_p99_ms", "Producer p99 ms"), ("tp_peak", "TP peak"),
        ("process_peak", "Process peak"), ("queued_peak", "Queue peak"),
        ("first_open_success_p99_ms", "First open p99 ms"),
        ("open_failure_p99_ms", "Failed open p99 ms"),
        ("completed_per_second", "Completed cycles/s"),
        ("failures", "Failures"), ("inflight_at_end", "In flight at deadline"),
        ("unstarted_workers", "Unstarted workers"), ("missing_probes", "Missing/missed probes"),
        ("observations_dropped", "Operation samples dropped"),
    ]
    table = ["<table><thead><tr><th>Source / RTT / Case</th><th>N</th><th>Scheduler verdicts</th>"]
    table += [f"<th>{label}</th>" for _, label in columns]
    table += ["</tr></thead><tbody>"]
    for (source, rtt, case), group in grouped.items():
        verdicts = ", ".join(f"{v}: {sum(r['scheduler_health'] == v for r in group)}"
                             for v in ("Pass", "Fail", "Insufficient"))
        table.append(f"<tr><td>{html.escape(source)} / {rtt} / {html.escape(case)}</td><td>{len(group)}</td>"
                     f"<td>{verdicts}</td>")
        table += [f"<td>{interval(group, key)}</td>" for key, _ in columns]
        table.append("</tr>")
    table.append("</tbody></table>")
    content = """<!doctype html><html lang="en"><meta charset="utf-8">
<title>OpenAsync health boundary study</title>
<style>body{font:15px system-ui;margin:32px;color:#172033;max-width:1400px}
h1,h2{color:#163d67}p,li{max-width:1050px;line-height:1.55}
.warning{background:#fff4d6;padding:18px;border-left:5px solid #d99000}
svg{width:100%;margin:16px 0;font:13px system-ui;border:1px solid #ddd}
table{border-collapse:collapse;font-size:12px}th,td{border:1px solid #ddd;padding:8px}
th{background:#e8eff7}td{white-space:nowrap}.scroll{overflow-x:auto}</style>
<h1>OpenAsync health boundary study</h1>
<p>Fresh-process experiments. Bars show medians; black lines and brackets show the observed
minimum and maximum across process repetitions, not confidence intervals.</p>
<div class="warning"><b>Scope:</b> SCOPE_NOTE</div>
<h2>Protocol and interpretation</h2>
<ul><li>Eight-second observation, 25-ms probes, capacity 64, one-second probe recovery,
and bounded worker drain. Each case runs in a fresh process; randomized order uses seed 5014.</li>
<li>Cases default to V2, cold pool, pool size 100, ThreadPool min/max 16/32767.
Suffixes change one setting: long = LongRunning dispatch; min64 = TP minimum 64;
max16/max32 = TP ceiling; v1 = legacy pool; nonpooled = no pool;
warm = prewarmed connections; pool16 = pool capacity 16.</li>
<li>SQL cases loop OpenAsync/SELECT 1/close. Stalled cases launch one burst and never succeed.
PoolBlockingPeriod is NeverBlock. Process-cold JIT, authentication, and TLS initialization are
inside the cold observation; a warmed run removes more than just pool creation.</li>
<li>100 ms is an investigative scheduler budget, not an ASP.NET or SqlClient SLA.
Known over-budget results fail even when probes are missing; under-budget results with missing,
missed, or fewer than 100 probes are insufficient, not passing.</li>
<li>All-open p99 is dominated by fast reuse and is limited to the first 100,000 retained operation
records. The table exposes truncation. Scheduler probes and completed-work counters are independent
of that cap. First-open p99 and failed-open p99 avoid treating failures as fast successes.</li>
<li>A lower ThreadPool peak is not necessarily a resource improvement: LongRunning can create
dedicated threads. Compare total process threads, completions, and errors too.</li></ul>
<h2>Graphs</h2>"""
    content = content.replace("SCOPE_NOTE", html.escape(args.scope_note))
    if args.findings:
        narrative = ("<h2>Interpretation and next steps</h2>"
                     "<details><summary>Read detailed findings and limitations</summary>"
                     "<pre style='white-space:pre-wrap;font:14px system-ui;line-height:1.6'>"
                     + html.escape(args.findings.read_text(encoding="utf-8")) + "</pre></details>")
        content = content.replace("<h2>Graphs</h2>", narrative + "<h2>Graphs</h2>")
    content += "\n".join(charts)
    content += "<h2>Complete results</h2><p>Each metric: median [min, max].</p><div class='scroll'>"
    content += "".join(table) + "</div>"
    content += "<h2>Reproduction</h2><p>See eng/pipelines/perf/openasync-health.md for exact commands, "
    content += "RTT conventions, health eligibility, and baseline/candidate acceptance criteria. "
    content += "Raw per-case JSON and manifests remain beside each summary CSV.</p></html>"
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(content, encoding="utf-8")
    for index, graph in enumerate(charts, 1):
        if graph:
            args.output.with_name(f"health-chart-{index}.svg").write_text(graph, encoding="utf-8")
    print(args.output)


if __name__ == "__main__":
    main()
