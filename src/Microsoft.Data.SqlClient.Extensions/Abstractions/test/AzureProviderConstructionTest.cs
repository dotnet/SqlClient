// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Verifies Azure discovery constructs providers using the supported options contract.</summary>
public class AzureProviderConstructionTest
{
    public sealed class StubOptions
    {
        public string? ApplicationClientId { get; set; }
        public bool UseWamBroker { get; set; }
    }

    public sealed class StubProvider : SqlAuthenticationProvider
    {
        public StubOptions Options { get; }

        public StubProvider(StubOptions options) => Options = options;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
            => Task.FromResult(new SqlAuthenticationToken("stub", DateTimeOffset.UtcNow.AddMinutes(5)));

        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;
    }

    /// <summary>Discovery always uses options, preserving defaults unless configuration overrides them.</summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("app-123", null)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("app-123", false)]
    [InlineData("app-123", true)]
    public void CreateAzureAuthenticationProvider_UsesOptionsCtor(string? applicationClientId, bool? useWamBroker)
    {
        // The Options constructor is the sole discovery contract, including with no overrides.
        var instance = SqlAuthenticationProviderRegistry.CreateAzureAuthenticationProvider(
            typeof(StubProvider),
            typeof(StubOptions),
            applicationClientId,
            useWamBroker);

        // Unspecified values must retain option defaults rather than being forcibly overwritten.
        var stub = Assert.IsType<StubProvider>(instance);
        Assert.Equal(applicationClientId, stub.Options.ApplicationClientId);
        Assert.Equal(useWamBroker ?? false, stub.Options.UseWamBroker);
    }
}
