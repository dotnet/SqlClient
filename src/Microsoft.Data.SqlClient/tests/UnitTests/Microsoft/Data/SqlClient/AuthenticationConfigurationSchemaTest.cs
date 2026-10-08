// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Pins the internal section names and schema consumed by existing app.config files.</summary>
public class AuthenticationConfigurationSchemaTest
{
    /// <summary>Both original handlers stay in SqlClient and retain every supported property.</summary>
    [Theory]
    [InlineData(typeof(SqlAuthenticationProviderConfigurationSection), "SqlAuthenticationProviders")]
    [InlineData(typeof(SqlClientAuthenticationProviderConfigurationSection), "SqlClientAuthenticationProviders")]
    public void HandlerContractIsPreserved(Type type, string sectionName)
    {
        Assert.Equal("Microsoft.Data.SqlClient", type.Assembly.GetName().Name);
        Assert.Equal("Microsoft.Data.SqlClient." + sectionName.Replace("Providers", "ProviderConfigurationSection"),
            type.FullName);
        var section = Assert.IsAssignableFrom<ConfigurationSection>(Activator.CreateInstance(type));
        Assert.Equal(new[] { "applicationClientId", "initializerType", "providers", "useWamBroker" },
            section.ElementInformation.Properties.Cast<PropertyInformation>()
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.IsType<ProviderSettingsCollection>(section.ElementInformation.Properties["providers"].Value);
        Assert.Equal(string.Empty, section.ElementInformation.Properties["applicationClientId"].Value);
        Assert.Equal(string.Empty, section.ElementInformation.Properties["useWamBroker"].Value);
    }

    /// <summary>The relocated initializer is type-forwarded, not duplicated in the driver.</summary>
    [Fact]
    public void InitializerIsForwarded()
    {
        Assert.Equal("Microsoft.Data.SqlClient.Extensions.Abstractions",
            typeof(SqlAuthenticationInitializer).Assembly.GetName().Name);
        Assert.Same(typeof(SqlAuthenticationInitializer),
            typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.SqlAuthenticationInitializer"));
    }
}
