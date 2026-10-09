// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using System.Configuration;

string scenario = args.Single();
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
    "The driver's typed section retrieval did not load the configured provider.");
Require(TestInitializer.Initialized, "The configured driver initializer did not run.");
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

/// <summary>Proves initializer activation still uses the driver-owned type in this intermediate layer.</summary>
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
