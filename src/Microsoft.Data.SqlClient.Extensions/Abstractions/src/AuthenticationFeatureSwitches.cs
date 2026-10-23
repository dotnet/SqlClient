// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Data.SqlClient;

/// <summary>Defines cached authentication feature switches owned by Abstractions.</summary>
/// <remarks>Each switch is read on first access and cached for the lifetime of the process.</remarks>
internal static class AuthenticationFeatureSwitches
{
    /// <summary>Gets whether authentication configuration reading and parsing reject invalid settings.</summary>
    /// <remarks>
    /// Defaults to false when the AppContext switch is unset. Compatibility mode ignores selected
    /// configuration errors, which may cause the driver to install or configure providers differently
    /// from the application's intended configuration when the loader is integrated. Strict mode
    /// closes this gap by rejecting configuration-reading, schema and broker-value errors instead
    /// of continuing with fallback providers or default settings.
    /// </remarks>
    [FeatureSwitchDefinition("Switch.Microsoft.Data.SqlClient.UseStrictAuthenticationConfiguration")]
    internal static bool UseStrictAuthenticationConfiguration =>
        AcquireAndReturn(
            "Switch.Microsoft.Data.SqlClient.UseStrictAuthenticationConfiguration",
            ref s_useStrictAuthenticationConfiguration,
            defaultValue: false);

    internal enum SwitchValue : byte
    {
        None = 0,
        True = 1,
        False = 2
    }

    internal static SwitchValue s_useStrictAuthenticationConfiguration;

    private static bool AcquireAndReturn(
        string switchName, ref SwitchValue switchValue, bool defaultValue)
    {
        if (switchValue != SwitchValue.None)
        {
            return switchValue == SwitchValue.True;
        }

        bool enabled =
            AppContext.TryGetSwitch(switchName, out bool acquiredValue) ? acquiredValue : defaultValue;

        switchValue = enabled ? SwitchValue.True : SwitchValue.False;
        return enabled;
    }
}
