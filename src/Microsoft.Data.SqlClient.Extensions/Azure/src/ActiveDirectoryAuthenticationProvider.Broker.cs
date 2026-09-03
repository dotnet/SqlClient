// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Identity.Client;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Cross-platform broker plumbing for <see cref="ActiveDirectoryAuthenticationProvider"/>. The
/// other parts of the partial class live in <c>ActiveDirectoryAuthenticationProvider.cs</c> and
/// <c>ActiveDirectoryAuthenticationProvider.Windows.cs</c>.
/// </summary>
/// <remarks>
/// MSAL routes brokered authentication through the native <c>msalruntime</c> library shipped by
/// the <c>Microsoft.Identity.Client.NativeInterop</c> package. That package only carries native
/// binaries for a subset of runtime identifiers, and the Linux binary additionally links against
/// desktop libraries that are absent from most server and container images. MSAL throws
/// <c>MsalClientException("wam_runtime_init_failed")</c> on the first token request when the
/// native library cannot be loaded, with no fallback to the system browser. This file decides,
/// once per process, whether the broker can actually be used on the current platform so that
/// unsupported runtimes fall back to the browser instead of failing outright.
/// </remarks>
public sealed partial class ActiveDirectoryAuthenticationProvider
{
    // The broker operating system flag matching the current runtime, or None when the broker
    // cannot be used here. Computed at most once because none of the inputs (OS, process
    // architecture, whether the native library can be loaded) change during the process lifetime.
    //
    // Deliberately lazy rather than a plain static field initializer. SqlAuthenticationProviderManager
    // constructs this provider from its own static constructor, so a field initializer would run
    // the Linux probe -- an assembly load plus dlopen of libmsalruntime.so and its libwebkit2gtk,
    // libsecret and libx11 dependencies -- during class initialization for every process using
    // Entra ID authentication. Managed identity, workload identity, service principal and default
    // credential flows never build a PublicClientApplication, so they must not pay for it, and
    // triggering an assembly load from a nested type initializer is a loader-ordering hazard.
    private static readonly Lazy<BrokerOptions.OperatingSystems> s_brokerOperatingSystem =
        new(DetectBrokerOperatingSystem);

    /// <summary>
    /// The <see cref="BrokerOptions.OperatingSystems"/> flag supported by the current runtime, or
    /// <see cref="BrokerOptions.OperatingSystems.None"/> when brokered authentication is not
    /// available. Exposed as <c>internal</c> for tests.
    /// </summary>
    internal static BrokerOptions.OperatingSystems SupportedBrokerOperatingSystem =>
        s_brokerOperatingSystem.Value;

    /// <summary>
    /// Whether this provider instance will actually enable the broker: it must be opted in (see
    /// <see cref="UseWamBroker"/>) and running on a platform where the broker is available.
    /// Exposed as <c>internal</c> for tests.
    /// </summary>
    internal bool IsBrokerEnabledOnCurrentPlatform =>
        _useWamBroker && s_brokerOperatingSystem.Value != BrokerOptions.OperatingSystems.None;

    /// <summary>
    /// Resolves the MSAL redirect URI for this provider instance.
    /// </summary>
    /// <param name="authenticationMethod">
    /// The authentication method being attempted, used to describe failures.
    /// </param>
    /// <remarks>
    /// Each broker uses a different redirect URI scheme, and the Entra ID app registration for the
    /// configured client id must list the URI selected here.
    /// </remarks>
    /// <exception cref="Extensions.Azure.AuthenticationException">
    /// The host is a bundled macOS application using SqlClient's first-party application id. Such
    /// applications need a redirect URI derived from their own bundle identifier, which can only
    /// be registered on an app registration the caller controls.
    /// </exception>
    internal string GetRedirectUri(SqlAuthenticationMethod authenticationMethod)
    {
        if (IsBrokerEnabledOnCurrentPlatform)
        {
            switch (s_brokerOperatingSystem.Value)
            {
                case BrokerOptions.OperatingSystems.Windows:
                    return s_wamBrokerRedirectUriPrefix + _applicationClientId;

                case BrokerOptions.OperatingSystems.Linux:
                    // The Linux broker requires the native client redirect URI, which must be
                    // explicitly enabled on the app registration.
                    return s_nativeClientRedirectUri;

                case BrokerOptions.OperatingSystems.OSX:
                    return GetMacBrokerRedirectUri(authenticationMethod);
            }
        }

        #if NETFRAMEWORK
        // .NET Framework falls back to the embedded WebView, which uses the native client
        // redirect URI rather than a loopback address.
        return s_nativeClientRedirectUri;
        #else
        return s_systemBrowserRedirectUri;
        #endif
    }

    /// <summary>
    /// Resolves the redirect URI expected by the macOS broker for the current host process.
    /// </summary>
    /// <remarks>
    /// The macOS broker keys the redirect URI off the host application rather than the client id.
    /// A bundled application must use <c>msauth.[bundle_id]://auth</c>, while a loose executable
    /// or script uses the fixed <c>msauth.com.msauth.unsignedapp://auth</c> URI. Because a bundle
    /// identifier is specific to the calling application, it can never be registered on SqlClient's
    /// first-party app registration, so that combination is rejected with an actionable error.
    /// </remarks>
    private string GetMacBrokerRedirectUri(SqlAuthenticationMethod authenticationMethod)
    {
        string? bundleIdentifier = Interop.NSBundle.TryGetMainBundleIdentifier();

        if (bundleIdentifier is null)
        {
            return s_macUnsignedAppRedirectUri;
        }

        // Device code flow never reaches the broker: MSAL's DeviceCodeRequest has no broker
        // strategy, and the user completes sign-in on a separate device. Rejecting it here would
        // remove the one interactive flow a bundled application can still use with the built-in
        // application id, so fall back to a redirect URI that is registered on it.
        if (_applicationClientId == s_sqlClientApplicationId)
        {
            if (authenticationMethod == SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)
            {
                return s_macUnsignedAppRedirectUri;
            }

            throw new Extensions.Azure.AuthenticationException(
                authenticationMethod,
                $"The macOS authentication broker requires the redirect URI " +
                $"'{s_macBrokerRedirectUriPrefix}{bundleIdentifier}{s_macBrokerRedirectUriSuffix}' for bundled applications, " +
                $"which cannot be registered on SqlClient's built-in application id. Register that redirect URI on your " +
                $"own Entra ID application and supply its client id via " +
                $"{nameof(ActiveDirectoryAuthenticationProviderOptions)}.{nameof(ActiveDirectoryAuthenticationProviderOptions.ApplicationClientId)}, " +
                $"or use {nameof(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)} instead.");
        }

        return s_macBrokerRedirectUriPrefix + bundleIdentifier + s_macBrokerRedirectUriSuffix;
    }

    private static BrokerOptions.OperatingSystems DetectBrokerOperatingSystem()
    {
        // Microsoft.Identity.Client.NativeInterop ships msalruntime for win-x86, win-x64 and
        // win-arm64, so every Windows architecture we support is covered.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return BrokerOptions.OperatingSystems.Windows;
        }

        // osx-x64 and osx-arm64 binaries both ship, and the broker itself is provided by the
        // Company Portal app rather than a native dependency of our own.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return BrokerOptions.OperatingSystems.OSX;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && IsLinuxBrokerRuntimeAvailable())
        {
            return BrokerOptions.OperatingSystems.Linux;
        }

        return BrokerOptions.OperatingSystems.None;
    }

    /// <summary>
    /// Whether the native msalruntime binary can be loaded on the current Linux runtime.
    /// </summary>
    /// <remarks>
    /// Microsoft.Identity.Client.NativeInterop only ships <c>linux-x64</c>. There is no
    /// <c>linux-arm64</c>, <c>linux-arm</c> or <c>linux-musl-*</c> binary, and the musl RIDs do
    /// not fall back to <c>linux-x64</c> in the RID graph. Even on <c>linux-x64</c> the binary
    /// links against <c>libwebkit2gtk</c>, <c>libsecret</c> and <c>libX11</c>, which are absent
    /// from most server and container images. Rather than guess at that dependency list, this
    /// attempts the load MSAL would perform and treats a failure as "no broker here".
    /// </remarks>
    private static bool IsLinuxBrokerRuntimeAvailable()
    {
        #if NETFRAMEWORK
        // .NET Framework only runs on Windows, so this is unreachable.
        return false;
        #else
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return false;
        }

        // RuntimeInformation.RuntimeIdentifier is not available on netstandard2.0, but it is a
        // thin wrapper over this AppContext entry, which the host populates on .NET Core and
        // later. A null value means we cannot tell, in which case the x64 check above is the
        // best signal we have.
        string? runtimeIdentifier = AppContext.GetData("RUNTIME_IDENTIFIER") as string;
        if (runtimeIdentifier is not null &&
            runtimeIdentifier.IndexOf("musl", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return false;
        }

        return CanLoadMsalRuntime();
        #endif
    }

    // The name Microsoft.Identity.Client.NativeInterop's P/Invokes use for the native library on
    // linux-x64. That package picks an architecture-specific name ("msalruntime_x86",
    // "msalruntime_arm64", and so on), so this value is only correct because the Linux broker is
    // restricted to x64 above. It resolves to "libmsalruntime.so".
    private const string LinuxMsalRuntimeLibraryName = "msalruntime";

    /// <summary>
    /// Attempts to load the native msalruntime library the same way MSAL's P/Invokes would.
    /// </summary>
    /// <remarks>
    /// This resolves through the <c>Microsoft.Identity.Client.NativeInterop</c> assembly so that
    /// the runtime applies the same deps.json and RID-specific probing paths a <c>DllImport</c>
    /// from that assembly would use. On Linux that is the whole story: the package's own explicit
    /// preload builds its path with Windows separators, so the file is never found there and
    /// resolution falls through to the plain <c>DllImport</c>. Loading the library is side-effect
    /// free, because the global <c>MSALRUNTIME_Startup</c> initialization only happens when MSAL
    /// constructs its <c>NativeInterop.Core</c>, and <c>dlopen</c> is reference counted so the
    /// later load reuses this handle. <c>System.Runtime.InteropServices.NativeLibrary</c> is not
    /// part of netstandard2.0, so it is invoked reflectively; when it is unavailable (which
    /// includes .NET Framework, where this path is unreachable anyway) the broker is left
    /// disabled.
    /// </remarks>
    private static bool CanLoadMsalRuntime()
    {
        try
        {
            Assembly? nativeInterop = Type
                .GetType("Microsoft.Identity.Client.NativeInterop.Core, Microsoft.Identity.Client.NativeInterop", throwOnError: false)
                ?.Assembly;
            if (nativeInterop is null)
            {
                return false;
            }

            Type? nativeLibrary = Type.GetType(
                "System.Runtime.InteropServices.NativeLibrary, System.Runtime.InteropServices",
                throwOnError: false);
            MethodInfo? tryLoad = nativeLibrary
                ?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(static method =>
                    method.Name == "TryLoad" &&
                    method.GetParameters() is { Length: 4 } parameters &&
                    parameters[0].ParameterType == typeof(string) &&
                    parameters[1].ParameterType == typeof(Assembly));
            if (tryLoad is null)
            {
                return false;
            }

            object?[] arguments = new object?[] { LinuxMsalRuntimeLibraryName, nativeInterop, null, null };

            return tryLoad.Invoke(null, arguments) is true;
        }
        catch (Exception)
        {
            // A missing dependency surfaces as a TargetInvocationException wrapping
            // DllNotFoundException; anything else means we could not determine support. Either
            // way, leaving the broker disabled keeps the browser fallback available.
            return false;
        }
    }
}
