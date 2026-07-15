// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Tests.Common;
#if NET
using System.Runtime.InteropServices;
#endif
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
        switchesHelper.UseLegacyIdleTimeoutBehavior = null;
        switchesHelper.UseMinimumLoginTimeout = null;
        #if NET
        switchesHelper.GlobalizationInvariantMode = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            switchesHelper.UseManagedNetworking = null;
        }
        #endif
        #if NETFRAMEWORK
        switchesHelper.DisableTnirByDefault = null;
        #endif

        Assert.False(switchesHelper.LegacyRowVersionNullBehavior);
        Assert.False(switchesHelper.SuppressInsecureTlsWarning);
        Assert.False(switchesHelper.MakeReadAsyncBlocking);
        Assert.True(switchesHelper.UseMinimumLoginTimeout);
        Assert.True(switchesHelper.LegacyVarTimeZeroScaleBehaviour);
        Assert.True(switchesHelper.UseCompatibilityProcessSni);
        Assert.True(switchesHelper.UseCompatibilityAsyncBehaviour);
        Assert.True(switchesHelper.UseLegacyIdleTimeoutBehavior);
        Assert.False(switchesHelper.UseConnectionPoolV2);
        Assert.False(switchesHelper.UseOverallConnectTimeoutForPoolWait);
        Assert.False(switchesHelper.TruncateScaledDecimal);
        Assert.False(switchesHelper.IgnoreServerProvidedFailoverPartner);
        Assert.False(switchesHelper.UseLegacyFailoverAlternationOnLoginSqlErrors);
        Assert.False(switchesHelper.EnableMultiSubnetFailoverByDefault);
        #if NET
        Assert.False(switchesHelper.GlobalizationInvariantMode);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.False(switchesHelper.UseManagedNetworking);
        }
        else
        {
            // On .NET Unix, native SNI is unavailable, so UseManagedNetworking
            // is a constant true.
            Assert.True(switchesHelper.UseManagedNetworking);
        }
        #endif
        #if NETFRAMEWORK
        Assert.False(switchesHelper.DisableTnirByDefault);
        #endif
    }
}
