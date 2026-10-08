// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Defines authentication bootstrap switches and capability guards so configuration loading,
/// Azure extension discovery, and runtime version checks can be excluded from trimmed or AOT builds.
/// </summary>
/// <remarks>
/// Configuration and discovery default to enabled to preserve existing runtime behavior.
/// Publish-time switch values allow trimming tools to remove guarded reflection paths;
/// changing an AppContext switch at runtime does not change what was retained during publishing.
/// </remarks>
internal static class AuthenticationFeatureSwitches
{
    /// <summary>
    /// Indicates whether authentication bootstrap may inspect loaded SqlClient family assembly versions.
    /// </summary>
    /// <remarks>
    /// Returns true during ordinary execution. The FeatureGuard attributes identify the guarded
    /// path as requiring unreferenced code and dynamic code capabilities for trimming and AOT analysis.
    /// The IL4000 suppression permits this guard because fixed trimmed/AOT images cannot replace
    /// assemblies at runtime and their package graph is validated at build time.
    /// </remarks>
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "Runtime assembly replacement is unavailable in fixed trimmed/AOT images; the package graph is checked at build time.")]
    internal static bool IsRuntimeVersionValidationSupported => true;

    /// <summary>
    /// Gets whether authentication providers and initializers may be loaded from app.config.
    /// </summary>
    /// <remarks>
    /// Defaults to true when the AppContext switch is unset. FeatureSwitchDefinition associates
    /// this property with the named switch so trimming tools can substitute its publish-time value.
    /// </remarks>
    [FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.EnableAppConfig")]
    internal static bool EnableAppConfig =>
        !AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAppConfig", out bool enabled) ||
        enabled;

    /// <summary>
    /// Gets whether authentication bootstrap may discover the Azure extension provider through reflection.
    /// </summary>
    /// <remarks>
    /// Defaults to true when the AppContext switch is unset. FeatureSwitchDefinition associates
    /// this property with the named switch so trimming tools can substitute its publish-time value.
    /// </remarks>
    [FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery")]
    internal static bool EnableAzureExtensionDiscovery =>
        !AppContext.TryGetSwitch("Switch.Microsoft.Data.SqlClient.EnableAzureExtensionDiscovery",
            out bool enabled) || enabled;

    /// <summary>
    /// Guards the reflection-based authentication configuration and initializer loading path.
    /// </summary>
    /// <remarks>
    /// Follows EnableAppConfig. The FeatureGuard attributes declare that a true value permits
    /// calls requiring unreferenced code and dynamic code capabilities.
    /// The IL4000 suppression permits a switch-backed guard because disabling the switch at
    /// publish time lets trimming tools replace it with false and remove the guarded path.
    /// </remarks>
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "The separate AppContext feature switch backs this guard; trimming replaces it with false.")]
    internal static bool IsAppConfigSupported => EnableAppConfig;

    /// <summary>
    /// Guards reflection-based Azure extension discovery and provider construction.
    /// </summary>
    /// <remarks>
    /// Follows EnableAzureExtensionDiscovery. The FeatureGuard attributes declare that a true
    /// value permits calls requiring unreferenced code and dynamic code capabilities.
    /// The IL4000 suppression permits a switch-backed guard because disabling the switch at
    /// publish time lets trimming tools replace it with false and remove the guarded path.
    /// </remarks>
    [FeatureGuard(typeof(RequiresUnreferencedCodeAttribute))]
    [FeatureGuard(typeof(RequiresDynamicCodeAttribute))]
    [UnconditionalSuppressMessage("Trimming", "IL4000",
        Justification = "The separate AppContext feature switch backs this guard; trimming replaces it with false.")]
    internal static bool IsAzureExtensionDiscoverySupported => EnableAzureExtensionDiscovery;
}
