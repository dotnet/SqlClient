# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Offline v2 workload accounting, counterbalancing and diagnostic observer contracts."""

import contextlib
import copy
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import threading
from types import SimpleNamespace
import unittest
from unittest import mock
import uuid

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import interleave_perf
import openasync_report as v1
import scoreboard_observer as observer
import scoreboard_report as report


def sqlcmd_output(data, width=65535, headers=True):
    payload = json.dumps(data)
    header = f"{observer.SQL_JSON_COLUMN}\n{'-' * len(observer.SQL_JSON_COLUMN)}\n" if headers else ""
    return header + "\n".join(payload[index:index + width] for index in range(0, len(payload), width)) + "\n"


def diagnostic():
    timeline = dict(TimestampMilliseconds=0, AfterWorkload=False)
    for name in ("OutstandingOpens", "OwnedConnections", "WorkerThreads", "BusyWorkerThreads",
                 "PendingWorkItems", "CompletedWorkItems", "ProcessThreads", "CpuMilliseconds",
                 "WorkingSetBytes", "PrivateMemoryBytes", "ManagedHeapBytes", "HeapSizeAfterLastGcBytes",
                 "FragmentedBytes", "TotalAllocatedBytes", "Gen0Collections", "Gen1Collections", "Gen2Collections"):
        timeline[name] = 1
    return dict(SampleIntervalMilliseconds=10, WindowEndMilliseconds=10, UnobservedGcCount=0,
                GcObservationMethod="Last-GC polling; intermediate collections can be missed",
                Timeline=[timeline, dict(timeline, TimestampMilliseconds=12, AfterWorkload=True)],
                GcObservations=[], UnrelatedWork=dict(Capacity=64, Scheduled=2, Posted=1,
                Executed=1, Dropped=1, OutstandingAtStop=1, DrainMilliseconds=2,
                Samples=[dict(ScheduledMilliseconds=0, PostedMilliseconds=1, ExecutedMilliseconds=11)]))


def sample(request_id, api, start=1, worker=0, operation=0):
    return dict(RequestId=request_id, Worker=worker, Operation=operation,
                StartMilliseconds=start, TaskReturnMilliseconds=start + .1 if api == "Async" else None,
                CompletedAtReturn=False if api == "Async" else None,
                CompletionMilliseconds=start + .5, LifecycleCompletionMilliseconds=start + 1,
                Outcome="Success", ErrorType=None, SqlErrorNumber=None, OfferedMilliseconds=None, Phase=None)


def invocation(case, repetition=1, index=1, stage="WorkloadActual"):
    _, scenario, api, concurrency, *suffix = case.split("-")
    concurrency = int(concurrency)
    operations = 1 if scenario == "E4" else 64
    data = dict(SchemaVersion=2, WorkloadVersion=2, CaseId=case, Scenario=scenario, Api=api,
                Concurrency=concurrency, Instrumentation="Diagnostic" if suffix else "Sampled",
                OperationsPerWorker=operations, PoolSize=0 if scenario == "E3" else 4 if scenario in ("E2", "E6") else concurrency,
                HoldMilliseconds=10 if scenario in ("E2", "E6") else 0,
                ProcessRepetition=repetition, ProcessId=1000 + repetition, RunId=f"process-{repetition}",
                InvocationId=f"{stage}-{index}", Invocation=index, MeasurementStage=stage, IsInstrumented=True,
                UseConnectionPoolV2=False, UseOptimizedAsyncBehaviour=True, UseManagedSniOnWindows=False,
                ConnectTimeoutSeconds=30, ConnectRetryCount=0, Enlist=False, Runtime=".NET 10",
                OS="test", DriverVersion="7.0.2", DriverAssemblyVersion="7.0.0.0", ProcessorCount=8,
                ElapsedMilliseconds=7000, AdmissionMilliseconds=[1] * concurrency,
                QuerySamples=[], Load=None, Diagnostics=diagnostic() if suffix else None,
                LoadPhaseMilliseconds=2000, LoadQueueCapacity=64, LoadBaseRate=25, LoadOverloadRate=800)
    data["StartedUtc"] = f"2026-01-01T00:00:0{index}Z" if suffix else None
    data["CompletedUtc"] = f"2026-01-01T00:00:0{index}.001Z" if suffix else None
    data.update({name: 1 for name in v1.RESOURCE_FIELDS})
    data["Samples"] = [sample(worker * operations + operation, api, worker=worker, operation=operation)
                       for worker in range(concurrency) for operation in range(operations)]
    if scenario == "E5":
        data["QuerySamples"] = [dict(RequestId=item["RequestId"], StartMilliseconds=1.5,
                                    CompletionMilliseconds=1.75, Outcome="Success", ErrorType=None, SqlErrorNumber=None)
                               for item in data["Samples"]]
    if scenario == "E6":
        data["Samples"] = []
        offers = []
        generation_start = 50
        for phase_index, phase in enumerate(("Baseline", "Overload", "Recovery")):
            rate = 800 if phase == "Overload" else 25
            for tick in range(rate * 2):
                request_id = len(offers)
                scheduled = generation_start + phase_index * 2000 + tick * 1000 / rate
                accepted = tick < 2
                offers.append(dict(RequestId=request_id, Phase=phase, ScheduledMilliseconds=scheduled,
                                   ActualOfferMilliseconds=scheduled + .1, Accepted=accepted))
                if accepted:
                    item = sample(request_id, api, scheduled + .2, operation=len(data["Samples"]))
                    item.update(Phase=phase, OfferedMilliseconds=scheduled)
                    data["Samples"].append(item)
        data["Load"] = dict(PhaseMilliseconds=2000, QueueCapacity=64, BaseRate=25, OverloadRate=800,
                            Offers=offers, GenerationStartMilliseconds=generation_start,
                            GenerationCompletedMilliseconds=6050, DrainCompletedMilliseconds=6051)
    return data


class SchemaTests(unittest.TestCase):
    def test_serializer_utc_fields_accept_sampled_nulls_and_diagnostic_offsets(self):
        case = "OpenAsyncScoreboard-E1-Async-1"
        data = invocation(case)
        data.update(StartedUtc=None, CompletedUtc=None, WasCanceled=False)
        self.assertIsNone(report.summarize(data, case, 1)["StartedUtc"])
        data.update(StartedUtc="2026-10-12T23:38:50.0447951+00:00",
                    CompletedUtc="2026-10-12T23:38:50.0550527+00:00")
        self.assertEqual(report.summarize(data, case, 1)["CompletedUtc"], data["CompletedUtc"])
        for changes in (
            {"StartedUtc": None},
            {"StartedUtc": "2026-10-12T23:38:50.0447951"},
            {"CompletedUtc": "2026-10-11T23:38:50.0550527+00:00"},
            {"WasCanceled": "false"},
        ):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                report.summarize(dict(data, **changes), case, 1)

    def test_e6_admission_has_one_numeric_value_per_worker(self):
        case = "OpenAsyncScoreboard-E6-Async-8"
        data = invocation(case)
        data["AdmissionMilliseconds"] = [1] + [-1] * 7
        self.assertEqual(report.summarize(data, case, 1)["AdmissionMilliseconds"], [1] + [None] * 7)
        for admission in ([1], [1] + [None] * 7, [1] + [-2] * 7):
            with self.subTest(admission=admission), self.assertRaises(ValueError):
                report.summarize(dict(data, AdmissionMilliseconds=admission), case, 1)

    def test_controls_only_toggle_existing_enable_flags(self):
        config = {"Benchmarks": {
            "SqlConnectionRunnerConfig": {"Enabled": False, "IterationCount": 17, "WarmupCount": 5},
            "ConnectionPoolChurnRunnerConfig": {"Enabled": False, "InvocationCount": 123},
            "OtherRunnerConfig": {"Enabled": True, "LaunchCount": 9},
            "OpenAsyncScoreboardRunnerConfig": {"Enabled": True, "IterationCount": 3}},
            "OpenAsyncScoreboard": {"OperationsPerWorker": 64}, "OtherSettings": {"PoolSize": 27}}
        before = copy.deepcopy(config)
        v1.apply_profile(config, v1.CONTROLS_PROFILE)
        for name, settings in config["Benchmarks"].items():
            self.assertEqual(settings["Enabled"], name in ("SqlConnectionRunnerConfig", "ConnectionPoolChurnRunnerConfig"))
            settings_without_enabled = {key: value for key, value in settings.items() if key != "Enabled"}
            original = {key: value for key, value in before["Benchmarks"][name].items() if key != "Enabled"}
            self.assertEqual(settings_without_enabled, original)
        self.assertEqual(config["OtherSettings"], before["OtherSettings"])
        self.assertEqual(config["OpenAsyncScoreboard"], before["OpenAsyncScoreboard"])

    def test_full_matrix_and_profile_settings(self):
        self.assertEqual(len(set(v1.FULL_CASES)), 96)
        cfg = {"Benchmarks": {"Other": {"Enabled": True}}}
        v1.apply_profile(cfg, v1.FULL_PROFILE)
        self.assertEqual(cfg["Benchmarks"]["OpenAsyncScoreboardRunnerConfig"]["WarmupCount"], 2)
        self.assertEqual(cfg["OpenAsyncScoreboard"]["OperationsPerWorker"], 64)
        self.assertEqual(cfg["OpenAsyncScoreboard"]["InstrumentationModes"], ["Sampled", "Diagnostic"])
        self.assertEqual(cfg["OpenAsyncScoreboard"]["LoadBaseRate"], 25)
        self.assertEqual(cfg["OpenAsyncScoreboard"]["LoadPhaseMilliseconds"], 2000)
        self.assertNotIn("E6LoadPhaseMilliseconds", cfg["OpenAsyncScoreboard"])
        for case in v1.FULL_CASES:
            with self.subTest(case=case):
                result = report.summarize(invocation(case), case, 1)
                self.assertEqual(result["CaseId"], case)

    def test_e5_lifecycle_query_and_pending_return_are_distinct(self):
        case = "OpenAsyncScoreboard-E5-Async-1"
        data = invocation(case)
        data["Samples"][0]["CompletedAtReturn"] = True
        row = report.summarize(data, case, 1)
        self.assertEqual(row["QueryMilliseconds"]["P50"], .25)
        self.assertEqual(row["LifecycleMilliseconds"]["P50"], 1)
        self.assertEqual(row["PendingTaskReturnMilliseconds"]["Count"], 63)
        self.assertEqual(row["CompletedAtReturnFraction"], 1 / 64)
        data["QuerySamples"].pop()
        with self.assertRaisesRegex(ValueError, "one query"):
            report.summarize(data, case, 1)

    def test_e6_load_accounting_keeps_rejections_and_phase_tails(self):
        case = "OpenAsyncScoreboard-E6-Async-1"
        row = report.summarize(invocation(case), case, 1)
        phases = row["LoadSummary"]["Phases"]
        self.assertEqual(phases["Overload"]["Offered"], 1600)
        self.assertEqual(phases["Baseline"]["Offered"], 50)
        self.assertEqual(phases["Recovery"]["Offered"], 50)
        self.assertEqual(phases["Overload"]["Rejected"], 1598)
        self.assertEqual(phases["Recovery"]["Completed"], 2)
        self.assertEqual(row["SampleCount"], 6)
        self.assertEqual(row["LoadSummary"]["RemainingAfterDrain"], 0)
        self.assertEqual(row["LoadSummary"]["GenerationStartMilliseconds"], 50)
        self.assertEqual(phases["Recovery"]["StartMilliseconds"], 4050)
        self.assertEqual(phases["Recovery"]["EndMilliseconds"], 6050)

    def test_e6_recovery_activity_includes_overload_cohort_and_keeps_drain(self):
        case = "OpenAsyncScoreboard-E6-Async-1"
        data = invocation(case)
        overloaded = [item for item in data["Samples"] if item["Phase"] == "Overload"]
        for item, start in zip(overloaded, (5650, 6099)):
            item.update(StartMilliseconds=start, TaskReturnMilliseconds=start + .1,
                        CompletionMilliseconds=start + .5, LifecycleCompletionMilliseconds=start + 1)
        data["Load"]["DrainCompletedMilliseconds"] = 6100
        summary = report.summarize(data, case, 1)["LoadSummary"]
        phases = summary["Phases"]
        self.assertEqual(phases["Overload"]["OfferCohortCompleted"], 2)
        self.assertEqual(phases["Overload"]["Completed"], 0)
        self.assertEqual(phases["Overload"]["BacklogAtPhaseEnd"], 2)
        self.assertEqual(phases["Recovery"]["OfferCohortCompleted"], 2)
        self.assertEqual(phases["Recovery"]["Started"], 3)
        self.assertEqual(phases["Recovery"]["Completed"], 3)
        self.assertEqual(phases["Recovery"]["CompletedByOfferPhase"]["Overload"], 1)
        self.assertEqual(phases["Recovery"]["BacklogAtPhaseStart"], 2)
        self.assertEqual(phases["Recovery"]["BacklogAtPhaseEnd"], 1)
        for late in summary["RecoveryWindows"].values():
            self.assertEqual(late["Completed"], 1)
            self.assertEqual(late["CompletedByOfferPhase"]["Overload"], 1)
            self.assertEqual(late["BacklogAtStart"], 2)
            self.assertEqual(late["BacklogAtEnd"], 1)
        self.assertEqual(summary["RemainingAtGenerationEnd"], 1)
        self.assertEqual(summary["Drain"]["Started"], 1)
        self.assertEqual(summary["Drain"]["Completed"], 1)
        self.assertEqual(summary["Drain"]["CompletedByOfferPhase"]["Overload"], 1)
        self.assertTrue(summary["Drain"]["EndInclusive"])
        self.assertEqual(summary["Drain"]["BacklogAtEnd"], 0)

    def test_e6_fractional_generation_epoch_and_phase_boundary_backlog(self):
        case = "OpenAsyncScoreboard-E6-Sync-1"
        data = invocation(case)
        shift = 73.456789
        for field in ("GenerationStartMilliseconds", "GenerationCompletedMilliseconds", "DrainCompletedMilliseconds"):
            data["Load"][field] += shift
        for offer in data["Load"]["Offers"]:
            for field in ("ScheduledMilliseconds", "ActualOfferMilliseconds"):
                offer[field] += shift
        for item in data["Samples"]:
            for field in ("StartMilliseconds", "CompletionMilliseconds", "LifecycleCompletionMilliseconds", "OfferedMilliseconds"):
                item[field] += shift
        boundary = data["Load"]["GenerationStartMilliseconds"] + 2000
        data["Samples"][0]["LifecycleCompletionMilliseconds"] = boundary
        phases = report.summarize(data, case, 1)["LoadSummary"]["Phases"]
        self.assertEqual(phases["Baseline"]["Completed"], 1)
        self.assertEqual(phases["Baseline"]["BacklogAtPhaseEnd"], 0)
        self.assertEqual(phases["Overload"]["Completed"], 3)
        self.assertEqual(phases["Overload"]["CompletedByOfferPhase"]["Baseline"], 1)

    def test_missing_rejected_offer_accepted_sample_and_duplicate_ids_fail(self):
        case = "OpenAsyncScoreboard-E6-Sync-1"
        for mutate in (
            lambda data: data["Load"]["Offers"].pop(),
            lambda data: data["Samples"].pop(),
            lambda data: data["Samples"][0].update(RequestId=data["Samples"][1]["RequestId"]),
            lambda data: data["Samples"][0].update(Phase="Recovery"),
            lambda data: data["Samples"][0].update(LifecycleCompletionMilliseconds=None),
            lambda data: data["Load"].update(DrainCompletedMilliseconds=5999),
            lambda data: data["Load"].pop("GenerationStartMilliseconds"),
            lambda data: data["Load"].update(GenerationStartMilliseconds=-1),
            lambda data: data["Load"].update(GenerationStartMilliseconds=float("nan")),
            lambda data: data["Load"].update(GenerationStartMilliseconds=0),
            lambda data: data["Load"].update(GenerationCompletedMilliseconds=6000),
        ):
            data = invocation(case)
            mutate(data)
            with self.subTest(mutate=mutate), self.assertRaises(ValueError):
                report.summarize(data, case, 1)

    def test_diagnostics_probe_drain_is_not_lost_or_zero_filled(self):
        row = report.summarize(invocation("OpenAsyncScoreboard-E1-Async-1-Diagnostic"),
                               "OpenAsyncScoreboard-E1-Async-1-Diagnostic", 1)
        summary = row["DiagnosticSummary"]
        self.assertEqual(summary["UnrelatedDispatchMilliseconds"]["P50"], 10)
        self.assertEqual(summary["OutstandingAtWorkloadStop"], 1)
        self.assertEqual(summary["DroppedProbes"], 1)
        self.assertEqual(summary["ProbeCompletionStages"], ["PostWorkload"])
        self.assertIsNone(summary["UnrelatedDispatchWithinWorkloadMilliseconds"])
        self.assertEqual(summary["UnrelatedDispatchPostWorkloadMilliseconds"]["P50"], 10)
        broken = diagnostic()
        broken["UnrelatedWork"]["Executed"] = 0
        with self.assertRaises(ValueError):
            report.summarize_diagnostics(broken)

    def test_cumulative_deltas_and_peaks_exclude_drain_and_out_of_window_points(self):
        data = diagnostic()
        first = data["Timeline"][0]
        during = dict(first, TimestampMilliseconds=5, CpuMilliseconds=10, BusyWorkerThreads=2)
        race = dict(first, TimestampMilliseconds=11, CpuMilliseconds=100, BusyWorkerThreads=100)
        final = dict(first, TimestampMilliseconds=12, AfterWorkload=True,
                     CpuMilliseconds=1000, BusyWorkerThreads=1000)
        data["Timeline"] = [first, during, race, final]
        result = report.summarize_diagnostics(data)
        self.assertEqual(result["WorkloadTimelineSampleCount"], 2)
        self.assertEqual(result["InWindowCumulativeDeltas"]["CpuMilliseconds"], 9)
        self.assertEqual(result["PeakBusyWorkerProxy"], 2)
        self.assertFalse(result["GcTraceComplete"])
        data["UnrelatedWork"]["Samples"][0]["ExecutedMilliseconds"] = 6
        result = report.summarize_diagnostics(data, elapsed=4)
        self.assertEqual(result["WorkloadWindowEndMilliseconds"], 4)
        self.assertEqual(result["DiagnosticWindowEndMilliseconds"], 10)
        self.assertEqual(result["WorkloadTimelineSampleCount"], 1)
        self.assertEqual(result["PeakBusyWorkerProxy"], 1)
        self.assertIsNone(result["InWindowCumulativeDeltas"]["CpuMilliseconds"])
        self.assertEqual(result["ProbeCompletionStages"], ["PostWorkload"])
        data["Timeline"] = [final]
        result = report.summarize_diagnostics(data)
        self.assertIsNone(result["PeakBusyWorkerProxy"])
        self.assertIsNone(result["InWindowCumulativeDeltas"]["CpuMilliseconds"])

    def test_workload_equivalence_ignores_driver_runtime_but_not_shape(self):
        left = invocation("OpenAsyncScoreboard-E1-Async-1")
        right = dict(left, DriverVersion="new", Runtime="other", UseConnectionPoolV2=True)
        report.validate_equivalence(left, right)
        right["OperationsPerWorker"] = 1
        with self.assertRaisesRegex(ValueError, "OperationsPerWorker"):
            report.validate_equivalence(left, right)

    def test_repetition_and_stage_are_not_interchangeable(self):
        case = "OpenAsyncScoreboard-E4-Sync-1"
        data = invocation(case)
        with self.assertRaisesRegex(ValueError, "repetition"):
            report.summarize(data, case, 2)
        with self.assertRaisesRegex(ValueError, "stage"):
            report.summarize(data, case, 1, "WorkloadWarmup")


class CollectionTests(unittest.TestCase):
    def setUp(self):
        self.root = Path.cwd() / (".test-scoreboard-" + uuid.uuid4().hex)
        self.root.mkdir()
        self.addCleanup(shutil.rmtree, self.root)
        self.case = "OpenAsyncScoreboard-E4-Async-1"

    def write_case(self, directory, case, repetition):
        directory.mkdir(parents=True, exist_ok=True)
        for stage, count, subdir in (("WorkloadActual", 3, ""), ("WorkloadWarmup", 2, "warmup")):
            target = directory / subdir
            target.mkdir(exist_ok=True)
            for index in range(1, count + 1):
                data = invocation(case, repetition, index, stage)
                (target / f"openasync-{index}.json").write_text(json.dumps(data), encoding="utf-8")

    def test_collection_preserves_identical_names_in_actual_and_warmup(self):
        work = self.root / "work"
        self.write_case(work / "BenchmarkDotNet.Artifacts" / "results", self.case, 1)
        interleave_perf.collect_results(str(work), str(self.root / "dest"), self.case)
        rows = report.load_case(self.root / "dest" / self.case, self.case, 1)
        self.assertEqual(len(rows), 5)
        self.assertEqual(sum(row["MeasurementStage"] == "WorkloadActual" for row in rows), 3)

    def test_preliminary_accepts_new_schema_without_changing_its_matrix_or_iterations(self):
        case = "OpenAsyncScoreboard-E1-Sync-1"
        directory = self.root / "current" / case
        directory.mkdir(parents=True)
        for index in range(1, 4):
            data = invocation(case, index=index)
            data.update(OperationsPerWorker=16, WarmupCount=0)
            data["Samples"] = data["Samples"][:16]
            data.pop("RunId")
            for field in report.LOAD_FIELDS:
                data.pop(field)
            (directory / f"openasync-{index}.json").write_text(json.dumps(data), encoding="utf-8")
        rows = v1.load_case(str(directory), case)
        self.assertEqual(len(rows), 3)
        self.assertTrue(all(row["OperationsPerWorker"] == 16 for row in rows))
        self.assertTrue(all(row["RunId"] is None for row in rows))
        v1.write_summary(str(self.root), [case], False)
        self.assertTrue((self.root / "openasync-summary.md").exists())

    def test_summary_retains_independent_repetitions_and_warmups(self):
        for repetition in range(1, 4):
            self.write_case(self.root / "repetitions" / f"rep{repetition}" / "current" / self.case,
                            self.case, repetition)
        report.write_summary(str(self.root), [self.case], False, 3)
        result = json.loads((self.root / "openasync-summary.json").read_text())
        self.assertEqual(len(result["Invocations"]), 15)
        self.assertEqual(len(result["RepetitionVariability"]), 3)
        self.assertEqual(result["RepetitionVariability"][0]["ProcessRepetitions"], [1, 2, 3])

    def test_sync_async_and_variant_order_counterbalance(self):
        units = ["OpenAsyncScoreboard-E1-Async-1", "OpenAsyncScoreboard-E1-Sync-1"]
        runner = interleave_perf.Runner("base", "current", "app", str(self.root), [],
                                       benchmark_profile=v1.FULL_PROFILE)
        for repetition, expected_api, expected_variant in ((1, "Sync", "baseline"), (2, "Async", "current"),
                                                           (3, "Sync", "baseline")):
            with mock.patch.object(runner, "_run_one", return_value={"Runner"}) as run, \
                    contextlib.redirect_stdout(io.StringIO()):
                runner.interleave(units, repetition, "b", "c")
            self.assertEqual(run.call_args_list[0].args[0], expected_variant)
            self.assertIn(f"-{expected_api}-", run.call_args_list[0].args[2])

    def test_predeclared_repetitions_do_not_enter_regression_reruns(self):
        runner = interleave_perf.Runner(None, "current", "app", str(self.root), [],
                                       benchmark_profile=v1.FULL_PROFILE, scoreboard_repetitions=3)
        with mock.patch.object(runner, "interleave") as run, \
                mock.patch.object(report, "write_summary"), \
                mock.patch.object(interleave_perf.compare_perf, "build_comparison", return_value=[]), \
                mock.patch.object(interleave_perf, "_regression_keys", side_effect=AssertionError("gate")):
            interleave_perf.orchestrate(runner, [self.case], str(self.root), 10, 99)
        self.assertEqual(run.call_count, 3)

    def test_controls_current_only_use_normal_reports_without_scoreboard_or_reruns(self):
        def run_control(exe_dir, assembly, unit, cwd, cpus, log_path, **kwargs):
            directory = Path(cwd) / "BenchmarkDotNet.Artifacts" / "results"
            directory.mkdir(parents=True)
            report_data = {"Benchmarks": [{"Type": unit + "Runner", "Method": "ExistingControl",
                                          "Parameters": "", "Statistics": {"Mean": 1000, "N": 17}}]}
            (directory / f"{unit}-report-full.json").write_text(json.dumps(report_data), encoding="utf-8")
            return 0

        with mock.patch.object(interleave_perf.Runner, "list_units", return_value=v1.CONTROL_UNITS), \
                mock.patch.object(interleave_perf, "run_unit_process", side_effect=run_control) as run, \
                mock.patch.object(report, "write_summary", side_effect=AssertionError("scoreboard schema")), \
                mock.patch.object(v1, "write_summary", side_effect=AssertionError("scoreboard schema")), \
                mock.patch.object(interleave_perf, "_regression_keys", side_effect=AssertionError("reruns")), \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            status = interleave_perf.main([
                "--current-exe-dir", "current", "--results-dir", str(self.root),
                "--work-dir", str(self.root / "work"), "--benchmark-profile", v1.CONTROLS_PROFILE,
                "--reps", "99"])
        self.assertEqual(status, 0)
        self.assertEqual(run.call_count, 2)
        data = json.loads((self.root / "comparison" / "comparison.json").read_text())
        self.assertEqual(data["confirmationRuns"], 1)
        self.assertTrue(all(entry["status"] == "report-only" for entry in data["entries"]))
        self.assertEqual(len(data["entries"]), 2)
        self.assertFalse((self.root / "openasync-summary.json").exists())
        self.assertIn("existing BDN workloads", (self.root / "summary.md").read_text(encoding="utf-8"))

    def test_controls_reject_regression_gate_before_processes(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
            interleave_perf.main(["--current-exe-dir", "current", "--results-dir", str(self.root),
                                  "--benchmark-profile", v1.CONTROLS_PROFILE, "--fail-on-regression"])
        self.assertEqual(error.exception.code, 2)

    def test_full_orchestration_retains_every_variant_repetition_and_stage(self):
        units = ["OpenAsyncScoreboard-E4-Sync-1", "OpenAsyncScoreboard-E4-Async-1"]
        runner = interleave_perf.Runner("base", "current", "app", str(self.root / "work"), [],
                                       benchmark_profile=v1.FULL_PROFILE, scoreboard_repetitions=3,
                                       unit_timeout_seconds=600)

        def run_case(exe_dir, assembly, unit, cwd, cpus, log_path, **kwargs):
            repetition = int(kwargs["env_overrides"]["PERF_PROCESS_REPETITION"])
            directory = Path(cwd) / "BenchmarkDotNet.Artifacts" / "results"
            self.write_case(directory, unit, repetition)
            measurements = []
            for stage, count in (("Actual", 3), ("Warmup", 2)):
                measurements.extend(dict(IterationMode="Workload", IterationStage=stage,
                                         LaunchIndex=1, IterationIndex=index, Operations=1, Nanoseconds=1000)
                                    for index in range(1, count + 1))
            bdn = {"Benchmarks": [dict(Type="Runner", Method="OpenCohort", Parameters=f"Case={unit}",
                                      Statistics={"Mean": 1000, "N": 3}, Measurements=measurements)]}
            (directory / "same-report-full.json").write_text(json.dumps(bdn), encoding="utf-8")
            return 0

        with mock.patch.object(interleave_perf, "run_unit_process", side_effect=run_case) as run, \
                contextlib.redirect_stdout(io.StringIO()):
            entries, confirmed, _ = interleave_perf.orchestrate(runner, units, str(self.root), 10, 99)
        self.assertEqual(run.call_count, 12)
        run_ids = [call.kwargs["env_overrides"]["PERF_RUN_ID"] for call in run.call_args_list]
        self.assertEqual(len(set(run_ids)), 12)
        for run_id in run_ids:
            uuid.UUID(run_id)
        self.assertEqual(len(entries), 6)
        self.assertFalse(confirmed)
        output = json.loads((self.root / "openasync-summary.json").read_text())
        self.assertEqual(len(output["Invocations"]), 60)
        self.assertEqual(len(output["Comparisons"]), 6)
        self.assertEqual(len(output["Comparisons"][0]["ActualInvocationComparisons"]), 3)

    def test_external_capture_labels_cross_boundary_without_inventing_attribution(self):
        case = self.case + "-Diagnostic"
        instance = observer.Observer(str(self.root), case, 123, {"stack": "stack", "sqlcmd": "sql"})
        marker = dict(Active=True, Stage="WorkloadActual", CaseId=case, ProcessId=123, InvocationId="one")
        sql = dict(ActiveUserRequests=1, RunnableTasks=2, RunningTasks=3, SchedulerRunnableQueue=1,
                   SchedulerCurrentTasks=2, UserSessions=2, Connections=3, SqlProcessCpuPercent=None,
                   LoginsCounter=9,
                   Waits=[{"WaitType": "WAIT", "Tasks": 1, "Milliseconds": 2}])

        def command(args, path, env=None):
            if args[0] == "stack":
                path.write_text("Thread (0x1):\nSystem.Threading.WaitHandle.WaitOne()\n"
                                "Microsoft.Data.SqlClient.SqlConnection.Open()\n", encoding="utf-8")
            else:
                self.assertNotIn("<pwd>", args)
                self.assertEqual(env["SQLCMDPASSWORD"], "<pwd>")
                self.assertNotIn("-h", args)
                self.assertEqual(args[args.index("-y") + 1], "0")
                path.write_text(sqlcmd_output(sql, width=80), encoding="utf-8")

        calls = threading.local()
        def boundary_marker():
            count = getattr(calls, "count", 0)
            calls.count = count + 1
            return dict(marker, Active=count % 2 == 0)

        with mock.patch.object(instance, "command", side_effect=command), \
                mock.patch.object(instance, "marker", side_effect=boundary_marker), \
                mock.patch.dict(os.environ, {"SQL_SERVER": "test", "SQL_PASSWORD": "<pwd>"}):
            instance.capture(marker)
        self.assertEqual(instance.records[0]["StackOverlap"], "AmbiguousBoundary")
        self.assertEqual(instance.records[0]["SqlOverlap"], "AmbiguousBoundary")
        self.assertEqual(instance.records[0]["StackCounts"]["OpenWithBlockingPrimitiveThreads"], 1)
        self.assertIn("Unavailable", instance.records[0]["SqlProcessCpuAvailability"])

    def test_external_collectors_do_not_start_after_actual_marker_ended(self):
        instance = observer.Observer(str(self.root), self.case, 123, {"stack": "stack", "sqlcmd": "sql"})
        marker = dict(Active=True, Stage="WorkloadActual", CaseId=self.case, ProcessId=123, InvocationId="one")
        with mock.patch.object(instance, "command") as command, \
                mock.patch.object(instance, "marker", return_value=dict(marker, Active=False)), \
                mock.patch.dict(os.environ, {"SQL_SERVER": "test", "SQL_PASSWORD": "<pwd>"}):
            instance.capture(marker)
        command.assert_not_called()
        self.assertIsNone(instance.records[0]["StackCounts"])
        self.assertIsNone(instance.records[0]["Sql"])
        self.assertEqual(instance.records[0]["SqlOverlap"], "NotCollectedOutsideActual")

    def test_missing_collectors_fail_and_write_metadata(self):
        instance = observer.Observer(str(self.root), self.case, 123, {})
        instance.thread = mock.Mock()
        instance.thread.is_alive.return_value = False
        with self.assertRaisesRegex(RuntimeError, "collectors"):
            instance.finish()
        result = json.loads((instance.directory / "observations.json").read_text())
        self.assertEqual(result["Coverage"], "Failed")
        self.assertFalse(result["TimingComparableWithSampled"])

    def test_short_windows_report_insufficient_without_zero_cohort_metrics(self):
        case = self.case + "-Diagnostic"
        work = self.root / "work"
        self.write_case(work / "BenchmarkDotNet.Artifacts" / "results", case, 1)
        instance = observer.Observer(str(work), case, 1001,
                                     {"stack": "stack", "sqlcmd": "sql", "sql_capability_verified": True})
        self.assertFalse(instance.marker_path.exists())
        instance.thread = mock.Mock()
        instance.thread.is_alive.return_value = False
        instance.finish()
        result = json.loads((instance.directory / "observations.json").read_text())
        self.assertEqual(result["Coverage"], "InsufficientWindowSamples")
        self.assertIsNone(result["Error"])
        self.assertEqual(len(result["InvocationCoverage"]), 3)
        for coverage in result["InvocationCoverage"]:
            self.assertEqual(coverage["ActualWindow"]["WindowSource"], "ExportedActualArtifact")
            self.assertIsNotNone(coverage["ActualWindow"]["CompletedUtc"])
            self.assertEqual(coverage["CaptureAttempts"], 0)
        interleave_perf.collect_results(str(work), str(self.root / "dest"), case)
        rows = report.load_case(self.root / "dest" / case, case, 1)
        actual = [row for row in rows if row["MeasurementStage"] == "WorkloadActual"]
        self.assertEqual(actual[0]["ExternalDiagnosticCoverage"]["Coverage"], "InsufficientWindowSamples")
        self.assertIsNone(actual[0]["ExternalDiagnostics"]["StackSnapshots"])
        self.assertIsNone(actual[0]["ExternalDiagnostics"]["ServerSnapshots"])

    def test_exported_windows_reconcile_partial_markers_and_capture_intervals(self):
        case = self.case + "-Diagnostic"
        self.write_case(self.root / "BenchmarkDotNet.Artifacts" / "results", case, 1)
        instance = observer.Observer(str(self.root), case, 1001,
                                     {"stack": "stack", "sqlcmd": "sql", "sql_capability_verified": True})
        invocation_id = "WorkloadActual-1"
        instance.windows[invocation_id] = {"StartedUtc": "2026-01-01T00:00:01Z", "CompletedUtc": None}
        capture = {"StartedUtc": "2026-01-01T00:00:01.0001Z",
                   "CompletedUtc": "2026-01-01T00:00:01.0002Z"}
        instance.records = [dict(InvocationId=invocation_id, StackCapture=capture, SqlCapture=capture,
                                 SqlCounterDeltaScope="CrossInvocationOrAmbiguous")]
        instance.thread = mock.Mock()
        instance.thread.is_alive.return_value = False
        instance.finish()
        result = json.loads((instance.directory / "observations.json").read_text())
        self.assertEqual(result["Snapshots"][0]["StackOverlap"], "ContainedInActual")
        self.assertEqual(result["InvocationCoverage"][0]["Coverage"], "Covered")
        self.assertEqual(result["Coverage"], "InsufficientWindowSamples")

    def test_missing_exported_window_and_real_collector_errors_still_fail(self):
        case = self.case + "-Diagnostic"
        directory = self.root / "BenchmarkDotNet.Artifacts" / "results"
        self.write_case(directory, case, 1)
        for collector_error in (None, "Actual collector failure"):
            instance = observer.Observer(str(self.root), case, 1001,
                                         {"stack": "stack", "sqlcmd": "sql", "sql_capability_verified": True})
            instance.thread = mock.Mock()
            instance.thread.is_alive.return_value = False
            instance.error = collector_error
            if collector_error is None:
                path = next(directory.glob("openasync-*.json"))
                data = json.loads(path.read_text())
                data["CompletedUtc"] = None
                path.write_text(json.dumps(data), encoding="utf-8")
            with self.subTest(collector_error=collector_error), self.assertRaises(RuntimeError):
                instance.finish()
            result = json.loads((instance.directory / "observations.json").read_text())
            self.assertEqual(result["Coverage"], "Failed")

    def test_sql_permission_preflight_fails_even_without_measurement_opportunity(self):
        tool_dir = self.root / "observer-tools"
        tool_dir.mkdir()
        (tool_dir / "dotnet-stack").write_text("", encoding="utf-8")
        platform = SimpleNamespace(name="posix", path=os.path,
                                   environ={"SQL_SERVER": "test", "SQL_PASSWORD": "<pwd>"})
        def provision(command, **kwargs):
            version = "--version" in command
            kwargs["stdout"].write(observer.STACK_VERSION if version else "SQL permission denied")
            return subprocess.CompletedProcess(command, 0 if version else 1)

        with mock.patch.object(observer, "os", platform), \
                mock.patch.object(observer.shutil, "which", return_value="sqlcmd"), \
                mock.patch.object(observer.subprocess, "run", side_effect=provision) as run:
            with self.assertRaisesRegex(RuntimeError, "permission preflight"):
                observer.prepare_tools(str(self.root))
        self.assertEqual(run.call_count, 2)
        self.assertNotIn("<pwd>", run.call_args.args[0])
        self.assertEqual(run.call_args.kwargs["env"]["SQLCMDPASSWORD"], "<pwd>")

    def test_tool_install_uses_approved_feed_and_preserves_failure_log_in_results(self):
        results = self.root / "published"
        platform = SimpleNamespace(name="posix", path=os.path, environ={})

        def fail(command, **kwargs):
            kwargs["stdout"].write("Unable to load the configured NuGet service index.")
            return subprocess.CompletedProcess(command, 1)

        with mock.patch.object(observer, "os", platform), \
                mock.patch.object(observer.subprocess, "run", side_effect=fail) as run:
            with self.assertRaisesRegex(RuntimeError, "published.*install.log"):
                observer.prepare_tools(str(self.root / "work"), str(results))
        command = run.call_args.args[0]
        config = Path(command[command.index("--configfile") + 1])
        self.assertIn("<clear />", config.read_text())
        self.assertIn(observer.STACK_SOURCE, config.read_text())
        self.assertIn('key="governed"', config.read_text())
        self.assertNotIn("nuget.org", config.read_text())
        self.assertNotIn('key="local"', config.read_text())
        self.assertEqual(command[command.index("--version") + 1], "9.0.652701")
        self.assertIn("--allow-roll-forward", command)
        self.assertEqual(run.call_args.kwargs["timeout"], 180)
        self.assertIn("Unable to load", (results / "diagnostics" / "observer-tools" / "install.log").read_text())
        metadata = json.loads((results / "diagnostics" / "observer-tools" / "provisioning.json").read_text())
        self.assertEqual(metadata["WorkingDirectory"], str(Path.cwd()))
        self.assertEqual(metadata["PackageSource"], observer.STACK_SOURCE)

    def test_tool_install_timeout_preserves_partial_output_in_results(self):
        platform = SimpleNamespace(name="posix", path=os.path, environ={})

        def timeout(command, **kwargs):
            kwargs["stdout"].write("Restore started.\n")
            raise subprocess.TimeoutExpired(command, kwargs["timeout"])

        with mock.patch.object(observer, "os", platform), \
                mock.patch.object(observer.subprocess, "run", side_effect=timeout):
            with self.assertRaisesRegex(RuntimeError, "TimeoutExpired.*published"):
                observer.prepare_tools(str(self.root / "work"), str(self.root / "published"))
        output = (self.root / "published" / "diagnostics" / "observer-tools" / "install.log").read_text()
        self.assertIn("Restore started.", output)
        self.assertIn("TimeoutExpired", output)

    def test_successful_tool_and_sql_preflight_retains_all_readiness_logs(self):
        platform = SimpleNamespace(name="posix", path=os.path,
                                   environ={"SQL_SERVER": "test", "SQL_PASSWORD": "<pwd>"})
        sql = {key: 0 for key in ("ActiveUserRequests", "RunnableTasks", "RunningTasks",
                                 "SchedulerRunnableQueue", "SchedulerCurrentTasks", "UserSessions",
                                 "Connections", "LoginsCounter")}
        sql["Waits"] = []

        def provision(command, **kwargs):
            if command[0] == "dotnet":
                output = "Tool installed."
            elif "--version" in command:
                output = observer.STACK_VERSION + "+240cb1ce5bc30594c515206764241d7982e384af"
            else:
                self.assertNotIn("<pwd>", command)
                self.assertNotIn("-h", command)
                self.assertEqual(command[command.index("-y") + 1], "0")
                output = sqlcmd_output(sql, width=80)
            kwargs["stdout"].write(output)
            return subprocess.CompletedProcess(command, 0)

        with mock.patch.object(observer, "os", platform), \
                mock.patch.object(observer.shutil, "which", return_value="sqlcmd"), \
                mock.patch.object(observer.subprocess, "run", side_effect=provision):
            tools = observer.prepare_tools(str(self.root / "work"), str(self.root / "published"))
        self.assertTrue(tools["sql_capability_verified"])
        logs = self.root / "published" / "diagnostics" / "observer-tools"
        for name in ("install.log", "version.log", "sql-capability-preflight.json", "provisioning.json"):
            self.assertTrue((logs / name).is_file())


class ObserverTests(unittest.TestCase):
    def test_shared_sql_batch_sets_required_xml_session_options_before_query(self):
        with mock.patch.dict(os.environ, {"SQL_SERVER": "test"}):
            command = observer.sql_command("sqlcmd")
        query = command[command.index("-Q") + 1]
        query_start = query.index("DECLARE @Snapshot")
        for setting in ("SET NOCOUNT ON;", "SET QUOTED_IDENTIFIER ON;"):
            self.assertLess(query.index(setting), query_start)
        self.assertIn("monitor.Payload.value(", query)
        self.assertIn("FROM sys.dm_os_ring_buffers", query)
        self.assertIn("SqlProcessCpuPercent", query)

    def test_sqlcmd_command_uses_compatible_unlimited_output_options(self):
        with mock.patch.dict(os.environ, {"SQL_SERVER": "test"}):
            command = observer.sql_command("sqlcmd")
        self.assertEqual(command[command.index("-y") + 1], "0")
        self.assertEqual(command[command.index("-w") + 1], "65535")
        self.assertNotIn("-h", command)
        self.assertNotIn("-W", command)
        self.assertIn("-b", command)
        query = command[command.index("-Q") + 1]
        self.assertIn("SET NOCOUNT ON", query)
        self.assertIn("DECLARE @Snapshot nvarchar(max)", query)
        self.assertIn(f"SELECT @Snapshot AS [{observer.SQL_JSON_COLUMN}]", query)

    def test_sqlcmd_parser_preserves_large_wrapped_json_and_optional_headers(self):
        sql = {key: 0 for key in ("ActiveUserRequests", "RunnableTasks", "RunningTasks",
                                 "SchedulerRunnableQueue", "SchedulerCurrentTasks", "UserSessions",
                                 "Connections", "LoginsCounter")}
        sql["Waits"] = [{"WaitType": f"WAIT_{index}", "Tasks": index, "Milliseconds": index * 3}
                        for index in range(3000)]
        self.assertGreater(len(json.dumps(sql)), 65535)
        for headers in (True, False):
            with self.subTest(headers=headers):
                self.assertEqual(observer.parse_sql_output(sqlcmd_output(sql, headers=headers)), sql)
        # Include whitespace within a wrapped JSON string; trimming each physical line corrupts it.
        sql["Waits"] = [{"WaitType": "ABC DEF", "Tasks": 0, "Milliseconds": 0}]
        self.assertEqual(observer.parse_sql_output(sqlcmd_output(sql, width=1)), sql)

    def test_sqlcmd_parser_rejects_errors_truncation_and_unrecognized_framing(self):
        for text in (
            'Sqlcmd: The -h and the -y 0 options are mutually exclusive.',
            '{"Waits": [',
            '{"Waits": []}\nMsg 297, Level 16: permission denied',
            f'{observer.SQL_JSON_COLUMN}\nmissing separator\n{{"Waits":[]}}',
            'OtherColumn\n-----------\n{"Waits":[]}',
            '{"Waits":[]}\n(1 rows affected)',
        ):
            with self.subTest(text=text), self.assertRaises(ValueError):
                observer.parse_sql_output(text)

    def test_utc_intervals_not_active_marker_alone_determine_association(self):
        window = {"StartedUtc": "2026-01-01T00:00:01Z", "CompletedUtc": "2026-01-01T00:00:02Z"}
        def capture(start, end):
            return {"StartedUtc": f"2026-01-01T00:00:0{start}Z",
                    "CompletedUtc": f"2026-01-01T00:00:0{end}Z"}
        self.assertEqual(observer.association(capture("1.1", "1.9"), window), "ContainedInActual")
        self.assertEqual(observer.association(capture("1.9", "2.1"), window), "AmbiguousBoundary")
        self.assertEqual(observer.association(capture("2.1", "2.5"), window), "OutsideActual")
        self.assertEqual(observer.association(capture("1.1", "1.9"), None), "AmbiguousBoundary")
        self.assertEqual(observer.association(capture("1", "2"), window), "AmbiguousBoundary")

    def test_blocked_open_counts_require_both_frames_in_same_thread(self):
        text = """Thread (0x1):
System.Threading.WaitHandle.WaitOneCore
Microsoft.Data.SqlClient.SqlConnection.Open()
Thread (0x2):
Microsoft.Data.SqlClient.SqlConnection.OpenAsync()
Thread (0x3):
System.Threading.Monitor.Wait()
"""
        result = observer.stack_counts(text)
        self.assertEqual(result["ObservedThreads"], 3)
        self.assertEqual(result["OpenWithBlockingPrimitiveThreads"], 1)
        with self.assertRaises(ValueError):
            observer.stack_counts("No stack available")

    def test_cumulative_wait_resets_fail_not_negative_deltas(self):
        before = {"Waits": [{"WaitType": "WAIT", "Tasks": 2, "Milliseconds": 3}]}
        after = {"Waits": [{"WaitType": "WAIT", "Tasks": 3, "Milliseconds": 8}]}
        self.assertEqual(observer.waits_delta(before, after)[0]["Milliseconds"], 5)
        with self.assertRaises(ValueError):
            observer.waits_delta(after, before)

    def test_windows_capability_is_explicitly_unsupported(self):
        with mock.patch.object(observer.os, "name", "nt"), self.assertRaisesRegex(RuntimeError, "unsupported"):
            observer.prepare_tools("unused")

    def test_only_matching_actual_active_markers_are_eligible(self):
        instance = object.__new__(observer.Observer)
        instance.case, instance.process_id = "case", 123
        good = dict(Active=True, Stage="WorkloadActual", CaseId="case", ProcessId=123, InvocationId="one")
        self.assertTrue(instance.eligible(good))
        for change in (dict(Active=False), dict(Stage="WorkloadWarmup"), dict(CaseId="other"), dict(ProcessId=999)):
            self.assertFalse(instance.eligible(dict(good, **change)))


if __name__ == "__main__":
    unittest.main()
