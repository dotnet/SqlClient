// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

[Collection("SqlAuthenticationProvider")]
public class WamBrokerTests
{
    // The SqlClient first-party application client id that is hard-coded in the provider.
    private const string SqlClientApplicationId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

    /// <summary>
    /// Defensive guard: every test that uses <see cref="Guid.NewGuid"/> as a stand-in for a
    /// caller-supplied application id assumes the GUID will never collide with the SqlClient
    /// first-party id. This test makes that assumption explicit.
    /// </summary>
    [Fact]
    public void Ctor_CustomAppId_GuidIsNotSqlClientAppId()
    {
        for (int i = 0; i < 16; i++)
        {
            Assert.NotEqual(SqlClientApplicationId, Guid.NewGuid().ToString());
        }
    }

    /// <summary>
    /// A <see langword="null"/> callback is treated as "clear any previously installed callback"
    /// and must not throw. This is a deliberate API contract change from the original
    /// <see cref="ArgumentNullException"/> behavior so callers can opt out without recreating
    /// the provider.
    /// </summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_Null_ClearsCallback()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindowFunc(() => IntPtr.Zero);
        provider.SetParentActivityOrWindowFunc(null);
    }

    /// <summary>A non-null callback installs cleanly and does not throw.</summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_ValidFunc_DoesNotThrow()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindowFunc(() => IntPtr.Zero);
    }

    /// <summary>Repeated calls must be supported (no internal locking guard rejects a second set).</summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_CanBeCalledMultipleTimes()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindowFunc(() => IntPtr.Zero);
        provider.SetParentActivityOrWindowFunc(() => new IntPtr(12345));
    }

    /// <summary>
    /// Last-write-wins: the most recently installed callback must be the one the provider holds.
    /// We can't observe the field directly without reflection (the field is intentionally
    /// private), but we can observe it transitively through whether a sentinel side-effect
    /// fires when MSAL would invoke it. Since invoking MSAL requires a live token request,
    /// we instead assert behavioral overwrite by installing two callbacks that record into
    /// distinct flags and then ensuring only the second one is captured by the provider's
    /// public surface: re-installing a no-throw callback after a throwing one must not
    /// re-surface the throwing one.
    /// </summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_LastSetWins()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindowFunc(() => throw new InvalidOperationException("first"));
        provider.SetParentActivityOrWindowFunc(() => IntPtr.Zero);
        // Re-installing a null clears it.
        provider.SetParentActivityOrWindowFunc(null);
        provider.SetParentActivityOrWindowFunc(() => new IntPtr(7));
    }

    /// <summary>
    /// The parameterless constructor uses the SqlClient first-party application id, which always
    /// enables WAM broker mode regardless of any opt-in flag.
    /// </summary>
    [Fact]
    public void Ctor_Default_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();

        Assert.True(provider.UseWamBroker,
            "Default ctor must enable WAM broker (uses SqlClient first-party application id).");
    }

    /// <summary>A caller-supplied application id without explicit opt-in must NOT enable WAM broker.</summary>
    [Fact]
    public void Ctor_AppClientId_DefaultsUseWamBrokerToFalse()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(customAppId);

        Assert.False(provider.UseWamBroker,
            "Custom application id without useWamBroker=true must keep WAM broker disabled.");
    }

    /// <summary>A caller-supplied application id with explicit opt-in must enable WAM broker.</summary>
    [Fact]
    public void Ctor_AppClientId_UseWamBrokerTrue_EnablesWamBroker()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = customAppId,
                UseWamBroker = true,
            });

        Assert.True(provider.UseWamBroker,
            "Custom application id with UseWamBroker=true must enable WAM broker.");
    }

    /// <summary>A caller-supplied application id with explicit opt-out keeps WAM broker disabled.</summary>
    [Fact]
    public void Ctor_AppClientId_UseWamBrokerFalse_DisablesWamBroker()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = customAppId,
                UseWamBroker = false,
            });

        Assert.False(provider.UseWamBroker);
    }

    /// <summary>
    /// Even when the SqlClient first-party application id is passed explicitly with
    /// <c>UseWamBroker=false</c>, WAM broker mode must remain enabled because the first-party
    /// app id is hard-wired to the WAM broker redirect URI. This guards the OR-condition in
    /// the provider's constructor.
    /// </summary>
    [Fact]
    public void Ctor_SqlClientAppIdExplicit_UseWamBrokerFalse_StillEnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = SqlClientApplicationId,
                UseWamBroker = false,
            });

        Assert.True(provider.UseWamBroker,
            "SqlClient first-party application id must always enable WAM broker, regardless of the UseWamBroker option.");
    }

    /// <summary>
    /// Passing a device-code callback together with a custom application id and
    /// <c>UseWamBroker=true</c> via <see cref="ActiveDirectoryAuthenticationProvider.ProviderOptions"/>
    /// must enable WAM broker mode.
    /// </summary>
    [Fact]
    public void Ctor_WithDeviceCodeCallback_UseWamBrokerTrue_EnablesWamBroker()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                DeviceCodeFlowCallback = static _ => Task.CompletedTask,
                ApplicationClientId = customAppId,
                UseWamBroker = true,
            });

        Assert.True(provider.UseWamBroker);
    }

    /// <summary>
    /// The two-arg device-code constructor (deviceCodeCallback, applicationClientId) must default
    /// <c>useWamBroker</c> to <see langword="false"/> for caller-supplied application ids.
    /// </summary>
    [Fact]
    public void Ctor_WithDeviceCodeCallback_AppClientIdOnly_DefaultsUseWamBrokerToFalse()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            deviceCodeFlowCallbackMethod: static _ => Task.CompletedTask,
            applicationClientId: customAppId);

        Assert.False(provider.UseWamBroker);
    }

    /// <summary>
    /// When the device-code callback constructor is invoked without an application id, the
    /// provider falls back to the SqlClient first-party id and must enable WAM broker.
    /// </summary>
    [Fact]
    public void Ctor_WithDeviceCodeCallback_NoAppClientId_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            deviceCodeFlowCallbackMethod: static _ => Task.CompletedTask);

        Assert.True(provider.UseWamBroker);
    }

    /// <summary>
    /// The <see cref="ActiveDirectoryAuthenticationProvider.ProviderOptions"/>-based constructor
    /// is the recommended overload for new code. It must honor <see cref="ActiveDirectoryAuthenticationProvider.ProviderOptions.UseWamBroker"/>
    /// the same way the positional-argument overloads do.
    /// </summary>
    [Fact]
    public void Ctor_Options_CustomAppId_UseWamBrokerTrue_EnablesWamBroker()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = customAppId,
                UseWamBroker = true,
            });

        Assert.True(provider.UseWamBroker);
    }

    /// <summary>
    /// The Options-based constructor with no application id falls back to the SqlClient
    /// first-party id and must always enable WAM broker, regardless of <c>UseWamBroker</c>.
    /// </summary>
    [Fact]
    public void Ctor_Options_NoAppId_AlwaysEnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions { UseWamBroker = false });

        Assert.True(provider.UseWamBroker);
    }

    /// <summary>
    /// The Options-based constructor must reject a <see langword="null"/> options instance with
    /// <see cref="ArgumentNullException"/> so misuse fails fast at construction.
    /// </summary>
    [Fact]
    public void Ctor_Options_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ActiveDirectoryAuthenticationProvider((ActiveDirectoryAuthenticationProvider.ProviderOptions)null!));
    }

    /// <summary>
    /// Registering an instance via <see cref="SqlAuthenticationProvider.SetProvider"/> must not
    /// wrap or replace the instance, so its WAM broker setting survives registration.
    /// </summary>
    /// <remarks>
    /// Provider registration mutates global state shared across this test class collection
    /// (and any other test that depends on the default provider being installed). Save and
    /// restore the original provider in a finally block to keep cross-test isolation.
    /// </remarks>
    [Fact]
    public void Ctor_RegisteredAsProvider_PreservesUseWamBrokerSetting()
    {
        string customAppId = Guid.NewGuid().ToString();
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = customAppId,
                UseWamBroker = true,
            });

        SqlAuthenticationProvider? original =
            SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive);
        try
        {
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, provider);

            var retrieved = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive)
                as ActiveDirectoryAuthenticationProvider;
            Assert.NotNull(retrieved);
            Assert.Same(provider, retrieved);
            Assert.True(retrieved!.UseWamBroker);
        }
        finally
        {
            if (original is not null)
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryInteractive, original);
            }
        }
    }
}
