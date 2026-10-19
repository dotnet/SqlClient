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
        var registry = new SqlAuthenticationProviderRegistry();
        var expected = new OutOfMemoryException("Test fatal configuration failure");

        Assert.Same(expected, Assert.Throws<OutOfMemoryException>(() =>
            registry.EnsureBootstrapped(_ => throw expected)));
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
        _switches.EnableAppConfig = false;

        new SqlAuthenticationProviderRegistry().EnsureBootstrapped(_ =>
            throw new InvalidOperationException("Disabled configuration must not be read."));
    }
}
