// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests for verifying the default values of all SqlClient-specific AppContext switches.
/// </summary>
public class LocalAppContextSwitchesTest
{
    /// <summary>
    /// Tests the default values of every AppContext switch used by SqlClient.
    /// </summary>
    [Fact]
    public void TestDefaultAppContextSwitchValues()
    {
        Assert.False(LocalAppContextSwitches.LegacyRowVersionNullBehavior);
        Assert.False(LocalAppContextSwitches.SuppressInsecureTlsWarning);
        Assert.False(LocalAppContextSwitches.SuppressNativeActivityTelemetry);
        Assert.False(LocalAppContextSwitches.MakeReadAsyncBlocking);
        Assert.True(LocalAppContextSwitches.UseMinimumLoginTimeout);
        Assert.True(LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour);
        Assert.True(LocalAppContextSwitches.UseCompatibilityProcessSni);
        Assert.True(LocalAppContextSwitches.UseCompatibilityAsyncBehaviour);
        Assert.False(LocalAppContextSwitches.UseConnectionPoolV2);
        Assert.False(LocalAppContextSwitches.TruncateScaledDecimal);
        Assert.False(LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner);
        Assert.False(LocalAppContextSwitches.EnableUserAgent);
        Assert.False(LocalAppContextSwitches.EnableMultiSubnetFailoverByDefault);
        #if NET
        Assert.False(LocalAppContextSwitches.GlobalizationInvariantMode);
        #endif
        #if NET && _WINDOWS
        Assert.False(LocalAppContextSwitches.UseManagedNetworking);
        #endif
        #if NETFRAMEWORK
        Assert.False(LocalAppContextSwitches.DisableTnirByDefault);
        #endif
    }
}
