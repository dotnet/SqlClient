// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using System.Configuration;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

string scenario = args.Single();
if (scenario == "disabled" || scenario == "bad-runtime-disabled")
{
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", false);
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", false);
}
if (scenario.StartsWith("bad-", StringComparison.Ordinal))
{
    if (scenario is "bad-provider" or "bad-runtime-disabled")
    {
        Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, new TestProvider()),
            "Failed bootstrap did not return false.");
    }
    else
    {
        Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
            "Failed bootstrap did not return null.");
    }

    using var listener = new AuthenticationTraceListener();
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
        "Cached version failure did not return null.");
    Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, new TestProvider()),
        "Cached version failure did not return false.");
    string expected = scenario switch
    {
        "bad-provider" => "MissingProvider",
        "bad-initializer" => "MissingInitializer",
        "bad-unsupported" => "UnsupportedProvider",
        _ => "version mismatch"
    };
    Require(listener.Messages.Any(message => message.Contains(expected)),
        "Cached failure diagnostics omitted the failing type or version.");
    if (scenario.StartsWith("bad-runtime", StringComparison.Ordinal))
    {
        Require(listener.Messages.Any(message => message.Contains("7.0.1") &&
            message.Contains("Upgrade all SqlClient family packages together")),
            "Cached runtime mismatch diagnostics omitted the exact version or corrective guidance.");
    }
    Require(listener.Messages.Any(message => message.Contains("GetProvider") &&
        message.Contains("ActiveDirectoryDefault") && message.Contains("returning null") && message.Contains("restart")) &&
        listener.Messages.Any(message => message.Contains("SetProvider") &&
        message.Contains("ActiveDirectoryDefault") && message.Contains("returning false") && message.Contains("restart")),
        "Cached failures did not diagnose both registry entry points.");
    Console.WriteLine("PASS: " + scenario);
    return;
}

if (scenario == "disabled")
{
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
        "Discovery switch was ignored.");
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive) is null,
        "Configuration switch was ignored.");
    Require(!TestInitializer.Initialized, "Disabled initializer ran.");
}
else
{
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
    using var precedenceListener = new AuthenticationTraceListener();
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)
        is ActiveDirectoryAuthenticationProvider, "First access did not discover Azure.");
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive)?.GetType() == typeof(TestProvider),
        "Configuration did not replace the initializer provider.");
    Require(TestInitializer.Initialized, "The configured initializer did not run.");
    Require(typeof(SqlAuthenticationInitializer).Assembly.GetName().Name == "Microsoft.Data.SqlClient.Extensions.Abstractions",
        "The initializer type did not relocate to Abstractions.");
    Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, new TestProvider()),
        "Application registration replaced the configured provider.");
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow) is InitializerProvider,
        "Azure replaced the initializer provider.");
    Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new TestProvider()),
        "Application registration replaced the Config-tier initializer.");
    Require(precedenceListener.Messages.Any(message => message.Contains("ActiveDirectoryInteractive") &&
        message.Contains("update app.config or its initializer")),
        "Configuration precedence refusal did not explain how to change the provider.");
}
var applicationProvider = new TestProvider();
Require(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, applicationProvider),
    "Application registration did not replace the discovered provider.");
Require(ReferenceEquals(applicationProvider,
    SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)),
    "Application registration did not round-trip.");
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
    public override void Initialize()
    {
        Initialized = SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new InitializerProvider()) &&
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive, new InitializerProvider());
    }
}

/// <summary>Registers a provider without contacting an identity service or a database.</summary>
public class TestProvider : SqlAuthenticationProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => true;

    /// <inheritdoc />
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
        throw new NotSupportedException();
}

/// <summary>Distinguishes initializer registration from configuration and application registration.</summary>
public sealed class InitializerProvider : TestProvider { }

/// <summary>Exercises configuration bootstrap failure for an unsupported method.</summary>
public sealed class UnsupportedProvider : TestProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => false;
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
