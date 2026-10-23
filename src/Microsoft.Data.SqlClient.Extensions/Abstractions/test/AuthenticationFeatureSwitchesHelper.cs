// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using SwitchValue = Microsoft.Data.SqlClient.AuthenticationFeatureSwitches.SwitchValue;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>
/// Snapshots, overrides and restores authentication switch caches without changing AppContext.
/// Use within the serialized authentication feature switch test collection.
/// </summary>
internal sealed class AuthenticationFeatureSwitchesHelper : IDisposable
{
    private readonly SwitchValue _useStrictAuthenticationConfigurationOriginal =
        AuthenticationFeatureSwitches.s_useStrictAuthenticationConfiguration;

    /// <summary>Overrides strict parsing; null clears the cache so the next read acquires the runtime value or default.</summary>
    internal bool? UseStrictAuthenticationConfiguration
    {
        get => AuthenticationFeatureSwitches.UseStrictAuthenticationConfiguration;
        set => AuthenticationFeatureSwitches.s_useStrictAuthenticationConfiguration = ToCachedValue(value);
    }

    /// <summary>Restores the exact cached state captured at construction, including an uninitialized value.</summary>
    public void Dispose() =>
        AuthenticationFeatureSwitches.s_useStrictAuthenticationConfiguration =
            _useStrictAuthenticationConfigurationOriginal;

    /// <summary>Converts a nullable override to the corresponding cached switch state.</summary>
    /// <param name="value">The override, or null to clear the cache.</param>
    /// <returns>The cached state represented by the override.</returns>
    private static SwitchValue ToCachedValue(bool? value) => value switch
    {
        true => SwitchValue.True,
        false => SwitchValue.False,
        null => SwitchValue.None
    };
}
