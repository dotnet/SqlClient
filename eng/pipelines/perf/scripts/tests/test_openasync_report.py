# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""No SQL Server or benchmark execution is required for these reporting contracts."""

import copy
import contextlib
import io
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import unittest
from unittest import mock
import uuid

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))
import interleave_perf
import openasync_report as report


def invocation(case="OpenAsyncScoreboard-E1-Async-1", index=1):
    _, scenario, api, concurrency = case.split("-")
    concurrency = int(concurrency)
    operations = 1 if scenario == "E4" else 16
    data = dict(
        SchemaVersion=1, CaseId=case, Scenario=scenario, Api=api, Concurrency=concurrency,
        OperationsPerWorker=operations, PoolSize=0 if scenario == "E3" else 4 if scenario == "E2" else concurrency,
        HoldMilliseconds=10 if scenario == "E2" else 0, UseConnectionPoolV2=False,
        Runtime=".NET 10", OS="test OS", DriverVersion="test driver", DriverAssemblyVersion="7.0.0.0",
        ProcessorCount=8, Invocation=index, ElapsedMilliseconds=1000,
        AdmissionMilliseconds=[1] * concurrency,
        Samples=[dict(Worker=worker, Operation=operation, StartMilliseconds=1,
                      TaskReturnMilliseconds=2 if api == "Async" else None,
                      CompletionMilliseconds=operation + 3,
                      CompletedAtReturn=False if api == "Async" else None,
                      Outcome="Success", ErrorType=None, SqlErrorNumber=None)
                 for worker in range(concurrency) for operation in range(operations)],
    )
    data.update({field: 1 for field in report.RESOURCE_FIELDS})
    if scenario == "E4":
        data["PeakOpenConnections"] = concurrency
    return data


class ValidationTests(unittest.TestCase):
    def test_exact_preliminary_matrix(self):
        self.assertEqual(len(report.CASES), 24)
        for case in report.CASES:
            with self.subTest(case=case):
                data = invocation(case)
                row = report.summarize_invocation(data, case)
                self.assertEqual(row["Outcomes"]["Success"], len(data["Samples"]))
                self.assertEqual(row["SuccessThroughputPerSecond"], len(data["Samples"]))
                self.assertEqual(row["TaskReturnMilliseconds"] is None, data["Api"] == "Sync")

    def test_percentiles_are_individual_open_latencies(self):
        data = invocation()
        row = report.summarize_invocation(data, data["CaseId"])
        self.assertEqual(row["OpenLatencyMilliseconds"]["P50"], 9.5)
        self.assertAlmostEqual(row["OpenLatencyMilliseconds"]["P95"], 16.25)
        self.assertAlmostEqual(row["OpenLatencyMilliseconds"]["P99"], 16.85)
        self.assertEqual(row["TaskReturnMilliseconds"]["P99"], 1)
        self.assertIsNone(report.distribution([]))

    def test_missing_duplicate_or_misidentified_samples_fail(self):
        for change in (
            lambda data: data["Samples"].pop(),
            lambda data: data["Samples"].append(data["Samples"][0]),
            lambda data: data["Samples"][0].update(Worker=99),
            lambda data: data["Samples"][0].update(Operation=1),
            lambda data: data.update(CaseId="OpenAsyncScoreboard-E2-Async-1"),
            lambda data: data.update(Api="Sync"),
            lambda data: data.update(OperationsPerWorker=8),
            lambda data: data.update(PoolSize=2),
            lambda data: data.update(AdmissionMilliseconds=[]),
            lambda data: data.update(SchemaVersion=2),
        ):
            data = invocation()
            change(data)
            with self.subTest(change=change), self.assertRaises(ValueError):
                report.summarize_invocation(data, "OpenAsyncScoreboard-E1-Async-1")

    def test_missing_nonfinite_and_negative_timings_never_become_zero(self):
        for field in ("StartMilliseconds", "CompletionMilliseconds", "TaskReturnMilliseconds"):
            for value in (None, float("nan"), float("inf"), -1):
                data = invocation()
                data["Samples"][0][field] = value
                with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                    report.summarize_invocation(data, data["CaseId"])
        for field in ("CpuMilliseconds", "ElapsedMilliseconds", "AllocatedBytes"):
            data = invocation()
            data[field] = float("nan")
            with self.subTest(field=field), self.assertRaises(ValueError):
                report.summarize_invocation(data, data["CaseId"])

    def test_relative_timestamps_must_be_ordered_and_within_invocation(self):
        for field, value in (("CompletionMilliseconds", .5), ("CompletionMilliseconds", 1001),
                             ("TaskReturnMilliseconds", .5), ("TaskReturnMilliseconds", 1001)):
            data = invocation()
            data["Samples"][0][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                report.summarize_invocation(data, data["CaseId"])

    def test_sync_cannot_claim_task_return(self):
        data = invocation("OpenAsyncScoreboard-E1-Sync-1")
        data["Samples"][0]["TaskReturnMilliseconds"] = 2
        with self.assertRaisesRegex(ValueError, "Sync sample"):
            report.summarize_invocation(data, data["CaseId"])

    def test_failed_outcomes_remain_accountable_without_inventing_timings(self):
        data = invocation()
        for sample, outcome in zip(data["Samples"], ("Failed", "Canceled")):
            sample.update(Outcome=outcome, ErrorType="TestException",
                          TaskReturnMilliseconds=None, CompletedAtReturn=None)
        row = report.summarize_invocation(data, data["CaseId"])
        self.assertEqual(row["Outcomes"], {"Success": 14, "Failed": 1, "Canceled": 1})
        self.assertEqual(row["TaskReturnMilliseconds"]["Count"], 14)
        self.assertEqual(row["SuccessThroughputPerSecond"], 14)

    def test_profile_is_opt_in_and_bounded(self):
        config = {"Benchmarks": {"Other": {"Enabled": True, "IterationCount": 50}},
                  "UseConnectionPoolV2": True}
        original = copy.deepcopy(config)
        report.apply_profile(config, "default")
        self.assertEqual(config, original)
        report.apply_profile(config, report.PROFILE)
        self.assertFalse(config["Benchmarks"]["Other"]["Enabled"])
        self.assertTrue(config["UseConnectionPoolV2"])
        self.assertEqual(config["Benchmarks"]["OpenAsyncScoreboardRunnerConfig"], {
            "Enabled": True, "LaunchCount": 1, "IterationCount": 3, "InvocationCount": 1,
            "WarmupCount": 0, "RunStrategy": "Monitoring", "TimeoutMinutes": 5,
        })
        self.assertEqual(config["OpenAsyncScoreboard"]["Concurrency"], [1, 8, 32])


class ArtifactTests(unittest.TestCase):
    def setUp(self):
        self.root = Path.cwd() / (".test-openasync-" + uuid.uuid4().hex)
        self.root.mkdir()
        self.addCleanup(shutil.rmtree, self.root)
        self.case = "OpenAsyncScoreboard-E1-Async-1"

    def write_case(self, directory, case=None, failed=False):
        directory.mkdir(parents=True, exist_ok=True)
        case = case or self.case
        for index in range(1, 4):
            data = invocation(case, index)
            # Different elapsed times must produce separate rows, not pooled percentiles.
            data["ElapsedMilliseconds"] *= index
            if failed:
                data["Samples"][0].update(Outcome="Failed", ErrorType="TestException")
            (directory / f"openasync-{index}.json").write_text(json.dumps(data), encoding="utf-8")
        (directory / "SameType-report-full.json").write_text(json.dumps({
            "Benchmarks": [{"Type": "OpenAsyncScoreboardRunner", "Method": "OpenCohort",
                            "Parameters": f"Case={case}", "Statistics": {"Mean": 1000000, "N": 3},
                            "Measurements": [
                                {"IterationMode": "Workload", "IterationStage": "Actual", "LaunchIndex": 1,
                                 "IterationIndex": index, "Operations": 1, "Nanoseconds": 1000000}
                                for index in range(1, 4)]}]
        }), encoding="utf-8")

    def test_collection_keeps_same_bdn_filename_for_all_cases(self):
        destination = self.root / "results" / "current"
        for case in (self.case, "OpenAsyncScoreboard-E2-Async-1"):
            run_dir = self.root / case
            self.write_case(run_dir / "BenchmarkDotNet.Artifacts" / "results", case)
            types = interleave_perf.collect_results(str(run_dir), str(destination), case, strict=True)
            self.assertEqual(types, {"OpenAsyncScoreboardRunner"})
        self.assertEqual(len(list(destination.rglob("*-report-full.json"))), 2)
        self.assertEqual(len(list(destination.rglob("openasync-*.json"))), 6)
        self.assertEqual(len(interleave_perf.compare_perf._load_benchmarks(str(destination))), 2)

    def test_recollection_does_not_mix_stale_invocations(self):
        run_dir = self.root / "run"
        self.write_case(run_dir / "BenchmarkDotNet.Artifacts" / "results")
        dest = self.root / "results"
        interleave_perf.collect_results(str(run_dir), str(dest), self.case, strict=True)
        (dest / self.case / "openasync-stale.json").write_text("{}", encoding="utf-8")
        interleave_perf.collect_results(str(run_dir), str(dest), self.case, strict=True)
        self.assertEqual(len(report.load_case(str(dest / self.case), self.case)), 3)

    def test_failed_runner_preserves_supplements_before_failing(self):
        runner = interleave_perf.Runner(None, "current", "PerformanceTests.dll",
                                       str(self.root / "work"), [], benchmark_profile=report.PROFILE)

        def fail_run(exe_dir, assembly, unit, cwd, cpus, log_path, **kwargs):
            self.write_case(Path(cwd) / "BenchmarkDotNet.Artifacts" / "results", failed=True)
            Path(log_path).write_text("Failed invocation", encoding="utf-8")
            return 1

        with mock.patch.object(interleave_perf, "run_unit_process", side_effect=fail_run), \
                contextlib.redirect_stderr(io.StringIO()), self.assertRaisesRegex(RuntimeError, "exit 1"):
            runner._run_one("current", "current", self.case, 1, str(self.root / "results"))
        rows = report.load_case(str(self.root / "results" / self.case), self.case)
        self.assertEqual(rows[0]["Outcomes"]["Failed"], 1)

    def test_bdn_case_mismatch_is_not_silently_compared(self):
        run_dir = self.root / "run"
        self.write_case(run_dir / "BenchmarkDotNet.Artifacts" / "results",
                        "OpenAsyncScoreboard-E2-Async-1")
        with self.assertRaisesRegex(ValueError, "BDN parameter case"):
            interleave_perf.collect_results(str(run_dir), str(self.root / "dest"),
                                            self.case, strict=True)

    def test_invalid_or_missing_bdn_results_fail(self):
        run_dir = self.root / "run"
        artifacts = run_dir / "BenchmarkDotNet.Artifacts" / "results"
        for stats in (None, {"N": 3, "Mean": None}, {"N": 2, "Mean": 10},
                      {"N": 3, "Mean": float("nan")}):
            self.write_case(artifacts)
            path = artifacts / "SameType-report-full.json"
            data = json.loads(path.read_text())
            data["Benchmarks"][0]["Statistics"] = stats
            path.write_text(json.dumps(data), encoding="utf-8")
            with self.subTest(stats=stats), self.assertRaises(ValueError):
                interleave_perf.collect_results(str(run_dir), str(self.root / "dest"), self.case, strict=True)
        with self.assertRaises(ValueError):
            interleave_perf.collect_results(str(self.root / "missing"), str(self.root / "dest"),
                                            self.case, strict=True)

    def test_actual_records_are_complete_distinct_single_invocations(self):
        run_dir = self.root / "run"
        artifacts = run_dir / "BenchmarkDotNet.Artifacts" / "results"
        for change in (
            lambda measurements: measurements.pop(),
            lambda measurements: measurements.append(copy.deepcopy(measurements[0])),
            lambda measurements: measurements[0].update(IterationStage="Warmup"),
            lambda measurements: measurements[0].update(IterationMode="Overhead"),
            lambda measurements: measurements[0].update(Operations=2),
            lambda measurements: measurements[0].update(IterationIndex=2),
            lambda measurements: measurements[0].update(LaunchIndex=2),
            lambda measurements: measurements[0].update(Nanoseconds=float("nan")),
        ):
            self.write_case(artifacts)
            path = artifacts / "SameType-report-full.json"
            data = json.loads(path.read_text())
            change(data["Benchmarks"][0]["Measurements"])
            path.write_text(json.dumps(data), encoding="utf-8")
            with self.subTest(change=change), self.assertRaises(ValueError):
                interleave_perf.collect_results(str(run_dir), str(self.root / "dest"),
                                                self.case, strict=True)

    def test_summary_retains_all_invocations_and_resource_values(self):
        self.write_case(self.root / "current" / self.case)
        report.write_summary(str(self.root), [self.case], False)
        data = json.loads((self.root / "openasync-summary.json").read_text())
        rows = data["Invocations"]
        self.assertEqual([row["Invocation"] for row in rows], [1, 2, 3])
        self.assertEqual([row["SuccessThroughputPerSecond"] for row in rows], [16, 8, 16 / 3])
        self.assertEqual(rows[0]["CpuMilliseconds"], 1)
        self.assertEqual(rows[0]["AdmissionMilliseconds"], [1])
        self.assertIn("not phase-compliance", data["Notice"])
        self.assertIn("N/A", self._sync_summary())

    def _sync_summary(self):
        case = "OpenAsyncScoreboard-E1-Sync-1"
        self.write_case(self.root / "current" / case, case)
        report.write_summary(str(self.root), [case], False)
        return (self.root / "openasync-summary.md").read_text(encoding="utf-8")

    def test_failure_writes_outcome_summary_then_fails(self):
        self.write_case(self.root / "current" / self.case, failed=True)
        with self.assertRaisesRegex(ValueError, "failed/canceled"):
            report.write_summary(str(self.root), [self.case], False)
        self.assertTrue((self.root / "openasync-summary.json").exists())

    def test_missing_invocation_or_changed_metadata_fails(self):
        directory = self.root / "current" / self.case
        self.write_case(directory)
        path = directory / "openasync-3.json"
        data = json.loads(path.read_text())
        data["UseConnectionPoolV2"] = True
        path.write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "metadata"):
            report.load_case(str(directory), self.case)
        path.unlink()
        with self.assertRaisesRegex(ValueError, "exactly 3"):
            report.load_case(str(directory), self.case)

    def test_preliminary_current_only_uses_per_case_processes_without_reruns(self):
        runner = interleave_perf.Runner(None, "current", "PerformanceTests.dll",
                                       str(self.root), [], benchmark_profile=report.PROFILE)
        with mock.patch.object(runner, "_run_one", return_value={"Runner"}) as run:
            runner.interleave([self.case], 1, "baseline-results", "current-results")
        run.assert_called_once_with("current", "current", self.case, 1, "current-results")
        with mock.patch.object(runner, "interleave", return_value={self.case: {"Runner"}}) as run, \
                mock.patch.object(report, "write_summary"), \
                mock.patch.object(interleave_perf.compare_perf, "build_comparison", return_value=[]), \
                mock.patch.object(interleave_perf, "_regression_keys", side_effect=AssertionError("regression logic")):
            interleave_perf.orchestrate(runner, [self.case], str(self.root), 10, 99)
        run.assert_called_once()

    def test_gate_is_rejected_before_starting_processes(self):
        with contextlib.redirect_stderr(io.StringIO()), self.assertRaises(SystemExit) as error:
            interleave_perf.main(["--current-exe-dir", "current", "--results-dir", str(self.root),
                                  "--benchmark-profile", report.PROFILE, "--fail-on-regression"])
        self.assertEqual(error.exception.code, 2)

    def test_incompatible_source_baseline_fails_before_running(self):
        with mock.patch.object(interleave_perf.Runner, "list_units",
                               side_effect=[report.CASES, ["OldBenchmark"]]), \
                mock.patch.object(interleave_perf.Runner, "interleave") as run, \
                contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaisesRegex(RuntimeError, "Incompatible baseline"):
                interleave_perf.main(["--current-exe-dir", "current", "--baseline-exe-dir", "old",
                                      "--results-dir", str(self.root), "--work-dir", str(self.root),
                                      "--benchmark-profile", report.PROFILE])
        run.assert_not_called()

    def test_full_matrix_current_only_and_switch_baseline_reports(self):
        def run_case(exe_dir, assembly, unit, cwd, cpus, log_path, **kwargs):
            self.assertEqual(kwargs["timeout_seconds"], 600)
            directory = Path(cwd) / "BenchmarkDotNet.Artifacts" / "results"
            self.write_case(directory, unit)
            path = directory / "SameType-report-full.json"
            data = json.loads(path.read_text())
            # BDN's default display width may hide scenario differences in this column.
            data["Benchmarks"][0]["Parameters"] = "Case=OpenA(...)ync-1 [29]"
            data["Benchmarks"][0]["FullName"] = f"OpenAsyncScoreboardRunner.OpenCohort(Case: {unit})"
            path.write_text(json.dumps(data), encoding="utf-8")
            return 0

        for has_baseline in (False, True):
            output = self.root / str(has_baseline)
            args = ["--current-exe-dir", "build", "--results-dir", str(output),
                    "--work-dir", str(self.root / "work"), "--benchmark-profile", report.PROFILE,
                    "--reps", "99", "--current-runner-config", "current-config"]
            if has_baseline:
                args += ["--baseline-exe-dir", "build", "--baseline-runner-config", "baseline-config"]
            with mock.patch.object(interleave_perf.Runner, "list_units", return_value=report.CASES), \
                    mock.patch.object(interleave_perf, "run_unit_process", side_effect=run_case) as run, \
                    contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(interleave_perf.main(args), 0)
            variants = 2 if has_baseline else 1
            self.assertEqual(run.call_count, 24 * variants)
            for call in run.call_args_list:
                expected = "baseline-config" if "baseline" in Path(call.args[3]).parts else "current-config"
                self.assertEqual(call.kwargs["env_overrides"], {"RUNNER_CONFIG": expected})
            summary = json.loads((output / "openasync-summary.json").read_text())
            self.assertEqual(len(summary["Invocations"]), 72 * variants)
            comparison = json.loads((output / "comparison" / "comparison.json").read_text())
            self.assertEqual(comparison["confirmationRuns"], 1)
            self.assertEqual(comparison["confirmedRegressions"], 0)
            self.assertEqual(len(comparison["entries"]), 24)
            self.assertTrue(all(entry["status"] == "report-only" for entry in comparison["entries"]))
            self.assertTrue((output / "summary.md").exists())

    def test_default_profile_keeps_best_of_n_confirmation(self):
        runner = interleave_perf.Runner("baseline", "current", "PerformanceTests.dll",
                                       str(self.root), [])
        entries = [{"key": "Runner.Method()", "status": "regression", "benchmarkName": "Runner"}]
        with mock.patch.object(runner, "interleave", return_value={"Unit": {"Runner"}}) as run, \
                mock.patch.object(interleave_perf, "_regression_keys",
                                  return_value=(entries, {"Runner.Method()"})), \
                contextlib.redirect_stdout(io.StringIO()):
            _, confirmed, _ = interleave_perf.orchestrate(runner, ["Unit"], str(self.root), 10, 3)
        self.assertEqual(run.call_count, 3)
        self.assertEqual(confirmed[0]["regressionReps"], 3)

    def test_watchdog_kills_child_tree_and_fails(self):
        proc = mock.Mock(pid=123)
        proc.wait.side_effect = [subprocess.TimeoutExpired("dotnet", 600), 1]
        with mock.patch.object(interleave_perf.subprocess, "Popen", return_value=proc), \
                mock.patch.object(interleave_perf.subprocess, "run") as kill, \
                mock.patch.object(interleave_perf.os, "killpg", create=True) as killpg:
            with self.assertRaisesRegex(RuntimeError, "watchdog"):
                interleave_perf.run_unit_process("build", "PerformanceTests.dll", self.case,
                                                str(self.root), [], str(self.root / "run.log"),
                                                timeout_seconds=600)
        proc.wait.assert_any_call(timeout=600)
        self.assertTrue(kill.called if os.name == "nt" else killpg.called)


if __name__ == "__main__":
    unittest.main()
