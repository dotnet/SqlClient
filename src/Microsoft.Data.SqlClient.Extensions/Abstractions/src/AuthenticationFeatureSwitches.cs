// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Defines authentication switches so reflective bootstrap can be retained in trimmed JIT apps
/// or excluded at publish time for explicit registration in trimmed and AOT applications.
/// </summary>
/// <remarks>
/// Configuration and discovery default to enabled to preserve existing runtime behavior.  Each
/// switch is read on first access and cached for the lifetime of the process.  Publish-time switch
/// values allow trimming tools to remove guarded reflection paths; changing an AppContext switch at
/// runtime does not change what was retained during publishing.
/// </remarks>
internal static class AuthenticationFeatureSwitches
{
    #region Feature Switches

    /// <summary>
    /// Gets whether authentication providers and initializers may be loaded from app.config.
    /// </summary>
    /// <remarks>
    /// Defaults to true when the AppContext switch is unset.
    /// </remarks>
    // Associates this property with the named switch so trimming tools can substitute its
    // publish-time value.
    [FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.EnableAppConfig")]
    internal static bool EnableAppConfig =>
        AcquireAndReturn(
            "Switch.Microsoft.Data.SqlClient.EnableAppConfig",
            ref s_enableAppConfig,
            defaultValue: true);

    /// <summary>
    /// Gets whether authentication bootstrap may discover the Azure extension provider through
    /// reflection.
    /// </summary>
    /// <remarks>
    /// Defaults to true when the AppContext switch is unset.
    /// </remarks>
    // Associates this property with the named switch so trimming tools can substitute its
    // publish-time value.
    [FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery")]
    internal static bool EnableAzureExtensionDiscovery =>
        AcquireAndReturn(
            "Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery",
            ref s_enableAzureExtensionDiscovery,
            defaultValue: true);

    #endregion

    #region Helpers

    // The possible states of a feature switch.
    internal enum SwitchValue : byte
    {
        None = 0,
        True = 1,
        False = 2
    }

    // Cached feature switch values.
    internal static SwitchValue s_enableAppConfig;
    internal static SwitchValue s_enableAzureExtensionDiscovery;

    // Acquires the feature switch value from AppContext if it hasn't been cached yet.  Switches
    // that aren't set will assume the default value.
    private static bool AcquireAndReturn(
        string switchName, ref SwitchValue switchValue, bool defaultValue)
    {
        // Short circuit if we've already cached the switch value.
        if (switchValue != SwitchValue.None)
        {
            return switchValue == SwitchValue.True;
        }

        // Acquire the switch value from AppContext.  If not present, use the default value.
        bool enabled =
            AppContext.TryGetSwitch(switchName, out bool acquiredValue) ? acquiredValue : defaultValue;

        switchValue = enabled ? SwitchValue.True : SwitchValue.False;
        return enabled;
    }
    #endregion
}
