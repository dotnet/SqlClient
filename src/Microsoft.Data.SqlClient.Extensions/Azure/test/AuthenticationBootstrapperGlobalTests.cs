// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Tests for the SqlClient-internal <c>AuthenticationBootstrapper</c> that run the full bootstrap
/// and so mutate the process-wide <c>AuthenticationProviderRegistry.Instance</c>. They are
/// serialized via the shared <c>SqlAuthenticationProvider</c> collection.
/// </summary>
/// <remarks>
/// Like <see cref="AuthenticationBootstrapperTests"/>, these require the Azure extension assembly
/// to be present (guaranteed only by this test project). The non-global, side-effect-free tests
/// live in <see cref="AuthenticationBootstrapperTests"/>.
/// </remarks>
[Collection("SqlAuthenticationProviderGlobal")]
public class AuthenticationBootstrapperGlobalTests
{
    public AuthenticationBootstrapperGlobalTests()
    {
        // Precondition: the Azure extension assembly must be present for these tests to be
        // meaningful. This is what distinguishes this project from the core UnitTests.
        Assert.NotNull(Assembly.Load("Microsoft.Data.SqlClient.Extensions.Azure"));
    }

    // Verify that the bootstrapper installs the Azure auth provider for all AAD/Entra
    // authentication methods, and not for any other methods.
    //
    // This project configures neither applicationClientId nor useWamBroker (it has no app.config
    // overrides), so the bootstrapper constructs the Azure extension's
    // ActiveDirectoryAuthenticationProvider via its parameterless constructor and registers that
    // single instance for every Active Directory method.
    [Fact]
    public void Bootstrap_InstallsAzureProvider_ForAllActiveDirectoryMethods()
    {
        // Under the lazy-bootstrap model the SqlClient bootstrapper only runs on first federated
        // authentication. Force it to run so the Azure extension provider is discovered and
        // registered.
        //
        // GOTCHA: This modifies global state.
        Bootstrap();

        // Iterate over all authentication methods rather than specifying them via Theory data so
        // that we detect any new methods that don't meet our expectations.
        #if NET
        var methods = Enum.GetValues<SqlAuthenticationMethod>();
        #else
        var methods = Enum.GetValues(typeof(SqlAuthenticationMethod)).Cast<SqlAuthenticationMethod>();
        #endif

        foreach (var method in methods)
        {
            SqlAuthenticationProvider? provider = SqlAuthenticationProvider.GetProvider(method);

            switch (method)
            {
                #pragma warning disable 0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryPassword:
                #pragma warning restore 0618 // Type or member is obsolete
                case SqlAuthenticationMethod.ActiveDirectoryIntegrated:
                case SqlAuthenticationMethod.ActiveDirectoryInteractive:
                case SqlAuthenticationMethod.ActiveDirectoryServicePrincipal:
                case SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow:
                case SqlAuthenticationMethod.ActiveDirectoryManagedIdentity:
                case SqlAuthenticationMethod.ActiveDirectoryMSI:
                case SqlAuthenticationMethod.ActiveDirectoryDefault:
                case SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity:
                {
                    Assert.NotNull(provider);
                    Assert.IsType<ActiveDirectoryAuthenticationProvider>(provider);
                    break;
                }
                default:
                {
                    // There is either no provider installed, or it is not ours.
                    if (provider is not null)
                    {
                        Assert.IsNotType<ActiveDirectoryAuthenticationProvider>(provider);
                    }
                    break;
                }
            }
        }
    }

    // Forces the MDS bootstrapper to run by invoking its internal static Bootstrap() method via
    // reflection. This project does not have InternalsVisibleTo from Microsoft.Data.SqlClient, so
    // the method cannot be called directly.
    //
    // NOTE: This perturbs GLOBAL state -- Bootstrap() seeds the process-wide
    // AuthenticationProviderRegistry.Instance (installing the Azure provider for the AD methods).
    // That is why this class lives in the [Collection("SqlAuthenticationProviderGlobal")] collection,
    // which serializes it with the other tests that mutate the shared registry.
    //
    // TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/41888):
    // Once PR #4385 completes (signing Azure/Azure.Test for internal Package-mode CI builds), grant
    // this project InternalsVisibleTo from Microsoft.Data.SqlClient and replace this reflection
    // with a direct call to AuthenticationBootstrapper.Bootstrap().
    private static void Bootstrap()
    {
        Type? bootstrapper = Type.GetType(
            "Microsoft.Data.SqlClient.AuthenticationBootstrapper, Microsoft.Data.SqlClient");
        Assert.NotNull(bootstrapper);

        MethodInfo? bootstrap = bootstrapper!.GetMethod(
            "Bootstrap",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(bootstrap);

        bootstrap!.Invoke(null, null);
    }
}
