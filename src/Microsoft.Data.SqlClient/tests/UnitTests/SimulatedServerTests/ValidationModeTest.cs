// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Verifies the networking mode in the temporary focused CI test process.
/// </summary>
[Collection(SimulatedServerTestCollection.Name)]
public class ValidationModeTest
{
    /// <summary>
    /// Prevents a managed-SNI pipeline leg from silently exercising native SNI.
    /// </summary>
    [Fact]
    public void NetworkingModeMatchesPipeline()
    {
        Assert.Equal(bool.Parse(Environment.GetEnvironmentVariable("SQLCLIENT_EXPECT_MANAGED_SNI")!), LocalAppContextSwitches.UseManagedNetworking);
    }
}
