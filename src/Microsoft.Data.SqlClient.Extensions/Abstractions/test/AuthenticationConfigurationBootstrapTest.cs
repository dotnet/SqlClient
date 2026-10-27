// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Verifies bootstrap lifecycle with isolated section readers and Azure assembly loaders.
/// </summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationConfigurationBootstrapTest : IDisposable
{
    private readonly AuthenticationFeatureSwitchesHelper _switches = new()
    {
        EnableAppConfig = true,
        EnableAzureExtensionDiscovery = false
    };

    public void Dispose() => _switches.Dispose();

    /// <summary>
    /// Successful bootstrap applies configuration once and never invokes later readers.
    /// </summary>
    [Fact]
    public void EnsureBootstrappedLoadsConfigurationOnce()
    {
        // A reader that fails on reuse distinguishes cached success from repeated bootstrap.
        var registry = new SqlAuthenticationProviderRegistry();
        int reads = 0;

        registry.EnsureBootstrapped(_ =>
        {
            reads++;
            return new SqlClientAuthenticationProviderConfigurationSection();
        });
        registry.EnsureBootstrapped(_ => throw new InvalidOperationException(
            "Completed bootstrap must not read configuration."));

        Assert.Equal(1, reads);
    }

    /// <summary>
    /// Recoverable reading and activation failures are tolerated without retrying configuration.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureBootstrappedToleratesConfigurationFailureWithoutRetry(bool retrieval)
    {
        var registry = new SqlAuthenticationProviderRegistry();
        int reads = 0;
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(object).AssemblyQualifiedName;
        var expected = new InvalidOperationException("Test section retrieval failure");

        using var listener = new AuthenticationTraceListener();
        registry.EnsureBootstrapped(_ =>
        {
            reads++;
            if (retrieval)
            {
                throw expected;
            }
            return section;
        });
        registry.EnsureBootstrapped(_ =>
            throw new InvalidOperationException("Completed bootstrap must not retry."));

        Assert.Contains(listener.Messages, message => message.Contains(retrieval
            ? expected.Message
            : typeof(object).AssemblyQualifiedName!));
        Assert.Equal(1, reads);
    }

    /// <summary>
    /// Azure discovery follows recoverable config failures, preserves valid options and providers,
    /// and still respects the discovery switch.
    /// </summary>
    [Theory]
    [InlineData("read", true)]
    [InlineData("read", false)]
    [InlineData("initializer", true)]
    [InlineData("initializer", false)]
    [InlineData("provider", true)]
    [InlineData("provider", false)]
    public void EnsureBootstrappedDiscoversAzureAfterConfigurationFailure(
        string failureAt, bool discovery)
    {
        _switches.EnableAzureExtensionDiscovery = discovery;
        var registry = new SqlAuthenticationProviderRegistry();
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["applicationClientId"].Value = "configured-client";
        section.ElementInformation.Properties["useWamBroker"].Value = "true";
        if (failureAt == "initializer")
        {
            section.ElementInformation.Properties["initializerType"].Value =
                typeof(object).AssemblyQualifiedName;
        }
        section.Providers.Add(new ProviderSettings(
            "Active Directory Integrated", typeof(ConfiguredProvider).AssemblyQualifiedName));
        if (failureAt == "provider")
        {
            section.Providers.Add(new ProviderSettings(
                "Active Directory Interactive", typeof(object).AssemblyQualifiedName));
        }
        section.Providers.Add(new ProviderSettings(
            "Active Directory Service Principal", typeof(ConfiguredProvider).AssemblyQualifiedName));
        int reads = 0;
        int loads = 0;
        ActiveDirectoryAuthenticationProviderOptions? options = null;
        ThrowingProvider.OnConstructed = value => options = value;
        using var listener = new AuthenticationTraceListener();
        try
        {
            registry.EnsureBootstrapped(_ =>
            {
                reads++;
                if (failureAt == "read")
                {
                    throw new InvalidOperationException("Test configuration retrieval failure");
                }
                return section;
            }, () =>
            {
                loads++;
                return typeof(ThrowingProvider).Assembly;
            });
            registry.EnsureBootstrapped(
                _ => throw new InvalidOperationException("Configuration must not be retried."),
                () => throw new InvalidOperationException("Discovery must not be retried."));

            Assert.Equal(1, reads);
            Assert.Equal(discovery ? 1 : 0, loads);
            if (discovery)
            {
                Assert.NotNull(options);
                Assert.Equal(failureAt == "read" ? null : "configured-client",
                    options.ApplicationClientId);
                Assert.Equal(failureAt != "read", options.UseWamBroker);
            }
            else
            {
                Assert.Null(options);
            }
            Assert.Contains(listener.Messages, message => message.Contains(failureAt == "read"
                ? "Test configuration retrieval failure"
                : typeof(object).AssemblyQualifiedName!));
            if (failureAt != "read")
            {
                // Configuration wins over application registrations and discovered defaults.
                Assert.False(registry.Register(
                    SqlAuthenticationMethod.ActiveDirectoryIntegrated, new ConfiguredProvider()));
                Assert.False(registry.Register(
                    SqlAuthenticationMethod.ActiveDirectoryServicePrincipal, new ConfiguredProvider()));
            }
            Assert.Equal(!discovery, registry.Register(
                SqlAuthenticationMethod.ActiveDirectoryInteractive, new ConfiguredProvider(),
                SqlAuthenticationProviderRegistry.RegistrationTier.Default));
        }
        finally
        {
            ThrowingProvider.OnConstructed = null;
        }
    }

    /// <summary>
    /// Fatal errors escape uncached and release the in-progress state in the finally block.
    /// </summary>
    [Fact]
    public void EnsureBootstrappedDoesNotCacheFatalFailure()
    {
        // Simulate a fatal runtime failure without exhausting memory in the test process.
        _switches.EnableAzureExtensionDiscovery = true;
        var registry = new SqlAuthenticationProviderRegistry();
        var expected = new OutOfMemoryException("Test fatal configuration failure");
        bool discoveryAttempted = false;

        Assert.Same(expected, Assert.Throws<OutOfMemoryException>(() =>
            registry.EnsureBootstrapped(_ => throw expected, () =>
            {
                discoveryAttempted = true;
                return typeof(ThrowingProvider).Assembly;
            })));
        Assert.False(discoveryAttempted);
        // A second attempt must actually read, proving the in-progress flag was released.
        int reads = 0;
        registry.EnsureBootstrapped(_ =>
        {
            reads++;
            return new SqlClientAuthenticationProviderConfigurationSection();
        });

        Assert.Equal(1, reads);
    }

    /// <summary>
    /// Same-thread reentry returns immediately rather than recursively loading configuration.
    /// </summary>
    [Fact]
    public void EnsureBootstrappedAllowsReentrantAccess()
    {
        // Reenter while the outer reader holds the bootstrap lock on this same thread.
        var registry = new SqlAuthenticationProviderRegistry();
        int reads = 0;

        registry.EnsureBootstrapped(_ =>
        {
            reads++;
            registry.EnsureBootstrapped(_ =>
                throw new InvalidOperationException("Reentry must not recursively bootstrap."));
            return new SqlClientAuthenticationProviderConfigurationSection();
        });

        Assert.Equal(1, reads);
    }

    /// <summary>
    /// Disabling configuration prevents even a failing reader from being invoked.
    /// </summary>
    [Fact]
    public void EnsureBootstrappedHonorsConfigurationSwitch()
    {
        // Override only the cached switch so process-wide AppContext state remains untouched.
        _switches.EnableAppConfig = false;

        new SqlAuthenticationProviderRegistry().EnsureBootstrapped(_ =>
            throw new InvalidOperationException("Disabled configuration must not be read."));
    }

    /// <summary>Relaxed parsing completes bootstrap after configuration reading or broker-value errors.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureBootstrappedToleratesConfigurationErrors(bool retrieval)
    {
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["useWamBroker"].Value = "invalid";
        int reads = 0;
        var registry = new SqlAuthenticationProviderRegistry();

        registry.EnsureBootstrapped(_ =>
        {
            reads++;
            if (retrieval)
            {
                throw new ConfigurationErrorsException("Test section retrieval failure");
            }
            return section;
        });
        registry.EnsureBootstrapped(_ => throw new InvalidOperationException(
            "Successful relaxed bootstrap must not retry."));

        Assert.Equal(1, reads);
    }

    /// <summary>Provides a valid configured registration without acquiring real tokens.</summary>
    public sealed class ConfiguredProvider : SqlAuthenticationProvider
    {
        public override bool IsSupported(SqlAuthenticationMethod authenticationMethod) => true;

        public override Task<SqlAuthenticationToken> AcquireTokenAsync(
            SqlAuthenticationParameters parameters) => throw new NotSupportedException();
    }
}
