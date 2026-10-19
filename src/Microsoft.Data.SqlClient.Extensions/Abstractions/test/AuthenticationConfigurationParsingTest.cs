// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

// The section class names are long and confusingly similar, so we alias them for clarity.
using LegacySection = Microsoft.Data.SqlClient.SqlAuthenticationProviderConfigurationSection;
using ModernSection = Microsoft.Data.SqlClient.SqlClientAuthenticationProviderConfigurationSection;

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
        // Both forwarded handlers must expose the same string contract without trimming values.
        foreach (var section in new LegacySection[]
        {
            new LegacySection(),
            new ModernSection()
        })
        {
            Assert.Equal(string.Empty, SqlAuthenticationConfiguration.ReadString(section, name));
            section.ElementInformation.Properties[name].Value = " configured value ";
            Assert.Equal(" configured value ",
                SqlAuthenticationConfiguration.ReadString(section, name));
        }
    }

    /// <summary>
    /// Null string settings remain optional when configuration normalizes them to empty.
    /// </summary>
    [Fact]
    public void ReadStringNormalizesNull()
    {
        // ConfigurationManager can normalize a null optional string before ReadString sees it.
        var section = new NullableStringSection();
        section.ElementInformation.Properties["value"].Value = null;

        Assert.Equal(string.Empty, SqlAuthenticationConfiguration.ReadString(section, "value"));
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
        // A missing or mistyped property is a schema error, not an absent optional override.
        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            SqlAuthenticationConfiguration.ReadString(new NullableStringSection(), name));

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
        // Check every allowlisted spelling in both cases rather than relying on enum parsing.
        Assert.Equal(expected,
            SqlAuthenticationConfiguration.AuthenticationEnumFromString(name));
        Assert.Equal(expected, SqlAuthenticationConfiguration.AuthenticationEnumFromString(
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
        // Reject numeric and CLR enum spellings even when they could identify a valid enum value.
        var failure = Assert.Throws<NotSupportedException>(() =>
            SqlAuthenticationConfiguration.AuthenticationEnumFromString(name));

        Assert.Contains(name, failure.Message);
    }

    /// <summary>
    /// Settings are captured before mutable ConfigurationSection objects can change.
    /// </summary>
    [Fact]
    public void ParsedValuesAreIndependentOfSectionChanges()
    {
        // Take a snapshot of all four settings before simulating later configuration edits.
        var section = new ModernSection();
        section.ElementInformation.Properties["applicationClientId"].Value = "original-client";
        section.ElementInformation.Properties["useWamBroker"].Value = "true";
        section.ElementInformation.Properties["initializerType"].Value = "Original.Initializer";
        var provider = new ProviderSettings("Active Directory Integrated", "Original.Provider");
        section.Providers.Add(provider);
        var configuration = new SqlAuthenticationConfiguration(section);

        // Provider entries must be copied as well as the collection that contains them.
        section.ElementInformation.Properties["applicationClientId"].Value = "changed-client";
        section.ElementInformation.Properties["useWamBroker"].Value = "false";
        section.ElementInformation.Properties["initializerType"].Value = "Changed.Initializer";
        provider.Type = "Changed.Provider";
        section.Providers.Clear();

        Assert.Equal("original-client", configuration.ApplicationClientId);
        Assert.True(configuration.UseWamBroker);
        Assert.Equal("Original.Initializer", configuration.InitializerType);
        ProviderSettings captured = Assert.Single(configuration.Providers);
        Assert.Equal("Active Directory Integrated", captured.Name);
        Assert.Equal("Original.Provider", captured.Type);
    }

    /// <summary>
    /// Reading settings must not eagerly validate methods ahead of initializer execution.
    /// </summary>
    [Fact]
    public void ParsingDefersProviderMethodValidation()
    {
        // The registry validates entries in order, after running the configured initializer.
        var section = new ModernSection();
        section.Providers.Add(new ProviderSettings("unknown", "Missing.Provider"));

        var configuration = new SqlAuthenticationConfiguration(section);

        // Neither an unknown method nor an unresolved type should trigger activation while parsing.
        Assert.Equal("unknown", Assert.Single(configuration.Providers).Name);
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
