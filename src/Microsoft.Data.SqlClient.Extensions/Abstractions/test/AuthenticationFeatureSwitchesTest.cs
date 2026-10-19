// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Serializes cached switch overrides against authentication bootstrap in other tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class AuthenticationFeatureSwitchTestCollection
{
    public const string Name = "Authentication feature switches";
}

/// <summary>Verifies unset authentication switch defaults and cached values through their capability guards.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationFeatureSwitchesTest
{
    /// <summary>Authentication tests must leave the process-wide switches unset and manipulate only their caches.</summary>
    [Fact]
    public void AppContextSwitchesAreUnset()
    {
        // Other tests may override caches, but must never leave global AppContext values behind.
        Assert.False(AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", out _));
        Assert.False(AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", out _));
    }

    /// <summary>Clearing cached values reads the enabled defaults without changing process-wide AppContext switches.</summary>
    [Fact]
    public void UnsetSwitchesDefaultToEnabled()
    {
        // Clear both caches to exercise first-read defaults rather than a previous test's values.
        using var switches = new AuthenticationFeatureSwitchesHelper();
        switches.EnableAppConfig = null;
        switches.EnableAzureExtensionDiscovery = null;

        Assert.False(AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", out _));
        Assert.False(AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery", out _));
        Assert.True(AuthenticationFeatureSwitches.EnableAppConfig);
        Assert.True(AuthenticationFeatureSwitches.EnableAzureExtensionDiscovery);
        Assert.True(AuthenticationFeatureSwitches.IsAppConfigSupported);
        Assert.True(AuthenticationFeatureSwitches.IsAzureExtensionDiscoverySupported);
    }

    /// <summary>The app.config switch and guard honor cached overrides without setting the AppContext switch.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableAppConfigAndGuardHonorCachedValue(bool cachedValue)
    {
        // The guard must follow the cached feature switch without publishing an AppContext value.
        using var switches = new AuthenticationFeatureSwitchesHelper();
        const string switchName = "Switch.Microsoft.Data.SqlClient.EnableAppConfig";
        Assert.False(AppContext.TryGetSwitch(switchName, out _));
        switches.EnableAppConfig = cachedValue;

        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.EnableAppConfig);
        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.EnableAppConfig);
        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.IsAppConfigSupported);
        Assert.False(AppContext.TryGetSwitch(switchName, out _));
    }

    /// <summary>The Azure discovery switch and guard honor cached overrides without setting the AppContext switch.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EnableAzureExtensionDiscoveryAndGuardHonorCachedValue(bool cachedValue)
    {
        // Keep discovery's capability guard and runtime switch consistent for either override.
        using var switches = new AuthenticationFeatureSwitchesHelper();
        const string switchName = "Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery";
        Assert.False(AppContext.TryGetSwitch(switchName, out _));
        switches.EnableAzureExtensionDiscovery = cachedValue;

        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.EnableAzureExtensionDiscovery);
        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.EnableAzureExtensionDiscovery);
        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.IsAzureExtensionDiscoverySupported);
        Assert.False(AppContext.TryGetSwitch(switchName, out _));
    }

    /// <summary>Disposal restores both original cached values after nested overrides, including an uninitialized cache.</summary>
    [Fact]
    public void HelperRestoresCachedValues()
    {
        // Nest helpers to verify disposal restores uninitialized caches, not just Boolean values.
        using var original = new AuthenticationFeatureSwitchesHelper();
        original.EnableAppConfig = null;
        original.EnableAzureExtensionDiscovery = false;
        using (var overrides = new AuthenticationFeatureSwitchesHelper())
        {
            overrides.EnableAppConfig = false;
            overrides.EnableAzureExtensionDiscovery = true;
            Assert.False(AuthenticationFeatureSwitches.EnableAppConfig);
            Assert.True(AuthenticationFeatureSwitches.EnableAzureExtensionDiscovery);
        }

        // Restoring an uninitialized cache must restore default-enabled evaluation.
        Assert.True(AuthenticationFeatureSwitches.EnableAppConfig);
        Assert.False(AuthenticationFeatureSwitches.EnableAzureExtensionDiscovery);
    }

}
