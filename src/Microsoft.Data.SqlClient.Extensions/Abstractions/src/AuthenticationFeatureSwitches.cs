// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Defines authentication bootstrap switches and capability guards so configuration loading, Azure
/// extension discovery, and runtime version checks can be excluded from trimmed or AOT builds.
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

    #region Trimming Guards

    /// <summary>Indicates whether bootstrap may inspect loaded SqlClient family assembly versions.</summary>
    // Fixed trimmed/AOT images cannot replace assemblies; their package graph is checked at build time.
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "Runtime assembly replacement is unavailable in fixed trimmed/AOT images; the package graph is checked at build time.")]
    internal static bool IsRuntimeVersionValidationSupported => true;

    /// <summary>
    /// Guards the reflection-based authentication configuration and initializer loading path.
    /// </summary>
    /// <remarks>
    /// Follows EnableAppConfig.
    /// </remarks>
    // Marks this property as guarding calls requiring unreferenced code. Keep it separate from the
    // switch definition so trimming removes the guarded path without a publish-time switch value.
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    // Marks this property as guarding calls requiring dynamic code.
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    // Permits a switch-backed guard because trimming replaces this capability guard with false.
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "The separate AppContext feature switch backs this guard; trimming replaces it with false.")]
    internal static bool IsAppConfigSupported => EnableAppConfig;

    /// <summary>
    /// Guards reflection-based Azure extension discovery and provider construction.
    /// </summary>
    /// <remarks>
    /// Follows EnableAzureExtensionDiscovery.
    /// </remarks>
    // Marks this property as guarding calls requiring unreferenced code. Keep it separate from the
    // switch definition so trimming removes the guarded path without a publish-time switch value.
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    // Marks this property as guarding calls requiring dynamic code.
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    // Permits a switch-backed guard because trimming replaces this capability guard with false.
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "The separate AppContext feature switch backs this guard; trimming replaces it with false.")]
    internal static bool IsAzureExtensionDiscoverySupported => EnableAzureExtensionDiscovery;

    #endregion

    #region Helpers

    // The possible states of a feature switch.
    private enum SwitchValue : byte
    {
        None = 0,
        True = 1,
        False = 2
    }

    // Cached feature switch values.
    private static SwitchValue s_enableAppConfig;
    private static SwitchValue s_enableAzureExtensionDiscovery;

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
