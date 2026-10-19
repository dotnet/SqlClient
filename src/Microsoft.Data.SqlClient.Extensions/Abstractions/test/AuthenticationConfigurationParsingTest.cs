// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Verifies configuration string metadata and the authentication-method allowlist directly.
/// </summary>
public class AuthenticationConfigurationParsingTest
{
    /// <summary>
    /// A caller-owned default configuration preserves provider defaults without loading app.config.
    /// </summary>
    [Fact]
    public void DefaultConstructorCreatesEmptyConfiguration()
    {
        var configuration = new SqlAuthenticationConfiguration();

        Assert.Null(configuration.ApplicationClientId);
        Assert.Null(configuration.UseWamBroker);
        Assert.Equal(string.Empty, configuration.InitializerType);
        Assert.Empty(configuration.Providers);
    }

    /// <summary>
    /// Both handlers expose their optional strings without normalization or substitution.
    /// </summary>
    [Theory]
    [InlineData("applicationClientId")]
    [InlineData("useWamBroker")]
    [InlineData("initializerType")]
    public void ReadStringPreservesConfiguredValue(string name)
    {
        foreach (var section in new SqlAuthenticationProviderConfigurationSection[]
        {
            new SqlAuthenticationProviderConfigurationSection(),
            new SqlClientAuthenticationProviderConfigurationSection()
        })
        {
            Assert.Equal(string.Empty, SqlAuthenticationProviderRegistry.ReadString(section, name));
            section.ElementInformation.Properties[name].Value = " configured value ";
            Assert.Equal(" configured value ",
                SqlAuthenticationProviderRegistry.ReadString(section, name));
        }
    }

    /// <summary>
    /// Null string settings remain optional when configuration normalizes them to empty.
    /// </summary>
    [Fact]
    public void ReadStringNormalizesNull()
    {
        var section = new NullableStringSection();
        section.ElementInformation.Properties["value"].Value = null;

        Assert.Equal(string.Empty, SqlAuthenticationProviderRegistry.ReadString(section, "value"));
    }

    /// <summary>
    /// Missing properties and non-string metadata produce actionable schema failures.
    /// </summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("number")]
    [InlineData("nullableNumber")]
    public void ReadStringRejectsInvalidSchema(string name)
    {
        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            SqlAuthenticationProviderRegistry.ReadString(new NullableStringSection(), name));

        Assert.Contains(name, failure.Message);
    }

    /// <summary>
    /// All supported configuration names map case-insensitively to their exact enum value.
    /// </summary>
    [Theory]
    [InlineData("active directory integrated", SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
    #pragma warning disable CS0618 // Type or member is obsolete
    [InlineData("active directory password", SqlAuthenticationMethod.ActiveDirectoryPassword)]
    #pragma warning restore CS0618 // Type or member is obsolete
    [InlineData("active directory interactive", SqlAuthenticationMethod.ActiveDirectoryInteractive)]
    [InlineData("active directory service principal",
        SqlAuthenticationMethod.ActiveDirectoryServicePrincipal)]
    [InlineData("active directory device code flow",
        SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
    [InlineData("active directory managed identity",
        SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
    [InlineData("active directory msi", SqlAuthenticationMethod.ActiveDirectoryMSI)]
    [InlineData("active directory default", SqlAuthenticationMethod.ActiveDirectoryDefault)]
    [InlineData("active directory workload identity",
        SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
    public void AuthenticationEnumFromStringRecognizesAllowedNames(
        string name, SqlAuthenticationMethod expected)
    {
        Assert.Equal(expected,
            SqlAuthenticationProviderRegistry.AuthenticationEnumFromString(name));
        Assert.Equal(expected, SqlAuthenticationProviderRegistry.AuthenticationEnumFromString(
            name.ToUpperInvariant()));
    }

    /// <summary>
    /// Numbers, enum identifiers and unrelated methods cannot bypass the explicit allowlist.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("1")]
    [InlineData("NotSpecified")]
    [InlineData("SqlPassword")]
    [InlineData("ActiveDirectoryIntegrated")]
    [InlineData("active directory integrated ")]
    [InlineData("unknown")]
    public void AuthenticationEnumFromStringRejectsOtherNames(string name)
    {
        var failure = Assert.Throws<NotSupportedException>(() =>
            SqlAuthenticationProviderRegistry.AuthenticationEnumFromString(name));

        Assert.Contains(name, failure.Message);
    }

    /// <summary>
    /// Supplies explicit null defaults and incompatible metadata for string-reader tests.
    /// </summary>
    private sealed class NullableStringSection : ConfigurationSection
    {
        [ConfigurationProperty("value", DefaultValue = null)]
        public string? Value => (string?)this["value"];

        [ConfigurationProperty("number", DefaultValue = 1)]
        public int Number => (int)this["number"];

        [ConfigurationProperty("nullableNumber", DefaultValue = null)]
        public int? NullableNumber => (int?)this["nullableNumber"];
    }
}
