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

    /// <summary>Absent section elements require no authentication overrides or provider entries.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingSectionsUseDefaults(bool declareSections)
    {
        WithConfiguration(declareSections ? Declarations : string.Empty, string.Empty,
            configuration =>
            {
                var settings = SqlAuthenticationConfiguration.Load(configuration.GetSection);
                Assert.Null(settings.ApplicationClientId);
                Assert.Null(settings.UseWamBroker);
                Assert.Empty(settings.InitializerType);
                Assert.Empty(settings.Providers);
            });
    }

    /// <summary>Empty and partial sections retain defaults for every omitted optional setting.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OptionalAttributesUseDefaults(bool modern, bool partial)
    {
        string name = modern ? Modern : Legacy;
        string handler = modern
            ? nameof(SqlClientAuthenticationProviderConfigurationSection)
            : nameof(SqlAuthenticationProviderConfigurationSection);
        string attributes = partial ? "applicationClientId='configured-client'" : string.Empty;

        WithConfiguration(
            $"<section name='{name}' type='Microsoft.Data.SqlClient.{handler}, Microsoft.Data.SqlClient.Extensions.Abstractions' />",
            $"<{name} {attributes} />",
            configuration =>
            {
                var settings = SqlAuthenticationConfiguration.Load(configuration.GetSection);
                Assert.Equal(partial ? "configured-client" : null, settings.ApplicationClientId);
                Assert.Null(settings.UseWamBroker);
                Assert.Empty(settings.InitializerType);
                Assert.Empty(settings.Providers);
            });
    }

    /// <summary>Both handlers reach the loader without activating configured types or loading the driver.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void HandlerLoadsSettings(bool modern, bool configureBroker)
    {
        string name = modern ? Modern : Legacy;
        // Declare only the selected handler: a declared but absent modern section still takes precedence.
        string handler = modern
            ? nameof(SqlClientAuthenticationProviderConfigurationSection)
            : nameof(SqlAuthenticationProviderConfigurationSection);
        string brokerAttribute = configureBroker ? "useWamBroker=\"false\"" : string.Empty;
        bool? expectedUseWamBroker = configureBroker
            ? false : new SqlAuthenticationConfiguration().UseWamBroker;
        WithConfiguration(
            $"<section name='{name}' type='Microsoft.Data.SqlClient.{handler}, Microsoft.Data.SqlClient.Extensions.Abstractions' />",
            $"""
            <{name} applicationClientId="configured-client" initializerType="Missing.Initializer" {brokerAttribute}>
              <providers><add name="active directory default" type="Missing.Provider" /></providers>
            </{name}>
            """,
            configuration =>
            {
                var settings = SqlAuthenticationConfiguration.Load(configuration.GetSection);
                Assert.Equal("configured-client", settings.ApplicationClientId);
                Assert.Equal(expectedUseWamBroker, settings.UseWamBroker);
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
                Assert.Equal(new SqlAuthenticationConfiguration().UseWamBroker, settings.UseWamBroker);
                Assert.Equal("Missing.Initializer", settings.InitializerType);
                Assert.Equal("Missing.Provider", Assert.Single(settings.Providers).Type);
            });
    }

    /// <summary>A declared modern section without an element prevents reading populated legacy settings.</summary>
    [Fact]
    public void ModernSectionDoesNotLoadFromLegacyConfigSection()
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
