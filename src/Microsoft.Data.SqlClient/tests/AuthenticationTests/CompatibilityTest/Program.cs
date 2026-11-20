// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.Data.SqlClient;

string scenario = args.Single();
TestInitializer.RegisterProviders = scenario == "initializer-registration";
Console.WriteLine("DRIVER:" + typeof(SqlConnection).Assembly.FullName);

if (scenario.StartsWith("bad-", StringComparison.Ordinal))
{
    Observe("failed-method-unavailable", () =>
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault) is null);
    var recovery = new TestProvider();
    Observe("explicit-recovery", () => SqlAuthenticationProvider.SetProvider(
        SqlAuthenticationMethod.ActiveDirectoryDefault, recovery));
    Observe("recovery-round-trip", () => ReferenceEquals(recovery,
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)));
}
else if (scenario == "initializer-registration")
{
    Observe("initializer-provider", () =>
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
            is InitializerProvider);
    Observe("initializer-ran", () => TestInitializer.Initialized);
    Observe("application-registration", () => SqlAuthenticationProvider.SetProvider(
        SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new TestProvider()));
}
else if (scenario is "configured" or "legacy" or "application-registration")
{
    Observe("azure-default", () =>
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)?
            .GetType().FullName == "Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider");
    if (scenario != "application-registration")
    {
        Observe("initializer-ran", () => TestInitializer.Initialized);
        Observe("configured-provider", () =>
            SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive)
                is TestProvider);
        Observe("configuration-precedence", () => !SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryInteractive, new TestProvider()));
    }
    var application = new TestProvider();
    Observe("application-registration", () => SqlAuthenticationProvider.SetProvider(
        SqlAuthenticationMethod.ActiveDirectoryDefault, application));
    Observe("application-round-trip", () => ReferenceEquals(application,
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)));
    var replacement = new TestProvider();
    Observe("application-replacement", () => SqlAuthenticationProvider.SetProvider(
        SqlAuthenticationMethod.ActiveDirectoryDefault, replacement));
    Observe("replacement-round-trip", () => ReferenceEquals(replacement,
        SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryDefault)));
}
else
{
    throw new ArgumentException("Unknown compatibility scenario: " + scenario);
}

/// <summary>Records public results or exception chains without normalizing version differences.</summary>
/// <param name="name">Identifies the operation in the comparison report.</param>
/// <param name="operation">Exercises the public contract in the scenario's isolated process.</param>
static void Observe(string name, Func<bool> operation)
{
    try
    {
        Console.WriteLine("OBSERVATION:" + JsonSerializer.Serialize(new
        {
            Name = name,
            Outcome = "Return",
            Value = operation().ToString()
        }));
    }
    catch (Exception error)
    {
        var types = new List<string>();
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            types.Add(current.GetType().FullName!);
        }
        Console.WriteLine("OBSERVATION:" + JsonSerializer.Serialize(new
        {
            Name = name,
            Outcome = "Exception",
            Value = string.Join(" -> ", types)
        }));
        Console.Error.WriteLine(error);
    }
}

/// <summary>Exercises initializer activation and optionally reentrant public registration.</summary>
public sealed class TestInitializer : SqlAuthenticationInitializer
{
    public static bool Initialized;
    public static bool RegisterProviders;

    /// <inheritdoc />
    public override void Initialize()
    {
        Initialized = true;
        if (RegisterProviders && !SqlAuthenticationProvider.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow, new InitializerProvider()))
        {
            throw new InvalidOperationException("Initializer registration was refused.");
        }
    }
}

/// <summary>Provides a local authentication contract without contacting an identity service.</summary>
public class TestProvider : SqlAuthenticationProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => true;

    /// <inheritdoc />
    public override Task<SqlAuthenticationToken> AcquireTokenAsync(
        SqlAuthenticationParameters parameters) => throw new NotSupportedException();
}

/// <summary>Identifies a provider installed by a configured initializer.</summary>
public sealed class InitializerProvider : TestProvider { }

/// <summary>Exercises invalid provider configuration through the same public APIs.</summary>
public sealed class UnsupportedProvider : TestProvider
{
    /// <inheritdoc />
    public override bool IsSupported(SqlAuthenticationMethod method) => false;
}
