// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ConnectionPoolRampStress.Tests;

/// <summary>Ensures counter observations do not enable hot-path driver tracing.</summary>
public sealed class TelemetryTests
{
    /// <summary>Receives physical-connection counters while informational trace keywords remain disabled.</summary>
    [Fact]
    public async Task CountersDoNotEnableDriverTracing()
    {
        SqlConnection.ClearAllPools();
        using PhysicalCounters counters = new();
        EventSource source = Assert.Single(EventSource.GetSources(),
            source => source.Name == "Microsoft.Data.SqlClient.EventSource");
        Assert.False(source.IsEnabled(EventLevel.Informational, (EventKeywords)2));
        Assert.False(source.IsEnabled(EventLevel.Informational, (EventKeywords)32));

        Stopwatch wait = Stopwatch.StartNew();
        while (counters.Read(0, 0, 0, 0).ActivePhysicalConnections is null &&
               wait.Elapsed < TimeSpan.FromSeconds(10))
        {
            await Task.Delay(50);
        }

        Assert.NotNull(counters.Read(0, 0, 0, 0).ActivePhysicalConnections);
        Assert.False(source.IsEnabled(EventLevel.Informational, (EventKeywords)2));
        Assert.False(source.IsEnabled(EventLevel.Informational, (EventKeywords)32));
    }
}
