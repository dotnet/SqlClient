// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

public class SqlAuthenticationProviderManagerTests
{
    private class Provider : SqlAuthenticationProvider
    {
        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters)
        {
            return Task.FromResult(
                new SqlAuthenticationToken(
                    "SampleAccessToken", DateTimeOffset.UtcNow.AddMinutes(5)));
        }

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
        {
            return authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow;
        }
    }

    // Verify that we can get and set providers via both the Abstractions
    // package and Manager class interchangeably.
    //
    // This tests the dynamic assembly loading code in the Abstractions
    // package.
    [Fact]
    public void Abstractions_And_Manager_GetSetProvider_Equivalent()
    {
        // Set via Manager, get via both.
        Provider provider1 = new();

        Assert.True(
            SqlAuthenticationProviderManager.SetProvider(
                // GOTCHA: On .NET Framework, the dummy provider is already
                // registered as the default provider for Interactive, so we
                // use DeviceCodeFlow instead.
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider1));

        Assert.Same(
            provider1,
            SqlAuthenticationProviderManager.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        Assert.Same(
            provider1,
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        // Set via Abstractions, get via both.
        Provider provider2 = new();

        Assert.True(
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider2));

        Assert.Same(
            provider2,
            SqlAuthenticationProviderManager.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        Assert.Same(
            provider2,
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }

    // Regression: the manager's static initializer reflectively constructs the Azure extension's
    // ActiveDirectoryAuthenticationProvider. That class has overlapping 1-arg constructors
    // ((string) and (ProviderOptions)), so calling Activator.CreateInstance(type, [null]) used
    // to throw AmbiguousMatchException -- which surfaced as TypeInitializationException from
    // GetProvider and broke every AD-authenticated connection. Calling GetProvider for an AD
    // method must succeed (returning either the registered provider or null) and must not throw.
    [Fact]
    public void GetProvider_ForActiveDirectoryMethod_DoesNotThrow()
    {
        foreach (SqlAuthenticationMethod method in new[]
        {
            SqlAuthenticationMethod.ActiveDirectoryIntegrated,
            #pragma warning disable CS0618 // ActiveDirectoryPassword is obsolete.
            SqlAuthenticationMethod.ActiveDirectoryPassword,
            #pragma warning restore CS0618
            SqlAuthenticationMethod.ActiveDirectoryInteractive,
            SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
            SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
            SqlAuthenticationMethod.ActiveDirectoryMSI,
            SqlAuthenticationMethod.ActiveDirectoryDefault,
            SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity,
        })
        {
            // No assertion on the value -- the provider may or may not be installed depending on
            // whether the Azure extension is on disk. We only assert no throw (which is what a
            // TypeInitializationException from the static initializer would do).
            _ = SqlAuthenticationProviderManager.GetProvider(method);
        }
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
        var instance = SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(
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
        var instance = SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(
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
        var instance = SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(
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
        var instance = SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(
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
            SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(
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
        var instance = SqlAuthenticationProviderManager.CreateAzureAuthenticationProvider(
            typeof(StubModern.ActiveDirectoryAuthenticationProvider),
            typeof(StubModern.ActiveDirectoryAuthenticationProviderOptions),
            applicationClientId: "app-789",
            useWamBroker: true);

        var stub = Assert.IsType<StubModern.ActiveDirectoryAuthenticationProvider>(instance);
        Assert.True(stub.OptionsCtorUsed);
        Assert.Equal("app-789", stub.CapturedApplicationClientId);
        Assert.Equal(true, stub.CapturedUseWamBroker);
    }
}
