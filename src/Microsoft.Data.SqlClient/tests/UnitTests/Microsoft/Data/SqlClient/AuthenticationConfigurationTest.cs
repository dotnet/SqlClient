// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Configuration;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.PreLogin;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies real configuration files activate driver-forwarded handlers and preserve metadata.</summary>
[Collection(AppContextSwitchTestCollection.Name)]
public class AuthenticationConfigurationTest
{
    /// <summary>Open and OpenAsync report missing providers as SqlException, including after cached bootstrap failure.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ConnectionErrorDirectsUsersToRegistrationAndTraces(bool cachedFailure, bool openAsync)
    {
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryDefault;
        SqlAuthenticationProvider.GetProvider(method);
        Type registryType = typeof(SqlAuthenticationProvider).Assembly.GetType(
            "Microsoft.Data.SqlClient.SqlAuthenticationProviderRegistry", throwOnError: true)!;
        object registry = registryType.GetField("Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        FieldInfo failureField = registryType.GetField("_bootstrapFailure", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var providers = Assert.IsAssignableFrom<IDictionary>(
            registryType.GetField("_providers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry));
        object? originalFailure = failureField.GetValue(registry);
        object? originalProvider = providers[method];
        providers.Remove(method);
        failureField.SetValue(registry, cachedFailure
            ? ExceptionDispatchInfo.Capture(new ArgumentException("Test configured initializer failure"))
            : null);
        try
        {
            using var server = new TdsServer(new TdsServerArguments
            {
                FedAuthRequiredPreLoginOption = TdsPreLoginFedAuthRequiredOption.FedAuthRequired,
            });
            server.Start();
            var builder = new SqlConnectionStringBuilder
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Authentication = method,
                Encrypt = SqlConnectionEncryptOption.Optional,
                Pooling = false,
                ConnectTimeout = 5,
                ConnectRetryCount = 0,
            };
            using var connection = new SqlConnection(builder.ConnectionString);

            SqlException failure = openAsync
                ? await Assert.ThrowsAsync<SqlException>(() => connection.OpenAsync())
                : Assert.Throws<SqlException>(() => connection.Open());
            Assert.Contains("SetProvider", failure.Message);
            Assert.Contains("Microsoft.Data.SqlClient.EventSource", failure.Message);
            Assert.Contains("compatible", failure.Message);
            Assert.Null(failure.InnerException);
            var error = Assert.IsType<SqlError>(Assert.Single(failure.Errors));
            Assert.Equal(builder.DataSource, error.Server);
            Assert.NotEqual(Guid.Empty, failure.ClientConnectionId);
        }
        finally
        {
            failureField.SetValue(registry, originalFailure);
            if (originalProvider is null)
            {
                providers.Remove(method);
            }
            else
            {
                providers[method] = originalProvider;
            }
        }
    }

    /// <summary>The driver-forwarded registry honors disabled authentication configuration on both runtimes.</summary>
    [Fact]
    public void DisabledAppConfigDoesNotReadAuthenticationConfiguration()
    {
        Assembly abstractions = typeof(SqlAuthenticationProvider).Assembly;
        Type switches = abstractions.GetType("Microsoft.Data.SqlClient.AuthenticationFeatureSwitches", throwOnError: true)!;
        Type registryType = abstractions.GetType("Microsoft.Data.SqlClient.SqlAuthenticationProviderRegistry", throwOnError: true)!;
        FieldInfo configSwitch = switches.GetField("s_enableAppConfig", BindingFlags.Static | BindingFlags.NonPublic)!;
        FieldInfo discoverySwitch = switches.GetField("s_enableAzureExtensionDiscovery", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? originalConfig = configSwitch.GetValue(null);
        object? originalDiscovery = discoverySwitch.GetValue(null);
        try
        {
            configSwitch.SetValue(null, Enum.Parse(configSwitch.FieldType, "False"));
            discoverySwitch.SetValue(null, Enum.Parse(discoverySwitch.FieldType, "False"));
            object registry = Activator.CreateInstance(registryType, nonPublic: true)!;
            MethodInfo bootstrap = registryType.GetMethod(
                "EnsureBootstrapped", BindingFlags.Instance | BindingFlags.NonPublic)!;
            int reads = 0;
            Func<string, object> reader = _ =>
            {
                reads++;
                throw new InvalidOperationException("Disabled authentication configuration must not be read.");
            };

            bootstrap.Invoke(registry, new object?[] { reader, null });

            Assert.Equal(0, reads);
        }
        finally
        {
            configSwitch.SetValue(null, originalConfig);
            discoverySwitch.SetValue(null, originalDiscovery);
        }
    }

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
                var section = Assert.IsAssignableFrom<ConfigurationSection>(
                    configuration.GetSection(sectionName));
                Assert.Equal(handlerType, section.GetType());
                Assert.Equal("Microsoft.Data.SqlClient.Extensions.Abstractions", section.GetType().Assembly.GetName().Name);

                Assert.Equal("configured-client", section.ElementInformation.Properties["applicationClientId"].Value);
                Assert.Equal("Example.Initializer, Example", section.ElementInformation.Properties["initializerType"].Value);
                Assert.Equal("false", section.ElementInformation.Properties["useWamBroker"].Value);
                var providers = Assert.IsType<ProviderSettingsCollection>(
                    section.ElementInformation.Properties["providers"].Value);
                var provider = Assert.Single(providers);
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
                var section = Assert.IsAssignableFrom<ConfigurationSection>(
                    configuration.GetSection(sectionName));
                Assert.Equal(string.Empty, section.ElementInformation.Properties["applicationClientId"].Value);
                Assert.Equal(string.Empty, section.ElementInformation.Properties["initializerType"].Value);
                Assert.Equal(string.Empty, section.ElementInformation.Properties["useWamBroker"].Value);
                Assert.Empty(Assert.IsType<ProviderSettingsCollection>(
                    section.ElementInformation.Properties["providers"].Value));
            });
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
