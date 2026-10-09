// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Serializes switch mutations against authentication bootstrap in other tests.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class AuthenticationFeatureSwitchTestCollection
{
    public const string Name = "Authentication feature switches";
}

/// <summary>Verifies authentication switches retain their first value through their capability guards.</summary>
[Collection(AuthenticationFeatureSwitchTestCollection.Name)]
public class AuthenticationFeatureSwitchesTest
{
    /// <summary>Changing AppContext after first access must not change the switch or its guard.</summary>
    [Theory]
    [InlineData("EnableAppConfig", "s_enableAppConfig", true)]
    [InlineData("EnableAppConfig", "s_enableAppConfig", false)]
    [InlineData("EnableAzureExtensionDiscovery", "s_enableAzureExtensionDiscovery", true)]
    [InlineData("EnableAzureExtensionDiscovery", "s_enableAzureExtensionDiscovery", false)]
    public void SwitchRetainsFirstValue(string propertyName, string fieldName, bool initialValue)
    {
        string switchName = "Switch.Microsoft.Data.SqlClient." + propertyName;
        Type type = typeof(AuthenticationFeatureSwitches);
        FieldInfo field = type.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)!;
        object cachedValue = field.GetValue(null)!;
        bool originalValue = !AppContext.TryGetSwitch(switchName, out bool value) || value;
        try
        {
            field.SetValue(null, Enum.ToObject(field.FieldType, 0));
            AppContext.SetSwitch(switchName, initialValue);
            Assert.Equal(initialValue, ReadSwitch(propertyName));
            AppContext.SetSwitch(switchName, !initialValue);
            Assert.Equal(initialValue, ReadSwitch(propertyName));
            Assert.Equal(initialValue, propertyName == "EnableAppConfig"
                ? AuthenticationFeatureSwitches.IsAppConfigSupported
                : AuthenticationFeatureSwitches.IsAzureExtensionDiscoverySupported);
        }
        finally
        {
            AppContext.SetSwitch(switchName, originalValue);
            field.SetValue(null, cachedValue);
        }
    }

    /// <summary>Reads a feature switch directly without triggering registry bootstrap.</summary>
    /// <param name="propertyName">The switch property to read.</param>
    /// <returns>The cached switch value.</returns>
    private static bool ReadSwitch(string propertyName) =>
        propertyName == "EnableAppConfig"
            ? AuthenticationFeatureSwitches.EnableAppConfig
            : AuthenticationFeatureSwitches.EnableAzureExtensionDiscovery;
}
