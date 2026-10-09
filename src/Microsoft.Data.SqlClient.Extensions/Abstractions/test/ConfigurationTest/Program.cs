// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;

string scenario = args.Length == 0 ? "configured" : args[0];
if (scenario == "bad-runtime-disabled")
{
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", false);
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", false);
}
if (scenario == "disabled")
{
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", false);
    AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", false);
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
        "Discovery switch was ignored.");
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive) is null,
        "Configuration switch was ignored.");
    Require(!TestInitializer.Registered, "Disabled initializer ran.");
}
else if (scenario.StartsWith("bad-", StringComparison.Ordinal))
{
    // Exercise both bootstrap entry points, then attach tracing after the initial failure.
    if (scenario is "bad-provider" or "bad-runtime-disabled")
    {
        Require(!SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDefault, new TestProvider()),
            "Failed bootstrap did not return false.");
    }
    else
    {
        Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
            "Failed bootstrap did not return null.");
    }
    using var listener = new AuthenticationTraceListener();
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null,
        "Cached bootstrap failure did not return null.");
    Require(!SqlAuthenticationProvider.SetProvider(
        SqlAuthenticationMethod.ActiveDirectoryDefault, new TestProvider()),
        "Cached bootstrap failure did not return false.");
    Require(listener.Messages.Any(message => message.Contains("GetProvider") &&
        message.Contains("ActiveDirectoryDefault") && message.Contains("returning null") &&
        message.Contains("restart")),
        "Cached GetProvider failure did not log actionable diagnostics.");
    Require(listener.Messages.Any(message => message.Contains("SetProvider") &&
        message.Contains("ActiveDirectoryDefault") && message.Contains("returning false") &&
        message.Contains("restart")),
        "Cached SetProvider failure did not log actionable diagnostics.");
    string expected = scenario switch
    {
        "bad-provider" => "MissingProvider",
        "bad-initializer" => "MissingInitializer",
        "bad-unsupported" => "UnsupportedProvider",
        _ => "version mismatch"
    };
    Require(listener.Messages.Any(message => message.Contains(expected)),
        "Bootstrap diagnostics omitted the failing type or version check.");
    if (scenario.StartsWith("bad-runtime", StringComparison.Ordinal))
    {
        Require(listener.Messages.Any(message => message.Contains("7.0.1") &&
            message.Contains("Upgrade all SqlClient family packages together")),
            "Exact loaded family version mismatch was not diagnosed.");
    }
}
else
{
    using var listener = new AuthenticationTraceListener();
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)
        is ActiveDirectoryAuthenticationProvider, "First access did not discover Azure.");
    Require(TestInitializer.Registered, "Initializer registration failed.");
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
        is TestProvider, "Azure replaced the initializer.");
    Require(SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive)
        is TestProvider, "Azure replaced the config provider.");
    Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
        new TestProvider()), "User registration replaced the Config-tier initializer.");
    Require(!SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive,
        new TestProvider()), "User registration replaced the config provider.");
    Require(listener.Messages.Any(message => message.Contains("ActiveDirectoryInteractive") &&
        message.Contains("update app.config or its initializer")),
        "Configuration precedence refusal did not explain how to change the provider.");
}
var user = new TestProvider();
if (!scenario.StartsWith("bad-", StringComparison.Ordinal))
{
    Require(SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault, user),
        "User registration did not replace Default.");
    Require(ReferenceEquals(user,
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)),
        "User registration did not round-trip.");
}
Console.WriteLine("PASS: " + scenario);

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

/// <summary>Captures authentication trace diagnostics, including cached bootstrap failures.</summary>
internal sealed class AuthenticationTraceListener : EventListener
{
    internal ConcurrentQueue<string> Messages { get; } = new();

    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
        {
            EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)2);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId == 3 && eventData.Payload?[0] is string message)
        {
            Messages.Enqueue(message);
        }
    }
}

/// <summary>Registers a provider through the reentrant configuration initializer path.</summary>
public sealed class TestInitializer : SqlAuthenticationInitializer
{
    public static bool Registered;
    /// <inheritdoc />
    public override void Initialize() =>
        Registered = SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new TestProvider());
}

/// <summary>Supports registration without contacting an identity service.</summary>
public class TestProvider : SqlAuthenticationProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => true;
    /// <inheritdoc />
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters) =>
        throw new NotSupportedException();
}

/// <summary>Exercises configuration bootstrap failures for unsupported methods.</summary>
public sealed class UnsupportedProvider : TestProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => false;
}
