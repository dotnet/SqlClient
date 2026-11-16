// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed record AsyncHealthOperationObservation(
    int Invocation, int Worker, bool First, double OfferedMs)
{
    public double? StartMs { get; set; }
    public double? OpenEndMs { get; set; }
    public double? EndMs { get; set; }
    public bool? OpenCompletedInline { get; set; }
    public string Outcome { get; set; } = "Queued";
    public string? Error { get; set; }
}

internal sealed class AsyncHealthOperationSnapshot
{
    public int Invocations { get; init; }
    public int CompletedInvocations { get; init; }
    public int ExpectedWorkers { get; init; }
    public int WorkersStarted { get; init; }
    public int FirstWorkerOutcomesMissing { get; init; }
    public long Offered { get; init; }
    public long Started { get; init; }
    public long Opened { get; init; }
    public long Completed { get; init; }
    public long Failed { get; init; }
    public long Cancelled { get; init; }
    public long OpenFailures { get; init; }
    public long InlineOpens { get; init; }
    public long PendingOpens { get; init; }
    public long Queued => Offered - Started;
    public long InFlight => Started - Completed - Failed - Cancelled;
    public long ObservationsDropped { get; init; }
    public double ElapsedMilliseconds { get; init; }
    public double? CompletedPerSecond => ElapsedMilliseconds > 0
        ? Completed * 1000 / ElapsedMilliseconds : null;
    public AsyncHealthOperationObservation[] Observations { get; init; } = [];
}

/// <summary>Records actual benchmark operations only while the diagnoser is observing.</summary>
internal sealed class AsyncHealthOperationRecorder
{
    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly long _origin;
    private readonly int _sampleLimit;
    private readonly List<AsyncHealthOperationObservation> _records = [];
    private readonly HashSet<(int, int)> _offeredWorkers = [];
    private readonly HashSet<(int, int)> _startedWorkers = [];
    private readonly HashSet<(int, int)> _finishedFirstWorkers = [];
    private int _invocations, _completedInvocations, _expectedWorkers;
    private long _offered, _started, _opened, _completed, _failed, _cancelled;
    private long _openFailures, _inline, _pending;
    private double? _firstOffer;
    private double _lastOutcome;

    internal AsyncHealthOperationRecorder(TimeProvider? time = null, int sampleLimit = 100000)
    {
        if (sampleLimit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleLimit));
        }
        _time = time ?? TimeProvider.System;
        _origin = _time.GetTimestamp();
        _sampleLimit = sampleLimit;
    }

    private double Timestamp() => _time.GetElapsedTime(_origin).TotalMilliseconds;

    internal Invocation BeginInvocation(int workers)
    {
        if (workers < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(workers));
        }
        lock (_gate)
        {
            _expectedWorkers += workers;
            return new Invocation(this, ++_invocations, workers);
        }
    }

    internal AsyncHealthOperationSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new AsyncHealthOperationSnapshot
            {
                Invocations = _invocations, CompletedInvocations = _completedInvocations,
                ExpectedWorkers = _expectedWorkers, WorkersStarted = _startedWorkers.Count,
                FirstWorkerOutcomesMissing = _expectedWorkers - _finishedFirstWorkers.Count,
                Offered = _offered, Started = _started, Opened = _opened, Completed = _completed,
                Failed = _failed, Cancelled = _cancelled, OpenFailures = _openFailures,
                InlineOpens = _inline, PendingOpens = _pending,
                ObservationsDropped = _offered - _records.Count,
                ElapsedMilliseconds = _firstOffer.HasValue
                    ? Math.Max(0, _lastOutcome - _firstOffer.Value) : 0,
                Observations = _records.Select(record => record with { }).ToArray()
            };
        }
    }

    internal sealed class Invocation(AsyncHealthOperationRecorder owner, int id, int workers)
    {
        private bool _completed;

        internal Operation Offer(int worker)
        {
            if (worker < 0 || worker >= workers)
            {
                throw new ArgumentOutOfRangeException(nameof(worker));
            }
            lock (owner._gate)
            {
                double offered = owner.Timestamp();
                owner._firstOffer ??= offered;
                bool first = owner._offeredWorkers.Add((id, worker));
                var record = new AsyncHealthOperationObservation(id, worker, first, offered);
                owner._offered++;
                if (owner._records.Count < owner._sampleLimit || first)
                {
                    owner._records.Add(record);
                }
                return new Operation(owner, record);
            }
        }

        internal void Complete()
        {
            lock (owner._gate)
            {
                if (_completed)
                {
                    throw new InvalidOperationException("Invocation already completed.");
                }
                _completed = true;
                owner._completedInvocations++;
            }
        }
    }

    internal sealed class Operation(
        AsyncHealthOperationRecorder owner, AsyncHealthOperationObservation record)
    {
        internal void Start()
        {
            lock (owner._gate)
            {
                if (record.StartMs.HasValue)
                {
                    throw new InvalidOperationException("Operation already started.");
                }
                record.StartMs = owner.Timestamp();
                record.Outcome = "InFlight";
                owner._started++;
                owner._startedWorkers.Add((record.Invocation, record.Worker));
            }
        }

        internal void OpenReturned(bool completedInline)
        {
            lock (owner._gate)
            {
                record.OpenCompletedInline = completedInline;
                if (completedInline) owner._inline++;
                else owner._pending++;
            }
        }

        internal void Opened()
        {
            lock (owner._gate)
            {
                record.OpenEndMs = owner.Timestamp();
                owner._opened++;
            }
        }

        internal void Complete() => Finish(null);
        internal void Fail(Exception error) => Finish(error);

        private void Finish(Exception? error)
        {
            lock (owner._gate)
            {
                if (!record.StartMs.HasValue || record.EndMs.HasValue)
                {
                    throw new InvalidOperationException("Invalid operation completion.");
                }
                if (error == null && !record.OpenEndMs.HasValue)
                {
                    throw new InvalidOperationException("Completed work has no successful open.");
                }
                record.EndMs = owner.Timestamp();
                owner._lastOutcome = record.EndMs.Value;
                if (record.First)
                {
                    owner._finishedFirstWorkers.Add((record.Invocation, record.Worker));
                }
                if (error == null)
                {
                    record.Outcome = "Completed";
                    owner._completed++;
                }
                else
                {
                    record.Outcome = error is OperationCanceledException ? "Cancelled" : "Failed";
                    // Never retain exception messages, which may contain endpoint/credential text.
                    record.Error = error.GetType().Name +
                        (error is SqlException sql ? ":" + sql.Number : "");
                    if (error is OperationCanceledException) owner._cancelled++;
                    else
                    {
                        owner._failed++;
                        if (!record.OpenEndMs.HasValue) owner._openFailures++;
                    }
                }
            }
        }
    }
}
