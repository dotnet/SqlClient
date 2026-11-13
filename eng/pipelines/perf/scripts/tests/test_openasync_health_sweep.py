"""Regression checks for missing-probe accounting and cold-open reporting."""

import importlib.util
from pathlib import Path
import unittest

SPEC = importlib.util.spec_from_file_location(
    "openasync_health_sweep", Path(__file__).resolve().parents[1] / "openasync_health_sweep.py")
SWEEP = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(SWEEP)


class SummaryTests(unittest.TestCase):
    """Prevent censored or failed work from being presented as healthy successful opens."""

    def data(self):
        """Return a short deterministic completed window with sufficient healthy probes."""
        return {
            "Status": "Completed", "Source": "AuthenticatedSql", "ProcessorCount": 16,
            "Settings": {"Workers": 2},
            "CutoffMs": 3000, "ObservationsDropped": 0, "Observations": [],
            "AtCutoff": {"Completed": 0, "Failed": 0, "InFlight": 0, "QueuedWorkers": 0},
            "Measurement": {
                "WorkloadStartMilliseconds": 0, "WorkloadEndMilliseconds": 3000,
                "OutstandingAtWorkloadEnd": 0, "ResourceSamplesDropped": 0, "Resources": [],
                "Probes": {"Dropped": 0, "Rejected": 0, "Skipped": 0, "Outstanding": 0,
                           "ProducerMissed": 0,
                           "Samples": [{"ScheduledMilliseconds": i * 25,
                                        "PostedMilliseconds": i * 25,
                                        "ExecutedMilliseconds": i * 25 + 1}
                                       for i in range(120)]},
            },
        }

    def test_missing_probes_prevent_healthy_classification(self):
        # Arrange
        data = self.data()
        data["Measurement"]["Probes"]["Outstanding"] = 1
        # Act
        row = SWEEP.summarize(data)
        # Assert
        self.assertEqual(row["scheduler_health"], "Insufficient")
        self.assertEqual(row["missing_probes"], 1)

    def test_known_bad_latency_is_not_hidden_by_missing_probes(self):
        # Arrange
        data = self.data()
        data["Measurement"]["Probes"]["Dropped"] = 10
        for probe in data["Measurement"]["Probes"]["Samples"]:
            probe["ExecutedMilliseconds"] += 2000
        # Act
        row = SWEEP.summarize(data)
        # Assert
        self.assertEqual(row["scheduler_health"], "Fail")
        self.assertEqual(row["dispatch_p99_ms"], 2001)

    def test_failed_opens_are_separate_from_successful_cold_opens(self):
        # Arrange
        data = self.data()
        data["Observations"] = [
            dict(Worker=0, OfferedMs=0, StartMs=500, OpenEndMs=1500, EndMs=1550,
                 Outcome="Completed"),
            dict(Worker=0, OfferedMs=1550, StartMs=1550, OpenEndMs=1551, EndMs=1600,
                 Outcome="Completed"),
            dict(Worker=1, OfferedMs=0, StartMs=0, OpenEndMs=None, EndMs=2500,
                 Outcome="Failed"),
        ]
        # Act
        row = SWEEP.summarize(data)
        # Assert
        self.assertEqual(row["first_open_success_p99_ms"], 1000)
        self.assertEqual(row["first_acquisition_from_offer_p99_ms"], 1500)
        self.assertEqual(row["open_failure_p99_ms"], 2500)
        self.assertEqual(row["initial_or_yield_queue_p99_ms"], 500)

    def test_sparse_completion_cohort_is_not_a_healthy_window(self):
        # Arrange
        data = self.data()
        data["Measurement"]["Probes"]["Samples"] = data["Measurement"]["Probes"]["Samples"][:3]
        # Act
        row = SWEEP.summarize(data)
        # Assert
        self.assertEqual(row["scheduler_health"], "Insufficient")

    def test_missing_first_worker_record_cannot_report_full_cold_percentile(self):
        # Arrange
        data = self.data()
        data["Observations"] = [dict(Worker=0, OfferedMs=0, StartMs=0,
                                    OpenEndMs=10, EndMs=20, Outcome="Completed")]
        # Act
        row = SWEEP.summarize(data)
        # Assert
        self.assertEqual(row["first_worker_samples_missing"], 1)
        self.assertIsNone(row["first_open_success_p99_ms"])

    def test_matrix_has_separate_warm_and_stalled_controls(self):
        # Act
        sql = dict(SWEEP.cases("sql"))
        stalled = dict(SWEEP.cases("stalled"))
        # Assert
        self.assertTrue(sql["w64-warm"]["WarmPool"])
        self.assertFalse(sql["w64"]["WarmPool"])
        self.assertNotIn("w64-warm", stalled)
        self.assertTrue(stalled["w64"]["Burst"])
        self.assertTrue(stalled["w64"]["StalledPrelogin"])

    def test_sql_gate_rejects_empty_failed_or_censored_work(self):
        # Arrange
        data = self.data()
        data["AtCutoff"]["Completed"] = 100
        good = SWEEP.summarize(data)
        # Act / Assert
        self.assertTrue(SWEEP.healthy_sql(good))
        for change in (dict(failures=1), dict(completed_per_second=0),
                       dict(unstarted_workers=1), dict(scheduler_health="Insufficient"),
                       dict(source="SyntheticStalledPrelogin"), dict(exit_code=1)):
            self.assertFalse(SWEEP.healthy_sql(dict(good, **change)))

    def test_success_suite_covers_both_pool_versions_and_nonpooled_initialization(self):
        # Act
        cases = dict(SWEEP.open_path_cases([64]))
        # Assert
        self.assertEqual(len(cases), 20)
        for mode in ("v1", "v2"):
            for operation in ("open", "query"):
                for state in ("cold", "initialized-cold", "warm", "warm-contended"):
                    case = cases[f"{mode}-{state}-{operation}-w64"]
                    self.assertTrue(case["Pooling"])
                    self.assertEqual(case["PoolV2"], mode == "v2")
                    self.assertEqual(case["Query"], operation == "query")
                    self.assertEqual(case["WarmPool"], state.startswith("warm"))
                    self.assertEqual(case["WarmDriver"], state == "initialized-cold")
                    self.assertEqual(case["PoolSize"] < case["Workers"], state == "warm-contended")
        for operation in ("open", "query"):
            cold = cases[f"nonpooled-cold-{operation}-w64"]
            warm = cases[f"nonpooled-initialized-{operation}-w64"]
            self.assertFalse(cold["WarmDriver"])
            self.assertTrue(warm["WarmDriver"])
            self.assertFalse(warm["Pooling"])
            self.assertFalse(warm["WarmPool"])

    def test_single_worker_suite_does_not_claim_pool_contention(self):
        # Act
        cases = dict(SWEEP.open_path_cases([1]))
        # Assert
        self.assertTrue(all("contended" not in name for name in cases))
        self.assertTrue(all(c["Workers"] == 1 for c in cases.values()))


class BaselineComparisonTests(unittest.TestCase):
    """Matched gates must protect useful work and process resources as well as scheduler latency."""

    def fixture(self):
        """Construct a matched, valid SQL baseline and candidate."""
        row = dict(case="warm", repetition=1, rtt_ms=0, source="AuthenticatedSql",
                   status="Completed", exit_code=0, failures=0, unstarted_workers=0,
                   completed_per_second=1000, cpu=16, process_peak=32,
                   scheduler_health="Pass", platform="Unix", runtime="10.0")
        manifest = dict(cases=[("warm", {})], rtt=[0], repetitions=1, mode="sql", status="Completed")
        return row, manifest

    def test_throughput_loss_fails_even_with_healthy_scheduler(self):
        # Arrange
        baseline, manifest = self.fixture()
        candidate = dict(baseline, completed_per_second=800)
        # Act
        failures = SWEEP.compare_baseline([candidate], [baseline], manifest, manifest)
        # Assert
        self.assertTrue(any("throughput" in failure for failure in failures))

    def test_moving_cost_to_dedicated_threads_fails_resource_gate(self):
        # Arrange
        baseline, manifest = self.fixture()
        candidate = dict(baseline, process_peak=90)
        # Act
        failures = SWEEP.compare_baseline([candidate], [baseline], manifest, manifest)
        # Assert
        self.assertTrue(any("process threads" in failure for failure in failures))

    def test_different_path_settings_cannot_be_compared(self):
        # Arrange
        baseline, manifest = self.fixture()
        different = dict(manifest, cases=[("warm", {"WarmPool": True})])
        # Act / Assert
        with self.assertRaises(ValueError):
            SWEEP.compare_baseline([baseline], [baseline], manifest, different)

    def test_equivalent_healthy_runs_pass(self):
        # Arrange
        baseline, manifest = self.fixture()
        # Act
        failures = SWEEP.compare_baseline([baseline], [baseline], manifest, manifest)
        # Assert
        self.assertEqual(failures, [])

    def test_baseline_and_candidate_runtime_must_match(self):
        # Arrange
        baseline, manifest = self.fixture()
        candidate = dict(baseline, runtime="11.0")
        # Act / Assert
        with self.assertRaises(ValueError):
            SWEEP.compare_baseline([candidate], [baseline], manifest, manifest)

    def test_losing_a_path_repetition_cannot_pass_comparison(self):
        # Arrange
        baseline, manifest = self.fixture()
        # Act / Assert
        with self.assertRaises(ValueError):
            SWEEP.compare_baseline([], [baseline], manifest, manifest)


if __name__ == "__main__":
    unittest.main()
