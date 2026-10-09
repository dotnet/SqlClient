// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

string scenario = args.Single();
if (scenario is "bad-runtime" or "bad-runtime-disabled")
{
    if (scenario == "bad-runtime-disabled")
    {
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", false);
        AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", false);
        Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, new TestProvider()),
            "Disabling configuration and discovery bypassed runtime version enforcement.");
    }
    else
    {
        Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
            "Mismatched driver bootstrap returned a provider.");
    }

    using var listener = new AuthenticationTraceListener();
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
        "Cached version failure did not return null.");
    Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, new TestProvider()),
        "Cached version failure did not return false.");
    Require(listener.Messages.Any(message => message.Contains("version mismatch") &&
        message.Contains("7.0.1") && message.Contains("Upgrade all SqlClient family packages together")),
        "Cached runtime mismatch diagnostics omitted the exact version or corrective guidance.");
    Require(listener.Messages.Any(message => message.Contains("GetProvider") && message.Contains("restart")) &&
        listener.Messages.Any(message => message.Contains("SetProvider") && message.Contains("restart")),
        "Cached version failures did not diagnose both registry entry points.");
    Console.WriteLine("PASS: " + scenario);
    return;
}

string sectionName = scenario switch
{
    "configured" => "SqlClientAuthenticationProviders",
    "legacy" => "SqlAuthenticationProviders",
    _ => throw new ArgumentException("Unknown configuration scenario: " + scenario)
};

var section = ConfigurationManager.GetSection(sectionName) as SqlAuthenticationProviderConfigurationSection
    ?? throw new InvalidOperationException("The section declaration did not resolve.");
ConfigurationSection configurationSection = section;
Require(configurationSection.ElementInformation.Properties.Count == 4 && section.Providers.Count == 1,
    "The public configuration schema did not compile or load through its transitive dependencies.");
Require(section.GetType().Assembly.GetName().Name == "Microsoft.Data.SqlClient.Extensions.Abstractions",
    "The assembly-qualified handler was not forwarded to Abstractions.");
Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive) is TestProvider,
    "The Abstractions registry did not load the configured provider.");
Require(TestInitializer.Initialized, "The configured initializer did not run.");
Require(typeof(SqlAuthenticationInitializer).Assembly.GetName().Name == "Microsoft.Data.SqlClient.Extensions.Abstractions",
    "The initializer type did not relocate to Abstractions.");
Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, new TestProvider()),
    "Application registration replaced the configured provider.");
Console.WriteLine("PASS: " + scenario);

/// <summary>Fails the executable scenario when its required compatibility contract is not preserved.</summary>
static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

/// <summary>Proves configured initializers activate through the forwarded Abstractions type.</summary>
public sealed class TestInitializer : SqlAuthenticationInitializer
{
    public static bool Initialized;

    /// <inheritdoc />
    public override void Initialize() => Initialized = true;
}

/// <summary>Registers a provider without contacting an identity service or a database.</summary>
public sealed class TestProvider : SqlAuthenticationProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => true;

    /// <inheritdoc />
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
        throw new NotSupportedException();
}

/// <summary>Captures diagnostics when tracing is enabled after the original bootstrap failure.</summary>
internal sealed class AuthenticationTraceListener : EventListener
{
    internal ConcurrentQueue<string> Messages { get; } = new();

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
        {
            EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)2);
        }
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId == 3 && eventData.Payload?[0] is string message)
        {
            Messages.Enqueue(message);
        }
    }
}
