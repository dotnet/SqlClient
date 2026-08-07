// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

[Collection("SqlAuthenticationProvider")]
public class WamBrokerTests
{
    // The SqlClient first-party application client id that is hard-coded in the provider.
    private const string SqlClientApplicationId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

    // MSAL's fixed Windows broker redirect URI prefix. Must match the constant in the provider.
    private const string BrokerRedirectUriPrefix = "ms-appx-web://microsoft.aad.brokerplugin/";

    // Redirect URI required by the Linux broker. Must match the constant in the provider.
    private const string NativeClientRedirectUri = "https://login.microsoftonline.com/common/oauth2/nativeclient";

    // Redirect URI the macOS broker expects from a non-bundled executable or script. Must match
    // the constant in the provider.
    private const string MacUnsignedAppRedirectUri = "msauth.com.msauth.unsignedapp://auth";

    // A fixed, deterministic stand-in for a caller-supplied application id. Hard-coded (instead
    // of Guid.NewGuid()) so test outcomes don't depend on RNG and so a single point asserts
    // that this value differs from the SqlClient first-party id.
    private const string TestCustomAppId = "11111111-2222-3333-4444-555555555555";

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

    /// <summary>
    /// The constructor uses the SqlClient first-party application id, which always
    /// enables WAM broker mode regardless of any opt-in flag.
    /// </summary>
    [Fact]
    public void Ctor_ApplicationClientId_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(SqlClientApplicationId);
        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker,
            "Constructor with SqlClient first-party application id must enable WAM broker.");
    }

    /// <summary>
    /// The parameterless constructor uses the SqlClient first-party application id, which always
    /// enables WAM broker mode regardless of any opt-in flag.
    /// </summary>
    [Fact]
    public void Ctor_Default_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();
        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker,
            "Default ctor must enable WAM broker (uses SqlClient first-party application id).");
    }

    /// <summary>A caller-supplied application id without explicit opt-in must NOT enable WAM broker.</summary>
    [Fact]
    public void Ctor_AppClientId_DefaultsUseWamBrokerToFalse()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(TestCustomAppId);

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
        Assert.False(provider.UseWamBroker,
            "Custom application id without useWamBroker=true must keep WAM broker disabled.");
    }

    /// <summary>
    /// Mirrors the previous test for the <see cref="ActiveDirectoryAuthenticationProviderOptions"/>
    /// constructor: a caller (or app.config) that sets only <c>ApplicationClientId</c> and skips
    /// <c>UseWamBroker</c> must get the documented default of <see langword="false"/>. This is
    /// the contract <c>SqlAuthenticationProviderManager</c> relies on when reflecting onto the
    /// Options ctor and only forwarding the properties that were explicitly configured.
    /// </summary>
    [Fact]
    public void Ctor_Options_AppClientIdOnly_DefaultsUseWamBrokerToFalse()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                // UseWamBroker intentionally left at its default (false).
            });

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
        Assert.False(provider.UseWamBroker,
            "Options ctor with ApplicationClientId set and UseWamBroker omitted must keep WAM broker disabled.");
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

        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker,
            "Single-string ctor with the SqlClient first-party id must enable WAM broker.");
    }

    /// <summary>A caller-supplied application id with explicit opt-in must enable WAM broker.</summary>
    [Fact]
    public void Ctor_AppClientId_UseWamBrokerTrue_EnablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = true,
            });

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker,
            "Custom application id with UseWamBroker=true must enable WAM broker.");
    }

    /// <summary>A caller-supplied application id with explicit opt-out keeps WAM broker disabled.</summary>
    [Fact]
    public void Ctor_AppClientId_UseWamBrokerFalse_DisablesWamBroker()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = false,
            });

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
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
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = SqlClientApplicationId,
                UseWamBroker = false,
            });

        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
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
            new ActiveDirectoryAuthenticationProviderOptions
            {
                DeviceCodeFlowCallback = static _ => Task.CompletedTask,
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = true,
            });

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
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
        Assert.NotEqual(SqlClientApplicationId, provider.ApplicationClientId);
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
        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
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
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = true,
            });

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
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
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = null,
                UseWamBroker = useWamBroker,
            });

        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
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
            () => new ActiveDirectoryAuthenticationProvider((ActiveDirectoryAuthenticationProviderOptions)null!));
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
            new ActiveDirectoryAuthenticationProviderOptions
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
            Assert.Equal(TestCustomAppId, retrieved!.ApplicationClientId);
            Assert.True(retrieved.UseWamBroker);
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
    /// The broker is supported on Windows, macOS and x64 glibc Linux where the native
    /// msalruntime binary can be loaded. This asserts the platform detection agrees with the
    /// runtime we are executing on, so the matrix legs each cover their own platform.
    /// </summary>
    [Fact]
    public void SupportedBrokerOperatingSystem_MatchesCurrentPlatform()
    {
        BrokerOptions.OperatingSystems actual =
            ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal(BrokerOptions.OperatingSystems.Windows, actual);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // Microsoft.Identity.Client.NativeInterop ships both osx-x64 and osx-arm64 binaries,
            // so the broker is available on every macOS host.
            Assert.Equal(BrokerOptions.OperatingSystems.OSX, actual);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // Microsoft.Identity.Client.NativeInterop only ships a linux-x64 msalruntime, and
            // that binary needs desktop libraries most images lack, so unsupported runtimes must
            // fall back to the system browser.
            string? rid = AppContext.GetData("RUNTIME_IDENTIFIER") as string;
            bool architectureSupported =
                RuntimeInformation.ProcessArchitecture == Architecture.X64 &&
                (rid is null || rid.IndexOf("musl", StringComparison.OrdinalIgnoreCase) < 0);

            if (!architectureSupported)
            {
                Assert.Equal(BrokerOptions.OperatingSystems.None, actual);
            }
            else
            {
                // Whether msalruntime loads depends on which packages the agent has installed,
                // so resolve it independently here rather than hard-coding an expectation. This
                // is the only leg that exercises the native probe, so it has to pin the
                // correlation instead of accepting either answer.
                Assert.Equal(
                    CanLoadMsalRuntime() ? BrokerOptions.OperatingSystems.Linux : BrokerOptions.OperatingSystems.None,
                    actual);
            }
        }
        else
        {
            Assert.Equal(BrokerOptions.OperatingSystems.None, actual);
        }
    }

    /// <summary>
    /// The detected broker operating system must be a single flag that MSAL will accept, never a
    /// combination. Guards against a future edit that ORs multiple platforms together.
    /// </summary>
    [Fact]
    public void SupportedBrokerOperatingSystem_IsASingleFlag()
    {
        BrokerOptions.OperatingSystems actual =
            ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem;

        Assert.Contains(actual, new[]
        {
            BrokerOptions.OperatingSystems.None,
            BrokerOptions.OperatingSystems.Windows,
            BrokerOptions.OperatingSystems.Linux,
            BrokerOptions.OperatingSystems.OSX,
        });
    }

    /// <summary>
    /// Opting out of the broker must keep it disabled no matter which platform the tests run on.
    /// </summary>
    [Fact]
    public void IsBrokerEnabledOnCurrentPlatform_CustomAppIdWithoutOptIn_IsFalse()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(TestCustomAppId);

        Assert.False(provider.IsBrokerEnabledOnCurrentPlatform);
    }

    /// <summary>
    /// The broker is enabled for the first-party application id on every platform where the
    /// native msalruntime binary is available, which now includes Linux and macOS.
    /// </summary>
    [Fact]
    public void IsBrokerEnabledOnCurrentPlatform_FirstPartyAppId_TracksPlatformSupport()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();

        Assert.True(provider.UseWamBroker);
        Assert.Equal(
            ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem
                != BrokerOptions.OperatingSystems.None,
            provider.IsBrokerEnabledOnCurrentPlatform);
    }

    /// <summary>
    /// Each broker expects its own redirect URI scheme. Windows uses the WAM URI suffixed with
    /// the client id, Linux requires the native client URI, and macOS derives the URI from the
    /// host application bundle.
    /// </summary>
    [Fact]
    public void GetRedirectUri_BrokerEnabled_UsesPlatformBrokerRedirectUri()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();

        if (!provider.IsBrokerEnabledOnCurrentPlatform)
        {
            // Broker unsupported on this runtime; covered by the fallback test below.
            return;
        }

        Assert.Equal(
            ExpectedBrokerRedirectUri(SqlClientApplicationId),
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    /// <summary>
    /// The Linux broker rejects the WAM redirect URI; it requires the native client URI to be
    /// enabled on the app registration. Pinning the value here guards against reintroducing the
    /// WAM URI on non-Windows platforms.
    /// </summary>
    [Fact]
    public void GetRedirectUri_LinuxBroker_UsesNativeClientRedirectUri()
    {
        if (ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem
            != BrokerOptions.OperatingSystems.Linux)
        {
            return;
        }

        var provider = new ActiveDirectoryAuthenticationProvider();

        Assert.Equal(
            NativeClientRedirectUri,
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    /// <summary>
    /// The macOS broker derives the redirect URI from the host application bundle. The test host
    /// is not a bundled application, so it must get the fixed unsigned-app URI.
    /// </summary>
    [Fact]
    public void GetRedirectUri_MacBroker_NonBundledHost_UsesUnsignedAppRedirectUri()
    {
        if (ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem
            != BrokerOptions.OperatingSystems.OSX)
        {
            return;
        }

        var provider = new ActiveDirectoryAuthenticationProvider();

        Assert.Equal(
            MacUnsignedAppRedirectUri,
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    /// <summary>
    /// Without the broker, modern .NET uses the loopback system-browser redirect URI. .NET
    /// Framework keeps the native client URI used by the embedded WebView.
    /// </summary>
    [Fact]
    public void GetRedirectUri_BrokerDisabled_UsesNonBrokerRedirectUri()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(TestCustomAppId);

        Assert.False(provider.IsBrokerEnabledOnCurrentPlatform);

        #if NETFRAMEWORK
        Assert.Equal(
            NativeClientRedirectUri,
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive));
        #else
        Assert.Equal(
            "http://localhost",
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive));
        #endif
    }

    /// <summary>
    /// A custom application id that opts into the broker must get a redirect URI suffixed with
    /// its own client id, not the SqlClient first-party id. Only Windows embeds the client id in
    /// the redirect URI, so the other platforms are unaffected by the client id in use.
    /// </summary>
    [Fact]
    public void GetRedirectUri_CustomAppIdWithOptIn_UsesCustomClientId()
    {
        var provider = new ActiveDirectoryAuthenticationProvider(
            new ActiveDirectoryAuthenticationProviderOptions
            {
                ApplicationClientId = TestCustomAppId,
                UseWamBroker = true,
            });

        if (!provider.IsBrokerEnabledOnCurrentPlatform)
        {
            return;
        }

        Assert.Equal(
            ExpectedBrokerRedirectUri(TestCustomAppId),
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive));
    }

    /// <summary>
    /// The redirect URI must never carry a client id on Linux or macOS. Those brokers key off a
    /// fixed URI or the host bundle, so a client-id-suffixed URI would silently fail to match the
    /// app registration.
    /// </summary>
    [Fact]
    public void GetRedirectUri_NonWindowsBroker_DoesNotEmbedClientId()
    {
        BrokerOptions.OperatingSystems brokerOs =
            ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem;

        if (brokerOs != BrokerOptions.OperatingSystems.Linux &&
            brokerOs != BrokerOptions.OperatingSystems.OSX)
        {
            return;
        }

        var provider = new ActiveDirectoryAuthenticationProvider();
        string redirectUri = provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive);

        Assert.DoesNotContain(SqlClientApplicationId, redirectUri, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(BrokerRedirectUriPrefix, redirectUri, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The redirect URI is part of the PublicClientApplication cache key, so it must be stable
    /// across calls for a given provider instance.
    /// </summary>
    [Fact]
    public void GetRedirectUri_IsStableAcrossCalls()
    {
        var provider = new ActiveDirectoryAuthenticationProvider();

        Assert.Equal(
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryInteractive),
            provider.GetRedirectUri(SqlAuthenticationMethod.ActiveDirectoryIntegrated));
    }

    /// <summary>
    /// The two-argument AuthenticationException constructor must carry the authentication method
    /// on the exception, not just interpolate it into the message. ADP.CreateSqlException
    /// surfaces Method as SqlError.Procedure, and AcquireTokenAsync no longer re-wraps
    /// AuthenticationException, so this is the only thing keeping Procedure from reading
    /// "NotSpecified".
    /// </summary>
    [Fact]
    public void AuthenticationException_TwoArgumentConstructor_PreservesMethod()
    {
        var exception = new AuthenticationException(
            SqlAuthenticationMethod.ActiveDirectoryInteractive,
            "test message");

        Assert.Equal(SqlAuthenticationMethod.ActiveDirectoryInteractive, exception.Method);
        Assert.False(exception.ShouldRetry);
        Assert.Equal(0, exception.RetryPeriod);
        Assert.Contains("test message", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Independently resolves whether the native msalruntime library can be loaded, mirroring
    /// what the provider does but without reusing its implementation. Only meaningful on
    /// linux-x64, the single runtime identifier the library ships for on Linux.
    /// </summary>
    private static bool CanLoadMsalRuntime()
    {
        try
        {
            Assembly? nativeInterop = Type
                .GetType("Microsoft.Identity.Client.NativeInterop.Core, Microsoft.Identity.Client.NativeInterop", throwOnError: false)
                ?.Assembly;

            return nativeInterop is not null &&
                NativeLibrary.TryLoad("msalruntime", nativeInterop, null, out _);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the redirect URI the current platform's broker expects for a given client id.
    /// </summary>
    private static string ExpectedBrokerRedirectUri(string applicationClientId) =>
        ActiveDirectoryAuthenticationProvider.SupportedBrokerOperatingSystem switch
        {
            BrokerOptions.OperatingSystems.Windows => BrokerRedirectUriPrefix + applicationClientId,
            BrokerOptions.OperatingSystems.Linux => NativeClientRedirectUri,
            BrokerOptions.OperatingSystems.OSX => MacUnsignedAppRedirectUri,
            _ => throw new InvalidOperationException("The broker is not supported on this platform."),
        };
}
