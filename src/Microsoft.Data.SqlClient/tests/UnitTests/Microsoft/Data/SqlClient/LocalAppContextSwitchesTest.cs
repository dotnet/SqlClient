// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        using LocalAppContextSwitchesHelper appContextSwitchesHelper = new();

        Assert.False(appContextSwitchesHelper.LegacyRowVersionNullBehavior);
        Assert.False(appContextSwitchesHelper.SuppressInsecureTlsWarning);
        Assert.False(appContextSwitchesHelper.MakeReadAsyncBlocking);
        Assert.True(appContextSwitchesHelper.UseMinimumLoginTimeout);
        Assert.True(appContextSwitchesHelper.LegacyVarTimeZeroScaleBehaviour);
        Assert.True(appContextSwitchesHelper.UseCompatibilityProcessSni);
        Assert.True(appContextSwitchesHelper.UseCompatibilityAsyncBehaviour);
        Assert.False(appContextSwitchesHelper.UseConnectionPoolV2);
        Assert.False(appContextSwitchesHelper.TruncateScaledDecimal);
        Assert.False(appContextSwitchesHelper.IgnoreServerProvidedFailoverPartner);
        Assert.False(appContextSwitchesHelper.EnableMultiSubnetFailoverByDefault);
        #if NET
        Assert.False(appContextSwitchesHelper.GlobalizationInvariantMode);
        #endif
        #if NET && _WINDOWS
        Assert.False(appContextSwitchesHelper.UseManagedNetworking);
        #endif
        #if NETFRAMEWORK
        Assert.False(appContextSwitchesHelper.DisableTnirByDefault);
        #endif
    }
}
