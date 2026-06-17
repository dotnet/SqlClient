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
}
