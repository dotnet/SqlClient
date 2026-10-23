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
    /// Non-configuration retrieval errors and activation failures are cached without retry.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnsureBootstrappedCachesConfigurationFailure(bool retrieval)
    {
        // ConfigurationErrorsException is tolerated; other failures still poison bootstrap.
        var registry = new SqlAuthenticationProviderRegistry();
        int reads = 0;
        var section = new SqlClientAuthenticationProviderConfigurationSection();
        section.ElementInformation.Properties["initializerType"].Value =
            typeof(object).AssemblyQualifiedName;
        var expected = new InvalidOperationException("Test section retrieval failure");

        var first = Assert.ThrowsAny<Exception>(() =>
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
        var second = Assert.ThrowsAny<Exception>(() =>
            registry.EnsureBootstrapped(_ =>
                throw new InvalidOperationException("Failed bootstrap must not retry.")));

        Assert.Same(first, second);
        if (retrieval)
        {
            Assert.Same(expected, first);
        }
        else
        {
            Assert.IsType<ArgumentException>(first);
            Assert.Contains(typeof(object).AssemblyQualifiedName!, first.Message);
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
}
