// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Confirms the driver uses the public Abstractions registry after relocation.</summary>
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

    /// <summary>Registrations round-trip and can be replaced through the direct public API.</summary>
    [Fact]
    public void DirectRegistration_RoundTrips()
    {
        Provider provider1 = new();

        Assert.True(
            SqlAuthenticationProvider.SetProvider(
                // GOTCHA: On .NET Framework, the dummy provider is already
                // registered as the default provider for Interactive, so we
                // use DeviceCodeFlow instead.
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider1));

        Assert.Same(
            provider1,
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        Provider provider2 = new();

        Assert.True(
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider2));

        Assert.Same(
            provider2,
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

    }

    /// <summary>Provider lookup does not fail when Azure has overlapping constructor overloads.</summary>
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
            // Azure may not be installed, so a null provider is also valid.
            _ = SqlAuthenticationProvider.GetProvider(method);
        }
    }

}
