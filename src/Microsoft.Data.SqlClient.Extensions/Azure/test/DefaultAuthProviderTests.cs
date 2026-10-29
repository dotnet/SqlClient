// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Configuration;
using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

[Collection(SqlAuthenticationProviderCollection.Name)]
public class DefaultAuthProviderTests
{
    /// <summary>Authentication and attestation resolve the same optional Azure assembly.</summary>
    [Fact]
    public void IndependentLoadersResolveSameAzureAssembly()
    {
        Assembly expected = typeof(ActiveDirectoryAuthenticationProvider).Assembly;
        foreach (Assembly assembly in new[]
        {
            typeof(SqlAuthenticationProvider).Assembly,
            typeof(SqlConnection).Assembly
        })
        {
            Type loader = Assert.IsAssignableFrom<Type>(
                assembly.GetType("Microsoft.Data.SqlClient.AzureExtensionLoader"));
            MethodInfo load = Assert.IsAssignableFrom<MethodInfo>(
                loader.GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null, types: new[] { typeof(string) }, modifiers: null));

            Assert.Same(expected, load.Invoke(null, new object[] { nameof(DefaultAuthProviderTests) }));
        }
    }

    /// <summary>
    /// The installed Azure package fills unregistered methods after configuration failures
    /// without replacing a valid later configuration entry.
    /// </summary>
    [Fact]
    public void AzureDiscoveryContinuesAfterConfigurationFailures()
    {
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(object).AssemblyQualifiedName;
        section.Providers.Add(new ProviderSettings(
            "Active Directory Integrated", typeof(object).AssemblyQualifiedName));
        section.Providers.Add(new ProviderSettings(
            "Active Directory Service Principal", typeof(ConfiguredProvider).AssemblyQualifiedName));
        Type registryType = typeof(SqlAuthenticationProvider).Assembly.GetType(
            "Microsoft.Data.SqlClient.SqlAuthenticationProviderRegistry", throwOnError: true)!;
        object registry = Activator.CreateInstance(registryType, nonPublic: true)!;
        MethodInfo bootstrap = registryType.GetMethod(
            "EnsureBootstrapped", BindingFlags.Instance | BindingFlags.NonPublic)!;

        bootstrap.Invoke(registry, new object?[] { new Func<string, object?>(_ => section), null });

        var providers = Assert.IsAssignableFrom<IDictionary>(
            registryType.GetField("_providers", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(registry));
        foreach (var expected in new[]
        {
            (Method: SqlAuthenticationMethod.ActiveDirectoryIntegrated,
                ProviderType: typeof(ActiveDirectoryAuthenticationProvider)),
            (Method: SqlAuthenticationMethod.ActiveDirectoryServicePrincipal,
                ProviderType: typeof(ConfiguredProvider)),
            (Method: SqlAuthenticationMethod.ActiveDirectoryDefault,
                ProviderType: typeof(ActiveDirectoryAuthenticationProvider)),
        })
        {
            object? registration = providers[expected.Method];
            Assert.NotNull(registration);
            object? provider = registration.GetType()
                .GetProperty("Provider", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(registration);
            Assert.IsType(expected.ProviderType, provider);
        }
    }

    // Verify that our auth provider has been installed for all AAD/Entra
    // authentication methods, and not for any other methods.
    //
    // Note that this isn't testing anything in the Azure package.  It actually
    // tests registry bootstrap and the static GetProvider() and SetProvider()
    // methods in Abstractions. We're testing this here because this project
    // includes the real Azure provider for discovery.
    //
    // TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/41888):
    // Move this test to a more appropriate location once we have one.
    //
    [Fact]
    public void AuthProviderInstalled()
    {
        // Iterate over all authentication methods rather than specifying them
        // via Theory data so that we detect any new methods that don't meet
        // our expectations.
        #if NET
        var methods = Enum.GetValues<SqlAuthenticationMethod>();
        #else
        var methods = Enum.GetValues(typeof(SqlAuthenticationMethod)).Cast<SqlAuthenticationMethod>();
        #endif

        foreach (var method in methods)
        {
            SqlAuthenticationProvider? provider =
                SqlAuthenticationProvider.GetProvider(method);

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
                    Assert.NotNull(provider);
                    Assert.IsType<ActiveDirectoryAuthenticationProvider>(provider);
                    break;

                default:
                    // There is either no provider installed, or it is not ours.
                    if (provider is not null)
                    {
                        Assert.IsNotType<ActiveDirectoryAuthenticationProvider>(provider);
                    }
                    break;
            }
        }

    }

    /// <summary>Provides configuration-owned authentication without making a token request.</summary>
    public sealed class ConfiguredProvider : SqlAuthenticationProvider
    {
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }
}
