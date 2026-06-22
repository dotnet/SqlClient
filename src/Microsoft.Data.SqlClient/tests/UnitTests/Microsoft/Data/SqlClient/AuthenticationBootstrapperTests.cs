// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests for <see cref="AuthenticationBootstrapper"/>, the core-side
/// component that discovers config-driven and Azure extension authentication
/// providers and seeds them into the Abstractions registry.
/// </summary>
public class AuthenticationBootstrapperTests
{
    // The Azure extension assembly is intentionally NOT referenced by this project (nor by the
    // core driver), so these tests exercise the bootstrapper's Azure-ABSENT behavior: stub-based
    // constructor selection (CreateAzureAuthenticationProvider_*) and config-driven providers.
    // The Azure-PRESENT behavior is covered by
    // Microsoft.Data.SqlClient.Extensions.Azure.Test.AuthenticationBootstrapperTests, the only
    // test project that references (and therefore guarantees the presence of) the Azure extension.
    public AuthenticationBootstrapperTests()
    {
        // Precondition: confirm the Azure extension is not present in this test context, so the
        // Azure-absent assumptions in these tests hold.
        Assert.Throws<FileNotFoundException>(
            () => Assembly.Load("Microsoft.Data.SqlClient.Extensions.Azure"));
    }

    // CreateAzureAuthenticationProvider tests ----------------------------------------------
    //
    // Each Stub* container mimics one shape the real Azure extension might expose:
    //   * StubModern  - both a (string) ctor and an (Options) ctor.
    //   * StubLegacy  - only the (string) ctor; no Options type at all.
    //   * StubMinimal - only a parameterless ctor.
    //
    // The helper takes a Type directly, so these stubs do not need any particular full name.

    public class StubProviderBase : SqlAuthenticationProvider
    {
        public string? CapturedApplicationClientId;
        public bool? CapturedUseWamBroker;
        public bool ParameterlessCtorUsed;
        public bool StringCtorUsed;
        public bool OptionsCtorUsed;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
            => Task.FromResult(new SqlAuthenticationToken("stub", DateTimeOffset.UtcNow.AddMinutes(5)));

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;
    }

    public static class StubModern
    {
        public sealed class ActiveDirectoryAuthenticationProviderOptions
        {
            public string? ApplicationClientId { get; set; }
            public bool UseWamBroker { get; set; }
        }

        public sealed class ActiveDirectoryAuthenticationProvider : StubProviderBase
        {
            public ActiveDirectoryAuthenticationProvider() { ParameterlessCtorUsed = true; }

            public ActiveDirectoryAuthenticationProvider(string applicationClientId)
            {
                StringCtorUsed = true;
                CapturedApplicationClientId = applicationClientId;
            }

            public ActiveDirectoryAuthenticationProvider(ActiveDirectoryAuthenticationProviderOptions options)
            {
                OptionsCtorUsed = true;
                CapturedApplicationClientId = options.ApplicationClientId;
                CapturedUseWamBroker = options.UseWamBroker;
            }
        }
    }

    public static class StubLegacy
    {
        // No Options type defined -- mimics older Azure extension versions.
        public sealed class ActiveDirectoryAuthenticationProvider : StubProviderBase
        {
            public ActiveDirectoryAuthenticationProvider() { ParameterlessCtorUsed = true; }

            public ActiveDirectoryAuthenticationProvider(string applicationClientId)
            {
                StringCtorUsed = true;
                CapturedApplicationClientId = applicationClientId;
            }
        }
    }

    public static class StubMinimal
    {
        // Parameterless only -- mimics a hypothetical extension with no 1-arg ctors at all.
        public sealed class ActiveDirectoryAuthenticationProvider : StubProviderBase
        {
            public ActiveDirectoryAuthenticationProvider() { ParameterlessCtorUsed = true; }
        }
    }

    [Fact]
    public void CreateAzureAuthenticationProvider_NeitherConfigured_UsesParameterlessCtor()
    {
        var instance = AuthenticationBootstrapper.CreateAzureAuthenticationProvider(
            typeof(StubModern.ActiveDirectoryAuthenticationProvider),
            typeof(StubModern.ActiveDirectoryAuthenticationProviderOptions),
            applicationClientId: null,
            useWamBroker: null);

        var stub = Assert.IsType<StubModern.ActiveDirectoryAuthenticationProvider>(instance);
        Assert.True(stub.ParameterlessCtorUsed);
        Assert.False(stub.StringCtorUsed);
        Assert.False(stub.OptionsCtorUsed);
        Assert.Null(stub.CapturedApplicationClientId);
        Assert.Null(stub.CapturedUseWamBroker);
    }

    [Fact]
    public void CreateAzureAuthenticationProvider_AppIdOnly_OptionsAvailable_UsesOptionsCtor()
    {
        var instance = AuthenticationBootstrapper.CreateAzureAuthenticationProvider(
            typeof(StubModern.ActiveDirectoryAuthenticationProvider),
            typeof(StubModern.ActiveDirectoryAuthenticationProviderOptions),
            applicationClientId: "app-123",
            useWamBroker: null);

        var stub = Assert.IsType<StubModern.ActiveDirectoryAuthenticationProvider>(instance);
        Assert.True(stub.OptionsCtorUsed);
        Assert.False(stub.StringCtorUsed);
        Assert.Equal("app-123", stub.CapturedApplicationClientId);
        Assert.Equal(false, stub.CapturedUseWamBroker);
    }

    [Fact]
    public void CreateAzureAuthenticationProvider_AppIdOnly_OptionsMissing_FallsBackToStringCtor()
    {
        var instance = AuthenticationBootstrapper.CreateAzureAuthenticationProvider(
            typeof(StubLegacy.ActiveDirectoryAuthenticationProvider),
            optionsType: null,
            applicationClientId: "legacy-456",
            useWamBroker: null);

        var stub = Assert.IsType<StubLegacy.ActiveDirectoryAuthenticationProvider>(instance);
        Assert.True(stub.StringCtorUsed);
        Assert.False(stub.OptionsCtorUsed);
        Assert.False(stub.ParameterlessCtorUsed);
        Assert.Equal("legacy-456", stub.CapturedApplicationClientId);
        Assert.Null(stub.CapturedUseWamBroker);
    }

    [Fact]
    public void CreateAzureAuthenticationProvider_AppIdOnly_NoCompatibleCtor_ReturnsNull()
    {
        var instance = AuthenticationBootstrapper.CreateAzureAuthenticationProvider(
            typeof(StubMinimal.ActiveDirectoryAuthenticationProvider),
            optionsType: null,
            applicationClientId: "no-ctor",
            useWamBroker: null);

        Assert.Null(instance);
    }

    [Fact]
    public void CreateAzureAuthenticationProvider_UseWamBroker_OptionsMissing_Throws()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            AuthenticationBootstrapper.CreateAzureAuthenticationProvider(
                typeof(StubLegacy.ActiveDirectoryAuthenticationProvider),
                optionsType: null,
                applicationClientId: null,
                useWamBroker: true));

        Assert.Contains("ActiveDirectoryAuthenticationProviderOptions", ex.Message);
        Assert.Contains("Microsoft.Data.SqlClient.Extensions.Azure", ex.Message);
    }

    [Fact]
    public void CreateAzureAuthenticationProvider_UseWamBroker_OptionsAvailable_UsesOptionsCtor()
    {
        var instance = AuthenticationBootstrapper.CreateAzureAuthenticationProvider(
            typeof(StubModern.ActiveDirectoryAuthenticationProvider),
            typeof(StubModern.ActiveDirectoryAuthenticationProviderOptions),
            applicationClientId: "app-789",
            useWamBroker: true);

        var stub = Assert.IsType<StubModern.ActiveDirectoryAuthenticationProvider>(instance);
        Assert.True(stub.OptionsCtorUsed);
        Assert.Equal("app-789", stub.CapturedApplicationClientId);
        Assert.Equal(true, stub.CapturedUseWamBroker);
    }

    // ApplicationClientId tests ------------------------------------------------------------

    /// <summary>
    /// Verifies that ApplicationClientId is accessible and returns null when no app.config
    /// section is present (non-Framework targets), and that the bootstrapper exposes the
    /// registry it was constructed with.
    /// </summary>
    [ConditionalFact(nameof(IsNotNetFramework))]
    public void ApplicationClientId_IsNull_WhenNoConfig()
    {
        // On non-Framework targets there is no app.config, so ApplicationClientId should be null.
        AuthenticationProviderRegistry registry = new();
        AuthenticationBootstrapper bootstrapper = new(registry);

        // The bootstrapper exposes the registry it was given.
        Assert.Same(registry, bootstrapper.Registry);

        Assert.Null(bootstrapper.ApplicationClientId);
    }

    // The UnitTests project has an app.config that configures a dummy
    // authentication provider for ActiveDirectoryInteractive and sets
    // applicationClientId.  The following tests verify this on .NET Framework.

    /// <summary>
    /// Verifies that ApplicationClientId is read from the app.config section and that the
    /// bootstrapper exposes the registry it was constructed with.
    /// </summary>
    [ConditionalFact(nameof(IsNetFramework))]
    public void ApplicationClientId_ReadFromAppConfig()
    {
        // The app.config sets applicationClientId="f3e3a0a0-1234-5678-9abc-def012345678".
        AuthenticationProviderRegistry registry = new();
        AuthenticationBootstrapper bootstrapper = new(registry);

        Assert.Same(registry, bootstrapper.Registry);
        Assert.Equal(
            "f3e3a0a0-1234-5678-9abc-def012345678",
            bootstrapper.ApplicationClientId);
    }

    // UseWamBroker tests -------------------------------------------------------------------

    /// <summary>
    /// Verifies that UseWamBroker is accessible and returns null when no app.config
    /// section is present (non-Framework targets).
    /// </summary>
    [ConditionalFact(nameof(IsNotNetFramework))]
    public void UseWamBroker_IsNull_WhenNoConfig()
    {
        // On non-Framework targets there is no app.config, so the property should be null.
        AuthenticationBootstrapper bootstrapper = new(new AuthenticationProviderRegistry());
        Assert.Null(bootstrapper.UseWamBroker);
    }

    /// <summary>
    /// Verifies that UseWamBroker is read from the app.config section.
    /// </summary>
    [ConditionalFact(nameof(IsNetFramework))]
    public void UseWamBroker_ReadFromAppConfig()
    {
        // The app.config sets useWamBroker="true".
        AuthenticationBootstrapper bootstrapper = new(new AuthenticationProviderRegistry());
        Assert.True(bootstrapper.UseWamBroker);
    }

    /// <summary>
    /// Verifies that the dummy provider from app.config is registered for
    /// ActiveDirectoryInteractive and that no other methods have providers.
    /// </summary>
    [ConditionalFact(nameof(IsNetFramework))]
    public void DefaultAuthenticationProviders_AppConfig()
    {
        AuthenticationProviderRegistry registry = new();
        _ = new AuthenticationBootstrapper(registry);

        foreach (SqlAuthenticationMethod method in Enum.GetValues(typeof(SqlAuthenticationMethod)))
        {
            var provider = registry.GetProvider(method);

            if (method == SqlAuthenticationMethod.ActiveDirectoryInteractive)
            {
                Assert.IsType<DummySqlAuthenticationProvider>(provider);
            }
            else
            {
                Assert.Null(provider);
            }
        }
    }

    /// <summary>
    /// Verifies that the app.config-registered dummy provider can acquire a token.
    /// </summary>
    [ConditionalFact(nameof(IsNetFramework))]
    public async Task DefaultAuthenticationProvider_AcquiresToken()
    {
        AuthenticationProviderRegistry registry = new();
        _ = new AuthenticationBootstrapper(registry);

        var provider = registry.GetProvider(
            SqlAuthenticationMethod.ActiveDirectoryInteractive);
        Assert.NotNull(provider);
        var token = await provider.AcquireTokenAsync(null!);
        Assert.Equal(DummySqlAuthenticationProvider.DummyAccessToken, token.AccessToken);
    }

    /// <summary>
    /// Verifies that the app.config-registered provider cannot be replaced.
    /// </summary>
    [ConditionalFact(nameof(IsNetFramework))]
    public void DefaultAuthenticationProviders_NoReplace()
    {
        AuthenticationProviderRegistry registry = new();
        _ = new AuthenticationBootstrapper(registry);

        Assert.IsType<DummySqlAuthenticationProvider>(
            registry.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive));

        bool setResult = registry.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryInteractive,
            new InteractiveProvider());

        Assert.False(setResult);

        Assert.IsType<DummySqlAuthenticationProvider>(
            registry.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    private static bool IsNetFramework =>
        RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework");

    private static bool IsNotNetFramework => !IsNetFramework;

    private class InteractiveProvider : SqlAuthenticationProvider
    {
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
        {
            throw new NotImplementedException();
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        {
            return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryInteractive;
        }
    }
}
