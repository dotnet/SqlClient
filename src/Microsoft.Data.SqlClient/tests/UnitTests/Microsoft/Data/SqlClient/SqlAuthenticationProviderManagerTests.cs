// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
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

    // Verify that SqlAuthenticationProviderManager registers its GetProvider
    // and SetProvider methods as callbacks on SqlAuthenticationProvider during
    // static initialization.  This is the AOT-safe code path that avoids
    // reflection-based assembly loading.
    [Fact]
    public void CallbackDelegates_RegisteredByManager()
    {
        // Accessing SqlAuthenticationProviderManager triggers its static
        // constructor, which should register the callbacks.
        _ = SqlAuthenticationProviderManager.GetProvider(
            SqlAuthenticationMethod.NotSpecified);

        // Use reflection to inspect the private callback fields on
        // SqlAuthenticationProvider (the test itself doesn't need to be
        // AOT-safe).
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;

        FieldInfo? getCallbackField = typeof(SqlAuthenticationProvider)
            .GetField("s_getProviderCallback", flags);
        FieldInfo? setCallbackField = typeof(SqlAuthenticationProvider)
            .GetField("s_setProviderCallback", flags);

        Assert.NotNull(getCallbackField);
        Assert.NotNull(setCallbackField);

        object? getCallback = getCallbackField.GetValue(null);
        object? setCallback = setCallbackField.GetValue(null);

        Assert.NotNull(getCallback);
        Assert.NotNull(setCallback);
    }
}
