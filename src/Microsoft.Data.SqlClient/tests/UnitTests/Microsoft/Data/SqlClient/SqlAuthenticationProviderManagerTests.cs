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

    /// <summary>
    /// Verifies that GetProvider returns null when no provider has been registered
    /// for the specified authentication method.
    /// </summary>
    [Fact]
    public void GetProvider_ReturnsNull_WhenNoProviderRegistered()
    {
        // SqlPassword is never auto-registered with the AD provider.
        var result = SqlAuthenticationProviderManager.GetProvider(
            SqlAuthenticationMethod.SqlPassword);

        Assert.Null(result);
    }

    /// <summary>
    /// Verifies that SetProvider throws NotSupportedException when the provider
    /// does not support the specified authentication method.
    /// </summary>
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

    /// <summary>
    /// Verifies that ApplicationClientId is accessible and returns null when
    /// no app.config section is present.
    /// </summary>
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

    /// <summary>
    /// Verifies that SetProvider replaces a previously registered provider for
    /// the same authentication method.
    /// </summary>
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

    /// <summary>
    /// Verifies that SetProvider throws NullReferenceException when a null
    /// provider is passed (current behavior, not validated argument).
    /// </summary>
    [Fact]
    public void SetProvider_ThrowsOnNullProvider()
    {
        Assert.Throws<NullReferenceException>(() =>
            SqlAuthenticationProviderManager.SetProvider(
                SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow,
                null!));
    }

    /// <summary>
    /// Verifies that GetProvider returns null for NotSpecified, which is never
    /// a valid registration target.
    /// </summary>
    [Fact]
    public void GetProvider_ReturnsNull_ForNotSpecified()
    {
        var result = SqlAuthenticationProviderManager.GetProvider(
            SqlAuthenticationMethod.NotSpecified);

        Assert.Null(result);
    }
}
