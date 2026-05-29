// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Tests.Common;
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
        // LocalAppContextSwitches caches each switch value on first access for
        // the lifetime of the process.  Other tests running in parallel may
        // already have triggered caching, or may use LocalAppContextSwitchesHelper
        // to mutate the cached fields via reflection.  To make this test
        // deterministic, acquire the helper (which serializes against every
        // other helper user via a process-wide semaphore) and reset each
        // cached field to None so the properties re-read from AppContext.
        using LocalAppContextSwitchesHelper switchesHelper = new();

        switchesHelper.EnableMultiSubnetFailoverByDefault = null;
        switchesHelper.IgnoreServerProvidedFailoverPartner = null;
        switchesHelper.UseLegacyFailoverAlternationOnLoginSqlErrors = null;
        switchesHelper.LegacyRowVersionNullBehavior = null;
        switchesHelper.LegacyVarTimeZeroScaleBehaviour = null;
        switchesHelper.MakeReadAsyncBlocking = null;
        switchesHelper.SuppressInsecureTlsWarning = null;
        switchesHelper.TruncateScaledDecimal = null;
        switchesHelper.UseCompatibilityAsyncBehaviour = null;
        switchesHelper.UseCompatibilityProcessSni = null;
        switchesHelper.UseConnectionPoolV2 = null;
        switchesHelper.UseMinimumLoginTimeout = null;
        #if NET
        switchesHelper.GlobalizationInvariantMode = null;
        #endif
        #if NET && _WINDOWS
        switchesHelper.UseManagedNetworking = null;
        #endif
        #if NETFRAMEWORK
        switchesHelper.DisableTnirByDefault = null;
        #endif

        Assert.False(LocalAppContextSwitches.LegacyRowVersionNullBehavior);
        Assert.False(LocalAppContextSwitches.SuppressInsecureTlsWarning);
        Assert.False(LocalAppContextSwitches.MakeReadAsyncBlocking);
        Assert.True(LocalAppContextSwitches.UseMinimumLoginTimeout);
        Assert.True(LocalAppContextSwitches.LegacyVarTimeZeroScaleBehaviour);
        Assert.True(LocalAppContextSwitches.UseCompatibilityProcessSni);
        Assert.True(LocalAppContextSwitches.UseCompatibilityAsyncBehaviour);
        Assert.False(LocalAppContextSwitches.UseConnectionPoolV2);
        Assert.False(LocalAppContextSwitches.TruncateScaledDecimal);
        Assert.False(LocalAppContextSwitches.IgnoreServerProvidedFailoverPartner);
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
