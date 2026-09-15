#!/usr/bin/env python3

from __future__ import annotations

import argparse
import html
import math
import re
import statistics
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable
from xml.sax.saxutils import escape


BACKGROUND = "#f8fafc"
PANEL = "#ffffff"
TEXT = "#0f172a"
MUTED = "#64748b"
GRID = "#dbe4ee"
TRACK = "#e2e8f0"
LEGACY = "#94a3b8"
NEW_POOL = "#0f766e"
RED = "#dc2626"
SQL_ROW = "#eff6ff"
SQL_TEXT = "#1d4ed8"
SQL_HOLD_DESCRIPTION = (
    "executes SQL commands or deliberately holds a checked-out connection; "
    "pool overhead is only part of the measured time"
)
COLD_START_DESCRIPTION = (
    "Models startup and scale-out by opening the requested number of physical "
    "connections from a cold pool."
)
RAPID_FIRE_DESCRIPTION = (
    "Measures hot-path checkout and return throughput under sustained concurrent churn."
)

SQL_OR_HOLD_BENCHMARKS = {
    "SteadyStateOpenQueryClose",
    "SteadyStateOpenQueryCloseAsync",
    "SteadyStateOpenQueryCloseDedicatedThreads",
    "RandomizedHoldAndQuery",
    "MixedSyncAsyncContention",
    "MultiCommandReuse",
    "PoolExhaustionRecovery",
    "BurstyTrafficPattern",
}

RUNNER_LABELS = {
    "ConnectionPoolChurnRunner": "Churn",
    "ConnectionPoolContentionRunner": "Contention",
    "ConnectionPoolRampRunner": "Cold-start ramp",
    "ConnectionPoolStressRunner": "Stress",
    "ConnectionPoolThreadPoolPressureRunner": "Thread-pool pressure",
    "ParallelAsyncConnectionRunner": "Parallel connections",
    "SqlConnectionRunner": "SqlConnection",
}

PARAMETER_LABELS = {
    "Parallelism": "P",
    "MaxPoolSize": "Pool",
    "MinWorkerThreads": "Workers",
    "OpsPerWorker": "Ops/worker",
    "OpsPerInvocation": "Ops",
    "PoolDepth": "Depth",
    "Concurrency": "Concurrency",
}


@dataclass(frozen=True)
class Result:
    rank: int
    status: str
    runner: str
    benchmark: str
    parameters: str
    legacy: float
    new_pool: float
    timing_delta: float
    secondary_delta: float
    confirmation: str

    @property
    def params(self) -> dict[str, str]:
        if self.parameters == "-":
            return {}
        return dict(part.split("=", 1) for part in self.parameters.split("&"))

    @property
    def speedup(self) -> float:
        # The report's delta is calculated before the displayed means are rounded.
        return 1 / (1 + self.timing_delta / 100)


class Svg:
    def __init__(self, width: int, height: int, title: str):
        self.width = width
        self.height = height
        self.title = title
        self.parts = [
            (
                f'<svg xmlns="http://www.w3.org/2000/svg" width="{width}" '
                f'height="{height}" viewBox="0 0 {width} {height}" role="img">'
            ),
            f"<title>{escape(title)}</title>",
            (
                "<style>"
                "text{font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',sans-serif;"
                "font-variant-numeric:tabular-nums}"
                "</style>"
            ),
        ]
        self.rect(0, 0, width, height, fill=BACKGROUND)

    def rect(
        self,
        x: float,
        y: float,
        width: float,
        height: float,
        *,
        fill: str = "none",
        stroke: str | None = None,
        stroke_width: float = 1,
        rx: float = 0,
        opacity: float = 1,
    ) -> None:
        attrs = [
            f'x="{x:.2f}"',
            f'y="{y:.2f}"',
            f'width="{width:.2f}"',
            f'height="{height:.2f}"',
            f'fill="{fill}"',
        ]
        if stroke:
            attrs.extend([f'stroke="{stroke}"', f'stroke-width="{stroke_width}"'])
        if rx:
            attrs.append(f'rx="{rx}"')
        if opacity != 1:
            attrs.append(f'opacity="{opacity}"')
        self.parts.append(f"<rect {' '.join(attrs)}/>")

    def line(
        self,
        x1: float,
        y1: float,
        x2: float,
        y2: float,
        *,
        stroke: str = GRID,
        stroke_width: float = 1,
        dash: str | None = None,
    ) -> None:
        dash_attr = f' stroke-dasharray="{dash}"' if dash else ""
        self.parts.append(
            (
                f'<line x1="{x1:.2f}" y1="{y1:.2f}" x2="{x2:.2f}" y2="{y2:.2f}" '
                f'stroke="{stroke}" stroke-width="{stroke_width}"{dash_attr}/>'
            )
        )

    def text(
        self,
        x: float,
        y: float,
        value: str,
        *,
        size: int = 24,
        fill: str = TEXT,
        weight: int = 400,
        anchor: str = "start",
        opacity: float = 1,
        superscript: str | None = None,
    ) -> None:
        content = escape(value)
        if superscript is not None:
            superscript_size = max(9, round(size * 0.65))
            content += (
                f'<tspan baseline-shift="super" font-size="{superscript_size}" '
                f'dx="2">{escape(superscript)}</tspan>'
            )
        self.parts.append(
            (
                f'<text x="{x:.2f}" y="{y:.2f}" font-size="{size}" fill="{fill}" '
                f'font-weight="{weight}" text-anchor="{anchor}" opacity="{opacity}">'
                f"{content}</text>"
            )
        )

    def rotated_text(
        self,
        x: float,
        y: float,
        value: str,
        *,
        angle: int = -90,
        size: int = 24,
        fill: str = TEXT,
        weight: int = 400,
        anchor: str = "middle",
    ) -> None:
        self.parts.append(
            (
                f'<text x="{x:.2f}" y="{y:.2f}" font-size="{size}" fill="{fill}" '
                f'font-weight="{weight}" text-anchor="{anchor}" '
                f'transform="rotate({angle} {x:.2f} {y:.2f})">'
                f"{escape(value)}</text>"
            )
        )

    def circle(
        self,
        cx: float,
        cy: float,
        radius: float,
        *,
        fill: str,
        stroke: str | None = None,
        stroke_width: float = 1,
    ) -> None:
        stroke_attrs = (
            f' stroke="{stroke}" stroke-width="{stroke_width}"' if stroke else ""
        )
        self.parts.append(
            f'<circle cx="{cx:.2f}" cy="{cy:.2f}" r="{radius:.2f}" fill="{fill}"{stroke_attrs}/>'
        )

    def save(self, path: Path) -> None:
        self.parts.append("</svg>")
        path.write_text("\n".join(self.parts), encoding="utf-8")


def parse_results(path: Path) -> list[Result]:
    results: list[Result] = []
    for rank, raw_line in enumerate(
        path.read_text(encoding="utf-8").splitlines(),
        start=1,
    ):
        line = raw_line.strip()
        if not line:
            continue
        columns = line.split("\t")
        if len(columns) != 9:
            raise ValueError(
                f"Expected 9 tab-separated columns on row {rank}, got {len(columns)}"
            )
        status, runner, benchmark, parameters, legacy, new_pool, timing, secondary, confirmation = columns
        result = Result(
            rank=rank,
            status=status,
            runner=runner,
            benchmark=benchmark,
            parameters=parameters,
            legacy=float(legacy),
            new_pool=float(new_pool),
            timing_delta=float(timing.removesuffix("%")),
            secondary_delta=float(secondary.removesuffix("%")),
            confirmation=confirmation,
        )
        calculated_delta = (result.new_pool / result.legacy - 1) * 100
        if result.legacy >= 0.01 and not math.isclose(
            calculated_delta,
            result.timing_delta,
            abs_tol=0.02,
        ):
            raise ValueError(
                f"Timing delta mismatch at rank {result.rank}: "
                f"reported {result.timing_delta}, calculated {calculated_delta:.2f}"
            )
        results.append(result)
    return results


def find(
    results: Iterable[Result],
    runner: str,
    benchmark: str,
    **parameters: object,
) -> Result:
    expected = {key: str(value) for key, value in parameters.items()}
    matches = [
        result
        for result in results
        if result.runner == runner
        and result.benchmark == benchmark
        and all(result.params.get(key) == value for key, value in expected.items())
    ]
    if len(matches) != 1:
        raise ValueError(
            f"Expected one result for {runner}.{benchmark} {expected}, got {len(matches)}"
        )
    return matches[0]


def format_mean(value: float) -> str:
    if value >= 100:
        return f"{value:.1f}"
    if value >= 10:
        return f"{value:.1f}"
    if value >= 1:
        return f"{value:.2f}"
    return f"{value:.4f}"


def is_pool_related(result: Result) -> bool:
    if result.runner.startswith("ConnectionPool"):
        return True
    return (
        result.runner in {"ParallelAsyncConnectionRunner", "SqlConnectionRunner"}
        and result.params.get("Pooling") == "True"
    )


def is_sql_or_hold_heavy(result: Result) -> bool:
    return result.benchmark in SQL_OR_HOLD_BENCHMARKS


def benchmark_label(result: Result) -> str:
    runner = RUNNER_LABELS.get(result.runner, result.runner.removesuffix("Runner"))
    benchmark = re.sub(r"(?<=[a-z0-9])(?=[A-Z])", " ", result.benchmark)
    return f"{runner}: {benchmark}"


def compact_parameters(result: Result) -> str:
    parts: list[str] = []
    for key, value in result.params.items():
        if key == "Pooling":
            continue
        if key == "MARS":
            parts.append(f"MARS={'on' if value == 'True' else 'off'}")
            continue
        if key == "Parallelism" and result.runner == "ConnectionPoolRampRunner":
            parts.append(f"Connections={value}")
            continue
        parts.append(f"{PARAMETER_LABELS.get(key, key)}={value}")
    return ", ".join(parts)


def format_ratio(ratio: float) -> str:
    if ratio >= 2:
        return f"{ratio:.1f}x"
    if ratio >= 1.1:
        return f"{ratio:.2f}x"
    return f"{ratio:.3f}x"


def speed_change_label(speedup: float) -> str:
    if abs(speedup - 1) < 0.0005:
        return "1.00x (same)"
    if speedup > 1:
        return f"{format_ratio(speedup)} faster"
    return f"{format_ratio(1 / speedup)} slower"


def format_list(values: Iterable[object]) -> str:
    items = [str(value) for value in values]
    if len(items) == 1:
        return items[0]
    if len(items) == 2:
        return f"{items[0]} and {items[1]}"
    return f"{', '.join(items[:-1])}, and {items[-1]}"


def format_confirmation(value: str) -> str:
    match = re.fullmatch(r"(\d+)/(\d+)", value)
    if match is None:
        return value
    return f"{match.group(1)} of {match.group(2)} runs"


def build_regression_notes(
    pool_results: list[Result],
    platform: str,
) -> tuple[dict[Result, str], list[str]]:
    primary_regressions = {
        result for result in pool_results if result.timing_delta > 10
    }
    note_numbers: dict[Result, str] = {}
    notes: list[str] = []

    def add_note(group: list[Result], text: str) -> None:
        if not group:
            return
        note_number = str(len(notes) + 1)
        notes.append(text)
        for result in group:
            note_numbers[result] = note_number

    churn = sorted(
        (
            result
            for result in primary_regressions
            if result.runner == "ConnectionPoolChurnRunner"
            and result.benchmark
            in {
                "RapidOpenCloseSingleThread",
                "RapidOpenCloseSingleThreadAsync",
            }
        ),
        key=lambda result: (result.benchmark, int(result.params["PoolDepth"])),
    )
    if churn:
        depths = format_list(int(result.params["PoolDepth"]) for result in churn)
        modes = format_list(
            sorted(
                {
                    "async" if result.benchmark.endswith("Async") else "sync"
                    for result in churn
                }
            )
        )
        add_note(
            churn,
            (
                f"{platform}: {modes} single-thread churn at depths {depths} exposes reuse order: "
                "the legacy LIFO stack reuses one hot connection; the FIFO channel "
                "rotates through the idle set, reducing locality."
            ),
        )

    multi_command = sorted(
        (
            result
            for result in primary_regressions
            if result.runner == "ConnectionPoolStressRunner"
            and result.benchmark == "MultiCommandReuse"
        ),
        key=lambda result: result.parameters,
    )
    if multi_command:
        scenarios = format_list(compact_parameters(result) for result in multi_command)
        confirmations = format_list(
            sorted({format_confirmation(result.confirmation) for result in multi_command})
        )
        add_note(
            multi_command,
            (
                f"{platform}: multi-command reuse ({scenarios}) checks out once, then runs 5-15 SQL "
                f"commands. SQL/server time dominates; the regression reproduced in {confirmations}."
            ),
        )

    bursty = sorted(
        (
            result
            for result in primary_regressions
            if result.runner == "ConnectionPoolStressRunner"
            and result.benchmark == "BurstyTrafficPattern"
        ),
        key=lambda result: result.parameters,
    )
    if bursty:
        scenarios = format_list(compact_parameters(result) for result in bursty)
        confirmations = format_list(
            sorted({format_confirmation(result.confirmation) for result in bursty})
        )
        add_note(
            bursty,
            (
                f"{platform}: bursty traffic ({scenarios}) runs 1-5 SQL queries per "
                f"checkout in waves. SQL/server variance dominates; the slowdown "
                f"appeared in {confirmations}."
            ),
        )

    thread_pressure = sorted(
        (
            result
            for result in primary_regressions
            if result.runner == "ConnectionPoolThreadPoolPressureRunner"
            and result.benchmark == "SaturatedSyncOpenOnThreadPool"
        ),
        key=lambda result: int(result.params["MinWorkerThreads"]),
    )
    if thread_pressure:
        first = thread_pressure[0]
        regressed_floors = {
            int(result.params["MinWorkerThreads"]) for result in thread_pressure
        }
        control_floors = sorted(
            {
                int(result.params["MinWorkerThreads"])
                for result in pool_results
                if result.runner == first.runner
                and result.benchmark == first.benchmark
                and result.params["Parallelism"] == first.params["Parallelism"]
                and result.params["MaxPoolSize"] == first.params["MaxPoolSize"]
                and int(result.params["MinWorkerThreads"]) not in regressed_floors
            }
        )
        add_note(
            thread_pressure,
            (
                f"{platform}: with {format_list(sorted(regressed_floors))} minimum worker threads, "
                f"{first.params['Parallelism']} sync callers competing for "
                f"{first.params['MaxPoolSize']} connections starve queued channel "
                f"continuations. Results return near parity at worker floors "
                f"{format_list(control_floors)}."
            ),
        )

    missing = primary_regressions.difference(note_numbers)
    if missing:
        details = ", ".join(
            f"{result.runner}.{result.benchmark} {result.parameters}"
            for result in sorted(missing, key=lambda result: result.rank)
        )
        raise ValueError(f"No explanatory note for primary pool regressions: {details}")

    return note_numbers, notes


def add_header(svg: Svg, title: str, subtitle: str) -> None:
    svg.text(64, 68, title, size=38, weight=700)
    svg.text(64, 108, subtitle, size=20, fill=MUTED)


def make_full_results_chart(
    linux_results: list[Result],
    windows_results: list[Result],
    output: Path,
) -> None:
    key = lambda result: (result.runner, result.benchmark, result.parameters)
    linux_pool = [result for result in linux_results if is_pool_related(result)]
    windows_pool = [result for result in windows_results if is_pool_related(result)]
    linux_by_key = {key(result): result for result in linux_pool}
    windows_by_key = {key(result): result for result in windows_pool}
    if linux_by_key.keys() != windows_by_key.keys():
        raise ValueError("Linux and Windows pool benchmark matrices do not match")

    pairs = [
        (linux_by_key[result_key], windows_by_key[result_key])
        for result_key in linux_by_key
    ]
    pairs.sort(
        key=lambda pair: (
            -statistics.mean(result.speedup for result in pair),
            pair[0].runner,
            pair[0].benchmark,
            pair[0].parameters,
        )
    )

    linux_note_numbers, linux_notes = build_regression_notes(linux_pool, "Linux")
    windows_local_numbers, windows_notes = build_regression_notes(
        windows_pool,
        "Windows",
    )
    note_offset = len(linux_notes)
    windows_note_numbers = {
        result: str(int(number) + note_offset)
        for result, number in windows_local_numbers.items()
    }
    regression_notes = linux_notes + windows_notes

    def sql_summary(pool_results: list[Result]) -> tuple[float, int, int]:
        sql_results = [
            result for result in pool_results if is_sql_or_hold_heavy(result)
        ]
        return (
            statistics.median(result.speedup for result in sql_results),
            sum(0.9 <= result.speedup <= 1.1 for result in sql_results),
            len(sql_results),
        )

    linux_sql_median, linux_sql_near, linux_sql_count = sql_summary(linux_pool)
    windows_sql_median, windows_sql_near, windows_sql_count = sql_summary(
        windows_pool
    )

    width = 2000
    first_y = 178
    row_height = 64
    footer_height = 226 + len(regression_notes) * 26
    chart_height = first_y + len(pairs) * row_height + footer_height
    title = "Full benchmark results: Linux and Windows"
    svg = Svg(width, chart_height, title)
    svg.text(64, 64, title, size=38, weight=700)
    svg.text(
        64,
        98,
        (
            f"{len(pairs)} matched benchmark and parameter rows, sorted by mean "
            "Linux/Windows speedup."
        ),
        size=17,
        fill=MUTED,
    )

    label_x = 68
    tag_x = 680
    linux_bar_x = 900
    windows_bar_x = 1500
    bar_width = 360
    all_results = [result for pair in pairs for result in pair]
    min_scale = max(
        0.0,
        math.floor(min(result.speedup for result in all_results) * 2) / 2,
    )
    max_scale = float(math.ceil(max(result.speedup for result in all_results)))

    def value_x(bar_x: float, value: float) -> float:
        return (
            bar_x
            + (value - min_scale)
            / (max_scale - min_scale)
            * bar_width
        )

    ticks = [min_scale, 1.0]
    ticks.extend(float(value) for value in range(2, int(max_scale) + 1, 2))
    if ticks[-1] != max_scale:
        ticks.append(max_scale)
    ticks = list(dict.fromkeys(ticks))

    svg.text(label_x, 132, "Benchmark scenario", size=16, fill=MUTED, weight=700)
    for platform, bar_x in (("Linux", linux_bar_x), ("Windows", windows_bar_x)):
        svg.text(
            bar_x + bar_width / 2,
            132,
            f"{platform} speedup",
            size=16,
            fill=MUTED,
            weight=700,
            anchor="middle",
        )
        for tick in ticks:
            tick_x = value_x(bar_x, tick)
            svg.text(
                tick_x,
                158,
                f"{tick:g}x",
                size=12,
                fill=MUTED,
                anchor="middle",
            )
            svg.line(
                tick_x,
                first_y - 2,
                tick_x,
                first_y + len(pairs) * row_height - 10,
                stroke=GRID,
                dash="4 8",
            )

    def draw_platform_result(
        result: Result,
        bar_x: float,
        y: float,
        note_number: str | None,
    ) -> None:
        parity_x = value_x(bar_x, 1)
        endpoint_x = value_x(bar_x, result.speedup)
        bar_fill = NEW_POOL if result.speedup >= 1 else RED
        svg.rect(bar_x, y + 8, bar_width, 24, fill=TRACK, rx=6)
        if result.speedup >= 1:
            filled_x = parity_x
            filled_width = max(endpoint_x - parity_x, 1.5)
            label_position = endpoint_x + 8
            label_anchor = "start"
        else:
            filled_width = max(parity_x - endpoint_x, 1.5)
            filled_x = parity_x - filled_width
            label_position = endpoint_x - 8
            label_anchor = "end"
        svg.rect(filled_x, y + 8, filled_width, 24, fill=bar_fill, rx=6)
        svg.text(
            label_position,
            y + 27,
            speed_change_label(result.speedup),
            size=12,
            fill=bar_fill,
            weight=700,
            anchor=label_anchor,
            superscript=note_number,
        )
        svg.text(
            bar_x + bar_width / 2,
            y + 53,
            (
                f"{format_mean(result.legacy)} -> "
                f"{format_mean(result.new_pool)} ms"
            ),
            size=12,
            fill=MUTED,
            anchor="middle",
        )

    for index, (linux_result, windows_result) in enumerate(pairs):
        y = first_y + index * row_height
        if is_sql_or_hold_heavy(linux_result):
            svg.rect(54, y + 2, width - 108, row_height - 4, fill=SQL_ROW, rx=7)
            svg.rect(tag_x, y + 8, 120, 20, fill="#dbeafe", rx=10)
            svg.text(
                tag_x + 60,
                y + 23,
                "SQL/hold-heavy",
                size=10,
                fill=SQL_TEXT,
                weight=700,
                anchor="middle",
            )

        svg.text(
            label_x,
            y + 20,
            benchmark_label(linux_result),
            size=15,
            weight=600,
        )
        svg.text(
            label_x,
            y + 43,
            compact_parameters(linux_result),
            size=12,
            fill=MUTED,
        )
        draw_platform_result(
            linux_result,
            linux_bar_x,
            y,
            linux_note_numbers.get(linux_result),
        )
        draw_platform_result(
            windows_result,
            windows_bar_x,
            y,
            windows_note_numbers.get(windows_result),
        )

    footer_y = first_y + len(pairs) * row_height + 28
    svg.text(
        64,
        footer_y,
        (
            f"{len(pairs)} matched pool-focused rows; pooling-disabled controls omitted. "
            f"Linux SQL/hold median {format_ratio(linux_sql_median)} "
            f"({linux_sql_near}/{linux_sql_count} within 0.9x-1.1x); "
            f"Windows {format_ratio(windows_sql_median)} "
            f"({windows_sql_near}/{windows_sql_count})."
        ),
        size=15,
        fill=MUTED,
    )
    svg.text(
        64,
        footer_y + 26,
        (
            "Speedup is calculated within each platform: legacy mean / new-pool "
            "mean. 1.0x is parity."
        ),
        size=15,
        fill=MUTED,
    )
    svg.text(
        64,
        footer_y + 52,
        f"SQL/hold-heavy = {SQL_HOLD_DESCRIPTION}.",
        size=15,
        fill=MUTED,
    )
    notes_top = footer_y + 80
    svg.line(64, notes_top, width - 64, notes_top, stroke=GRID)
    svg.text(
        64,
        notes_top + 30,
        "Notes on platform-specific timing regressions greater than 1.1x slower",
        size=16,
        fill=TEXT,
        weight=700,
    )
    for index, note in enumerate(regression_notes, start=1):
        svg.text(
            64,
            notes_top + 32 + index * 26,
            f"{index}. {note}",
            size=13,
            fill=MUTED,
        )
    svg.save(output)


def draw_grouped_mean_panel(
    svg: Svg,
    *,
    x: float,
    y: float,
    width: float,
    height: float,
    title: str,
    data: list[tuple[int, Result]],
    y_max: float,
    tick_step: float = 50,
    x_axis_label: str = "Parallelism",
) -> None:
    svg.rect(x, y, width, height, fill=PANEL, stroke=GRID, rx=16)
    svg.text(x + 28, y + 42, title, size=24, weight=700)
    legend = [
        ("Legacy pool", LEGACY),
        ("New pool", NEW_POOL),
    ]
    legend_start = x + width - 380
    for index, (label, color) in enumerate(legend):
        item_x = legend_start + index * 190
        svg.rect(item_x, y + 24, 18, 18, fill=color, rx=4)
        svg.text(item_x + 28, y + 40, label, size=15, fill=MUTED)

    plot_left = x + 86
    plot_right = x + width - 28
    plot_top = y + 78
    plot_bottom = y + height - 100
    plot_height = plot_bottom - plot_top
    svg.rotated_text(
        x + 25,
        (plot_top + plot_bottom) / 2,
        "Mean time (ms)",
        size=15,
        fill=MUTED,
    )
    tick_count = int(round(y_max / tick_step))
    for tick_index in range(tick_count + 1):
        tick = tick_index * tick_step
        tick_y = plot_bottom - plot_height * tick / y_max
        svg.line(plot_left, tick_y, plot_right, tick_y, stroke=GRID)
        svg.text(
            plot_left - 14,
            tick_y + 6,
            f"{tick:g}",
            size=14,
            fill=MUTED,
            anchor="end",
        )

    group_width = (plot_right - plot_left) / len(data)
    bar_width = 76
    bar_gap = 12
    total_bar_width = 2 * bar_width + bar_gap
    for index, (parallelism, result) in enumerate(data):
        center = plot_left + group_width * (index + 0.5)
        first_bar_x = center - total_bar_width / 2
        series = [
            (result.legacy, LEGACY, MUTED, 600),
            (result.new_pool, NEW_POOL, NEW_POOL, 700),
        ]
        for series_index, (value, color, text_color, weight) in enumerate(series):
            bar_x = first_bar_x + series_index * (bar_width + bar_gap)
            bar_height = plot_height * value / y_max
            svg.rect(
                bar_x,
                plot_bottom - bar_height,
                bar_width,
                bar_height,
                fill=color,
                rx=5,
            )
            svg.text(
                bar_x + bar_width / 2,
                plot_bottom - bar_height - 10,
                format_mean(value),
                size=13,
                fill=text_color,
                weight=weight,
                anchor="middle",
            )
        svg.text(center, plot_bottom + 32, str(parallelism), size=17, weight=600, anchor="middle")

    svg.text(
        x + width / 2,
        y + height - 24,
        x_axis_label,
        size=16,
        fill=MUTED,
        anchor="middle",
    )


def make_cold_start_chart(
    linux_results: list[Result],
    windows_results: list[Result],
    output: Path,
    *,
    async_mode: bool,
) -> None:
    linux_ramp_results = [
        result
        for result in linux_results
        if result.runner == "ConnectionPoolRampRunner"
    ]
    windows_ramp_results = [
        result
        for result in windows_results
        if result.runner == "ConnectionPoolRampRunner"
    ]
    all_ramp_results = linux_ramp_results + windows_ramp_results
    max_pool_size = max(
        int(result.params["MaxPoolSize"]) for result in all_ramp_results
    )
    parallelism_values = sorted(
        {
            int(result.params["Parallelism"])
            for result in linux_ramp_results
            if int(result.params["MaxPoolSize"]) == max_pool_size
        }
    )
    benchmark = "ColdStartRampAsync" if async_mode else "ColdStartRamp"
    def platform_data(platform_results: list[Result]) -> list[tuple[int, Result]]:
        return [
            (
                parallelism,
                find(
                    platform_results,
                    "ConnectionPoolRampRunner",
                    benchmark,
                    Parallelism=parallelism,
                    MaxPoolSize=max_pool_size,
                ),
            )
            for parallelism in parallelism_values
        ]

    linux_data = platform_data(linux_results)
    windows_data = platform_data(windows_results)
    tick_step = 100
    y_max = math.ceil(
        max(result.legacy for result in all_ramp_results) / tick_step
    ) * tick_step
    mode = "async" if async_mode else "sync"
    linux_peak = max(result.speedup for _, result in linux_data)
    windows_peak = max(result.speedup for _, result in windows_data)

    title = f"Cold-start pool ramp, {mode}"
    svg = Svg(1600, 820, title)
    add_header(
        svg,
        title,
        (
            f"Linux up to {format_ratio(linux_peak)} faster; Windows up to "
            f"{format_ratio(windows_peak)} faster. Opens physical capacity from "
            f"a cold pool; max pool size {max_pool_size}."
        ),
    )

    draw_grouped_mean_panel(
        svg,
        x=64,
        y=145,
        width=720,
        height=650,
        title="Linux",
        data=linux_data,
        y_max=y_max,
        tick_step=tick_step,
        x_axis_label="Physical connections opened",
    )
    draw_grouped_mean_panel(
        svg,
        x=816,
        y=145,
        width=720,
        height=650,
        title="Windows",
        data=windows_data,
        y_max=y_max,
        tick_step=tick_step,
        x_axis_label="Physical connections opened",
    )
    svg.save(output)


def make_rapid_fire_chart(
    linux_results: list[Result],
    windows_results: list[Result],
    output: Path,
    max_pool_size: int,
    *,
    async_mode: bool,
) -> None:
    parallelism_values = sorted(
        {
            int(result.params["Parallelism"])
            for result in linux_results
            if result.runner == "ConnectionPoolStressRunner"
            and result.benchmark.startswith("RapidFireOpenClose")
            and int(result.params["MaxPoolSize"]) == max_pool_size
        }
    )
    benchmark = "RapidFireOpenCloseAsync" if async_mode else "RapidFireOpenCloseSync"
    def platform_data(platform_results: list[Result]) -> list[tuple[int, Result]]:
        return [
            (
                parallelism,
                find(
                    platform_results,
                    "ConnectionPoolStressRunner",
                    benchmark,
                    Parallelism=parallelism,
                    MaxPoolSize=max_pool_size,
                ),
            )
            for parallelism in parallelism_values
        ]

    linux_data = platform_data(linux_results)
    windows_data = platform_data(windows_results)
    all_rapid_fire_results = [
        result
        for result in linux_results + windows_results
        if result.runner == "ConnectionPoolStressRunner"
        and result.benchmark.startswith("RapidFireOpenClose")
        and int(result.params["MaxPoolSize"]) == max_pool_size
    ]
    tick_step = 0.5
    y_max = math.ceil(
        max(
            max(result.legacy, result.new_pool)
            for result in all_rapid_fire_results
        )
        / tick_step
    ) * tick_step
    mode = "async" if async_mode else "sync"
    linux_peak = max(result.speedup for _, result in linux_data)
    windows_peak = max(result.speedup for _, result in windows_data)

    title = f"Rapid-fire open/close, {mode}"
    svg = Svg(1600, 820, title)
    add_header(
        svg,
        title,
        (
            f"Linux up to {format_ratio(linux_peak)} faster; Windows up to "
            f"{format_ratio(windows_peak)} faster. Hot-path checkout/return under "
            f"concurrent churn; max pool size {max_pool_size}."
        ),
    )

    draw_grouped_mean_panel(
        svg,
        x=64,
        y=145,
        width=720,
        height=650,
        title="Linux",
        data=linux_data,
        y_max=y_max,
        tick_step=tick_step,
    )
    draw_grouped_mean_panel(
        svg,
        x=816,
        y=145,
        width=720,
        height=650,
        title="Windows",
        data=windows_data,
        y_max=y_max,
        tick_step=tick_step,
    )
    svg.save(output)


def make_max_pool_rapid_fire_chart(
    linux_results: list[Result],
    windows_results: list[Result],
    output: Path,
    *,
    async_mode: bool,
) -> None:
    max_pool_size = max(
        int(result.params["MaxPoolSize"])
        for result in linux_results
        if result.runner == "ConnectionPoolStressRunner"
        and result.benchmark.startswith("RapidFireOpenClose")
    )
    make_rapid_fire_chart(
        linux_results,
        windows_results,
        output,
        max_pool_size,
        async_mode=async_mode,
    )


def make_dashboard(
    linux_results: list[Result],
    windows_results: list[Result],
    output: Path,
    chart_names: list[str],
) -> None:
    pool_results = [
        result for result in linux_results if is_pool_related(result)
    ]

    def parameter_values(runner: str, key: str, benchmarks=()) -> list[int]:
        return sorted(
            {
                int(result.params[key])
                for result in pool_results
                if result.runner == runner
                and (not benchmarks or result.benchmark in benchmarks)
                and key in result.params
            }
        )

    def values_text(values: Iterable[int]) -> str:
        return "/".join(str(value) for value in values)

    cold_connections = values_text(
        parameter_values(
            "ConnectionPoolRampRunner",
            "Parallelism",
            ("ColdStartRamp", "ColdStartRampAsync"),
        )
    )
    cold_pool_sizes = values_text(
        parameter_values("ConnectionPoolRampRunner", "MaxPoolSize")
    )
    rapid_parallelism = values_text(
        parameter_values(
            "ConnectionPoolStressRunner",
            "Parallelism",
            ("RapidFireOpenCloseSync", "RapidFireOpenCloseAsync"),
        )
    )
    rapid_pool_sizes = values_text(
        parameter_values(
            "ConnectionPoolStressRunner",
            "MaxPoolSize",
            ("RapidFireOpenCloseSync", "RapidFireOpenCloseAsync"),
        )
    )
    churn_depths = values_text(
        parameter_values("ConnectionPoolChurnRunner", "PoolDepth")
    )
    churn_ops = values_text(
        parameter_values("ConnectionPoolChurnRunner", "OpsPerInvocation")
    )
    contention_parallelism = values_text(
        parameter_values("ConnectionPoolContentionRunner", "Parallelism")
    )
    contention_pool_sizes = values_text(
        parameter_values("ConnectionPoolContentionRunner", "MaxPoolSize")
    )
    contention_ops = values_text(
        parameter_values("ConnectionPoolContentionRunner", "OpsPerWorker")
    )
    thread_floors = values_text(
        parameter_values(
            "ConnectionPoolThreadPoolPressureRunner",
            "MinWorkerThreads",
        )
    )
    thread_parallelism = values_text(
        parameter_values(
            "ConnectionPoolThreadPoolPressureRunner",
            "Parallelism",
        )
    )
    thread_pool_sizes = values_text(
        parameter_values(
            "ConnectionPoolThreadPoolPressureRunner",
            "MaxPoolSize",
        )
    )
    stress_parallelism = values_text(
        parameter_values("ConnectionPoolStressRunner", "Parallelism")
    )
    stress_pool_sizes = values_text(
        parameter_values("ConnectionPoolStressRunner", "MaxPoolSize")
    )
    concurrent_opens = values_text(
        parameter_values("ParallelAsyncConnectionRunner", "Concurrency")
    )

    benchmark_rows = [
        (
            "ColdStartRamp / ColdStartRampAsync",
            f"Connections {cold_connections}; max pool {cold_pool_sizes}",
            "Clears the pool, opens the requested number of physical connections, and "
            "holds each until all callers connect. Measures cold startup and scale-out; "
            "sync uses dedicated threads and async uses tasks.",
        ),
        (
            "RapidFireOpenCloseSync / Async",
            f"Parallelism {rapid_parallelism}; max pool {rapid_pool_sizes}",
            "Uses a fully pre-warmed pool and repeatedly checks connections out and "
            "returns them immediately. This is a zero-hold-time worst case for the hot "
            "acquire/return and waiter handoff paths.",
        ),
        (
            "RapidOpenCloseSingleThread / Async",
            f"{churn_ops} operations; pool depths {churn_depths}",
            "Runs repeated checkout/return on one caller with no contention. Isolates "
            "per-operation pool overhead and the reuse-locality difference between LIFO "
            "and FIFO idle-connection ordering.",
        ),
        (
            "SteadyStateOpenQueryClose variants",
            (
                f"Parallelism {contention_parallelism}; max pool "
                f"{contention_pool_sizes}; {contention_ops} operations/worker"
            ),
            "Uses a pre-warmed pool for open, SELECT 1, and close loops. Pool sizes above, "
            "equal to, and below demand separate hot-path throughput from waiter "
            "back-pressure; variants use sync, async, or dedicated threads.",
        ),
        (
            "SaturatedSyncOpenOnThreadPool",
            (
                f"Parallelism {thread_parallelism}; pool {thread_pool_sizes}; "
                f"worker floors {thread_floors}"
            ),
            "Runs blocking Open calls on thread-pool threads against a saturated pool. "
            "Sweeping the minimum worker count exposes continuation starvation and "
            "thread-injection sensitivity.",
        ),
        (
            "RandomizedHoldAndQuery",
            f"Parallelism {stress_parallelism}; max pool {stress_pool_sizes}",
            "Holds each checkout for a random 0-50 ms and executes a small query about "
            "half the time. Represents mixed application hold times.",
        ),
        (
            "MixedSyncAsyncContention",
            f"Parallelism {stress_parallelism}; max pool {stress_pool_sizes}",
            "Splits workers between sync and async open/query/close loops to exercise "
            "both checkout paths against the same pool.",
        ),
        (
            "MultiCommandReuse",
            f"Parallelism {stress_parallelism}; max pool {stress_pool_sizes}",
            "Checks out once per worker and executes 5-15 sequential commands before "
            "returning the connection. Models multi-step ORM or unit-of-work usage.",
        ),
        (
            "PoolExhaustionRecovery",
            f"Parallelism {stress_parallelism}; max pool {stress_pool_sizes}",
            "Starts at least twice as many tasks as the pool can serve, runs a query, "
            "and holds connections for 10-100 ms. Measures queued-waiter back-pressure "
            "and recovery.",
        ),
        (
            "BurstyTrafficPattern",
            f"Parallelism {stress_parallelism}; max pool {stress_pool_sizes}",
            "Runs five waves of concurrent checkouts, each executing 1-5 queries, with "
            "a short pause between waves. Models clustered request traffic.",
        ),
        (
            "OpenConnectionsConcurrently",
            f"Concurrent opens {concurrent_opens}; pooling enabled",
            "Starts many OpenAsync calls together and disposes each connection after it "
            "opens. Measures concurrent connection acquisition with pooling enabled.",
        ),
        (
            "OpenConnection / OpenAsyncConnection",
            "Pooling enabled; MARS on/off",
            "Measures the basic sync and async pooled-open API path for a single "
            "connection, with and without Multiple Active Result Sets.",
        ),
    ]
    benchmark_rows_html = "".join(
        (
            "<tr>"
            f"<td><code>{html.escape(name)}</code></td>"
            f"<td>{html.escape(parameters)}</td>"
            f"<td>{html.escape(description)}</td>"
            "</tr>"
        )
        for name, parameters, description in benchmark_rows
    )

    figures_html = "".join(
        (
            '<figure class="chart">'
            f'<a href="{html.escape(name)}" target="_blank">'
            f'<img src="{html.escape(name)}" alt="{html.escape(Path(name).stem)}">'
            "</a>"
            "</figure>"
        )
        for name in chart_names
    )
    document = f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Connection Pool Performance: Legacy vs. New on Linux and Windows</title>
  <style>
    :root {{
      color-scheme: light;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
      color: #0f172a;
      background: #eef2f7;
    }}
    * {{ box-sizing: border-box; }}
    body {{ margin: 0; }}
    main {{ max-width: 1700px; margin: 0 auto; padding: 48px 32px 64px; }}
    h1 {{ margin: 0 0 28px; font-size: 42px; letter-spacing: -0.03em; }}
    .charts {{ display: grid; grid-template-columns: 1fr; gap: 24px; }}
    .chart {{
      margin: 0;
      background: white;
      border: 1px solid #dbe4ee;
      border-radius: 16px;
      overflow: hidden;
      box-shadow: 0 8px 24px rgba(15, 23, 42, 0.05);
    }}
    .chart img {{ display: block; width: 100%; height: auto; }}
    .downloads {{ margin-top: 16px; color: #64748b; font-size: 14px; }}
    .methodology {{
      margin-top: 32px;
      padding: 30px;
      background: white;
      border: 1px solid #dbe4ee;
      border-radius: 16px;
      box-shadow: 0 8px 24px rgba(15, 23, 42, 0.05);
    }}
    .methodology h2 {{ margin: 0 0 22px; font-size: 30px; }}
    .methodology h3 {{ margin: 0 0 14px; font-size: 20px; }}
    .method-grid {{
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 22px;
      margin-bottom: 28px;
    }}
    .method-card {{
      padding: 20px 22px;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 12px;
    }}
    .method-card ul {{ margin: 0; padding-left: 21px; }}
    .method-card li {{ margin: 8px 0; line-height: 1.45; }}
    .environment {{
      display: grid;
      grid-template-columns: max-content 1fr;
      gap: 9px 16px;
      margin: 0;
    }}
    .environment dt {{ font-weight: 700; }}
    .environment dd {{ margin: 0; color: #475569; line-height: 1.4; }}
    .table-wrap {{ overflow-x: auto; }}
    .benchmark-table {{
      width: 100%;
      border-collapse: collapse;
      font-size: 14px;
      line-height: 1.45;
    }}
    .benchmark-table th {{
      padding: 11px 12px;
      background: #f1f5f9;
      border-bottom: 2px solid #cbd5e1;
      text-align: left;
    }}
    .benchmark-table td {{
      padding: 12px;
      border-bottom: 1px solid #e2e8f0;
      vertical-align: top;
    }}
    .benchmark-table td:first-child {{ width: 25%; }}
    .benchmark-table td:nth-child(2) {{ width: 24%; color: #475569; }}
    code {{ font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; }}
    @media (max-width: 1050px) {{
      .method-grid {{ grid-template-columns: 1fr; }}
    }}
    @media (max-width: 650px) {{
      main {{ padding: 28px 14px 40px; }}
      h1 {{ font-size: 34px; }}
    }}
  </style>
</head>
<body>
  <main>
    <h1>Connection Pool Performance: Legacy vs. New on Linux and Windows</h1>
    <section class="charts">{figures_html}</section>
    <p class="downloads">Click any graph to open its full-resolution SVG.</p>
    <section class="methodology">
      <h2>Methodology and test coverage</h2>
      <div class="method-grid">
        <section class="method-card">
          <h3>Comparison method</h3>
          <ul>
            <li>Linux and Windows used the same 68 benchmark and parameter rows.
              Each platform ran the same Release build in separate processes with
              <code>UseConnectionPoolV2=false</code> for the legacy pool and
              <code>true</code> for the new channel pool.</li>
            <li>Benchmark units were interleaved legacy then new, back to back on the
              same host and reserved CPU set, to reduce machine-state drift.</li>
            <li>BenchmarkDotNet used the in-process MediumRun job targeting
              <code>net9.0</code>, Server GC, MemoryDiagnoser, and ThreadingDiagnoser.
              Benchmark-specific iteration and warmup counts came from the runner config.</li>
            <li>A slowdown above 10% was rerun across three total passes and marked
              confirmed only when it reproduced in a strict majority.</li>
            <li>Speedup is calculated within each platform as legacy mean divided by
              new-pool mean; 1.0x is parity. Absolute milliseconds should not be
              compared across Linux and Windows because they are separate machine runs.</li>
          </ul>
        </section>
        <section class="method-card">
          <h3>Perf lab environment</h3>
          <dl class="environment">
            <dt>Platforms</dt>
            <dd>Separate Linux and Windows client runs in the internal SqlClient
              performance lab on Azure Dedicated Hosts.</dd>
            <dt>Windows run</dt>
            <dd><a href="https://sqlclientdrivers.visualstudio.com/ADO.Net/_build/results?buildId=170431&amp;view=ms.vss-build-web.run-extensions-tab">Azure DevOps build 170431</a>.</dd>
            <dt>Linux run</dt>
            <dd>The previously supplied Linux comparison using the matching parameter
              matrix.</dd>
            <dt>CPU isolation</dt>
            <dd>The harness pins the benchmark client and SQL Server to disjoint CPU
              sets supplied by the perf-lab template.</dd>
            <dt>Runtime</dt>
            <dd><code>net9.0</code>, Release configuration, Server GC.</dd>
            <dt>SQL Server</dt>
            <dd>The perf-lab SQL instance and <code>sqlclient-perf-db</code> database.
              Each build records the exact CPU topology, SQL version, MAXDOP, memory,
              affinity, and tempdb layout in its diagnostics artifact.</dd>
          </dl>
        </section>
      </div>
      <h3>What each pool benchmark measures</h3>
      <div class="table-wrap">
        <table class="benchmark-table">
          <thead>
            <tr><th>Benchmark</th><th>Parameters in this run</th><th>Measured workload</th></tr>
          </thead>
          <tbody>{benchmark_rows_html}</tbody>
        </table>
      </div>
    </section>
  </main>
</body>
</html>
"""
    output.write_text(document, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(
        description=(
            "Generate dependency-free Linux and Windows pool performance graphs "
            "from two comparison reports."
        )
    )
    parser.add_argument("linux_input", type=Path)
    parser.add_argument("windows_input", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()

    linux_results = parse_results(args.linux_input)
    windows_results = parse_results(args.windows_input)
    args.output.mkdir(parents=True, exist_ok=True)

    charts = [
        (
            "01-cold-start-sync.svg",
            lambda linux, windows, path: make_cold_start_chart(
                linux,
                windows,
                path,
                async_mode=False,
            ),
        ),
        (
            "02-cold-start-async.svg",
            lambda linux, windows, path: make_cold_start_chart(
                linux,
                windows,
                path,
                async_mode=True,
            ),
        ),
        (
            "03-rapid-fire-sync.svg",
            lambda linux, windows, path: make_max_pool_rapid_fire_chart(
                linux,
                windows,
                path,
                async_mode=False,
            ),
        ),
        (
            "04-rapid-fire-async.svg",
            lambda linux, windows, path: make_max_pool_rapid_fire_chart(
                linux,
                windows,
                path,
                async_mode=True,
            ),
        ),
        ("05-full-benchmark-results.svg", make_full_results_chart),
    ]
    for filename, generator in charts:
        generator(linux_results, windows_results, args.output / filename)
    make_dashboard(
        linux_results,
        windows_results,
        args.output / "index.html",
        [name for name, _ in charts],
    )

    print(
        f"Parsed {len(linux_results)} Linux rows and "
        f"{len(windows_results)} Windows rows"
    )
    for filename, _ in charts:
        print(args.output / filename)
    print(args.output / "index.html")


if __name__ == "__main__":
    main()
