// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests that the EnableAppConfig switch gates every app.config reader: the
/// configurable retry logic providers, the switch overrides applied during
/// static initialization. Authentication configuration is covered by the
/// dedicated AuthenticationTests configuration-process harness.
/// </summary>
[Collection(AppContextSwitchTestCollection.Name)]
public class EnableAppConfigSwitchTest
{
    /// <summary>
    /// With the switch off, commands share the factory's non-retriable
    /// provider rather than one from the configuration manager, which is what
    /// reads the retry sections.
    /// </summary>
    [Fact]
    public void Disabled_CommandsShareNonRetriableProvider()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = false;

        using SqlCommand first = new();
        using SqlCommand second = new();

        Assert.Same(SqlConfigurableRetryFactory.NoneRetryProvider, first.RetryLogicProvider);
        Assert.Same(first.RetryLogicProvider, second.RetryLogicProvider);
        Assert.False(SqlConfigurableRetryFactory.IsRetriable(first.RetryLogicProvider));
    }

    /// <summary>
    /// With the switch off, connections share the same provider as commands.
    /// </summary>
    [Fact]
    public void Disabled_ConnectionsShareNonRetriableProvider()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = false;

        using SqlConnection first = new();
        using SqlConnection second = new();

        Assert.Same(SqlConfigurableRetryFactory.NoneRetryProvider, first.RetryLogicProvider);
        Assert.Same(first.RetryLogicProvider, second.RetryLogicProvider);
        Assert.False(SqlConfigurableRetryFactory.IsRetriable(first.RetryLogicProvider));
    }

    /// <summary>
    /// With the switch on, commands and connections use the configuration
    /// manager's providers, as they did before the switch existed.
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

    /// <summary>
    /// The switch gates the switch overrides section read during static
    /// initialization.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SwitchOverridesAreReadOnlyWhenEnabled(bool enabled)
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = enabled;

        Assert.Equal(enabled, LocalAppContextSwitches.ApplyAppConfigSwitchOverrides());
    }

}

#endif
