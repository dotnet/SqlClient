// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Tests.Common;
#if NET && _WINDOWS
using System.Reflection;
#endif
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests for verifying the default values of all SqlClient-specific AppContext switches.
/// </summary>
[Collection(AppContextSwitchTestCollection.Name)]
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

    /// <summary>
    /// Keeps the configured async value observable without changing the parser override.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AsyncCompatibility_ReportsConfiguredAndEffectiveValues(
        bool configuredAsync, bool configuredProcessSni)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseCompatibilityAsyncBehaviour = configuredAsync,
            UseCompatibilityProcessSni = configuredProcessSni
        };
        bool expectedEffectiveAsync = configuredAsync || configuredProcessSni;

        // Act
        bool effectiveAsync = LocalAppContextSwitches.UseCompatibilityAsyncBehaviour;
        bool actualConfiguredAsync = LocalAppContextSwitches.UseCompatibilityAsyncBehaviourConfigured;
        ulong properties = (ulong)SqlClientDriverPropertiesResolver.Current;

        // Assert
        Assert.Equal(expectedEffectiveAsync, effectiveAsync);
        Assert.Equal(configuredAsync, actualConfiguredAsync);
        Assert.Equal(configuredAsync ? 2UL : 0UL, properties & 2UL);
        Assert.Equal(configuredProcessSni ? 4UL : 0UL, properties & 4UL);
    }

    [Fact]
    public void DriverProperties_Report_Default_Compatibility()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new()
        {
            UseConnectionPoolV2 = null,
            UseCompatibilityAsyncBehaviour = null,
            UseCompatibilityProcessSni = null
        };

        // Act
        bool configuredAsync = LocalAppContextSwitches.UseCompatibilityAsyncBehaviourConfigured;
        SqlClientDriverProperties properties = SqlClientDriverPropertiesResolver.Current;

        // Assert
        Assert.True(configuredAsync);
        Assert.Equal((SqlClientDriverProperties)6, properties);
    }

    #if NET && _WINDOWS
    [Fact]
    public void UseManagedNetworkingSetter_MutatesCacheAndRestoresOriginalValue()
    {
        // Arrange
        var cacheField = typeof(LocalAppContextSwitches).GetField(
            "s_useManagedNetworking", BindingFlags.Static | BindingFlags.NonPublic)!;
        // The private SwitchValue enum uses None = 0, True = 1, False = 2.
        byte original = (byte)cacheField.GetValue(null)!;
        bool newValue = original != 1;
        byte expected = (byte)(newValue ? 1 : 2);
        byte actual;

        // Act
        using (LocalAppContextSwitchesHelper switches = new())
        {
            switches.UseManagedNetworking = newValue;
            actual = (byte)cacheField.GetValue(null)!;
        }
        byte restored = (byte)cacheField.GetValue(null)!;

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(original, restored);
    }
    #endif
}
