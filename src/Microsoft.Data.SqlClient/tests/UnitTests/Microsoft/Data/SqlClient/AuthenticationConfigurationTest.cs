// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies real configuration files activate forwarded handlers and preserve reader behavior.</summary>
public class AuthenticationConfigurationTest
{
    /// <summary>Both driver-qualified declarations load the sealed handler and retain every configured value.</summary>
    [Theory]
    [InlineData("SqlAuthenticationProviders", typeof(SqlAuthenticationProviderConfigurationSection))]
    [InlineData("SqlClientAuthenticationProviders", typeof(SqlClientAuthenticationProviderConfigurationSection))]
    public void DriverQualifiedSectionPreservesValues(string sectionName, Type handlerType)
    {
        WithConfiguration(
            $"<section name='{sectionName}' type='{handlerType.FullName}, Microsoft.Data.SqlClient' />",
            $@"<{sectionName} applicationClientId='configured-client' initializerType='Example.Initializer, Example' useWamBroker='false'>
                <providers>
                  <add name='active directory default' type='Example.Provider, Example' customOption='configured-value' />
                </providers>
              </{sectionName}>",
            configuration =>
            {
                object section = configuration.GetSection(sectionName);
                Assert.Equal(handlerType, section.GetType());
                Assert.Equal("Microsoft.Data.SqlClient.Extensions.Abstractions", section.GetType().Assembly.GetName().Name);

                var settings = SqlAuthenticationProviderManager.ReadConfigurationSection(configuration.GetSection);
                Assert.NotNull(settings);
                Assert.Equal("configured-client", settings.ApplicationClientId);
                Assert.Equal("Example.Initializer, Example", settings.InitializerType);
                Assert.Equal("false", settings.UseWamBroker);
                var provider = Assert.Single(settings.Providers);
                var providerSettings = Assert.IsType<ProviderSettings>(provider);
                Assert.Equal("active directory default", providerSettings.Name);
                Assert.Equal("Example.Provider, Example", providerSettings.Type);
                Assert.Equal("configured-value", providerSettings.Parameters["customOption"]);
            });
    }

    /// <summary>Empty real sections preserve empty strings and an empty provider collection.</summary>
    [Theory]
    [InlineData("SqlAuthenticationProviders", typeof(SqlAuthenticationProviderConfigurationSection))]
    [InlineData("SqlClientAuthenticationProviders", typeof(SqlClientAuthenticationProviderConfigurationSection))]
    public void DriverQualifiedSectionPreservesDefaults(string sectionName, Type handlerType)
    {
        WithConfiguration(
            $"<section name='{sectionName}' type='{handlerType.FullName}, Microsoft.Data.SqlClient' />",
            $"<{sectionName} />",
            configuration =>
            {
                var settings = SqlAuthenticationProviderManager.ReadConfigurationSection(configuration.GetSection);
                Assert.NotNull(settings);
                Assert.Equal(string.Empty, settings.ApplicationClientId);
                Assert.Equal(string.Empty, settings.InitializerType);
                Assert.Equal(string.Empty, settings.UseWamBroker);
                Assert.Empty(settings.Providers);
            });
    }

    /// <summary>The modern section takes precedence without reading the legacy section.</summary>
    [Fact]
    public void ModernSectionTakesPrecedence()
    {
        WithConfiguration(
            @"<section name='SqlAuthenticationProviders' type='Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient' />
              <section name='SqlClientAuthenticationProviders' type='Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient' />",
            @"<SqlAuthenticationProviders applicationClientId='legacy-client' />
              <SqlClientAuthenticationProviders applicationClientId='modern-client' />",
            configuration =>
            {
                List<string> reads = new();
                var settings = SqlAuthenticationProviderManager.ReadConfigurationSection(name =>
                {
                    reads.Add(name);
                    return configuration.GetSection(name);
                });
                Assert.NotNull(settings);
                Assert.Equal("modern-client", settings.ApplicationClientId);
                Assert.Equal(new[] { "SqlClientAuthenticationProviders" }, reads);
            });
    }

    /// <summary>When neither section exists, the reader checks both names and returns no settings.</summary>
    [Fact]
    public void MissingSectionsReturnNull()
    {
        WithConfiguration(string.Empty, string.Empty, configuration =>
        {
            List<string> reads = new();
            var settings = SqlAuthenticationProviderManager.ReadConfigurationSection(name =>
            {
                reads.Add(name);
                return configuration.GetSection(name);
            });
            Assert.Null(settings);
            Assert.Equal(new[] { "SqlClientAuthenticationProviders", "SqlAuthenticationProviders" }, reads);
        });
    }

    /// <summary>A mismatched modern handler is ignored rather than accepted solely because its schema matches.</summary>
    [Fact]
    public void WrongModernHandlerFallsBackToLegacy()
    {
        WithConfiguration(
            @"<section name='SqlAuthenticationProviders' type='Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient' />
              <section name='SqlClientAuthenticationProviders' type='Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient' />",
            @"<SqlAuthenticationProviders applicationClientId='legacy-client' />
              <SqlClientAuthenticationProviders applicationClientId='wrong-handler' />",
            configuration =>
            {
                var settings = SqlAuthenticationProviderManager.ReadConfigurationSection(configuration.GetSection);
                Assert.NotNull(settings);
                Assert.Equal("legacy-client", settings.ApplicationClientId);
            });
    }

    /// <summary>A malformed modern section still fails parsing instead of silently falling back.</summary>
    [Fact]
    public void MalformedModernSectionDoesNotFallBack()
    {
        WithConfiguration(
            @"<section name='SqlAuthenticationProviders' type='Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient' />
              <section name='SqlClientAuthenticationProviders' type='Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection, Microsoft.Data.SqlClient' />",
            @"<SqlAuthenticationProviders applicationClientId='legacy-client' />
              <SqlClientAuthenticationProviders unknownAttribute='invalid' />",
            configuration => Assert.Throws<ConfigurationErrorsException>(() =>
                SqlAuthenticationProviderManager.ReadConfigurationSection(configuration.GetSection)));
    }

    /// <summary>Loads an isolated configuration file and removes it even when an assertion fails.</summary>
    /// <param name="declarations">Configuration section declarations.</param>
    /// <param name="sections">Configuration section values.</param>
    /// <param name="assertConfiguration">Assertions executed while the file exists.</param>
    private static void WithConfiguration(string declarations, string sections, Action<Configuration> assertConfiguration)
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, $"<configuration><configSections>{declarations}</configSections>{sections}</configuration>");
            var fileMap = new ExeConfigurationFileMap { ExeConfigFilename = path };
            Configuration configuration = ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
            assertConfiguration(configuration);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
