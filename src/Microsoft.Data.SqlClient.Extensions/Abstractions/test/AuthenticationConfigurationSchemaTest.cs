// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Configuration;

// The section class names are long and confusingly similar, so we alias them for clarity.
using LegacySection = Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection;
using ModernSection = Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Pins authentication section names, configuration properties and defaults in Abstractions.</summary>
public class AuthenticationConfigurationSchemaTest
{
    /// <summary>Both infrastructure-only handlers preserve their schema and remain hidden from IntelliSense.</summary>
    [Theory]
    [InlineData(typeof(LegacySection), "SqlAuthenticationProviders")]
    [InlineData(typeof(ModernSection), "SqlClientAuthenticationProviders")]
    public void HandlerContractIsPreserved(Type type, string sectionName)
    {
        Assert.True(type.IsSealed);
        Assert.Equal(typeof(ConfigurationSection), type.BaseType);
        var editorBrowsable = Assert.IsType<EditorBrowsableAttribute>(
            Assert.Single(type.GetCustomAttributes(typeof(EditorBrowsableAttribute), inherit: false)));
        Assert.Equal(EditorBrowsableState.Never, editorBrowsable.State);
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

#if NETFRAMEWORK
    /// <summary>The net462 handlers use framework configuration without adding ConfigurationManager.</summary>
    [Fact]
    public void HandlersUseFrameworkConfiguration()
    {
        Assert.Equal("System.Configuration", typeof(ConfigurationSection).Assembly.GetName().Name);
        Assert.DoesNotContain(
            typeof(SqlAuthenticationProviderConfigurationSection).Assembly.GetReferencedAssemblies(),
            assembly => assembly.Name == "System.Configuration.ConfigurationManager");
    }
#endif
}
