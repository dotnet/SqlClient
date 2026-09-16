// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests that the EnableAppConfig switch gates the configurable retry logic
/// providers used by commands and connections.
/// </summary>
/// <remarks>
/// The authentication provider and switch override readers run once, in
/// static constructors, so they cannot be exercised in-process.
/// </remarks>
[Collection(AppContextSwitchTestCollection.Name)]
public class EnableAppConfigSwitchTest
{
    /// <summary>
    /// With the switch off, commands share one non-retriable provider that
    /// does not come from the configurable retry logic manager.
    /// </summary>
    [Fact]
    public void Disabled_CommandsShareNonRetriableProvider()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = false;

        using SqlCommand first = new();
        using SqlCommand second = new();

        Assert.Same(first.RetryLogicProvider, second.RetryLogicProvider);
        Assert.False(SqlConfigurableRetryFactory.IsRetriable(first.RetryLogicProvider));
        Assert.NotSame(SqlConfigurableRetryLogicManager.CommandProvider, first.RetryLogicProvider);
    }

    /// <summary>
    /// With the switch off, connections share one non-retriable provider that
    /// does not come from the configurable retry logic manager.
    /// </summary>
    [Fact]
    public void Disabled_ConnectionsShareNonRetriableProvider()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = false;

        using SqlConnection first = new();
        using SqlConnection second = new();

        Assert.Same(first.RetryLogicProvider, second.RetryLogicProvider);
        Assert.False(SqlConfigurableRetryFactory.IsRetriable(first.RetryLogicProvider));
        Assert.NotSame(SqlConfigurableRetryLogicManager.ConnectionProvider, first.RetryLogicProvider);
    }

    /// <summary>
    /// With the switch on, commands and connections use the manager's
    /// providers, as they did before the switch existed.
    /// </summary>
    [Fact]
    public void Enabled_UsesManagerProviders()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = true;

        using SqlCommand command = new();
        using SqlConnection connection = new();

        Assert.Same(SqlConfigurableRetryLogicManager.CommandProvider, command.RetryLogicProvider);
        Assert.Same(SqlConfigurableRetryLogicManager.ConnectionProvider, connection.RetryLogicProvider);
    }
}
