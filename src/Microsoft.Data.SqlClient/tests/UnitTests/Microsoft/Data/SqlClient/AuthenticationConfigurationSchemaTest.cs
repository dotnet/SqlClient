// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Pins section identity and schema for existing assembly-qualified app.config declarations.</summary>
public class AuthenticationConfigurationSchemaTest
{
    /// <summary>Both public handlers preserve their original names, configuration properties and empty defaults.</summary>
    [Theory]
    [InlineData("SqlAuthenticationProviderConfigurationSection", "SqlAuthenticationProviders")]
    [InlineData("SqlClientAuthenticationProviderConfigurationSection", "SqlClientAuthenticationProviders")]
    public void HandlerContractIsPreserved(string typeName, string sectionName)
    {
        Type type = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient." + typeName, throwOnError: true)!;
        Assert.Equal("Microsoft.Data.SqlClient.Extensions.Abstractions", type.Assembly.GetName().Name);
        Assert.True(type.IsPublic);
        Assert.Same(type, Type.GetType(type.FullName + ", Microsoft.Data.SqlClient", throwOnError: true));
        var section = Assert.IsAssignableFrom<ConfigurationSection>(Activator.CreateInstance(type));
        Assert.Equal(new[] { "applicationClientId", "initializerType", "providers", "useWamBroker" },
            section.ElementInformation.Properties.Cast<PropertyInformation>()
                .Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.IsType<ProviderSettingsCollection>(section.ElementInformation.Properties["providers"].Value);
        Assert.Equal(sectionName, type.GetField("Name")!.GetRawConstantValue());
        Assert.Equal(string.Empty, section.ElementInformation.Properties["initializerType"].Value);
        Assert.Equal(string.Empty, section.ElementInformation.Properties["applicationClientId"].Value);
        Assert.Equal(string.Empty, section.ElementInformation.Properties["useWamBroker"].Value);
    }
}
