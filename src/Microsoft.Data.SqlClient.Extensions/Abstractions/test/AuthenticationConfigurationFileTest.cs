// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Exercises the loader with real files declaring the Abstractions-owned handlers.</summary>
public class AuthenticationConfigurationFileTest
{
    private const string Modern = SqlClientAuthenticationProviderConfigurationSection.Name;
    private const string Legacy = SqlAuthenticationProviderConfigurationSection.Name;
    private const string Declarations = """
        <section name="SqlClientAuthenticationProviders"
                 type="Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient.Extensions.Abstractions" />
        <section name="SqlAuthenticationProviders"
                 type="Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient.Extensions.Abstractions" />
        """;

    /// <summary>Both handlers reach the loader without activating configured types or loading the driver.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HandlerLoadsSettings(bool modern)
    {
        string name = modern ? Modern : Legacy;
        // Declare only the selected handler: a declared but absent modern section still takes precedence.
        string handler = modern
            ? nameof(SqlClientAuthenticationProviderConfigurationSection)
            : nameof(SqlAuthenticationProviderConfigurationSection);
        WithConfiguration(
            $"<section name='{name}' type='Microsoft.Data.SqlClient.{handler}, Microsoft.Data.SqlClient.Extensions.Abstractions' />",
            $"""
            <{name} applicationClientId="configured-client" initializerType="Missing.Initializer" useWamBroker="false">
              <providers><add name="active directory default" type="Missing.Provider" /></providers>
            </{name}>
            """,
            configuration =>
            {
                var settings = SqlAuthenticationConfiguration.Load(configuration.GetSection);
                Assert.Equal("configured-client", settings.ApplicationClientId);
                Assert.False(settings.UseWamBroker);
                Assert.Equal("Missing.Initializer", settings.InitializerType);
                Assert.Equal("Missing.Provider", Assert.Single(settings.Providers).Type);
            });
    }

    /// <summary>Invalid broker values retain other settings from either real handler, as on main.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidBrokerValuePreservesConfiguredSettings(bool modern)
    {
        string name = modern ? Modern : Legacy;
        string handler = modern
            ? nameof(SqlClientAuthenticationProviderConfigurationSection)
            : nameof(SqlAuthenticationProviderConfigurationSection);
        WithConfiguration(
            $"<section name='{name}' type='Microsoft.Data.SqlClient.{handler}, Microsoft.Data.SqlClient.Extensions.Abstractions' />",
            $"""
            <{name} applicationClientId="configured-client" initializerType="Missing.Initializer" useWamBroker="invalid">
              <providers><add name="active directory default" type="Missing.Provider" /></providers>
            </{name}>
            """,
            configuration =>
            {
                var settings = SqlAuthenticationConfiguration.Load(configuration.GetSection);
                Assert.Equal("configured-client", settings.ApplicationClientId);
                Assert.Null(settings.UseWamBroker);
                Assert.Equal("Missing.Initializer", settings.InitializerType);
                Assert.Equal("Missing.Provider", Assert.Single(settings.Providers).Type);
            });
    }

    /// <summary>A declared modern section without an element prevents reading populated legacy settings.</summary>
    [Fact]
    public void DeclaredModernSectionWithoutElementTakesPrecedence()
    {
        WithConfiguration(Declarations,
            $"""<{Legacy} applicationClientId="legacy-client" />""",
            configuration =>
            {
                var reads = new List<string>();
                var settings = SqlAuthenticationConfiguration.Load(name =>
                {
                    reads.Add(name);
                    return configuration.GetSection(name);
                });

                Assert.Equal(new[] { Modern }, reads);
                Assert.Null(settings.ApplicationClientId);
                Assert.Null(settings.UseWamBroker);
                Assert.Empty(settings.InitializerType);
                Assert.Empty(settings.Providers);
            });
    }

    /// <summary>Malformed real configuration yields empty settings without fallback, as on main.</summary>
    [Fact]
    public void MalformedModernSectionReturnsEmptySettings()
    {
        WithConfiguration(Declarations,
            $"""
            <{Modern} unknownAttribute="invalid" />
            <{Legacy} applicationClientId="legacy-client" />
            """,
            configuration =>
            {
                var reads = new List<string>();
                SqlAuthenticationConfiguration Load() => SqlAuthenticationConfiguration.Load(name =>
                {
                    reads.Add(name);
                    return configuration.GetSection(name);
                });

                var settings = Load();
                Assert.Null(settings.ApplicationClientId);
                Assert.Null(settings.UseWamBroker);
                Assert.Empty(settings.InitializerType);
                Assert.Empty(settings.Providers);
                Assert.Equal(new[] { Modern }, reads);
            });
    }

    /// <summary>Creates and deletes an isolated configuration file without changing process configuration.</summary>
    /// <param name="declarations">Handler declarations in configSections.</param>
    /// <param name="sections">Configuration section elements.</param>
    /// <param name="assertConfiguration">Assertions made while the file exists.</param>
    private static void WithConfiguration(
        string declarations, string sections, Action<Configuration> assertConfiguration)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                $"<configuration><configSections>{declarations}</configSections>{sections}</configuration>");
            var map = new ExeConfigurationFileMap { ExeConfigFilename = path };
            var configuration = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
            assertConfiguration(configuration);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
