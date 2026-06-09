// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
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

        #pragma warning disable CS0618 // Type or member is obsolete
        Assert.Same(
            provider1,
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        #pragma warning restore CS0618

        // Set via Abstractions, get via both.
        Provider provider2 = new();

        #pragma warning disable CS0618 // Type or member is obsolete
        Assert.True(
            SqlAuthenticationProvider.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                provider2));
        #pragma warning restore CS0618

        Assert.Same(
            provider2,
            SqlAuthenticationProviderManager.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        #pragma warning disable CS0618 // Type or member is obsolete
        Assert.Same(
            provider2,
            SqlAuthenticationProvider.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
        #pragma warning restore CS0618
    }

    [Fact]
    public void GetProvider_ReturnsNull_WhenNoProviderRegistered()
    {
        // SqlPassword is never auto-registered with the AD provider.
        var result = SqlAuthenticationProviderManager.GetProvider(
            SqlAuthenticationMethod.SqlPassword);

        Assert.Null(result);
    }

    [Fact]
    public void SetProvider_ThrowsOnUnsupportedMethod()
    {
        // Our test Provider only supports DeviceCodeFlow.
        // Attempting to register it for a different method should throw.
        Provider provider = new();

        Assert.Throws<NotSupportedException>(() =>
            SqlAuthenticationProviderManager.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryInteractive,
                provider));
    }

    [Fact]
    public void ApplicationClientId_IsAccessible()
    {
        // ApplicationClientId should be accessible (may be null if no config
        // section is present, which is the typical unit test scenario).
        // This test verifies the property doesn't throw.
        string? clientId = SqlAuthenticationProviderManager.ApplicationClientId;

        // In unit tests without an app.config section, this will be null.
        Assert.Null(clientId);
    }

    [Fact]
    public void SetProvider_ReplacesExistingProvider()
    {
        Provider provider1 = new();
        Provider provider2 = new();

        SqlAuthenticationProviderManager.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
            provider1);

        Assert.Same(
            provider1,
            SqlAuthenticationProviderManager.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));

        // Replace with provider2.
        SqlAuthenticationProviderManager.SetProvider(
            SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
            provider2);

        Assert.Same(
            provider2,
            SqlAuthenticationProviderManager.GetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow));
    }

    [Fact]
    public void SetProvider_ThrowsOnNullProvider()
    {
        Assert.Throws<NullReferenceException>(() =>
            SqlAuthenticationProviderManager.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                null!));
    }

    [Fact]
    public void GetProvider_ReturnsNull_ForNotSpecified()
    {
        var result = SqlAuthenticationProviderManager.GetProvider(
            SqlAuthenticationMethod.NotSpecified);

        Assert.Null(result);
    }
}
