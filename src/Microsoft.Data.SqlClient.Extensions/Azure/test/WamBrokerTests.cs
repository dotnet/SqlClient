// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

public class WamBrokerTests
{
    // The SqlClient first-party application client id that is hard-coded in the provider.
    private const string SqlClientApplicationId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

    [Fact]
    public void SetParentActivityOrWindow_NullArgument_ThrowsArgumentNullException()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        Assert.Throws<ArgumentNullException>("parentActivityOrWindowFunc",
            () => provider.SetParentActivityOrWindow(null!));
    }

    [Fact]
    public void SetParentActivityOrWindow_ValidFunc_DoesNotThrow()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindow(() => IntPtr.Zero);
    }

    [Fact]
    public void SetParentActivityOrWindow_CanBeCalledMultipleTimes()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindow(() => IntPtr.Zero);
        provider.SetParentActivityOrWindow(() => new IntPtr(12345));
    }

    [Fact]
    public void Ctor_Default_EnablesWamBroker()
    {
        // The parameterless ctor uses the SqlClient first-party app id, so WAM is always on.
        var provider = new ActiveDirectoryAuthenticationProvider();

        Assert.True(GetUseWamBrokerField(provider),
            "Default ctor must enable WAM broker (uses SqlClient first-party application id).");
    }

    [Fact]
    public void Ctor_AppClientId_DefaultsUseWamBrokerToFalse()
    {
        // A custom application id without explicitly opting in must NOT enable WAM broker.
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(customAppId);

        Assert.False(GetUseWamBrokerField(provider),
            "Custom application id without useWamBroker=true must keep WAM broker disabled.");
    }

    [Fact]
    public void Ctor_AppClientId_UseWamBrokerTrue_EnablesWamBroker()
    {
        // A custom application id with useWamBroker=true must enable WAM broker.
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(customAppId, useWamBroker: true);

        Assert.True(GetUseWamBrokerField(provider),
            "Custom application id with useWamBroker=true must enable WAM broker.");
    }

    [Fact]
    public void Ctor_AppClientId_UseWamBrokerFalse_DisablesWamBroker()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(customAppId, useWamBroker: false);

        Assert.False(GetUseWamBrokerField(provider));
    }

    [Fact]
    public void Ctor_SqlClientAppIdExplicit_UseWamBrokerFalse_StillEnablesWamBroker()
    {
        // Even if a caller passes the SqlClient first-party application id explicitly with
        // useWamBroker=false, WAM broker mode must still be enabled because the SqlClient
        // first-party app id is hard-wired to use WAM. This guards the OR-condition in:
        //   _useWamBroker = _applicationClientId == s_sqlclientapplicationid || useWamBroker;
        var provider = new ActiveDirectoryAuthenticationProvider(SqlClientApplicationId, useWamBroker: false);

        Assert.True(GetUseWamBrokerField(provider),
            "SqlClient first-party application id must always enable WAM broker, regardless of the useWamBroker argument.");
    }

    [Fact]
    public void Ctor_WithDeviceCodeCallback_UseWamBrokerTrue_EnablesWamBroker()
    {
        // The three-arg ctor (deviceCodeCallback, applicationClientId, useWamBroker) is the most
        // flexible overload and must honor useWamBroker just like the two-arg ctor.
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            deviceCodeFlowCallbackMethod: static _ => Task.CompletedTask,
            applicationClientId: customAppId,
            useWamBroker: true);

        Assert.True(GetUseWamBrokerField(provider));
    }

    [Fact]
    public void Ctor_WithDeviceCodeCallback_AppClientIdOnly_DefaultsUseWamBrokerToFalse()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            deviceCodeFlowCallbackMethod: static _ => Task.CompletedTask,
            applicationClientId: customAppId);

        Assert.False(GetUseWamBrokerField(provider));
    }

    [Fact]
    public void Ctor_WithDeviceCodeCallback_NoAppClientId_EnablesWamBroker()
    {
        // When applicationClientId is omitted, the provider uses the SqlClient first-party app id
        // and therefore WAM broker must be enabled, regardless of the useWamBroker default.
        var provider = new ActiveDirectoryAuthenticationProvider(
            deviceCodeFlowCallbackMethod: static _ => Task.CompletedTask);

        Assert.True(GetUseWamBrokerField(provider));
    }

    [Fact]
    public void Ctor_RegisteredAsProvider_PreservesUseWamBrokerSetting()
    {
        // A provider created with useWamBroker=true and registered via SqlAuthenticationProvider
        // must retain its WAM broker setting after registration (i.e. SetProvider must not wrap
        // or replace the instance).
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(customAppId, useWamBroker: true);

        // Save the original provider so we can restore it after the assertion, otherwise we
        // leak state into other tests that depend on the default provider being installed
        // (e.g. DefaultAuthProviderTests.AuthProviderInstalled).
        SqlAuthenticationProvider? original =
            SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);
        try
        {
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, provider);

            var retrieved = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive)
                as ActiveDirectoryAuthenticationProvider;
            Assert.NotNull(retrieved);
            Assert.Same(provider, retrieved);
            Assert.True(GetUseWamBrokerField(retrieved));
        }
        finally
        {
            if (original is not null)
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, original);
            }
        }
    }

    /// <summary>
    /// Reads the private <c>_useWamBroker</c> field from the provider via reflection.
    /// The field is intentionally private because it is an internal implementation detail
    /// of the WAM broker plumbing, so tests must reach in to verify the constructor logic.
    /// </summary>
    private static bool GetUseWamBrokerField(ActiveDirectoryAuthenticationProvider provider)
    {
        FieldInfo? field = typeof(ActiveDirectoryAuthenticationProvider)
            .GetField("_useWamBroker", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        object? value = field!.GetValue(provider);
        Assert.NotNull(value);
        return (bool)value!;
    }
}

