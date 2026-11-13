"""Coverage and graph semantics for the authenticated RTT report."""

import importlib.util
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET

SPEC = importlib.util.spec_from_file_location(
    "openasync_rtt_report", Path(__file__).resolve().parents[1] / "openasync_rtt_report.py")
REPORT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(REPORT)


class RttReportTests(unittest.TestCase):
    """Incomplete repetitions and mixed sources must not become a final health boundary."""

    def test_repeated_repetition_cannot_replace_missing_one(self):
        # Arrange
        manifest = dict(status="Completed", cases=[("w64", {})], rtt=[100], repetitions=3)
        rows = [dict(case="w64", rtt_ms=100, repetition=n, source="AuthenticatedSql")
                for n in (1, 2, 2)]
        # Act / Assert
        with self.assertRaises(ValueError):
            REPORT.validate_results(rows, manifest)

    def test_heatmap_names_metric_and_defines_every_health_color(self):
        # Act
        svg = REPORT.heatmap({}, ["case"], [100], "dispatch_p99_ms", "Health", "ms", True)
        # Assert
        ET.fromstring(svg)
        self.assertIn("Cell value: ThreadPool dispatch p99 (ms)", svg)
        for label in ("Red:", "Green:", "Amber:", "Gray:"):
            self.assertIn(label, svg)
        self.assertIn("median of the per-run values", svg)

    def test_case_names_come_from_actual_settings_not_identifier(self):
        # Arrange
        config = dict(Pooling=True, PoolV2=False, Workers=32, PoolSize=8, WarmPool=True,
                      MinimumThreads=4, MaximumThreads=128, LongRunning=False, Query=False)
        # Act
        labels = REPORT.case_labels({"cases": [("opaque-id", config)]})
        svg = REPORT.heatmap({}, ["opaque-id"], [100], "dispatch_p99_ms",
                             "Health", "ms", True, labels)
        # Assert
        self.assertIn("Pooled V1 | 32 workers", svg)
        self.assertIn("Prewarmed pool | Pool max 8", svg)
        self.assertIn("TP min/max 4/128 | Open/close (no query)", svg)
        self.assertNotIn("opaque-id", svg)

    def test_synthetic_source_cannot_be_labeled_authenticated(self):
        # Arrange
        manifest = dict(status="Completed", cases=[("w64", {})], rtt=[100], repetitions=1)
        rows = [dict(case="w64", rtt_ms=100, repetition=1, source="SyntheticStalledPrelogin")]
        # Act / Assert
        with self.assertRaises(ValueError):
            REPORT.validate_results(rows, manifest)

    def test_one_failed_repetition_makes_low_median_cell_red(self):
        # Arrange
        rows = [dict(dispatch_p99_ms=value, missing_probes=0, status="Completed",
                     scheduler_health=verdict)
                for value, verdict in [(1, "Pass"), (2, "Pass"), (1000, "Fail")]]
        # Act
        svg = REPORT.heatmap({("w64", 100): rows}, ["w64"], [100],
                            "dispatch_p99_ms", "Scheduler", "ms", True)
        # Assert
        cells = ET.fromstring(svg).findall(".//*[@class='metric-cell']")
        self.assertEqual([cell.attrib["fill"] for cell in cells], ["#fecaca"])

    def test_missing_probe_cell_is_not_green(self):
        # Arrange
        rows = [dict(dispatch_p99_ms=1, missing_probes=64, status="Completed",
                     scheduler_health="Insufficient")]
        # Act
        svg = REPORT.heatmap({("w64", 100): rows}, ["w64"], [100],
                            "dispatch_p99_ms", "Scheduler", "ms", True)
        # Assert
        cells = ET.fromstring(svg).findall(".//*[@class='metric-cell']")
        self.assertEqual([cell.attrib["fill"] for cell in cells], ["#fef3c7"])
        self.assertIn("1.0 *", svg)

    def test_nonzero_rtt_plot_scale_excludes_zero_delay_outlier(self):
        # Arrange
        selected = {("w64", 100): [dict(completed_per_second=400)]}
        with_zero = dict(selected)
        with_zero[("w64", 0)] = [dict(completed_per_second=1000000)]
        # Act
        chart = REPORT.linechart(with_zero, ["w64"], [100],
                                 "completed_per_second", "Throughput", "cycles/s")
        expected = REPORT.linechart(selected, ["w64"], [100],
                                    "completed_per_second", "Throughput", "cycles/s")
        # Assert
        self.assertEqual(chart, expected)


if __name__ == "__main__":
    unittest.main()
