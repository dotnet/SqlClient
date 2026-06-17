// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

[Collection("SqlAuthenticationProvider")]
public class WamBrokerTests
{
    // The SqlClient first-party application client id that is hard-coded in the provider.
    private const string SqlClientApplicationId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

    // A fixed, deterministic stand-in for a caller-supplied application id. Hard-coded (instead
    // of Guid.NewGuid()) so test outcomes don't depend on RNG and so a single point asserts
    // that this value differs from the SqlClient first-party id.
    private const string TestCustomAppId = "11111111-2222-3333-4444-555555555555";

    /// <summary>
    /// Defensive guard: <see cref="TestCustomAppId"/> is reused across the entire suite as a
    /// stand-in for a caller-supplied application id. This single assertion makes the
    /// "not the SqlClient first-party id" precondition explicit instead of relying on RNG.
    /// </summary>
    [Fact]
    public void TestCustomAppId_IsNotSqlClientAppId()
    {
        Assert.NotEqual(SqlClientApplicationId, TestCustomAppId);
    }

    // Reads the private _parentActivityOrWindowFunc field. Used to assert downstream effects
    // of SetParentActivityOrWindowFunc without triggering a live MSAL flow.
    private static Func<object>? GetParentActivityOrWindowFunc(ActiveDirectoryAuthenticationProvider provider)
    {
        FieldInfo? field = typeof(ActiveDirectoryAuthenticationProvider).GetField(
            "_parentActivityOrWindowFunc",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return (Func<object>?)field!.GetValue(provider);
    }

    /// <summary>
    /// A <see langword="null"/> callback is treated as "clear any previously installed callback"
    /// and must not throw. This is a deliberate API contract change from the original
    /// <see cref="ArgumentNullException"/> behavior so callers can opt out without recreating
    /// the provider. Asserts the underlying field is reset to <see langword="null"/> so the
    /// provider's downstream consumer (MSAL parameters builder) sees the cleared state.
    /// </summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_Null_ClearsCallback()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        Func<object> first = () => IntPtr.Zero;
        provider.SetParentActivityOrWindowFunc(first);
        Assert.Same(first, GetParentActivityOrWindowFunc(provider));

        provider.SetParentActivityOrWindowFunc(null);
        Assert.Null(GetParentActivityOrWindowFunc(provider));
    }

    /// <summary>A non-null callback installs cleanly, does not throw, and is the value the provider holds.</summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_ValidFunc_DoesNotThrow()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        Func<object> func = () => IntPtr.Zero;
        provider.SetParentActivityOrWindowFunc(func);
        Assert.Same(func, GetParentActivityOrWindowFunc(provider));
    }

    /// <summary>Repeated calls must be supported (no internal locking guard rejects a second set).</summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_CanBeCalledMultipleTimes()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        provider.SetParentActivityOrWindowFunc(() => IntPtr.Zero);
        Func<object> second = () => new IntPtr(12345);
        provider.SetParentActivityOrWindowFunc(second);
        Assert.Same(second, GetParentActivityOrWindowFunc(provider));
    }

    /// <summary>
    /// Last-write-wins: the most recently installed callback is the one the provider exposes
    /// downstream. Verified by reading the private backing field after a sequence of sets
    /// (including a null clear in the middle).
    /// </summary>
    [Fact]
    public void SetParentActivityOrWindowFunc_LastSetWins()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();

        Func<object> firstThrowing = () => throw new InvalidOperationException("first");
        Func<object> secondZero = () => IntPtr.Zero;
        Func<object> finalSeven = () => new IntPtr(7);

        provider.SetParentActivityOrWindowFunc(firstThrowing);
        Assert.Same(firstThrowing, GetParentActivityOrWindowFunc(provider));

        provider.SetParentActivityOrWindowFunc(secondZero);
        Assert.Same(secondZero, GetParentActivityOrWindowFunc(provider));

        // Re-installing a null clears it.
        provider.SetParentActivityOrWindowFunc(null);
        Assert.Null(GetParentActivityOrWindowFunc(provider));

        provider.SetParentActivityOrWindowFunc(finalSeven);
        Assert.Same(finalSeven, GetParentActivityOrWindowFunc(provider));

        // The earlier throwing callback must not resurface.
        Assert.NotSame(firstThrowing, GetParentActivityOrWindowFunc(provider));
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
        var provider = new ActiveDirectoryAuthenticationProvider(TestCustomAppId);

        Assert.False(provider.UseWamBroker,
            "Custom application id without useWamBroker=true must keep WAM broker disabled.");
    }

    /// <summary>
    /// Passing the SqlClient first-party application id to the single-string constructor must
    /// enable WAM broker. The first-party app id is hard-wired to the WAM broker redirect URI,
    /// so callers that opt into it explicitly should get the same behavior as the parameterless
    /// constructor.
    /// </summary>
    [Fact]
    public void Ctor_AppClientId_SqlClientId_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(SqlClientApplicationId);

        Assert.True(provider.UseWamBroker,
            "Single-string ctor with the SqlClient first-party id must enable WAM broker.");
    }

    /// <summary>A caller-supplied application id with explicit opt-in must enable WAM broker.</summary>
    [Fact]
    public void Ctor_AppClientId_UseWamBrokerTrue_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = true,
            });

        Assert.True(provider.UseWamBroker,
            "Custom application id with UseWamBroker=true must enable WAM broker.");
    }

    /// <summary>A caller-supplied application id with explicit opt-out keeps WAM broker disabled.</summary>
    [Fact]
    public void Ctor_AppClientId_UseWamBrokerFalse_DisablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
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
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                DeviceCodeFlowCallback = static _ => Task.CompletedTask,
                ApplicationClientId = TestCustomAppId,
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
        var provider = new ActiveDirectoryAuthenticationProvider(
            deviceCodeFlowCallbackMethod: static _ => Task.CompletedTask,
            applicationClientId: TestCustomAppId);

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
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = true,
            });

        Assert.True(provider.UseWamBroker);
    }

    /// <summary>
    /// Options with <c>ApplicationClientId = null</c> falls back to the SqlClient first-party
    /// id, which always enables WAM broker, regardless of <c>UseWamBroker</c>.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ctor_Options_NullAppId_AlwaysEnablesWamBroker(bool useWamBroker)
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = null,
                UseWamBroker = useWamBroker,
            });

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
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProvider.ProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
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
