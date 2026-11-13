"""SQL-independent regression, evidence, baseline-build and child-lifecycle contracts."""

from copy import deepcopy
import json
import math
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import openasync_health as health


def evidence():
    """Small complete raw result with enough timed probes and a first outcome for every worker."""
    settings = health.cases(workers=4, duration=3)[0][1]
    counts = dict(Started=4, Opened=4, Completed=4, Failed=0, Cancelled=0, InFlight=0,
                  WorkersStarted=4, InlineOpens=0, PendingOpens=4)
    return dict(
        SchemaVersion=1, Settings=settings, Status="Completed", Drained=True,
        Source="AuthenticatedSql", Runtime="10.0", OS="Unix", ProcessorCount=16,
        MaximumThreads=32767, DriverVersion="driver", DriverSha256="A" * 64, CutoffMs=3000,
        AtCutoff=counts, Final=dict(counts), Errors={}, ObservationsDropped=0,
        Observations=[dict(Worker=i, OfferedMs=0, StartMs=1, OpenEndMs=20,
                           EndMs=25, Outcome="Completed") for i in range(4)],
        Measurement=dict(
            ObserverError=None, ResourceSamplesDropped=0,
            WorkloadStartMilliseconds=0, WorkloadEndMilliseconds=3000,
            Probes=dict(Total=120, Posted=120, Executed=120, Dropped=0, Rejected=0, Skipped=0,
                        ProducerMissed=0, Outstanding=0, Samples=[
                            dict(ScheduledMilliseconds=i * 25, PostedMilliseconds=i * 25,
                                 ExecutedMilliseconds=i * 25 + 1) for i in range(120)]),
            Resources=[dict(TimestampMilliseconds=1, Threads=16, ProcessThreads=25,
                            QueueDepth=0, AllocatedBytes=100, Gen2Collections=0)]))


def row(**changes):
    """Create a validated summary before applying a specific comparison condition."""
    data = evidence()
    return dict(health.summarize(data, data["Settings"], 0), **changes)


class EvidenceTests(unittest.TestCase):
    """Invalid data and failed/censored work cannot be reduced to a successful-only p99."""

    def test_symmetric_twenty_paths_without_experimental_switch(self):
        selection = dict(health.cases())
        self.assertEqual(len(selection), 20)
        self.assertEqual(sum(c["Query"] for c in selection.values()), 10)
        for mode in ("v1", "v2"):
            self.assertEqual(sum(n.startswith(mode) for n in selection), 8)
            self.assertEqual(selection[f"{mode}-warm-open"]["PoolSize"], 100)
            self.assertEqual(selection[f"{mode}-warm-contended-open"]["PoolSize"], 16)
        self.assertFalse(selection["nonpooled-initialized-query"]["WarmPool"])
        self.assertTrue(selection["nonpooled-initialized-query"]["WarmDriver"])
        self.assertFalse(any("LongRunning" in c for c in selection.values()))

    def test_valid_evidence_reports_dispatch_offer_and_resource_cost(self):
        data = evidence()
        result = health.summarize(data, data["Settings"], 0)
        self.assertEqual(result["evidence"], "complete")
        self.assertEqual(result["absolute_health"], "Pass")
        self.assertEqual(result["dispatch_p99_ms"], 1)
        self.assertEqual(result["first_acquisition_p99_ms"], 20)
        self.assertEqual(result["first_queue_p99_ms"], 1)
        self.assertEqual(result["process_threads"], 25)
        self.assertAlmostEqual(result["throughput"], 4 / 3)

    def test_invalid_or_failed_evidence_is_not_comparable(self):
        mutations = [
            lambda d: d.update(SchemaVersion=99),
            lambda d: d.update(Status="Incomplete"),
            lambda d: d.update(Drained=False),
            lambda d: d.update(Source="SyntheticStalledPrelogin"),
            lambda d: d.update(CutoffMs=100),
            lambda d: d["Final"].update(Failed=1),
            lambda d: d["AtCutoff"].update(Completed=0),
            lambda d: d["AtCutoff"].update(WorkersStarted=3),
            lambda d: d["Measurement"].update(ObserverError="fault"),
            lambda d: d["Measurement"]["Probes"].update(Posted=119),
            lambda d: d["Measurement"]["Probes"]["Samples"][0].update(
                ExecutedMilliseconds=math.nan),
            lambda d: d["Observations"].pop(),
            lambda d: d["Observations"][0].update(OpenEndMs=None),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                data = evidence()
                mutate(data)
                with self.assertRaises(ValueError):
                    health.summarize(data, data["Settings"], 0)
        data = evidence()
        with self.assertRaisesRegex(ValueError, "Harness failed"):
            health.summarize(data, data["Settings"], 1)

    def test_dropped_and_pending_probes_are_censored_not_healthy(self):
        data = evidence()
        probes = data["Measurement"]["Probes"]
        probes.update(Total=121, Executed=119, Dropped=1, Outstanding=1)
        probes["Samples"][0]["ExecutedMilliseconds"] = None
        result = health.summarize(data, data["Settings"], 0)
        self.assertEqual(result["evidence"], "censored")
        self.assertEqual(result["absolute_health"], "Insufficient")
        self.assertEqual(result["missing_probes"], 2)

    def test_cancelled_first_open_stays_censored_after_drain(self):
        data = evidence()
        data["Observations"][0].update(Outcome="Cancelled", OpenEndMs=None, EndMs=3100)
        data["AtCutoff"].update(Opened=3, Completed=3, InFlight=1)
        data["Final"].update(Opened=3, Completed=3, Cancelled=1)
        result = health.summarize(data, data["Settings"], 0)
        self.assertEqual(result["evidence"], "censored")
        self.assertEqual(result["first_incomplete"], 1)
        self.assertEqual(result["cancelled"], 1)

    def test_operation_cap_does_not_hide_missing_first_worker(self):
        data = evidence()
        data["Observations"] = data["Observations"][1:]
        data["ObservationsDropped"] = 1
        with self.assertRaisesRegex(ValueError, "first-worker"):
            health.summarize(data, data["Settings"], 0)

    def test_producer_descheduling_at_window_end_counts_as_missing_coverage(self):
        data = evidence()
        probes = data["Measurement"]["Probes"]
        probes["Samples"] = probes["Samples"][:10]
        probes.update(Total=10, Posted=10, Executed=10)
        result = health.summarize(data, data["Settings"], 0)
        self.assertEqual(result["evidence"], "censored")
        self.assertEqual(result["missing_probes"], 110)
        self.assertAlmostEqual(result["missing_fraction"], 110 / 120)


class ComparisonTests(unittest.TestCase):
    """Matched regression budgets preserve absolute debt, resource tradeoffs and noise labels."""

    def test_known_absolute_baseline_debt_does_not_permanently_fail(self):
        baseline = row(dispatch_p99_ms=2700, absolute_health="Fail")
        current = row(dispatch_p99_ms=2790, absolute_health="Fail")
        result = health.compare_pair(baseline, current)
        self.assertEqual(result["status"], "baseline-debt")
        self.assertEqual(result["regressions"], [])

    def test_improvement_cannot_hide_throughput_or_dedicated_thread_regression(self):
        baseline = row(dispatch_p99_ms=2700, absolute_health="Fail", throughput=100)
        current = row(dispatch_p99_ms=10, process_threads=70, throughput=85)
        result = health.compare_pair(baseline, current)
        self.assertEqual(result["status"], "regression")
        self.assertEqual(set(result["regressions"]), {"throughput", "process_threads"})
        self.assertIn("dispatch_p99_ms", result["improvements"])

    def test_censored_baseline_is_explicitly_inconclusive(self):
        baseline = row(evidence="censored", missing_fraction=.1, absolute_health="Insufficient")
        current = dict(baseline, dispatch_p99_ms=.01)
        self.assertEqual(health.compare_pair(baseline, current)["status"],
                         "inconclusive-baseline-debt")
        self.assertIn("coverage", health.compare_pair(row(), current)["regressions"])
        self.assertEqual(health.compare_pair(baseline, row())["status"], "improvement")

    def test_censored_baseline_does_not_waive_new_candidate_missing_work(self):
        baseline = row(evidence="censored", missing_fraction=.1, first_incomplete=1,
                       absolute_health="Insufficient")
        for current in (dict(baseline, missing_fraction=.2), dict(baseline, first_incomplete=2)):
            with self.subTest(current=current):
                pair = health.compare_pair(baseline, current)
                self.assertIn("coverage", pair["regressions"])
                result = health.aggregate([dict(repetition=i, **pair) for i in range(3)], 3)
                self.assertEqual(result["status"], "regression")
                self.assertEqual(result["confirmed_metrics"], ["coverage"])

    def test_budget_requires_both_relative_and_absolute_latency_or_thread_growth(self):
        baseline = row(dispatch_p99_ms=10, process_threads=25)
        self.assertEqual(health.compare_pair(
            baseline, row(dispatch_p99_ms=34, process_threads=33))["regressions"], [])
        self.assertEqual(set(health.compare_pair(
            baseline, row(dispatch_p99_ms=36, process_threads=34))["regressions"]),
            {"dispatch_p99_ms", "process_threads"})

    def test_old_baseline_without_v2_is_not_a_fake_matched_pass(self):
        unsupported = dict(evidence="unsupported", absolute_health="Unmeasured")
        self.assertEqual(health.compare_pair(unsupported, row())["status"], "uncompared")
        with self.assertRaisesRegex(ValueError, "Candidate"):
            health.compare_pair(row(), unsupported)

    def test_runtime_or_cpu_mismatch_is_invalid(self):
        for field, value in (("runtime", "11"), ("cpu", 32), ("platform", "Windows"),
                             ("maximum_threads", 64)):
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "differs"):
                health.compare_pair(row(), row(**{field: value}))

    def test_confirm_same_metric_not_unrelated_noisy_failures(self):
        pairs = [dict(repetition=i, status="regression", regressions=[metric])
                 for i, metric in enumerate(("throughput", "dispatch_p99_ms", "process_threads"))]
        self.assertEqual(health.aggregate(pairs, 3)["status"], "unconfirmed-noise")
        pairs[1]["regressions"] = ["throughput"]
        self.assertEqual(health.aggregate(pairs, 3)["confirmed_metrics"], ["throughput"])
        with self.assertRaisesRegex(ValueError, "Missing"):
            health.aggregate(pairs[:2], 3)


class RunnerTests(unittest.TestCase):
    """Exercise real orchestration boundaries without needing a privileged SQL host."""

    def test_baseline_build_uses_candidate_harness_and_only_baseline_driver(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            candidate, baseline, output = root / "candidate", root / "old-baseline", root / "out"
            (baseline / health.DRIVER).parent.mkdir(parents=True)
            (baseline / health.DRIVER).touch()
            output.mkdir()
            (output / "OpenAsyncHealth.dll").touch()
            with patch.object(health.subprocess, "run") as build:
                health.build_harness(candidate, output, driver_root=baseline)
            command = build.call_args.args[0]
            self.assertEqual(command[2], str(candidate / health.PROJECT))
            self.assertIn(f"-p:HealthDriverProject={baseline / health.DRIVER}", command)
            self.assertFalse((baseline / health.PROJECT).exists())

    def test_package_build_does_not_require_or_use_an_old_perf_app(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp)
            (output / "OpenAsyncHealth.dll").touch()
            with patch.object(health.subprocess, "run") as build:
                health.build_harness(health.REPO, output, package="6.1.1")
            command = build.call_args.args[0]
            self.assertIn("-p:HealthPackageVersion=6.1.1", command)
            self.assertFalse(any("HealthDriverProject=" in arg for arg in command))

    def test_every_pair_has_identical_settings_and_order_alternates(self):
        calls = []

        def run(binary, settings, folder, env, timeout):
            calls.append((str(binary), deepcopy(settings), folder.name))
            return row()

        with tempfile.TemporaryDirectory() as temp, patch.object(health, "run_case", side_effect=run):
            rows, results = health.execute(
                dict(baseline=Path("old"), current=Path("new")), health.cases(),
                Path(temp), {}, 3, [0], "localhost")
        self.assertEqual(len(calls), 120)
        self.assertEqual(len(rows), 120)
        self.assertEqual(len(results), 20)
        for i in range(0, len(calls), 2):
            self.assertEqual(calls[i][1], calls[i + 1][1])
            self.assertEqual(calls[i][0], "new" if "-r1-" in calls[i][2] else "old")

    def test_timeout_kills_and_reaps_child(self):
        with tempfile.TemporaryDirectory() as temp, patch.object(health.subprocess, "Popen") as start:
            process = start.return_value
            process.wait.side_effect = [subprocess.TimeoutExpired("dotnet", 1), 0]
            process.poll.return_value = None
            with self.assertRaises(subprocess.TimeoutExpired):
                health.run_case(Path("harness"), {}, Path(temp) / "case", {}, 1)
            process.kill.assert_called_once()
            self.assertEqual(process.wait.call_count, 2)
            self.assertTrue((Path(temp) / "case/case.json").exists())

    def test_nonzero_child_cannot_be_hidden_by_a_completed_result(self):
        with tempfile.TemporaryDirectory() as temp, patch.object(health.subprocess, "Popen") as start:
            data = evidence()
            folder = Path(temp) / "case"

            def wait(**kwargs):
                health.write_json(folder / "result.json", data)
                return 1

            start.return_value.wait.side_effect = wait
            start.return_value.poll.return_value = 1
            with self.assertRaisesRegex(ValueError, "Harness failed"):
                health.run_case(Path("harness"), data["Settings"], folder, {}, 1)

    def test_netem_is_cleaned_when_case_fails(self):
        import netem
        with tempfile.TemporaryDirectory() as temp, \
                patch.object(netem, "NetworkDelay") as network, \
                patch.object(health, "run_case", side_effect=ValueError("invalid")):
            with self.assertRaisesRegex(ValueError, "invalid"):
                health.execute(dict(current=Path("new")), health.cases(), Path(temp), {},
                               3, [100], "localhost")
            network.return_value.setup.assert_called_once_with(50, "localhost")
            network.return_value.cleanup.assert_called_once()

    def test_invalid_configuration_publishes_failure_without_private_text(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            config = root / "private.json"
            config.write_text('{"ConnectionString":', encoding="utf-8")
            result = health.main(["--results-dir", str(root / "results"),
                                  "--runner-config", str(config)])
            self.assertEqual(result, 2)
            manifest = json.loads((root / "results/openasync-health/manifest.json").read_text())
            self.assertEqual(manifest["status"], "InvalidEvidence")
            self.assertNotIn("ConnectionString", json.dumps(manifest))
            self.assertIn("no health pass", (root / "results/openasync-health.md").read_text())


if __name__ == "__main__":
    unittest.main()
