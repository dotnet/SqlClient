// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests that the EnableAppConfig switch gates the configurable retry logic
/// providers used by commands and connections, and that nothing reads the
/// configuration file while it is disabled.
/// </summary>
/// <remarks>
/// The authentication provider reader runs once, in a static constructor, so it
/// cannot be exercised in-process.
/// </remarks>
[Collection(AppContextSwitchTestCollection.Name)]
public class EnableAppConfigSwitchTest
{
    /// <summary>
    /// With the switch off, commands share the factory's non-retriable
    /// provider rather than one from the configuration manager.
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
    /// With the switch off, obtaining the providers reads no configuration
    /// section.  Every path through AppConfigManager traces the section name,
    /// so the absence of such a trace shows the file was not read.
    /// </summary>
    [Fact]
    public void Disabled_ReadsNoConfigurationSection()
    {
        using LocalAppContextSwitchesHelper switchesHelper = new();
        switchesHelper.EnableAppConfig = false;

        using TraceCollector traces = new();

        using SqlCommand command = new();
        _ = command.RetryLogicProvider;
        using SqlConnection connection = new();
        _ = connection.RetryLogicProvider;

        Assert.DoesNotContain(traces.Messages, message => message.Contains(RetrySectionNameFragment));

        // A read of the same sections is traced, so the check above cannot pass
        // merely because traces are unavailable.
        _ = AppConfigManager.FetchConfigurationSection<SqlConfigurableRetryConnectionSection>(
            SqlConfigurableRetryConnectionSection.Name);

        Assert.Contains(traces.Messages, message => message.Contains(RetrySectionNameFragment));
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
    /// Shared by the retry sections and by the configurable retry logic
    /// manager's own traces.
    /// </summary>
    private const string RetrySectionNameFragment = "SqlConfigurableRetryLogic";

    /// <summary>
    /// Collects SqlClient's trace messages for the lifetime of the instance.
    /// </summary>
    private sealed class TraceCollector : EventListener
    {
        private readonly ConcurrentQueue<string> _messages = new();

        public IEnumerable<string> Messages => _messages;

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
            {
                EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Payload is null)
            {
                return;
            }

            foreach (object? payload in eventData.Payload)
            {
                if (payload is string message)
                {
                    _messages.Enqueue(message);
                }
            }
        }
    }
}

#endif
