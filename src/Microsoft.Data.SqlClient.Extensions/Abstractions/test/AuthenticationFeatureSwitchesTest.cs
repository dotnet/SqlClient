// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Serializes cached switch overrides against authentication loading in other tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class AuthenticationFeatureSwitchTestCollection
{
    public const string Name = "Authentication feature switches";
}

/// <summary>Verifies strict configuration defaults, cached overrides and test-helper isolation.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationFeatureSwitchesTest
{
    private const string SwitchName =
        "Switch.Microsoft.Data.SqlClient.UseStrictAuthenticationConfiguration";

    /// <summary>Tests must leave the process-wide switch unset and manipulate only its cache.</summary>
    [Fact]
    public void AppContextSwitchIsUnset() => Assert.False(AppContext.TryGetSwitch(SwitchName, out _));

    /// <summary>An uninitialized cache reads and caches the compatibility default.</summary>
    [Fact]
    public void UnsetSwitchDefaultsToCompatibilityMode()
    {
        using var switches = new AuthenticationFeatureSwitchesHelper();
        switches.UseStrictAuthenticationConfiguration = null;

        Assert.False(AppContext.TryGetSwitch(SwitchName, out _));
        Assert.False(AuthenticationFeatureSwitches.UseStrictAuthenticationConfiguration);
        Assert.Equal(AuthenticationFeatureSwitches.SwitchValue.False,
            AuthenticationFeatureSwitches.s_useStrictAuthenticationConfiguration);
    }

    /// <summary>Both cached overrides are honored without publishing an AppContext value.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StrictConfigurationHonorsCachedValue(bool cachedValue)
    {
        using var switches = new AuthenticationFeatureSwitchesHelper();
        switches.UseStrictAuthenticationConfiguration = cachedValue;

        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.UseStrictAuthenticationConfiguration);
        Assert.Equal(cachedValue, AuthenticationFeatureSwitches.UseStrictAuthenticationConfiguration);
        Assert.False(AppContext.TryGetSwitch(SwitchName, out _));
    }

    /// <summary>Nested helpers restore exact cached states, including the uninitialized state.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void HelperRestoresCachedValue(bool? original)
    {
        using var switches = new AuthenticationFeatureSwitchesHelper();
        switches.UseStrictAuthenticationConfiguration = original;
        var expected = AuthenticationFeatureSwitches.s_useStrictAuthenticationConfiguration;

        using (var overrides = new AuthenticationFeatureSwitchesHelper())
        {
            overrides.UseStrictAuthenticationConfiguration = original != true;
            Assert.Equal(original != true, AuthenticationFeatureSwitches.UseStrictAuthenticationConfiguration);
        }

        Assert.Equal(expected, AuthenticationFeatureSwitches.s_useStrictAuthenticationConfiguration);
        Assert.Equal(original ?? false, AuthenticationFeatureSwitches.UseStrictAuthenticationConfiguration);
    }
}
