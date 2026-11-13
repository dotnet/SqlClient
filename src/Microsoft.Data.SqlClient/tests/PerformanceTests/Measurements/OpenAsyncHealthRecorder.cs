// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.PerformanceTests;

internal sealed record OpenAsyncHealthObservation(
    int Worker, double OfferedMs, double StartMs, double? OpenEndMs, double EndMs, string Outcome);

internal sealed class OpenAsyncHealthCounts
{
    public long Started { get; set; }
    public long Opened { get; set; }
    public long Completed { get; set; }
    public long Failed { get; set; }
    public long Cancelled { get; set; }
    public long InlineOpens { get; set; }
    public long PendingOpens { get; set; }
    public long InFlight => Started - Completed - Failed - Cancelled;
    public int WorkersStarted { get; set; }
}

/// <summary>Reconciled counters and bounded retention, reserving every worker's first outcome.</summary>
internal sealed class OpenAsyncHealthRecorder(int workers, int sampleLimit = 100000)
{
    private readonly object _gate = new();
    private readonly bool[] _seen = new bool[workers];
    private readonly OpenAsyncHealthCounts _counts = new();
    private readonly List<OpenAsyncHealthObservation> _observations = [];
    private long _outcomes;

    public void Start(bool first)
    {
        lock (_gate)
        {
            _counts.Started++;
            if (first)
            {
                _counts.WorkersStarted++;
            }
        }
    }

    public void OpenReturned(bool inline)
    {
        lock (_gate)
        {
            if (inline) _counts.InlineOpens++;
            else _counts.PendingOpens++;
        }
    }

    public void Opened()
    {
        lock (_gate) _counts.Opened++;
    }

    public void Finish(OpenAsyncHealthObservation observation)
    {
        lock (_gate)
        {
            switch (observation.Outcome)
            {
                case "Completed": _counts.Completed++; break;
                case "Failed": _counts.Failed++; break;
                case "Cancelled": _counts.Cancelled++; break;
                default: throw new ArgumentException("Unknown operation outcome.");
            }

            _outcomes++;
            if (_outcomes <= sampleLimit || !_seen[observation.Worker])
            {
                _observations.Add(observation);
            }
            _seen[observation.Worker] = true;
        }
    }

    public OpenAsyncHealthCounts Counts()
    {
        lock (_gate)
        {
            return new OpenAsyncHealthCounts
            {
                Started = _counts.Started, Opened = _counts.Opened, Completed = _counts.Completed,
                Failed = _counts.Failed, Cancelled = _counts.Cancelled,
                InlineOpens = _counts.InlineOpens, PendingOpens = _counts.PendingOpens,
                WorkersStarted = _counts.WorkersStarted
            };
        }
    }

    public (OpenAsyncHealthObservation[] Observations, long Dropped) Snapshot()
    {
        lock (_gate) return (_observations.ToArray(), _outcomes - _observations.Count);
    }
}
