// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System;
using System.Diagnostics;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni;

/// <summary>Verifies managed instance discovery uses the active connection trace.</summary>
public class SniProxyTests
{
    /// <summary>An expired named-instance lookup records its failure before SNI translates it.</summary>
    [Fact]
    public void CreateConnectionHandle_ExpiredInstanceLookup_RecordsDiscoveryFailure()
    {
        // Arrange
        using ConnectionOpenTrace trace = new();
        using SqlConnection owner = new();
        using OperationScope? root = ConnectionOpenActivity.Start();
        using OperationScope? attempt = ConnectionOpenActivity.StartAttempt(root, owner);
        using OperationScope? physical = ConnectionOpenActivity.StartPhysical(owner, out IDisposable? route);
        SQLDNSInfo? dns = null;

        // Act
        Assert.Throws<TimeoutException>(() => SniProxy.CreateConnectionHandle(
            @"tcp:localhost\instance", TimeoutTimer.StartExpired(), out _, out _, null,
            false, false, false, false, SqlConnectionIPAddressPreference.UsePlatformDefault,
            null, ref dns, false, null, null));

        // Assert
        Activity discovery = Assert.Single(trace.AllSpans,
            span => span.OperationName == "connection.instance_discovery");
        Assert.Equal(physical?.Activity?.SpanId, discovery.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Error, discovery.Status);
        Assert.Empty(discovery.Events);

        // Cleanup
        route?.Dispose();
        ConnectionOpenActivity.FinishAttempt(attempt);
    }
}
#endif
