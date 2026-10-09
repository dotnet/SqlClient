# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Verify custom scorecard metrics survive baseline comparison and Kusto conversion."""

import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

import compare_perf
import perf_to_kusto


class CompareMetricsTests(unittest.TestCase):
    """Compare by stable IDs, preserving zero and missing values explicitly."""

    def report(self, directory, metrics):
        benchmark = {
            "Type": "ConnectivityLoadRunner",
            "Method": "OpenAsyncLoop",
            "Parameters": "Concurrency=16, Pooling=False",
            "Statistics": {"Mean": 1_000_000},
            "Metrics": [
                {
                    "Descriptor": {
                        "Id": metric_id,
                        "DisplayName": display_name,
                        "Legend": display_name,
                        "Unit": "workers",
                        "TheGreaterTheBetter": False,
                    },
                    "Value": value,
                }
                for metric_id, display_name, value in metrics
            ],
        }
        with open(os.path.join(directory, "load-report-full.json"), "w") as handle:
            json.dump({"Benchmarks": [benchmark]}, handle)
        return benchmark

    def test_metrics_match_by_id_not_display_name(self):
        with tempfile.TemporaryDirectory() as baseline, tempfile.TemporaryDirectory() as current:
            self.report(baseline, [("AverageOccupiedWorkers", "Old label", 8)])
            self.report(current, [("AverageOccupiedWorkers", "Avg workers", 2)])
            entries = compare_perf.build_comparison(baseline, current, 10)
        self.assertEqual(entries[0]["status"], "unchanged")
        metric = entries[0]["metrics"][0]
        self.assertEqual(metric["baselineValue"], 8)
        self.assertEqual(metric["currentValue"], 2)
        self.assertEqual(metric["deltaPct"], -75)
        markdown = compare_perf.render_markdown(entries, "baseline", 10)
        self.assertIn("## Diagnoser metrics", markdown)
        self.assertIn("Avg workers", markdown)
        self.assertIn("-75.00%", markdown)

    def test_zero_and_one_sided_metrics_remain_visible(self):
        with tempfile.TemporaryDirectory() as baseline, tempfile.TemporaryDirectory() as current:
            self.report(baseline, [("zero", "Zero", 0), ("removed", "Removed", 4)])
            self.report(current, [("zero", "Zero", 2), ("new", "New", 3)])
            entries = compare_perf.build_comparison(baseline, current, 10)
        metrics = {metric["id"]: metric for metric in entries[0]["metrics"]}
        self.assertEqual(metrics["zero"]["baselineValue"], 0)
        self.assertEqual(metrics["zero"]["currentValue"], 2)
        self.assertIsNone(metrics["zero"]["deltaPct"])
        self.assertIsNone(metrics["new"]["baselineValue"])
        self.assertIsNone(metrics["removed"]["currentValue"])
        markdown = compare_perf.render_markdown(entries, "baseline", 10)
        self.assertIn("| 0.0000 | 2.0000 | - |", markdown)

    def test_kusto_converter_preserves_scorecard_values(self):
        with tempfile.TemporaryDirectory() as directory:
            benchmark = self.report(directory, [("AverageOccupiedWorkers", "Avg workers", 0)])
        self.assertEqual(perf_to_kusto._driver_specific_metrics(benchmark)["Avg workers"], 0)

    def test_existing_reports_without_metrics_keep_timing_comparison(self):
        with tempfile.TemporaryDirectory() as baseline, tempfile.TemporaryDirectory() as current:
            self.report(baseline, [])
            self.report(current, [])
            entries = compare_perf.build_comparison(baseline, current, 10)
        self.assertEqual(entries[0]["metrics"], [])
        self.assertNotIn("## Diagnoser metrics", compare_perf.render_markdown(entries, "baseline", 10))


if __name__ == "__main__":
    unittest.main()
