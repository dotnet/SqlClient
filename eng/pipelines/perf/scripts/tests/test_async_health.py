"""Deterministic async-health attribution, reporting and orchestration contracts."""

import json
import io
from contextlib import redirect_stderr, redirect_stdout
from collections import defaultdict
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import async_health_report as health
import compare_perf
import interleave_perf
import perf_to_kusto


def sample_row():
    return dict(
        SchemaVersion=4, MeasurementMode="async-health", Status="Completed", Benchmark="Runner.Method",
        Parameters="Parallelism=4", ProcessRepetition=1, Window=1, Invocations=1,
        CompletedInvocations=1, ProcessId=123, CaseId="case", RunId="run",
        OperationsPerInvoke=1, Policy="ConfiguredInvocationBatch", Configuration={},
        Options=dict(MinimumWindowMilliseconds=100, MinimumSamples=100),
        Measurement=dict(
            WorkloadStartMilliseconds=0, WorkloadEndMilliseconds=1000, RecoveryEndMilliseconds=1500,
            ObserverError=None, Resources=[dict(TimestampMilliseconds=5, BusyWorkerProxy=4, Threads=8, QueueDepth=3)],
            Probes=dict(Total=100, Posted=100, Executed=100, Dropped=0, Rejected=0, Skipped=0, ProducerMissed=0, Outstanding=0,
                        Samples=[dict(ScheduledMilliseconds=i * 5, PostedMilliseconds=i * 5 + 1,
                                      ExecutedMilliseconds=i * 5 + 3) for i in range(100)])))


class AsyncHealthTests(unittest.TestCase):
    def test_collected_report_exports_all_available_statistics(self):
        """Pipeline artifacts expose diagnostics without replacing normal timing results."""
        # Arrange
        with tempfile.TemporaryDirectory() as root:
            directory = Path(root, "current", "BenchmarkDotNet.Artifacts", "async-health")
            directory.mkdir(parents=True)
            row = sample_row()
            row["Measurement"]["ProcessThreadsAtStart"] = 12
            row["Measurement"]["OutstandingAtWorkloadEnd"] = 0
            row["Measurement"]["ResourceSamplesDropped"] = 0
            row["Measurement"]["Resources"] = [
                dict(TimestampMilliseconds=5, BusyWorkerProxy=2, Threads=8,
                     ProcessThreads=20, QueueDepth=1, CompletedWorkItems=100,
                     AllocatedBytes=1000, Gen2Collections=2),
                dict(TimestampMilliseconds=500, BusyWorkerProxy=4, Threads=10,
                     ProcessThreads=44, QueueDepth=3, CompletedWorkItems=140,
                     AllocatedBytes=9000, Gen2Collections=3),
            ]
            (directory / "async-health-window.json").write_text(json.dumps(row))
            timing = Path(root, "summary.md")
            timing.write_text("Existing timing comparison")

            # Act
            output = subprocess.run(
                [sys.executable, str(Path(health.__file__)), "--results-dir", root],
                capture_output=True, text=True, timeout=30)

            # Assert
            self.assertEqual(output.returncode, 0, output.stdout + output.stderr)
            report = json.loads(Path(root, "async-health-summary.json").read_text())
            self.assertEqual(len(report["Windows"]), 1)
            result = report["Windows"][0]
            self.assertEqual(result["Variant"], "current")
            self.assertEqual(result["PeakQueueDepth"], 3)
            self.assertEqual(result["ResourceSampleCount"], 2)
            self.assertEqual(result["SampledCompletedWorkItemsDelta"], 40)
            self.assertEqual(result["SampledAllocatedBytesDelta"], 8000)
            self.assertEqual(result["SampledGen2CollectionsDelta"], 1)
            self.assertEqual(result["Work"]["DispatchMilliseconds"]["P50"], 2)
            self.assertEqual(timing.read_text(), "Existing timing comparison")
            text = Path(root, "async-health-summary.md").read_text()
            for label in ("P50", "P95", "P99", "Max", "Queue", "Allocated",
                          "Gen2", "Rejected", "Producer-missed", "Process threads"):
                self.assertIn(label, text)
            self.assertTrue(Path(root, "async-health-summary.csv").is_file())

    def test_collected_report_rejects_conflicting_window_copies(self):
        """Duplicate collection paths cannot silently select different versions of one window."""
        # Arrange
        with tempfile.TemporaryDirectory() as root:
            directory = Path(root, "current", "async-health")
            directory.mkdir(parents=True)
            row = sample_row()
            (directory / "async-health-one.json").write_text(json.dumps(row))
            row["Measurement"]["Resources"][0]["QueueDepth"] = 99
            (directory / "async-health-two.json").write_text(json.dumps(row))

            # Act
            output = subprocess.run(
                [sys.executable, str(Path(health.__file__)), "--results-dir", root],
                capture_output=True, text=True, timeout=30)

            # Assert
            self.assertNotEqual(output.returncode, 0)
            self.assertIn("Conflicting", output.stderr)

    def test_collected_report_deduplicates_identical_artifacts_and_can_be_regenerated(self):
        """Repeated collection and report generation retain exactly one row per window."""
        # Arrange
        with tempfile.TemporaryDirectory() as root:
            for name in ("async-health-one.json", "async-health-two.json"):
                directory = Path(root, "current", "async-health")
                directory.mkdir(parents=True, exist_ok=True)
                (directory / name).write_text(json.dumps(sample_row()))

            # Act
            first = health.write_collected_summary(root)
            second = health.write_collected_summary(root)

            # Assert
            self.assertEqual(first, 1)
            self.assertEqual(second, 1)
            self.assertEqual(
                len(json.loads(Path(root, "async-health-summary.json").read_text())["Windows"]), 1)

    def test_timing_only_collection_does_not_create_health_reports(self):
        """Unmarked timing-only workloads have no health observations to report."""
        # Arrange
        with tempfile.TemporaryDirectory() as root:
            timing = Path(root, "summary.md")
            timing.write_text("Timing comparison")

            # Act
            count = health.write_collected_summary(root)

            # Assert
            self.assertEqual(count, 0)
            self.assertEqual(timing.read_text(), "Timing comparison")
            self.assertFalse(Path(root, "async-health-summary.json").exists())

    def test_failed_window_without_measurement_is_reported_as_failed(self):
        """Failure sidecars remain visible even if the observer could not start."""
        # Arrange
        with tempfile.TemporaryDirectory() as root:
            directory = Path(root, "current", "async-health")
            directory.mkdir(parents=True)
            row = sample_row()
            row.update(Status="Failed", Error="No diagnostic window")
            del row["Measurement"]
            (directory / "async-health-failure.json").write_text(json.dumps(row))

            # Act
            count = health.write_collected_summary(root)

            # Assert
            self.assertEqual(count, 1)
            result = json.loads(Path(root, "async-health-summary.json").read_text())["Windows"][0]
            self.assertEqual(result["Coverage"], "Failed")
            self.assertIn("N/A", Path(root, "async-health-summary.md").read_text())

    def test_legacy_sampled_counters_remain_unavailable(self):
        """Missing historical counters must not be presented as zero activity."""
        # Act
        result = health.summarize(sample_row())

        # Assert
        self.assertIsNone(result["SampledCompletedWorkItemsDelta"])
        self.assertIsNone(result["SampledAllocatedBytesDelta"])
        self.assertIsNone(result["SampledGen2CollectionsDelta"])

    def test_legacy_scheduled_count_is_accepted(self):
        """Existing artifacts remain readable after renaming the total probe count."""
        # Arrange
        row = sample_row()
        row["SchemaVersion"] = 1
        probes = row["Measurement"]["Probes"]
        probes["Scheduled"] = probes.pop("Total")
        probes.update(Scheduled=114, Dropped=14, ProducerMissed=10)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["Count"], 100)
        self.assertEqual(result["SchemaVersion"], 2)
        self.assertEqual(result["DroppedProbes"], 4)
        self.assertEqual(result["ProducerMissedProbes"], 10)
        self.assertEqual(result["Measurement"]["Probes"]["Dropped"], 4)
        self.assertEqual(probes["Dropped"], 14)
        self.assertIsNone(result["SkippedProbes"])

    def test_legacy_total_count_converts_combined_drops(self):
        """Version-1 artifacts with the renamed Total field still use combined drops."""
        # Arrange
        row = sample_row()
        row["SchemaVersion"] = 1
        row["Measurement"]["Probes"].update(Total=110, Dropped=10, ProducerMissed=10)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["DroppedProbes"], 0)
        self.assertEqual(result["ProducerMissedProbes"], 10)

    def test_capacity_drops_report_counts_without_suppressing_percentiles(self):
        # Arrange
        row = sample_row()
        row["Measurement"]["Probes"].update(Total=110, Dropped=10)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["DroppedProbes"], 10)
        self.assertEqual(result["ProducerMissedProbes"], 0)
        self.assertEqual(result["AdmissionDroppedProbes"], 10)
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["Count"], 100)
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["P99"], 2)

    def test_sample_limit_skips_are_separate_from_drops_and_misses(self):
        """Skipped observations remain visible without suppressing executed-probe percentiles."""
        # Arrange
        row = sample_row()
        row["Measurement"]["Probes"].update(Total=115, Skipped=10, Dropped=3, ProducerMissed=2)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["SkippedProbes"], 10)
        self.assertEqual(result["DroppedProbes"], 3)
        self.assertEqual(result["ProducerMissedProbes"], 2)
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["P99"], 2)

    def test_dispatch_rejections_remain_separate_in_failed_windows(self):
        """Dispatch failures retain their rejection count without hiding observer failure."""
        # Arrange
        row = sample_row()
        row.update(Status="Failed", CompletedInvocations=None)
        row["Measurement"]["ObserverError"] = "InvalidOperationException"
        row["Measurement"]["Probes"].update(Total=101, Rejected=1)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Failed")
        self.assertEqual(result["RejectedProbes"], 1)
        self.assertEqual(result["DroppedProbes"], 0)
        self.assertEqual(result["Measurement"]["Probes"]["Posted"], 100)
        self.assertIsNone(result["Work"]["DispatchMilliseconds"]["P99"])

    def test_version_three_cannot_supply_rejected_counts(self):
        """Historical drops include dispatch failures whose count cannot be reconstructed."""
        # Arrange
        row = sample_row()
        row["SchemaVersion"] = 3
        row["Measurement"]["Probes"].update(Total=110, Dropped=10)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["DroppedProbes"], 10)
        self.assertIsNone(result["RejectedProbes"])
        self.assertNotIn("Rejected", result["Measurement"]["Probes"])

    def test_rejected_counts_must_reconcile(self):
        """A rejection must be represented in Total rather than counted as posted work."""
        # Arrange
        row = sample_row()
        row["Measurement"]["Probes"]["Rejected"] = 1

        # Act
        summarize = lambda: health.summarize(row)

        # Assert
        with self.assertRaisesRegex(ValueError, "accounting"):
            summarize()

    def test_version_two_cannot_supply_skipped_counts(self):
        """Historical drops include sample-limit rejections whose cause cannot be recovered."""
        # Arrange
        row = sample_row()
        row["SchemaVersion"] = 2
        row["Measurement"]["Probes"].update(Total=110, Dropped=10)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["DroppedProbes"], 10)
        self.assertIsNone(result["SkippedProbes"])
        self.assertNotIn("Skipped", result["Measurement"]["Probes"])

    def test_skipped_counts_must_reconcile(self):
        """Skipped probes must contribute to Total rather than becoming unaccounted observations."""
        # Arrange
        row = sample_row()
        row["Measurement"]["Probes"]["Skipped"] = 1

        # Act
        summarize = lambda: health.summarize(row)

        # Assert
        with self.assertRaisesRegex(ValueError, "accounting"):
            summarize()

    def test_producer_misses_report_counts_without_suppressing_percentiles(self):
        # Arrange
        row = sample_row()
        row["Measurement"]["Probes"].update(Total=110, ProducerMissed=10)

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["DroppedProbes"], 0)
        self.assertEqual(result["ProducerMissedProbes"], 10)
        self.assertEqual(result["AdmissionDroppedProbes"], 0)
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["P99"], 2)

    def test_process_threads_include_dedicated_threads_outside_pool(self):
        """Low pool occupancy must not hide a process-wide thread increase."""
        # Arrange
        row = sample_row()
        row["Measurement"]["ProcessThreadsAtStart"] = 12
        row["Measurement"]["Resources"] = [
            dict(TimestampMilliseconds=5, BusyWorkerProxy=2, Threads=8, QueueDepth=1, ProcessThreads=20),
            dict(TimestampMilliseconds=500, BusyWorkerProxy=2, Threads=8, QueueDepth=1, ProcessThreads=44),
            dict(TimestampMilliseconds=1200, BusyWorkerProxy=2, Threads=8, QueueDepth=1, ProcessThreads=60),
        ]

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["ProcessThreadsAtStart"], 12)
        self.assertEqual(result["PeakProcessThreads"], 44)
        self.assertEqual(result["ProcessThreadIncrease"], 32)
        self.assertEqual(result["PeakThreads"], 8)
        self.assertEqual(result["PeakBusyWorkerProxy"], 2)

    def test_legacy_resources_do_not_claim_zero_process_threads(self):
        """Older artifacts cannot supply total thread usage retrospectively."""
        # Arrange
        row = sample_row()

        # Act
        result = health.summarize(row)

        # Assert
        self.assertIsNone(result["ProcessThreadsAtStart"])
        self.assertIsNone(result["PeakProcessThreads"])
        self.assertIsNone(result["ProcessThreadIncrease"])

    def test_sub_hundred_probe_windows_report_observed_percentiles(self):
        """A configurable positive threshold permits sparse but complete windows."""
        # Arrange
        row = sample_row()
        row["Options"]["MinimumSamples"] = 1
        probes = row["Measurement"]["Probes"]
        probes.update(Total=60, Posted=60, Executed=60)
        probes["Samples"] = probes["Samples"][:60]

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["Count"], 60)
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["P99"], 2)

    def test_empty_window_has_no_percentiles_with_minimum_one(self):
        """Removing the statistical floor must not turn no observations into success."""
        # Arrange
        row = sample_row()
        row["Options"]["MinimumSamples"] = 1
        row["Measurement"]["Probes"].update(Total=0, Posted=0, Executed=0, Samples=[])

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Insufficient")
        self.assertIsNone(result["Work"]["DispatchMilliseconds"]["P99"])

    def test_all_probes_missed_does_not_create_supported_estimates(self):
        # Arrange
        row = sample_row()
        row["Options"]["MinimumSamples"] = 1
        row["Measurement"]["Probes"].update(
            Total=10, Posted=0, Executed=0, Dropped=0, ProducerMissed=10, Samples=[])

        # Act
        result = health.summarize(row)

        # Assert
        self.assertEqual(result["Coverage"], "Insufficient")
        self.assertEqual(result["DroppedProbes"], 0)
        self.assertEqual(result["ProducerMissedProbes"], 10)
        self.assertIsNone(result["Work"]["DispatchMilliseconds"]["P99"])

    def test_zero_sample_threshold_is_rejected(self):
        """A positive threshold remains necessary to reject empty windows."""
        # Arrange
        row = sample_row()
        row["Options"]["MinimumSamples"] = 0

        # Act
        error = None
        try:
            health.summarize(row)
        except ValueError as exception:
            error = exception

        # Assert
        self.assertIsInstance(error, ValueError)
        self.assertIn("coverage policy", str(error))

    def test_supported_distributions_keep_three_clocks_separate(self):
        result = health.summarize(sample_row())
        self.assertEqual(result["Coverage"], "Supported")
        self.assertEqual(result["Work"]["DispatchMilliseconds"]["P99"], 2)
        self.assertEqual(result["Work"]["ScheduledToExecutedMilliseconds"]["P99"], 3)
        self.assertEqual(result["Work"]["ProducerLatenessMilliseconds"]["P99"], 1)
        self.assertEqual(result["PeakBusyWorkerProxy"], 4)

    def test_recovery_does_not_relabel_late_work(self):
        row = sample_row()
        row["Measurement"]["Probes"]["Samples"][-1]["ExecutedMilliseconds"] = 1200
        result = health.summarize(row)
        self.assertEqual(result["Coverage"], "Incomplete")
        self.assertEqual(result["WorkLateOrOutstanding"], 1)
        self.assertEqual(result["Recovery"]["DispatchMilliseconds"]["Count"], 0)
        self.assertIsNone(result["Work"]["DispatchMilliseconds"]["P99"])

    def test_unexecuted_and_dropped_are_not_successful_zero_samples(self):
        row = sample_row()
        probes = row["Measurement"]["Probes"]
        probes.update(Total=104, Dropped=1, ProducerMissed=3, Executed=99, Outstanding=1)
        probes["Samples"][-1]["ExecutedMilliseconds"] = None
        result = health.summarize(row)
        self.assertEqual(result["Coverage"], "Incomplete")
        self.assertIsNone(result["Work"]["DispatchMilliseconds"]["P99"])

    def test_short_or_sparse_windows_have_no_percentiles(self):
        for field in ("MinimumSamples", "MinimumWindowMilliseconds"):
            row = sample_row()
            row["Options"][field] = 10001
            result = health.summarize(row)
            self.assertEqual(result["Coverage"], "Insufficient")
            self.assertIsNone(result["Work"]["DispatchMilliseconds"]["P99"])

    def test_recovery_requires_duration_and_complete_accounting(self):
        row = sample_row()
        samples = row["Measurement"]["Probes"]["Samples"]
        for sample in samples:
            for field in ("ScheduledMilliseconds", "PostedMilliseconds", "ExecutedMilliseconds"):
                sample[field] += 1000
        self.assertEqual(health.summarize(row)["Recovery"]["DispatchMilliseconds"]["P99"], 2)
        row["Options"]["MinimumWindowMilliseconds"] = 1000
        self.assertIsNone(health.summarize(row)["Recovery"]["DispatchMilliseconds"]["P99"])
        row["Options"]["MinimumWindowMilliseconds"] = 100
        row["Measurement"]["Probes"].update(Total=101, Dropped=1)
        self.assertEqual(health.summarize(row)["Recovery"]["DispatchMilliseconds"]["P99"], 2)

    def test_error_and_corrupt_counts_are_not_empty_success(self):
        row = sample_row()
        row["Measurement"]["ObserverError"] = "Deliberate"
        self.assertEqual(health.summarize(row)["Coverage"], "Failed")
        row["Measurement"]["Probes"]["Posted"] += 1
        with self.assertRaisesRegex(ValueError, "accounting"):
            health.summarize(row)
        with self.assertRaises(ValueError):
            health.summarize(dict(SchemaVersion=1, MeasurementMode="performance", Status="Completed"))

    def test_matching_repetitions_and_configuration_only(self):
        with tempfile.TemporaryDirectory() as root:
            for variant, skipped in (("baseline", 2), ("current", 5)):
                directory = Path(root, "repetitions", "rep1", variant, "Unit")
                directory.mkdir(parents=True)
                (directory / "async-health-process.json").write_text(json.dumps(dict(Status="Completed", Error=None, RunId="run")))
                row = sample_row()
                row["Measurement"]["Probes"].update(Total=100 + skipped, Skipped=skipped)
                (directory / "async-health-window.json").write_text(json.dumps(row))
            summary = health.write_summary(root, ["Unit"], True, 1)
            self.assertTrue(summary["Comparisons"][0]["Comparable"])
            self.assertEqual(summary["Comparisons"][0]["SkippedProbesDelta"], 3)
            self.assertEqual(summary["Comparisons"][0]["DroppedProbesDelta"], 0)
            self.assertIn("skipped probes delta: 3", Path(root, "summary.md").read_text())
            current = Path(root, "repetitions", "rep1", "current", "Unit", "async-health-window.json")
            row = sample_row()
            row["Configuration"] = dict(UseConnectionPoolV2=True)
            current.write_text(json.dumps(row))
            summary = health.write_summary(root, ["Unit"], True, 1)
            self.assertFalse(summary["Comparisons"][0]["Comparable"])
            self.assertIsNone(summary["Comparisons"][0]["DispatchP99DeltaMilliseconds"])
            self.assertIn("N/A", Path(root, "summary.md").read_text())
            self.assertIn("Peak pool threads", Path(root, "summary.md").read_text())
            self.assertIn("Dropped / rejected / skipped / producer-missed / outstanding", Path(root, "summary.md").read_text())
            self.assertIn("Supported does not imply loss-free", Path(root, "summary.md").read_text())
            self.assertIn("Process threads start / peak / increase", Path(root, "summary.md").read_text())
            baseline = Path(root, "repetitions", "rep1", "baseline", "Unit", "async-health-window.json")
            row["Configuration"] = dict(UseConnectionPoolV2=False)
            baseline.write_text(json.dumps(row))
            summary = health.write_summary(root, ["Unit"], True, 1, "UseConnectionPoolV2")
            self.assertTrue(summary["Comparisons"][0]["Comparable"])

    def test_different_probe_accounting_versions_are_not_comparable(self):
        """Legacy sample-limit drops cannot be compared directly to separated skipped counts."""
        # Arrange
        with tempfile.TemporaryDirectory() as root:
            for variant, version in (("baseline", 3), ("current", 4)):
                directory = Path(root, "repetitions", "rep1", variant, "Unit")
                directory.mkdir(parents=True)
                row = sample_row()
                row["SchemaVersion"] = version
                (directory / "async-health-process.json").write_text(
                    json.dumps(dict(Status="Completed", Error=None, RunId="run")))
                (directory / "async-health-window.json").write_text(json.dumps(row))

            # Act
            summary = health.write_summary(root, ["Unit"], True, 1)

            # Assert
            self.assertFalse(summary["Comparisons"][0]["Comparable"])
            self.assertIsNone(summary["Comparisons"][0]["SkippedProbesDelta"])
            self.assertIn("0 / N/A / 0 / 0 / 0", Path(root, "summary.md").read_text())

    def test_timing_and_ingestion_ignore_health_sidecars(self):
        """Health diagnostics can coexist with timing reports without becoming timing rows."""
        with tempfile.TemporaryDirectory() as root:
            Path(root, "async-health-window.json").write_text(json.dumps(sample_row()))
            Path(root, "Runner-report-full.json").write_text(json.dumps(dict(Benchmarks=[dict(
                Type="Runner", Method="Method", Statistics=dict(Mean=1000))])))
            timing = compare_perf._load_benchmarks(root)
            _, results = perf_to_kusto.translate(root, defaultdict(str))
            self.assertEqual(list(timing), ["Runner.Method()"])
            self.assertEqual(len(results), 1)
            self.assertEqual(results[0]["MethodName"], "Method")

    def test_failed_process_preserves_diagnostic_sidecars(self):
        with tempfile.TemporaryDirectory() as root:
            runner = interleave_perf.Runner(None, root, "PerformanceTests.dll", root, [])
            calls = []
            def execute(exe_dir, assembly, unit, cwd, cpus, log_path, **options):
                calls.append(options["env_overrides"])
                row = sample_row()
                row["RunId"] = options["env_overrides"]["PERF_RUN_ID"]
                row["ProcessRepetition"] = int(options["env_overrides"]["PERF_PROCESS_REPETITION"])
                output = Path(cwd, "BenchmarkDotNet.Artifacts", "async-health")
                output.mkdir(parents=True)
                (output / "async-health-window.json").write_text(json.dumps(row))
                Path(log_path).write_text("test")
                return 1
            with patch.object(interleave_perf, "run_unit_process", side_effect=execute):
                with self.assertRaises(RuntimeError):
                    runner._run_one("current", root, "Unit", 2, str(Path(root, "aggregate")))
            self.assertNotIn("PERF_MEASUREMENT_MODE", calls[0])
            self.assertEqual(calls[0]["PERF_PROCESS_REPETITION"], "2")
            self.assertTrue(Path(root, "aggregate", "async-health", "async-health-window.json").is_file())

    def test_timeout_is_preserved_and_does_not_enter_time_gate(self):
        with tempfile.TemporaryDirectory() as root:
            runner = interleave_perf.Runner(None, root, "p.dll", root, [])
            with patch.object(interleave_perf, "run_unit_process", side_effect=RuntimeError("timeout")):
                with self.assertRaisesRegex(RuntimeError, "timeout"):
                    runner._run_one("current", root, "Unit", 1, str(Path(root, "aggregate")))

    def test_obsolete_measurement_mode_is_not_accepted(self):
        with redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as failure:
            interleave_perf.main(["--measurement-mode", "async-health",
                                  "--current-exe-dir", ".", "--results-dir", "."])
        self.assertEqual(failure.exception.code, 2)

    def test_current_only_collects_timings_and_diagnostics_without_comparison(self):
        with tempfile.TemporaryDirectory() as root:
            def execute(exe_dir, assembly, unit, cwd, cpus, log_path, **options):
                row = sample_row()
                row["RunId"] = options["env_overrides"]["PERF_RUN_ID"]
                row["ProcessRepetition"] = int(options["env_overrides"]["PERF_PROCESS_REPETITION"])
                output = Path(cwd, "BenchmarkDotNet.Artifacts", "async-health")
                output.mkdir(parents=True)
                (output / "async-health-window.json").write_text(json.dumps(row))
                (output.parent / "Runner-report-full.json").write_text(json.dumps(dict(Benchmarks=[dict(
                    Type="Runner", Method="Method", Statistics=dict(Mean=1000))])))
                return 0
            args = ["--current-exe-dir", root, "--results-dir", str(Path(root, "results")),
                    "--work-dir", str(Path(root, "work"))]
            with patch.object(interleave_perf.Runner, "list_units", return_value=["Unit"]), \
                    patch.object(interleave_perf, "run_unit_process", side_effect=execute) as run, \
                    patch.object(interleave_perf, "orchestrate") as timing, \
                    redirect_stdout(io.StringIO()), redirect_stderr(io.StringIO()):
                self.assertEqual(interleave_perf.main(args), 0)
                self.assertEqual(run.call_count, 1)
                timing.assert_not_called()
            self.assertTrue(Path(root, "results", "current", "async-health", "async-health-window.json").is_file())
            self.assertIn("No baseline comparison", Path(root, "results", "summary.md").read_text())

    def test_process_watchdog_kills_owned_process_and_preserves_timeout(self):
        with tempfile.TemporaryDirectory() as root:
            process = Mock(pid=123)
            process.wait.side_effect = [subprocess.TimeoutExpired("dotnet", 1), 1]
            with patch.object(interleave_perf.subprocess, "Popen", return_value=process), \
                    patch.object(interleave_perf.subprocess, "run") as kill, \
                    patch.object(interleave_perf.os, "killpg", create=True) as kill_group:
                with self.assertRaises(subprocess.TimeoutExpired):
                    interleave_perf.run_unit_process(root, "p.dll", "Unit", root, [],
                                                     str(Path(root, "run.log")), timeout_seconds=1)
                if os.name == "nt":
                    self.assertEqual(kill.call_args.args[0], ["taskkill", "/PID", "123", "/T", "/F"])
                else:
                    kill_group.assert_called_once()
                self.assertEqual(process.wait.call_count, 2)

    def test_unconfirmed_process_shutdown_stops_the_run(self):
        with tempfile.TemporaryDirectory() as root:
            runner = interleave_perf.Runner(None, root, "p.dll", root, [])
            with patch.object(interleave_perf, "run_unit_process",
                              side_effect=interleave_perf.ProcessTerminationError("owned child still alive")):
                with self.assertRaises(interleave_perf.ProcessTerminationError):
                    runner._run_one("current", root, "Unit", 1, str(Path(root, "aggregate")))

    def test_entrypoints_do_not_require_a_measurement_mode(self):
        scripts = Path(__file__).resolve().parents[1]
        self.assertNotIn("MeasurementMode", (scripts / "run-perf-tests.ps1").read_text())
        self.assertNotIn("measurement-mode", (scripts / "run-perf-tests.sh").read_text())
        self.assertIn('"async_health_report.py") --results-dir $ResultsDir',
                      (scripts / "run-perf-tests.ps1").read_text())
        self.assertIn('async_health_report.py" --results-dir "${RESULTS_DIR}"',
                      (scripts / "run-perf-tests.sh").read_text())
        for pipeline in scripts.parent.glob("sqlclient-perf-*.yml"):
            text = pipeline.read_text()
            self.assertNotIn("measurementMode", text)
            self.assertNotIn("healthRepetitions", text)
            self.assertIn("-RunnerConfigFile ${{ parameters.runnerConfigFile }}", text)
            self.assertIn("--runner-config-file ${{ parameters.runnerConfigFile }}", text)
        text = (scripts.parent / "sqlclient-perf-pipeline.yml").read_text()
        self.assertNotIn("parameters.measurementMode", text)
        pr = (scripts.parent / "sqlclient-perf-pr-pipeline.yml").read_text()
        self.assertEqual(pr.count("eq(parameters.currentOnly, false)"), 3)

    @unittest.skipUnless(os.environ.get("PERF_HEALTH_TEST_BINARY"), "Set PERF_HEALTH_TEST_BINARY for real BDN lifecycle smoke")
    def test_real_benchmark_process_timeout(self):
        """The configured timeout stops a non-cooperative invocation batch without reporting success."""
        # Arrange
        binary = str(Path(os.environ["PERF_HEALTH_TEST_BINARY"]).resolve())
        with tempfile.TemporaryDirectory() as root:
            config = Path(root, "runner.json")
            config.write_text(json.dumps(dict(Benchmarks=dict(AsyncHealthCalibrationRunnerConfig=dict(
                Enabled=True, LaunchCount=1, IterationCount=1, InvocationCount=30,
                WarmupCount=0, RunStrategy="Monitoring", TimeoutMinutes=1)))))
            env = dict(os.environ, RUNNER_CONFIG=str(config), PERF_BENCHMARK="AsyncHealthCalibration")
            env.pop("PERF_LIST_BENCHMARKS", None)

            # Act
            output = subprocess.run(["dotnet", binary, "--filter", "*Runner.ProbeOnly"],
                                    cwd=root, env=env, text=True, capture_output=True, timeout=80)

            # Assert
            self.assertNotEqual(output.returncode, 0, output.stdout + output.stderr)
            self.assertIn("Benchmark execution timed out", output.stderr)
            rows = [json.loads(path.read_text()) for path in Path(root).rglob("async-health-*.json")]
            self.assertEqual(len(rows), 1)
            self.assertEqual(rows[0]["Status"], "Failed")

    @unittest.skipUnless(os.environ.get("PERF_HEALTH_TEST_BINARY"), "Set PERF_HEALTH_TEST_BINARY for real BDN lifecycle smoke")
    def test_real_benchmark_process_lifecycle(self):
        """Class-level diagnosis covers sync/async delegates while excluding fixture/warmup work."""
        binary = str(Path(os.environ["PERF_HEALTH_TEST_BINARY"]).resolve())
        with tempfile.TemporaryDirectory() as root:
            config = Path(root, "runner.json")
            config.write_text(json.dumps(dict(Benchmarks=dict(AsyncHealthCalibrationRunnerConfig=dict(
                Enabled=True, LaunchCount=1, IterationCount=2, InvocationCount=1,
                WarmupCount=1, RunStrategy="Monitoring")))))
            env = dict(os.environ, RUNNER_CONFIG=str(config), PERF_BENCHMARK="AsyncHealthCalibration")
            env.pop("PERF_LIST_BENCHMARKS", None)
            output = subprocess.run(["dotnet", binary, "--filter", "*Runner.AsyncWait",
                                     "*Runner.ProbeOnly", "*Runner.AsyncWaitAwaited"],
                                    cwd=root, env=env, text=True, capture_output=True, timeout=120)
            self.assertEqual(output.returncode, 0, output.stdout + output.stderr)
            rows = [json.loads(path.read_text()) for path in Path(root).rglob("async-health-*.json")]
            self.assertEqual(len(rows), 3)
            self.assertEqual(output.stdout.count("AsyncHealthLifecycle observed=1 "), 3)
            self.assertNotIn("AsyncHealthLifecycle observed=0 ", output.stdout)
            self.assertTrue(any("AsyncWaitAwaited" in row["Benchmark"] for row in rows))
            self.assertTrue(list(Path(root).rglob("*-report-full.json")))
            for row in rows:
                result = health.summarize(row)
                self.assertEqual(row["Configuration"]["Instrumentation"], "AsyncHealthDiagnoser")
                self.assertEqual(row["Configuration"]["DiagnosticRunMode"], "ExtraIteration")
                self.assertEqual(row["Invocations"], 1)
                self.assertGreater(result["DurationMilliseconds"], 2500)
                self.assertGreater(result["WorkExecuted"], 0)
                self.assertGreater(result["ProcessThreadsAtStart"], 0)
                self.assertGreater(result["PeakProcessThreads"], 0)
                self.assertGreaterEqual(result["PeakProcessThreads"], result["PeakThreads"])
                self.assertTrue(all(sample["ProcessThreads"] > 0
                                    for sample in row["Measurement"]["Resources"]))


if __name__ == "__main__":
    unittest.main()
