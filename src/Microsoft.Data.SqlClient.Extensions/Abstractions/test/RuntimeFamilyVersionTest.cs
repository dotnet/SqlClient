// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Checks exact loaded-family versions independently of assembly binding versions.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class RuntimeFamilyVersionTest
{
    private static string ExpectedVersion => typeof(SqlAuthenticationProvider).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];

    /// <summary>The owning assembly is always accepted as the expected family version.</summary>
    [Fact]
    public void MatchingRuntimeVersionSucceeds() =>
        SqlAuthenticationProviderRegistry.ValidateFamilyVersion(typeof(SqlAuthenticationProvider).Assembly);

    /// <summary>Source-control build metadata does not change an otherwise identical package version.</summary>
    [Fact]
    public void MatchingVersionWithDifferentBuildMetadataSucceeds()
    {
        SqlAuthenticationProviderRegistry.ValidateFamilyVersion(
            CreateAssembly(ExpectedVersion + "+different.commit"));
    }

    /// <summary>Different stable or prerelease versions fail with exact versions and upgrade guidance.</summary>
    [Theory]
    [InlineData("7.0.1")]
    [InlineData("1.0.0")]
    [InlineData("8.0.0-different-preview")]
    public void MixedRuntimeVersionThrows(string actual)
    {
        InvalidOperationException error =
            Assert.Throws<SqlAuthenticationProviderRegistry.FamilyVersionException>(
            () => SqlAuthenticationProviderRegistry.ValidateFamilyVersion(CreateAssembly(actual)));
        Assert.Contains("version mismatch", error.Message);
        Assert.Contains(actual, error.Message);
        Assert.Contains("Microsoft.Data.SqlClient.Extensions.Abstractions", error.Message);
        Assert.Contains("Upgrade all SqlClient family packages together", error.Message);
    }

    /// <summary>Assemblies lacking version evidence are rejected rather than silently accepted.</summary>
    [Fact]
    public void MissingInformationalVersionThrows()
    {
        InvalidOperationException error =
            Assert.Throws<SqlAuthenticationProviderRegistry.FamilyVersionException>(
            () => SqlAuthenticationProviderRegistry.ValidateFamilyVersion(CreateAssembly(null)));
        Assert.Contains("Cannot determine the exact SqlClient family version", error.Message);
    }

    /// <summary>A matching driver is found even when it is not the first loaded assembly.</summary>
    [Fact]
    public void MatchingLoadedDriverVersionSucceeds() =>
        Assert.True(SqlAuthenticationProviderRegistry.ValidateLoadedSqlClientVersions(
            [typeof(object).Assembly, new DriverAssembly(CreateAssembly(ExpectedVersion))]));

    /// <summary>Non-driver assemblies neither satisfy nor fail the loaded-driver check.</summary>
    [Fact]
    public void LoadedDriverCheckIgnoresUnrelatedAssemblies() =>
        Assert.False(SqlAuthenticationProviderRegistry.ValidateLoadedSqlClientVersions(
            [typeof(object).Assembly, CreateAssembly("0.0.0-unrelated")]));

    /// <summary>A matching driver in one context cannot mask an incompatible driver in another.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadedDriverCheckRejectsAnyMismatchedContext(bool mismatchFirst)
    {
        var matching = new DriverAssembly(CreateAssembly(ExpectedVersion));
        var mismatched = new DriverAssembly(CreateAssembly("0.0.0-incompatible"));
        Assembly[] assemblies = mismatchFirst ? [mismatched, matching] : [matching, mismatched];

        Assert.Throws<SqlAuthenticationProviderRegistry.FamilyVersionException>(() =>
            SqlAuthenticationProviderRegistry.ValidateLoadedSqlClientVersions(assemblies));
    }

    /// <summary>Azure policy failures disable discovery without failing or retrying bootstrap.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AzureVersionFailureDisablesDiscovery(bool missingMetadata)
    {
        using var switches = new AuthenticationFeatureSwitchesHelper
        {
            EnableAppConfig = false,
            EnableAzureExtensionDiscovery = true
        };
        var registry = new SqlAuthenticationProviderRegistry();
        Assembly azure = CreateAssembly(missingMetadata ? null : "0.0.0-incompatible");
        int loads = 0;
        using var listener = new AuthenticationTraceListener();

        registry.EnsureBootstrapped(loadAzureAssembly: () =>
        {
            loads++;
            return azure;
        });
        registry.EnsureBootstrapped(loadAzureAssembly: () =>
            throw new InvalidOperationException("Discovery must not be retried."));
        registry.LoadAzureExtension(() =>
            throw new InvalidOperationException("Version failure must disable later discovery."));
        Assert.Equal(1, loads);
        Assert.Contains(listener.Messages, message =>
            message.Contains("version validation failed") &&
            message.Contains("automatic Azure discovery") && message.Contains("restart"));
    }

    /// <summary>Version failures preserve public provider access and configuration precedence.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AzureVersionFailurePreservesPublicProviderAccess(
        bool missingMetadata, bool configuredProvider)
    {
        using var switches = new AuthenticationFeatureSwitchesHelper
        {
            EnableAppConfig = false,
            EnableAzureExtensionDiscovery = false
        };
        var registry = Assert.IsType<SqlAuthenticationProviderRegistry>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "Instance", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null));
        FieldInfo failureField = typeof(SqlAuthenticationProviderRegistry).GetField(
            "_bootstrapFailure", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo disabledField = typeof(SqlAuthenticationProviderRegistry).GetField(
            "_azureDiscoveryDisabledByVersionFailure", BindingFlags.Instance | BindingFlags.NonPublic)!;
        FieldInfo completedField = typeof(SqlAuthenticationProviderRegistry).GetField(
            "_bootstrapCompleted", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var providers = Assert.IsAssignableFrom<IDictionary>(
            typeof(SqlAuthenticationProviderRegistry).GetField(
                "_providers", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(registry));
        const SqlAuthenticationMethod method = SqlAuthenticationMethod.ActiveDirectoryDefault;
        object? originalFailure = failureField.GetValue(registry);
        object? originalDisabled = disabledField.GetValue(registry);
        object? originalCompleted = completedField.GetValue(registry);
        object? originalProvider = providers[method];
        var provider = new Provider();
        var replacement = new Provider();
        using var listener = new AuthenticationTraceListener();

        try
        {
            providers.Remove(method);
            failureField.SetValue(registry, null);
            disabledField.SetValue(registry, false);
            Assert.True(registry.Register(method, provider, configuredProvider
                ? SqlAuthenticationProviderRegistry.RegistrationTier.Config
                : SqlAuthenticationProviderRegistry.RegistrationTier.Application));
            registry.LoadAzureExtension(() =>
                CreateAssembly(missingMetadata ? null : "0.0.0-incompatible"));
            Assert.True(Assert.IsType<bool>(disabledField.GetValue(registry)));
            Assert.Null(failureField.GetValue(registry));
            for (int attempt = 0; attempt < 2; attempt++)
            {
                Assert.Same(configuredProvider || attempt == 0 ? provider : replacement,
                    SqlAuthenticationProvider.GetProvider(method));
                Assert.Equal(!configuredProvider,
                    SqlAuthenticationProvider.SetProvider(method, replacement));
                Assert.Same(configuredProvider ? provider : replacement,
                    SqlAuthenticationProvider.GetProvider(method));
            }
            registry.LoadAzureExtension(() =>
                throw new InvalidOperationException("Version failure must disable later discovery."));
            Assert.Single(listener.Messages, message =>
                message.Contains("version validation failed"));
            Assert.Contains(listener.Messages, message =>
                message.Contains("Explicit registration and existing providers remain available"));
        }
        finally
        {
            failureField.SetValue(registry, originalFailure);
            disabledField.SetValue(registry, originalDisabled);
            completedField.SetValue(registry, originalCompleted);
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

    private sealed class Provider : SqlAuthenticationProvider
    {
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }

    private sealed class DriverAssembly(Assembly metadata) : Assembly
    {
        public override AssemblyName GetName() => new("Microsoft.Data.SqlClient");

        public override object[] GetCustomAttributes(Type attributeType, bool inherit) =>
            metadata.GetCustomAttributes(attributeType, inherit);
    }

    /// <summary>Creates an in-memory assembly with controlled version metadata and no filesystem side effects.</summary>
    private static Assembly CreateAssembly(string? version)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("RuntimeVersionFixture"), AssemblyBuilderAccess.Run);
        if (version is not null)
        {
            ConstructorInfo constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [version]));
        }
        return assembly;
    }
}
