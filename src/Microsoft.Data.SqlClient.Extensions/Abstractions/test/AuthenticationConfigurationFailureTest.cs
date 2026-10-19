// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies configuration failures abort application without masking fatal errors.</summary>
public class AuthenticationConfigurationFailureTest
{
    /// <summary>Malformed broker values fail before any configured provider is activated.</summary>
    [Theory]
    [InlineData("invalid")]
    [InlineData("1")]
    [InlineData(" ")]
    public void InvalidBrokerValueAbortsConfiguration(string value)
    {
        // A fatal provider acts as a sentinel: broker validation must fail before activation.
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["useWamBroker"].Value = value;
        section.Providers.Add(new ProviderSettings(
            "Active Directory Interactive", typeof(FailingProvider).AssemblyQualifiedName));

        var failure = Assert.IsType<ConfigurationErrorsException>(ApplyInvalidConfiguration(section));
        Assert.Contains("useWamBroker", failure.Message);
        Assert.Contains(value, failure.Message);
        Assert.Contains("true", failure.Message);
        Assert.Contains("false", failure.Message);
    }

    /// <summary>Missing or incompatible metadata is not silently treated as absent configuration.</summary>
    [Theory]
    [InlineData(typeof(InvalidStringSection), "applicationClientId")]
    [InlineData(typeof(InvalidNullableStringSection), "applicationClientId")]
    [InlineData(typeof(InvalidProvidersSection), "providers")]
    [InlineData(typeof(MissingProvidersSection), "providers")]
    [InlineData(typeof(EmptySection), "applicationClientId")]
    public void InvalidSchemaAbortsConfiguration(Type type, string propertyName)
    {
        // Deliberately incompatible handlers must not be mistaken for empty optional settings.
        var section = Assert.IsAssignableFrom<ConfigurationSection>(
            Activator.CreateInstance(type, nonPublic: true));

        var failure = Assert.IsType<ConfigurationErrorsException>(ApplyInvalidConfiguration(section));
        Assert.Contains(propertyName, failure.Message);
    }

    /// <summary>Only a genuinely absent section permits fallback to legacy configuration.</summary>
    [Theory]
    [InlineData("appSettings", false)]
    [InlineData("SqlClientAuthenticationConfigurationFailureTest_Missing", true)]
    public void SectionHandlerMismatchIsNotTreatedAsAbsent(string name, bool missing)
    {
        // Compare a genuinely missing section with an existing handler for another schema.
        string expectedType = typeof(SqlClientAuthenticationProviderConfigurationSection).FullName!;
        if (missing)
        {
            Assert.Null(SqlAuthenticationConfiguration.FetchConfigurationSection(
                name, expectedType));
            return;
        }

        var failure = Assert.Throws<ConfigurationErrorsException>(() =>
            SqlAuthenticationConfiguration.FetchConfigurationSection(name, expectedType));
        Assert.Contains(name, failure.Message);
        Assert.Contains(expectedType, failure.Message);
    }

    /// <summary>Fatal initializer and provider failures propagate without an argument wrapper.</summary>
    [Theory]
    [InlineData(typeof(FailingInitializer))]
    [InlineData(typeof(FailingProvider))]
    public void FatalConfigurationFailuresAreNotWrapped(Type type)
    {
        // Fail through either reflective construction or the initializer callback.
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        if (typeof(SqlAuthenticationInitializer).IsAssignableFrom(type))
        {
            section.ElementInformation.Properties["initializerType"].Value = type.AssemblyQualifiedName;
        }
        else
        {
            section.Providers.Add(new ProviderSettings(
                "Active Directory Integrated", type.AssemblyQualifiedName));
        }

        // Fatal errors may retain reflection wrappers, but must not gain a configuration wrapper.
        Exception failure = ApplyInvalidConfiguration(section);
        Assert.IsNotType<ArgumentException>(failure);
        Assert.IsType<OutOfMemoryException>(failure.GetBaseException());
    }

    /// <summary>Null or incompatible activation results abort before later providers are created.</summary>
    [Theory]
    [InlineData(typeof(int?), true)]
    [InlineData(typeof(int?), false)]
    [InlineData(typeof(object), true)]
    [InlineData(typeof(object), false)]
    public void InvalidActivationAbortsBeforeLaterProviders(Type type, bool initializer)
    {
        // Nullable activation produces null; object activation produces an incompatible instance.
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        if (initializer)
        {
            section.ElementInformation.Properties["initializerType"].Value = type.AssemblyQualifiedName;
        }
        else
        {
            section.Providers.Add(new ProviderSettings(
                "Active Directory Integrated", type.AssemblyQualifiedName));
        }

        // Reaching this constructor would raise a fatal error rather than the configuration error.
        section.Providers.Add(new ProviderSettings(
            "Active Directory Interactive", typeof(FailingProvider).AssemblyQualifiedName));

        var failure = Assert.IsType<ArgumentException>(ApplyInvalidConfiguration(section));
        Assert.Contains(type.AssemblyQualifiedName!, failure.Message);
        Assert.True(ExceptionHelpers.IsRecoverableException(failure));
    }

    /// <summary>Applies invalid configuration to an isolated registry and captures its failure.</summary>
    /// <param name="section">The invalid section, without modifying process-wide configuration.</param>
    /// <returns>The failure escaping configuration application.</returns>
    private static Exception ApplyInvalidConfiguration(ConfigurationSection section)
    {
        return Assert.ThrowsAny<Exception>(() =>
            new SqlAuthenticationProviderRegistry().ApplyConfiguration(
                new SqlAuthenticationConfiguration(section)));
    }

    /// <summary>Simulates a handler missing the expected authentication schema.</summary>
    private sealed class EmptySection : ConfigurationSection { }

    /// <summary>Simulates an incompatible string property instead of an absent override.</summary>
    private sealed class InvalidStringSection : ConfigurationSection
    {
        [ConfigurationProperty("applicationClientId", DefaultValue = 1)]
        public int ApplicationClientId => (int)this["applicationClientId"];
    }

    /// <summary>Ensures a null value does not disguise an incompatible declared property type.</summary>
    private sealed class InvalidNullableStringSection : ConfigurationSection
    {
        [ConfigurationProperty("applicationClientId", DefaultValue = null)]
        public int? ApplicationClientId => (int?)this["applicationClientId"];
    }

    /// <summary>Simulates an incompatible provider collection with otherwise valid string settings.</summary>
    private sealed class InvalidProvidersSection : ConfigurationSection
    {
        [ConfigurationProperty("applicationClientId")]
        public string ApplicationClientId => (string)this["applicationClientId"];

        [ConfigurationProperty("useWamBroker")]
        public string UseWamBroker => (string)this["useWamBroker"];

        [ConfigurationProperty("initializerType")]
        public string InitializerType => (string)this["initializerType"];

        [ConfigurationProperty("providers", DefaultValue = 1)]
        public int Providers => (int)this["providers"];
    }

    /// <summary>Simulates a handler that omits the provider collection entirely.</summary>
    private sealed class MissingProvidersSection : ConfigurationSection
    {
        [ConfigurationProperty("applicationClientId")]
        public string ApplicationClientId => (string)this["applicationClientId"];

        [ConfigurationProperty("useWamBroker")]
        public string UseWamBroker => (string)this["useWamBroker"];

        [ConfigurationProperty("initializerType")]
        public string InitializerType => (string)this["initializerType"];
    }

    /// <summary>Simulates a fatal initializer callback without actually exhausting memory.</summary>
    private sealed class FailingInitializer : SqlAuthenticationInitializer
    {
        public FailingInitializer() { }

        public override void Initialize() =>
            throw new OutOfMemoryException("Test fatal initializer failure");
    }

    /// <summary>Simulates a fatal provider constructor without actually exhausting memory.</summary>
    private sealed class FailingProvider : SqlAuthenticationProvider
    {
        public FailingProvider() =>
            throw new OutOfMemoryException("Test fatal provider failure");

        public override bool IsSupported(SqlAuthenticationMethod method) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }
}
