// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Tests for the MDS-internal <c>AuthenticationBootstrapper</c> that require the Azure extension
/// assembly to be present but do NOT mutate global state. These exercise
/// <c>CreateAzureAuthenticationProvider</c>'s constructor selection against the real Azure provider,
/// so they need no <c>[Collection]</c> serialization.
/// </summary>
/// <remarks>
/// This is the only test project that references — and therefore guarantees the presence of —
/// <c>Microsoft.Data.SqlClient.Extensions.Azure</c>, so the bootstrapper's Azure-extension
/// discovery can be exercised for real here. The core UnitTests project, where the Azure extension
/// is absent, covers the bootstrapper's Azure-absent paths instead.
///
/// The global-state-mutating tests (which run the full bootstrap) live in
/// <see cref="AuthenticationBootstrapperGlobalTests"/> (AuthenticationBootstrapperGlobalTests.cs).
/// </remarks>
public class AuthenticationBootstrapperTests
{
    public AuthenticationBootstrapperTests()
    {
        // Precondition: the Azure extension assembly must be present for these tests to be
        // meaningful. This is what distinguishes this project from the core UnitTests.
        Assert.NotNull(Assembly.Load("Microsoft.Data.SqlClient.Extensions.Azure"));
    }

    // CreateAzureAuthenticationProvider -- constructor selection against the REAL Azure extension.
    //
    // These tests drive the same logic the bootstrapper runs inside LoadAzureExtensionProvider,
    // using the applicationClientId / useWamBroker values that the <SqlClientAuthenticationProviders>
    // app.config section would produce. Because the real Azure extension always exposes
    // ActiveDirectoryAuthenticationProviderOptions, the legacy-(string) fallback, the
    // "no compatible ctor -> null", and the "useWamBroker but Options missing -> throw" paths are
    // NOT reachable here; those are covered with stubs in the core UnitTests.

    // The SqlClient first-party application client id hard-coded in the provider (always enables
    // WAM broker).
    private const string SqlClientApplicationId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

    // A fixed stand-in for a caller-/config-supplied application id, distinct from the first-party id.
    private const string TestCustomAppId = "11111111-2222-3333-4444-555555555555";

    // No config (applicationClientId and useWamBroker both unset) -> parameterless ctor -> the
    // first-party id, which enables WAM broker.
    [Fact]
    public void CreateAzureProvider_NoConfig_UsesParameterlessCtor()
    {
        var provider = Assert.IsType<ActiveDirectoryAuthenticationProvider>(
            CreateAzureAuthenticationProvider(applicationClientId: null, useWamBroker: null));

        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker);
    }

    // applicationClientId only -> Options ctor; UseWamBroker stays at its default (false) for a
    // caller-supplied id.
    [Fact]
    public void CreateAzureProvider_AppClientIdOnly_UsesOptionsCtor_WamDisabled()
    {
        var provider = Assert.IsType<ActiveDirectoryAuthenticationProvider>(
            CreateAzureAuthenticationProvider(applicationClientId: TestCustomAppId, useWamBroker: null));

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
        Assert.False(provider.UseWamBroker);
    }

    // applicationClientId + useWamBroker=true -> Options ctor; both are forwarded.
    [Fact]
    public void CreateAzureProvider_AppClientIdAndUseWamBrokerTrue_UsesOptionsCtor_WamEnabled()
    {
        var provider = Assert.IsType<ActiveDirectoryAuthenticationProvider>(
            CreateAzureAuthenticationProvider(applicationClientId: TestCustomAppId, useWamBroker: true));

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker);
    }

    // applicationClientId + useWamBroker=false -> Options ctor; the explicit opt-out is honored.
    [Fact]
    public void CreateAzureProvider_AppClientIdAndUseWamBrokerFalse_UsesOptionsCtor_WamDisabled()
    {
        var provider = Assert.IsType<ActiveDirectoryAuthenticationProvider>(
            CreateAzureAuthenticationProvider(applicationClientId: TestCustomAppId, useWamBroker: false));

        Assert.Equal(TestCustomAppId, provider.ApplicationClientId);
        Assert.False(provider.UseWamBroker);
    }

    // useWamBroker=true with no applicationClientId -> Options ctor; the id falls back to the
    // first-party id, which enables WAM broker.
    [Fact]
    public void CreateAzureProvider_UseWamBrokerOnly_UsesOptionsCtor_WamEnabled()
    {
        var provider = Assert.IsType<ActiveDirectoryAuthenticationProvider>(
            CreateAzureAuthenticationProvider(applicationClientId: null, useWamBroker: true));

        Assert.Equal(SqlClientApplicationId, provider.ApplicationClientId);
        Assert.True(provider.UseWamBroker);
    }

    // Invokes the MDS-internal AuthenticationBootstrapper.CreateAzureAuthenticationProvider via
    // reflection, passing the REAL Azure provider and options types. Unwraps the reflection wrapper
    // so a test observes the real exception type, if any.
    //
    // NOTE: This reflection is only needed because this project does not have InternalsVisibleTo
    // from Microsoft.Data.SqlClient. This call has no global side effects -- it just returns a new
    // provider instance.
    //
    // TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/41888):
    // Once PR #4385 completes (signing Azure/Azure.Test for internal Package-mode CI builds), grant
    // this project InternalsVisibleTo from Microsoft.Data.SqlClient and replace this reflection
    // with a direct call to AuthenticationBootstrapper.CreateAzureAuthenticationProvider.
    private static SqlAuthenticationProvider? CreateAzureAuthenticationProvider(
        string? applicationClientId,
        bool? useWamBroker)
    {
        Type? bootstrapper = Type.GetType(
            "Microsoft.Data.SqlClient.AuthenticationBootstrapper, Microsoft.Data.SqlClient");
        Assert.NotNull(bootstrapper);

        MethodInfo? method = bootstrapper!.GetMethod(
            "CreateAzureAuthenticationProvider",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method);

        try
        {
            return (SqlAuthenticationProvider?)method!.Invoke(
                null,
                [
                    typeof(ActiveDirectoryAuthenticationProvider),
                    typeof(ActiveDirectoryAuthenticationProviderOptions),
                    applicationClientId,
                    useWamBroker,
                ]);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}
