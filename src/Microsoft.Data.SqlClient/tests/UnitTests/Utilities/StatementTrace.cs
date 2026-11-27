// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Records SqlClient command spans in this test's trace, isolating concurrent listener callbacks.
/// The test parent is current while the trace exists so that statement roots join its trace.
/// </summary>
internal sealed class StatementTrace : IDisposable
{
    private readonly ActivityListener _listener;
    internal Activity Parent { get; }
    internal ConcurrentQueue<Activity> Spans { get; } = new();

    internal StatementTrace()
    {
        // Created first so the stop callback can read its trace id; started after the
        // listener so it is current while statements run.
        Parent = new Activity("test").SetIdFormat(ActivityIdFormat.W3C);
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.Data.SqlClient.Command",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == Parent.TraceId)
                {
                    Spans.Enqueue(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
        Parent.Start();
    }

    public void Dispose()
    {
        _listener.Dispose();
        Parent.Dispose();
    }
}
