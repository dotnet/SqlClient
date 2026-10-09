// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Preserves Azure constructor selection for configured client IDs and broker options.</summary>
public class AzureProviderConstructionTest
{
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

    /// <summary>Unconfigured discovery avoids ambiguous null constructor arguments.</summary>
    [Fact]
    public void CreateAzureAuthenticationProvider_NeitherConfigured_UsesParameterlessCtor()
    {
        var instance = SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
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

    /// <summary>Modern Azure extensions receive client IDs through the options constructor.</summary>
    [Fact]
    public void CreateAzureAuthenticationProvider_AppIdOnly_OptionsAvailable_UsesOptionsCtor()
    {
        var instance = SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
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

    /// <summary>Legacy constructor selection remains compatible until phase 2 removes the fallback.</summary>
    [Fact]
    public void CreateAzureAuthenticationProvider_AppIdOnly_OptionsMissing_FallsBackToStringCtor()
    {
        var instance = SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
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

    /// <summary>Incompatible Azure types are not constructed with invalid arguments.</summary>
    [Fact]
    public void CreateAzureAuthenticationProvider_AppIdOnly_NoCompatibleCtor_ReturnsNull()
    {
        var instance = SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
            typeof(StubMinimal.ActiveDirectoryAuthenticationProvider),
            optionsType: null,
            applicationClientId: "no-ctor",
            useWamBroker: null);

        Assert.Null(instance);
    }

    /// <summary>A broker override without Azure options produces a localized error.</summary>
    [Fact]
    public void CreateAzureAuthenticationProvider_UseWamBroker_OptionsMissing_Throws()
    {
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
                typeof(StubLegacy.ActiveDirectoryAuthenticationProvider),
                optionsType: null,
                applicationClientId: null,
                useWamBroker: true));

        Assert.Contains("ActiveDirectoryAuthenticationProviderOptions", ex.Message);
        Assert.Contains("Microsoft.Data.SqlClient.Extensions.Azure", ex.Message);
    }

    /// <summary>The configured broker flag and client ID reach the modern options constructor.</summary>
    [Fact]
    public void CreateAzureAuthenticationProvider_UseWamBroker_OptionsAvailable_UsesOptionsCtor()
    {
        var instance = SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
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
