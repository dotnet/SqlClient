// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies configuration failures abort application without masking fatal errors.</summary>
public class AuthenticationConfigurationFailureTest
{
    /// <summary>Malformed broker values are ignored without skipping configured provider activation.</summary>
    [Theory]
    [InlineData("invalid")]
    [InlineData("1")]
    [InlineData(" ")]
    public void InvalidBrokerValueDoesNotPreventProviderActivation(string value)
    {
        // The fatal provider proves activation is attempted after the invalid broker is ignored.
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["useWamBroker"].Value = value;
        section.Providers.Add(new ProviderSettings(
            "Active Directory Interactive", typeof(FailingProvider).AssemblyQualifiedName));

        var settings = new SqlAuthenticationConfiguration(section);
        Assert.Null(settings.UseWamBroker);
        Exception failure = ApplyInvalidConfiguration(section);
        Assert.IsType<OutOfMemoryException>(failure.GetBaseException());
    }

    /// <summary>Absent sections and incompatible handlers both allow fallback in relaxed parsing.</summary>
    [Theory]
    [InlineData("appSettings", false)]
    [InlineData("SqlClientAuthenticationConfigurationFailureTest_Missing", true)]
    public void AbsentOrMismatchedSectionReturnsNull(string name, bool missing)
    {
        // Compare a genuinely missing section with an existing handler for another schema.
        Assert.Equal(missing, ConfigurationManager.GetSection(name) is null);
        Assert.Null(SqlAuthenticationConfiguration.FetchConfigurationSection(
            name, typeof(SqlClientAuthenticationProviderConfigurationSection)));
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
