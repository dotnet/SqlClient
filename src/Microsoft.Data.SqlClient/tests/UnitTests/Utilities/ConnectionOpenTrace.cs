// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Records only spans belonging to this test's parent, isolating concurrent listener callbacks.
/// </summary>
internal sealed class ConnectionOpenTrace : IDisposable
{
    private readonly ActivityListener _listener;
    internal Activity Parent { get; } = new Activity("test").SetIdFormat(ActivityIdFormat.W3C).Start();
    internal ConcurrentQueue<Activity> Spans { get; } = new();
    internal ConcurrentQueue<Activity> AllSpans { get; } = new();

    internal ConnectionOpenTrace()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Microsoft.Data.SqlClient.Connection",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == Parent.TraceId)
                {
                    AllSpans.Enqueue(activity);
                }
                if (activity.ParentSpanId == Parent.SpanId)
                {
                    Spans.Enqueue(activity);
                }
            }
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        Parent.Dispose();
    }
}
