// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Configuration;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Verifies bootstrap lifecycle with isolated section readers and disabled Azure discovery.
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
    /// Recoverable retrieval and application failures are cached without retry or replacement.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureBootstrappedCachesConfigurationFailure(bool retrieval)
    {
        // Fail either section retrieval or parsed-value validation, before optional discovery.
        var registry = new SqlAuthenticationProviderRegistry();
        int reads = 0;
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["useWamBroker"].Value = "invalid";
        var expected = new ConfigurationErrorsException("Test section retrieval failure");

        var first = Assert.Throws<ConfigurationErrorsException>(() =>
            registry.EnsureBootstrapped(_ =>
            {
                reads++;
                if (retrieval)
                {
                    throw expected;
                }
                return section;
            }));
        // Changing readers cannot repair a cached failure; callers must see the original exception.
        var second = Assert.Throws<ConfigurationErrorsException>(() =>
            registry.EnsureBootstrapped(_ =>
                throw new InvalidOperationException("Failed bootstrap must not retry.")));

        Assert.Same(first, second);
        if (retrieval)
        {
            Assert.Same(expected, first);
        }
        else
        {
            Assert.Contains("useWamBroker", first.Message);
        }
        Assert.Equal(1, reads);
    }

    /// <summary>
    /// Fatal errors escape uncached and release the in-progress state in the finally block.
    /// </summary>
    [Fact]
    public void EnsureBootstrappedDoesNotCacheFatalFailure()
    {
        // Simulate a fatal runtime failure without exhausting memory in the test process.
        var registry = new SqlAuthenticationProviderRegistry();
        var expected = new OutOfMemoryException("Test fatal configuration failure");

        Assert.Same(expected, Assert.Throws<OutOfMemoryException>(() =>
            registry.EnsureBootstrapped(_ => throw expected)));
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
}
