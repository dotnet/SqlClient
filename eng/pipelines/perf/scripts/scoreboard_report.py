# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

"""Schema-v2 scoreboard accounting. V1 validation intentionally stays in openasync_report."""

import json
import math
import os
from datetime import datetime
from pathlib import Path
import re
import statistics
import zipfile

import openasync_report as legacy

NOTICE = ("Report-only instrumented scoreboard. All process repetitions and warmups are retained; "
          "warmups are not measurements. No best-of-N, outlier removal, or numerical gate. "
          "Diagnostic stack collection can suspend the process; Diagnostic timing is not "
          "comparable with Sampled timing. Thread counts are not blocked-thread evidence. "
          "InsufficientWindowSamples is missing observational coverage, not zero blocked opens "
          "or a proven pass. Covered means some contained evidence from each external collector, "
          "not complete temporal or thread coverage. SQL observations are shared-instance load, not attribution.")
LOAD_FIELDS = ("LoadPhaseMilliseconds", "LoadQueueCapacity", "LoadBaseRate", "LoadOverloadRate")
EQUIVALENCE_FIELDS = (
    "WorkloadVersion", "Scenario", "Api", "Concurrency", "Instrumentation", "OperationsPerWorker",
    "PoolSize", "HoldMilliseconds", "ConnectTimeoutSeconds", "ConnectRetryCount", "Enlist",
    "ConnectionSettings",
) + LOAD_FIELDS


def need(condition, message):
    if not condition:
        raise ValueError(message)


def package_raw_measurements(results_dir):
    """Stream raw evidence into verified archives before the pipeline artifact upload."""
    root = Path(results_dir)
    for repetition in sorted((root / "repetitions").glob("rep*")):
        files = sorted(path for path in repetition.rglob("*") if path.is_file()
                       and (path.name.startswith("openasync-") and path.suffix == ".json"
                            or "external-diagnostics" in path.relative_to(repetition).parts))
        if not files:
            continue
        need(not any(path.is_symlink() for path in files), "Cannot archive symlinked measurements")
        destination = root / f"raw-measurements-{repetition.name}.zip"
        need(not destination.exists(), f"Archive already exists: {destination.name}")
        temporary = destination.with_suffix(".zip.tmp")
        need(not temporary.exists(), f"Incomplete archive already exists: {temporary.name}")
        with zipfile.ZipFile(temporary, "x", compression=zipfile.ZIP_DEFLATED,
                             compresslevel=6, allowZip64=True) as archive:
            for path in files:
                archive.write(path, path.relative_to(root).as_posix())
        with zipfile.ZipFile(temporary) as archive:
            need(len(archive.infolist()) == len(files) and archive.testzip() is None,
                 f"Raw measurement archive verification failed: {temporary.name}")
        temporary.rename(destination)
        # Originals are removed only after every member passes a streamed CRC check.
        for path in files:
            path.unlink()
        print(f"Preserved {len(files)} raw evidence files in {destination.name} "
              f"({destination.stat().st_size} bytes); extract into the results directory to reanalyze.")


def finite_tree(value):
    """Reject non-finite numeric diagnostics instead of emitting invalid JSON or invented zeroes."""
    if isinstance(value, float):
        legacy.number(value, "diagnostic numeric value")
    elif isinstance(value, dict):
        for item in value.values():
            finite_tree(item)
    elif isinstance(value, list):
        for item in value:
            finite_tree(item)


def outcome(sample):
    need(sample.get("Outcome") in ("Success", "Failed", "Canceled"), "Unknown outcome")
    if sample["Outcome"] == "Success":
        need(sample.get("ErrorType") is None and sample.get("SqlErrorNumber") is None,
             "Success cannot contain an error")
    else:
        need(isinstance(sample.get("ErrorType"), str) and bool(sample["ErrorType"]),
             "Failed/canceled operation must retain its error type")
    need(sample.get("SqlErrorNumber") is None or type(sample["SqlErrorNumber"]) is int,
         "Invalid SQL error number")


def duration(sample, first, last, elapsed, nullable=False):
    if nullable and sample.get(last) is None:
        return None
    start = legacy.number(sample.get(first), first)
    end = legacy.number(sample.get(last), last)
    need(start <= end <= elapsed, f"Invalid {first}/{last} relative duration")
    return end - start


def summarize(data, case, repetition, stage="WorkloadActual", preliminary=False):
    allowed = set(legacy.IDENTITY_FIELDS + legacy.RESOURCE_FIELDS + legacy.OPTIONAL_METADATA + LOAD_FIELDS) | {
        "SchemaVersion", "Invocation", "InvocationId", "RunId", "ProcessId", "ProcessRepetition",
        "Instrumentation", "ElapsedMilliseconds", "AdmissionMilliseconds", "Samples", "QuerySamples",
        "Diagnostics", "Load", "WarmupCount", "ConnectionSettings", "ExternalDiagnosticsRequired", "WasCanceled",
        "StartedUtc", "CompletedUtc",
    }
    need(isinstance(data, dict) and not (set(data) - allowed), "Unexpected v2 schema fields")
    need(data.get("SchemaVersion") == 2 and data.get("WorkloadVersion") == 2,
         "Full scoreboard requires schema/workload v2")
    need(case in (legacy.CASES if preliminary else legacy.FULL_CASES)
         and data.get("CaseId") == case, "Case mismatch")
    _, scenario, api, concurrency, *suffix = case.split("-")
    concurrency = int(concurrency)
    instrumentation = "Diagnostic" if suffix else "Sampled"
    need((data.get("Scenario"), data.get("Api"), data.get("Concurrency"), data.get("Instrumentation"))
         == (scenario, api, concurrency, instrumentation), "Case dimensions mismatch")
    need(data.get("ProcessRepetition") == repetition, "Process repetition mismatch")
    legacy.number(data.get("ProcessRepetition"), "ProcessRepetition", integer=True, positive=True)
    legacy.number(data.get("Concurrency"), "Concurrency", integer=True, positive=True)
    legacy.number(data.get("ProcessId"), "ProcessId", integer=True, positive=True)
    need(data.get("RunId") is None or (isinstance(data["RunId"], str) and bool(data["RunId"])),
         "Invalid RunId")
    need(isinstance(data.get("InvocationId"), str) and bool(data["InvocationId"]), "Missing InvocationId")
    need(data.get("MeasurementStage") == stage, "Warmup/actual stage mismatch")
    if "WasCanceled" in data:
        need(type(data["WasCanceled"]) is bool, "Invalid cancellation state")
    if data.get("StartedUtc") is not None or data.get("CompletedUtc") is not None:
        timestamps = []
        for field in ("StartedUtc", "CompletedUtc"):
            text = data.get(field)
            need(isinstance(text, str), f"Missing {field}")
            text = re.sub(r"(\.\d{6})\d+(?=[+-]\d{2}:\d{2}$)", r"\1", text.replace("Z", "+00:00"))
            value = datetime.fromisoformat(text)
            need(value.tzinfo is not None, f"{field} lacks UTC offset")
            timestamps.append(value)
        need(timestamps[0] <= timestamps[1], "Invalid invocation UTC interval")
    if "WarmupCount" in data:
        need(data["WarmupCount"] == (0 if preliminary else 2), "Configured warmup count mismatch")
    need(data.get("IsInstrumented") is True, "Missing instrumented-measurement declaration")
    for field in ("UseConnectionPoolV2", "UseOptimizedAsyncBehaviour", "UseManagedSniOnWindows", "Enlist"):
        need(type(data.get(field)) is bool, f"Invalid {field}")
    need(data.get("ConnectTimeoutSeconds") == 30 and data.get("ConnectRetryCount") == 0
         and data["Enlist"] is False, "Connection workload policy mismatch")
    if not preliminary and any(field in data for field in LOAD_FIELDS):
        need(tuple(data.get(field) for field in LOAD_FIELDS) == (2000, 64, 25, 800),
             "Load option metadata does not match the full profile")
    for field in ("Runtime", "OS", "DriverVersion", "DriverAssemblyVersion"):
        need(isinstance(data.get(field), str) and bool(data[field]), f"Missing {field}")
    operations = 1 if scenario == "E4" else 16 if preliminary else 64
    need(data.get("OperationsPerWorker") == operations, "Operation count mismatch")
    legacy.number(data.get("PoolSize"), "PoolSize", integer=True)
    legacy.number(data.get("HoldMilliseconds"), "HoldMilliseconds")
    expected_pool = 0 if scenario == "E3" else 4 if scenario in ("E2", "E6") else concurrency
    need(data["PoolSize"] == expected_pool, "Pool size mismatch")
    need(data["HoldMilliseconds"] == (10 if scenario in ("E2", "E6") else 0), "Hold duration mismatch")
    for field in legacy.RESOURCE_FIELDS:
        legacy.number(data.get(field), field, integer=field != "CpuMilliseconds")
    need(data["PeakOutstandingOpens"] <= concurrency and data["PeakOpenConnections"] <= concurrency,
         "Observed concurrency exceeds workers")
    need(not data["PoolSize"] or data["PeakOpenConnections"] <= data["PoolSize"],
         "Observed ownership exceeds configured pool")
    elapsed = legacy.number(data.get("ElapsedMilliseconds"), "ElapsedMilliseconds", positive=True)
    legacy.number(data.get("Invocation"), "Invocation", integer=True)
    legacy.number(data.get("ProcessorCount"), "ProcessorCount", integer=True, positive=True)
    diagnostics = data.get("Diagnostics")
    need((instrumentation == "Sampled" and diagnostics is None)
         or (instrumentation == "Diagnostic" and isinstance(diagnostics, dict) and bool(diagnostics)),
         "Diagnostic mode must supply diagnostics; Sampled must not")
    finite_tree(diagnostics)
    diagnostic_summary = summarize_diagnostics(diagnostics, elapsed) if diagnostics is not None else None
    samples = data.get("Samples")
    need(isinstance(samples, list) and bool(samples), "Missing samples")
    need(scenario == "E6" or len(samples) == concurrency * operations, "Incomplete sample count")
    ids, worker_ops = set(), set()
    latencies, returns, pending, lifecycle = [], [], [], []
    outcomes = {"Success": 0, "Failed": 0, "Canceled": 0}
    lifecycle_outcomes = {"Success": 0, "Failed": 0, "Canceled": 0}
    completed = 0
    for sample in samples:
        sample_fields = {
            "RequestId", "Worker", "Operation", "StartMilliseconds", "TaskReturnMilliseconds",
            "CompletionMilliseconds", "LifecycleCompletionMilliseconds", "CompletedAtReturn",
            "Outcome", "ErrorType", "SqlErrorNumber", "OfferedMilliseconds", "Phase"}
        lifecycle_fields = {"LifecycleOutcome", "LifecycleErrorType", "LifecycleSqlErrorNumber"}
        need(isinstance(sample, dict) and set(sample) in (sample_fields, sample_fields | lifecycle_fields),
             "Unexpected sample schema")
        request_id = legacy.number(sample.get("RequestId"), "RequestId", integer=True)
        need(request_id not in ids, "Duplicate RequestId")
        ids.add(request_id)
        worker = legacy.number(sample.get("Worker"), "Worker", integer=True)
        operation = legacy.number(sample.get("Operation"), "Operation", integer=True)
        need(worker < concurrency, "Invalid worker")
        if scenario != "E6":
            need(operation < operations and (worker, operation) not in worker_ops,
                 "Duplicate/unexpected worker operation")
        worker_ops.add((worker, operation))
        latencies.append(duration(sample, "StartMilliseconds", "CompletionMilliseconds", elapsed))
        returned, completed_at_return = sample.get("TaskReturnMilliseconds"), sample.get("CompletedAtReturn")
        if api == "Sync":
            need(returned is None and completed_at_return is None, "Sync must not invent task timings")
        elif returned is not None:
            returns.append(duration(sample, "StartMilliseconds", "TaskReturnMilliseconds", elapsed))
            need(type(completed_at_return) is bool, "Missing completed-at-return state")
            if completed_at_return:
                completed += 1
            else:
                pending.append(returns[-1])
        else:
            need(completed_at_return is None and sample.get("Outcome") != "Success",
                 "Missing successful async return timing")
        value = duration(sample, "StartMilliseconds", "LifecycleCompletionMilliseconds", elapsed, nullable=True)
        if value is not None:
            need(sample["LifecycleCompletionMilliseconds"] >= sample["CompletionMilliseconds"],
                 "Lifecycle completed before open")
            lifecycle.append(value)
        elif sample.get("Outcome") == "Success":
            raise ValueError("Successful sample is missing post-close lifecycle timing")
        if scenario != "E6":
            need(sample.get("OfferedMilliseconds") is None and sample.get("Phase") is None,
                 "Only E6 carries offered timestamps/phases")
        outcome(sample)
        outcomes[sample["Outcome"]] += 1
        if lifecycle_fields.issubset(sample):
            lifecycle_result = {"Outcome": sample["LifecycleOutcome"], "ErrorType": sample["LifecycleErrorType"],
                                "SqlErrorNumber": sample["LifecycleSqlErrorNumber"]}
            outcome(lifecycle_result)
            lifecycle_outcomes[lifecycle_result["Outcome"]] += 1
    admission = data.get("AdmissionMilliseconds")
    need(isinstance(admission, list), "Missing admission timings")
    need(len(admission) == concurrency, "Missing worker admission")
    for value in admission:
        if scenario == "E6" and value == -1:
            continue
        need(legacy.number(value, "AdmissionMilliseconds") <= elapsed, "Admission exceeds invocation")
    queries = data.get("QuerySamples")
    need(isinstance(queries, list), "Missing QuerySamples array")
    query_ids, query_latencies = set(), []
    by_id = {sample["RequestId"]: sample for sample in samples}
    for query in queries:
        need(isinstance(query, dict) and set(query) == {
            "RequestId", "StartMilliseconds", "CompletionMilliseconds", "Outcome", "ErrorType", "SqlErrorNumber"},
            "Unexpected query schema")
        request_id = legacy.number(query.get("RequestId"), "Query RequestId", integer=True)
        need(request_id in ids and request_id not in query_ids, "Unaccountable query sample")
        query_ids.add(request_id)
        outcome(query)
        query_latencies.append(duration(query, "StartMilliseconds", "CompletionMilliseconds", elapsed))
        need(query["StartMilliseconds"] >= by_id[request_id]["CompletionMilliseconds"],
             "Query started before open completed")
        close = by_id[request_id].get("LifecycleCompletionMilliseconds")
        need(close is not None and query["CompletionMilliseconds"] <= close, "Query completed after close")
    need(scenario == "E5" or not queries, "Query samples outside E5")
    if scenario == "E5":
        need(query_ids == {sample["RequestId"] for sample in samples if sample["Outcome"] == "Success"},
             "Every successful E5 open must have one query")
    row = {key: value for key, value in data.items() if key not in ("Samples", "QuerySamples", "Load")}
    row["RunId"] = data.get("RunId")
    row["AdmissionMilliseconds"] = [None if value == -1 else value for value in admission]
    row.update(Outcomes=outcomes, SampleCount=len(samples),
               LifecycleOutcomes=lifecycle_outcomes if any(lifecycle_outcomes.values()) else None,
               DiagnosticSummary=diagnostic_summary,
               SuccessThroughputPerSecond=outcomes["Success"] * 1000 / elapsed,
               OpenLatencyMilliseconds=legacy.distribution(latencies),
               TaskReturnMilliseconds=legacy.distribution(returns),
               PendingTaskReturnMilliseconds=legacy.distribution(pending),
               CompletedAtReturnFraction=completed / len(returns) if returns else None,
               LifecycleMilliseconds=legacy.distribution(lifecycle),
               QueryMilliseconds=legacy.distribution(query_latencies),
               QueryOutcomes={state: sum(query["Outcome"] == state for query in queries) for state in outcomes},
               CommonEpochCompletionMilliseconds=legacy.distribution(
                   [sample["CompletionMilliseconds"] for sample in samples]) if scenario == "E4" else None)
    # Retain the existing preliminary Markdown contract for newer emitters.
    row["AdmissionDistributionMilliseconds"] = legacy.distribution(
        [value for value in row["AdmissionMilliseconds"] if value is not None])
    if scenario == "E6":
        row["LoadSummary"] = summarize_load(data.get("Load"), samples, elapsed)
        row.update(LoadPhaseMilliseconds=data["Load"]["PhaseMilliseconds"],
                   LoadQueueCapacity=data["Load"]["QueueCapacity"], LoadBaseRate=data["Load"]["BaseRate"],
                   LoadOverloadRate=data["Load"]["OverloadRate"])
    else:
        need(data.get("Load") is None, "Load metadata outside E6")
    return row


def summarize_diagnostics(data, elapsed=None):
    need(data.get("SampleIntervalMilliseconds") == 10, "Unexpected diagnostic interval")
    need(isinstance(data.get("GcObservationMethod"), str) and bool(data["GcObservationMethod"]),
         "Missing GC observation limitations")
    diagnostic_end = legacy.number(data.get("WindowEndMilliseconds"), "Diagnostic window end", positive=True)
    window = min(diagnostic_end, legacy.number(elapsed, "Invocation elapsed", positive=True)) \
        if elapsed is not None else diagnostic_end
    legacy.number(data.get("UnobservedGcCount"), "UnobservedGcCount", integer=True)
    timeline = data.get("Timeline")
    need(isinstance(timeline, list) and bool(timeline), "Missing resource timeline")
    previous = -1
    for sample in timeline:
        stamp = legacy.number(sample.get("TimestampMilliseconds"), "Timeline timestamp")
        need(previous <= stamp, "Resource timeline is out of order")
        previous = stamp
        need(type(sample.get("AfterWorkload")) is bool, "Missing timeline stage marker")
        for field in ("OutstandingOpens", "OwnedConnections", "WorkerThreads", "BusyWorkerThreads",
                      "PendingWorkItems", "CompletedWorkItems", "ProcessThreads", "CpuMilliseconds",
                      "WorkingSetBytes", "PrivateMemoryBytes", "ManagedHeapBytes", "HeapSizeAfterLastGcBytes",
                      "FragmentedBytes", "TotalAllocatedBytes", "Gen0Collections", "Gen1Collections", "Gen2Collections"):
            if field == "ManagedHeapBytes":
                need(type(sample.get(field)) is int, "ManagedHeapBytes must be a signed integer estimate")
            else:
                legacy.number(sample.get(field), field, integer=field != "CpuMilliseconds")
    probe = data.get("UnrelatedWork")
    need(isinstance(probe, dict), "Missing unrelated-work accounting")
    for field in ("Capacity", "Scheduled", "Posted", "Executed", "Dropped", "OutstandingAtStop"):
        legacy.number(probe.get(field), field, integer=True)
    need(probe["Capacity"] > 0 and probe["Scheduled"] == probe["Posted"] + probe["Dropped"]
         and probe["Posted"] == probe["Executed"]
         and probe["OutstandingAtStop"] <= probe["Posted"],
         "Unrelated-work counters do not reconcile")
    legacy.number(probe.get("DrainMilliseconds"), "Probe drain")
    samples = probe.get("Samples")
    need(isinstance(samples, list) and len(samples) == probe["Executed"], "Missing executed probe samples")
    dispatch, overall, lateness = [], [], []
    probe_stages, in_window_dispatch, post_window_dispatch = [], [], []
    observation_end = max(diagnostic_end, previous)
    for sample in samples:
        dispatch.append(duration(sample, "PostedMilliseconds", "ExecutedMilliseconds", observation_end))
        overall.append(duration(sample, "ScheduledMilliseconds", "ExecutedMilliseconds", observation_end))
        lateness.append(duration(sample, "ScheduledMilliseconds", "PostedMilliseconds", observation_end))
        within = sample["ExecutedMilliseconds"] <= window
        (in_window_dispatch if within else post_window_dispatch).append(dispatch[-1])
        probe_stages.append("WithinWorkload" if within else "PostWorkload")
    need(isinstance(data.get("GcObservations"), list), "Missing GC observations")
    for observation in data["GcObservations"]:
        legacy.number(observation.get("Index"), "GC index", integer=True)
        legacy.number(observation.get("Generation"), "GC generation", integer=True)
        legacy.number(observation.get("ObservedMilliseconds"), "GC observed timestamp")
        need(isinstance(observation.get("PauseMilliseconds"), list), "Missing observed GC pauses")
        for pause in observation["PauseMilliseconds"]:
            legacy.number(pause, "Observed GC pause")
    in_window = [sample for sample in timeline if not sample["AfterWorkload"]
                 and sample["TimestampMilliseconds"] <= window]
    cumulative = ("CpuMilliseconds", "TotalAllocatedBytes", "CompletedWorkItems",
                  "Gen0Collections", "Gen1Collections", "Gen2Collections")
    deltas = {}
    for field in cumulative:
        values = [sample[field] for sample in in_window]
        need(all(after >= before for before, after in zip(values, values[1:])),
             f"Cumulative in-window counter decreased: {field}")
        deltas[field] = values[-1] - values[0] if len(values) >= 2 else None
    peak = lambda field: max((sample[field] for sample in in_window), default=None)

    def heap_estimate_counts(observations):
        negative = sum(sample["ManagedHeapBytes"] < 0 for sample in observations)
        return dict(Total=len(observations), Available=len(observations) - negative,
                    Unavailable=negative, Negative=negative)

    available_heap = [sample["ManagedHeapBytes"] for sample in in_window if sample["ManagedHeapBytes"] >= 0]
    return dict(UnrelatedDispatchMilliseconds=legacy.distribution(dispatch),
                UnrelatedScheduledToExecutedMilliseconds=legacy.distribution(overall),
                ProbeOfferLatenessMilliseconds=legacy.distribution(lateness),
                ProbeCompletionStages=probe_stages,
                UnrelatedDispatchWithinWorkloadMilliseconds=legacy.distribution(in_window_dispatch),
                UnrelatedDispatchPostWorkloadMilliseconds=legacy.distribution(post_window_dispatch),
                DroppedProbes=probe["Dropped"], UnobservedProbes=probe["Posted"] - probe["Executed"],
                OutstandingAtWorkloadStop=probe["OutstandingAtStop"],
                UnobservedGcCount=data["UnobservedGcCount"],
                GcObservationMethod=data.get("GcObservationMethod"),
                GcObservationStages=["ObservedWithinWorkload" if item["ObservedMilliseconds"] <= window
                                     else "ObservedAfterWorkload" for item in data["GcObservations"]],
                GcTraceComplete=False, WorkloadTimelineSampleCount=len(in_window),
                WorkloadWindowEndMilliseconds=window,
                DiagnosticWindowEndMilliseconds=diagnostic_end,
                InWindowCumulativeDeltas=deltas,
                PeakBusyWorkerProxy=peak("BusyWorkerThreads"),
                PeakPendingWorkItems=peak("PendingWorkItems"),
                PeakWorkingSetBytes=peak("WorkingSetBytes"),
                PeakManagedHeapBytes=max(available_heap, default=None),
                ManagedHeapEstimateSamples=dict(All=heap_estimate_counts(timeline),
                                                WithinWorkload=heap_estimate_counts(in_window)),
                ManagedHeapInterpretation="GC.GetTotalMemory(false) is approximate; negative estimates "
                                          "are retained raw but unavailable for heap statistics.",
                PeakOutstandingOpens=peak("OutstandingOpens"), PeakOwnedConnections=peak("OwnedConnections"),
                BusyWorkerInterpretation="Availability-derived proxy; not blocked-thread evidence")


def summarize_load(load, samples, elapsed):
    need(isinstance(load, dict), "E6 requires load accounting")
    for field in ("PhaseMilliseconds", "QueueCapacity", "BaseRate", "OverloadRate"):
        legacy.number(load.get(field), field, integer=True, positive=True)
    need((load["PhaseMilliseconds"], load["QueueCapacity"], load["BaseRate"], load["OverloadRate"])
         == (2000, 64, 25, 800), "E6 offered-load configuration mismatch")
    generation_start = legacy.number(load.get("GenerationStartMilliseconds"), "GenerationStartMilliseconds")
    generation = legacy.number(load.get("GenerationCompletedMilliseconds"), "GenerationCompletedMilliseconds")
    drain = legacy.number(load.get("DrainCompletedMilliseconds"), "DrainCompletedMilliseconds")
    schedule_end = generation_start + 3 * load["PhaseMilliseconds"]
    need(schedule_end <= generation <= drain <= elapsed, "Invalid generation/drain boundaries")
    offers = load.get("Offers")
    need(isinstance(offers, list) and bool(offers), "Missing offers")
    accepted, offer_ids = {}, set()
    phases = ("Baseline", "Overload", "Recovery")
    expected = []
    for index, phase in enumerate(phases):
        rate = load["OverloadRate"] if phase == "Overload" else load["BaseRate"]
        for tick in range((load["PhaseMilliseconds"] * rate + 999) // 1000):
            expected.append((phase, generation_start + index * load["PhaseMilliseconds"] + tick * 1000.0 / rate))
    need(len(offers) == len(expected), "Incomplete offered-load schedule")
    rows = {}
    by_id = {sample["RequestId"]: sample for sample in samples}
    for offer in offers:
        request_id = legacy.number(offer.get("RequestId"), "offer RequestId", integer=True)
        need(request_id not in offer_ids, "Duplicate offer")
        offer_ids.add(request_id)
        need(request_id < len(expected), "Unexpected offered request ID")
        scheduled = legacy.number(offer.get("ScheduledMilliseconds"), "ScheduledMilliseconds")
        need(offer.get("Phase") == expected[request_id][0]
             and math.isclose(scheduled, expected[request_id][1], rel_tol=1e-12, abs_tol=1e-6),
             "Offered-load schedule changed")
        need(offer.get("Phase") in phases and type(offer.get("Accepted")) is bool, "Invalid offer")
        duration(offer, "ScheduledMilliseconds", "ActualOfferMilliseconds", elapsed)
        need(offer["ActualOfferMilliseconds"] <= generation, "Offer after generation completed")
        need(generation_start <= scheduled < schedule_end, "Offer outside fixed generation schedule")
        if offer["Accepted"]:
            accepted[request_id] = offer
    need(set(accepted) == set(by_id), "Accepted offers and drained samples do not reconcile")
    for request_id, sample in by_id.items():
        offer = accepted[request_id]
        need(sample.get("Phase") == offer["Phase"]
             and sample.get("OfferedMilliseconds") == offer["ScheduledMilliseconds"],
             "Sample does not match offer")
        need(sample["StartMilliseconds"] >= offer["ActualOfferMilliseconds"], "Started before admission")
        close = sample.get("LifecycleCompletionMilliseconds")
        need(close is not None and close <= drain, "Accepted offer not drained")

    def backlog(boundary):
        return sum(offer["ActualOfferMilliseconds"] <= boundary
                   and by_id[request_id]["LifecycleCompletionMilliseconds"] > boundary
                   for request_id, offer in accepted.items())

    def window_activity(start, end, include_end=False):
        def inside(stamp):
            return start <= stamp and (stamp <= end if include_end else stamp < end)

        completed = [sample for sample in samples if inside(sample["LifecycleCompletionMilliseconds"])]
        return {
            "StartMilliseconds": start, "EndMilliseconds": end, "EndInclusive": include_end,
            "ActuallyOffered": sum(inside(offer["ActualOfferMilliseconds"]) for offer in offers),
            "ActuallyAccepted": sum(inside(offer["ActualOfferMilliseconds"]) for offer in accepted.values()),
            "ActuallyRejected": sum(not offer["Accepted"] and inside(offer["ActualOfferMilliseconds"])
                                    for offer in offers),
            "Started": sum(inside(sample["StartMilliseconds"]) for sample in samples),
            "OpenCompleted": sum(inside(sample["CompletionMilliseconds"]) for sample in samples),
            "Completed": len(completed),
            "CompletedByOfferPhase": {phase: sum(sample["Phase"] == phase for sample in completed) for phase in phases},
            "BacklogAtStart": backlog(start), "BacklogAtEnd": backlog(end),
            "CompletionWindowOfferedToCompletionMilliseconds": legacy.distribution(
                [sample["LifecycleCompletionMilliseconds"] - sample["OfferedMilliseconds"] for sample in completed]),
        }

    for phase in phases:
        phase_offers = [offer for offer in offers if offer["Phase"] == phase]
        phase_samples = [sample for sample in samples if sample["Phase"] == phase]
        index = phases.index(phase)
        boundary_start = generation_start + index * load["PhaseMilliseconds"]
        boundary_end = generation_start + (index + 1) * load["PhaseMilliseconds"]
        rows[phase] = {
            **window_activity(boundary_start, boundary_end),
            "Offered": len(phase_offers), "Accepted": sum(offer["Accepted"] for offer in phase_offers),
            "Rejected": sum(not offer["Accepted"] for offer in phase_offers),
            "OfferCohortStarted": len(phase_samples), "OfferCohortCompleted": len(phase_samples),
            "OfferLatenessMilliseconds": legacy.distribution(
                [offer["ActualOfferMilliseconds"] - offer["ScheduledMilliseconds"] for offer in phase_offers]),
            "OfferCohortAdmissionMilliseconds": legacy.distribution(
                [sample["StartMilliseconds"] - accepted[sample["RequestId"]]["ActualOfferMilliseconds"]
                 for sample in phase_samples]),
            "OfferCohortOfferedToCompletionMilliseconds": legacy.distribution(
                [sample["LifecycleCompletionMilliseconds"] - sample["OfferedMilliseconds"]
                 for sample in phase_samples]),
            "BacklogAtPhaseStart": backlog(boundary_start),
            "BacklogAtPhaseEnd": backlog(boundary_end),
        }
    recovery_start = generation_start + 2 * load["PhaseMilliseconds"]
    return dict(Phases=rows, GenerationStartMilliseconds=generation_start,
        RecoveryWindows={
            "LateHalf": window_activity(recovery_start + load["PhaseMilliseconds"] / 2, schedule_end),
            "LateQuarter": window_activity(recovery_start + 3 * load["PhaseMilliseconds"] / 4, schedule_end)},
        PostScheduleUntilGenerationCompleted=window_activity(schedule_end, generation),
        Drain=window_activity(generation, drain, include_end=True),
        RemainingAtGenerationEnd=backlog(generation),
        RemainingAfterDrain=0, GenerationCompletedMilliseconds=generation,
        DrainCompletedMilliseconds=drain, DrainMilliseconds=drain - generation)


def load_case(directory, case, repetition):
    rows = []
    for stage, count, subdir in (("WorkloadActual", 3, ""), ("WorkloadWarmup", 2, "warmup")):
        paths = sorted((Path(directory) / subdir).glob("openasync-*.json"))
        need(len(paths) == count, f"{case}: expected {count} {stage} artifacts, found {len(paths)}")
        stage_rows = []
        for path in paths:
            with path.open(encoding="utf-8-sig") as handle:
                stage_rows.append(summarize(json.load(handle), case, repetition, stage))
        need(len({row["Invocation"] for row in stage_rows}) == count, "Duplicate invocation")
        rows.extend(stage_rows)
    first = rows[0]
    need(len({row["InvocationId"] for row in rows}) == len(rows), "Duplicate invocation identity")
    for row in rows:
        for field in EQUIVALENCE_FIELDS + ("ProcessId", "RunId", "UseConnectionPoolV2",
                                          "UseOptimizedAsyncBehaviour", "UseManagedSniOnWindows"):
            need(row.get(field) == first.get(field), f"Within-process metadata mismatch: {field}")
    if case.endswith("-Diagnostic"):
        path = Path(directory) / "external-diagnostics" / "observations.json"
        need(path.is_file(), "Missing external Diagnostic observations")
        with path.open(encoding="utf-8") as handle:
            external = json.load(handle)
        need(not external.get("Error") and external.get("CollectorsPrepared") is True
             and external.get("Coverage") in ("Covered", "InsufficientWindowSamples"),
             "External Diagnostic collection failed")
        captures = external.get("Snapshots")
        need(isinstance(captures, list), "Missing external capture accounting")
        coverage_rows = external.get("InvocationCoverage")
        need(isinstance(coverage_rows, list) and len(coverage_rows) == 3,
             "Missing actual-invocation diagnostic coverage accounting")
        for row in rows:
            if row["MeasurementStage"] == "WorkloadActual":
                coverage = [item for item in coverage_rows if item.get("InvocationId") == row.get("InvocationId")
                            and item.get("CaseId") == case and item.get("ProcessId") == row["ProcessId"]]
                need(len(coverage) == 1, "Missing/duplicate actual-invocation coverage record")
                matching = [capture for capture in captures if capture.get("InvocationId") == row.get("InvocationId")
                            and capture.get("CaseId") == case and capture.get("ProcessId") == row["ProcessId"]
                            and capture.get("Stage") == "WorkloadActual"]
                stacks = [dict(Capture=item["StackCapture"], Counts=item["StackCounts"], RawFile=item["StackFile"])
                          for item in matching if item.get("StackOverlap") == "ContainedInActual"]
                server = [dict(Capture=item["SqlCapture"], Metrics=item["Sql"], RawFile=item["SqlFile"],
                               CounterDeltaScope=item["SqlCounterDeltaScope"],
                               WaitDeltas=item["SqlWaitDeltas"] if item["SqlCounterDeltaScope"] == "SameActualInvocation" else None)
                          for item in matching if item.get("SqlOverlap") == "ContainedInActual"]
                need(coverage[0].get("InWindowStackCaptures") == len(stacks)
                     and coverage[0].get("InWindowSqlCaptures") == len(server),
                     "External coverage counts do not match in-window observations")
                expected = "Covered" if stacks and server else "InsufficientWindowSamples"
                need(coverage[0].get("Coverage") == expected, "Incorrect external coverage verdict")
                row["ExternalDiagnosticCoverage"] = coverage[0]
                row["ExternalDiagnostics"] = {"StackSnapshots": stacks or None, "ServerSnapshots": server or None}
                row["ExternalRawObservationsFile"] = "external-diagnostics/observations.json"
    return rows


def validate_equivalence(left, right):
    """Driver/runtime versions are observations, not workload equivalence requirements."""
    for field in EQUIVALENCE_FIELDS:
        if field in LOAD_FIELDS and left["Scenario"] != "E6":
            continue
        need(left.get(field) == right.get(field), f"Incompatible comparison workload: {field}")


def write_summary(results_dir, units, has_baseline, repetitions):
    rows, comparisons = [], []
    for repetition in range(1, repetitions + 1):
        for case in units:
            variants = {}
            for variant in (("baseline", "current") if has_baseline else ("current",)):
                directory = Path(results_dir) / "repetitions" / f"rep{repetition}" / variant / case
                variants[variant] = load_case(directory, case, repetition)
                rows.extend(dict(row, Variant=variant) for row in variants[variant])
            if has_baseline:
                validate_equivalence(variants["baseline"][0], variants["current"][0])
                paired = []
                baseline_rows = {row["Invocation"]: row for row in variants["baseline"]
                                 if row["MeasurementStage"] == "WorkloadActual"}
                for row in variants["current"]:
                    if row["MeasurementStage"] != "WorkloadActual":
                        continue
                    baseline = baseline_rows[row["Invocation"]]
                    metrics = {}
                    for field in ("SuccessThroughputPerSecond", "CompletedAtReturnFraction"):
                        old, new = baseline[field], row[field]
                        metrics[field] = {"Baseline": old, "Current": new,
                                          "Ratio": new / old if old is not None and old > 0
                                          and new is not None else None}
                    for field in ("OpenLatencyMilliseconds", "PendingTaskReturnMilliseconds",
                                  "LifecycleMilliseconds", "QueryMilliseconds"):
                        metrics[field] = {"Baseline": baseline[field], "Current": row[field]}
                    paired.append({"Invocation": row["Invocation"], "Metrics": metrics})
                comparisons.append({"CaseId": case, "ProcessRepetition": repetition,
                                    "WorkloadEquivalent": True, "NumericalGate": False,
                                    "BaselineDriverVersion": variants["baseline"][0]["DriverVersion"],
                                    "CurrentDriverVersion": variants["current"][0]["DriverVersion"],
                                    "BaselineRequestedV2": variants["baseline"][0]["UseConnectionPoolV2"],
                                    "CurrentRequestedV2": variants["current"][0]["UseConnectionPoolV2"],
                                    "ChangedRequestedSwitches": [
                                        field for field in ("UseConnectionPoolV2", "UseOptimizedAsyncBehaviour",
                                                            "UseManagedSniOnWindows")
                                        if variants["baseline"][0][field] != variants["current"][0][field]],
                                    "ActualInvocationComparisons": paired,
                                    "SwitchSupport": "Not inferred from the requested flag"})
    groups = {}
    for row in rows:
        if row["MeasurementStage"] == "WorkloadActual":
            # Match actual iteration position; never pool percentiles across invocations.
            key = (row["Variant"], row["CaseId"], row["Invocation"])
            groups.setdefault(key, []).append(row)
    variability = []
    for (variant, case, invocation), group in groups.items():
        values = [row["SuccessThroughputPerSecond"] for row in group]
        variability.append(dict(Variant=variant, CaseId=case, Invocation=invocation,
                                ProcessRepetitions=[row["ProcessRepetition"] for row in group],
                                ThroughputValues=values, Min=min(values), Max=max(values),
                                StandardDeviation=statistics.stdev(values) if len(values) > 1 else None))
    output = dict(SchemaVersion=2, BenchmarkProfile=legacy.FULL_PROFILE, Notice=NOTICE,
                  Invocations=rows, Comparisons=comparisons, RepetitionVariability=variability)
    with open(os.path.join(results_dir, "openasync-summary.json"), "w", encoding="utf-8") as handle:
        json.dump(output, handle, indent=2, allow_nan=False)
    lines = ["# OpenAsync scoreboard", "", NOTICE, "",
             "Full distributions, E6 phase accounting, diagnostics and repetition variability are in JSON.",
             "Supplemental elapsed excludes setup/export and initial/final process snapshots; BDN includes "
             "observer bookkeeping. Allocation/CPU/GC metrics are process-wide.", "",
             "| Variant | Case | Rep / PID / run | Stage / invocation | Success/s | Open p95 ms | "
             "Pending-return p95 ms | Completed-at-return fraction | Lifecycle p95 ms | Query p95 ms | External coverage |",
             "| --- | --- | --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |"]
    def percentile(row, field):
        value = row[field]
        return "N/A" if value is None else f"{value['P95']:.4f}"
    for row in rows:
        lines.append(f"| {row['Variant']} | {row['CaseId']} | {row['ProcessRepetition']} / "
                     f"{row['ProcessId']} / {row['RunId'] or 'N/A'} | {row['MeasurementStage']} / {row['Invocation']} | "
                     f"{row['SuccessThroughputPerSecond']:.2f} | {percentile(row, 'OpenLatencyMilliseconds')} | "
                     f"{percentile(row, 'PendingTaskReturnMilliseconds')} | {row['CompletedAtReturnFraction']} | "
                     f"{percentile(row, 'LifecycleMilliseconds')} | {percentile(row, 'QueryMilliseconds')} | "
                     f"{row.get('ExternalDiagnosticCoverage', {}).get('Coverage', 'Not requested')} |")
    Path(results_dir, "openasync-summary.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    need(not any(row["Outcomes"]["Failed"] or row["Outcomes"]["Canceled"]
                 or row["QueryOutcomes"]["Failed"] or row["QueryOutcomes"]["Canceled"]
                 or (row["LifecycleOutcomes"] and (row["LifecycleOutcomes"]["Failed"]
                                                    or row["LifecycleOutcomes"]["Canceled"]))
                 or row.get("WasCanceled") for row in rows),
         "Failed/canceled scoreboard operation; raw records and summary retained")
