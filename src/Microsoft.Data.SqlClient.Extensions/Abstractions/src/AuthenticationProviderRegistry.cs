// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Holds the registry of <see cref="SqlAuthenticationProvider"/> instances keyed by
/// <see cref="SqlAuthenticationMethod"/>.  This is the shared store that backs the public
/// <see cref="SqlAuthenticationProvider.GetProvider"/> and
/// <see cref="SqlAuthenticationProvider.SetProvider"/> methods.
/// </summary>
/// <remarks>
/// Providers fall into two categories:
/// <list type="bullet">
///   <item>
///     <description>
///       Permanent providers, registered via <see cref="SetPermanentProvider"/> (e.g. from an
///       application's configuration). These take precedence and cannot be overridden by a later
///       call to <see cref="SetProvider"/>.
///     </description>
///   </item>
///   <item>
///     <description>
///       Overridable providers, registered via <see cref="SetProvider"/>. These can be replaced
///       by subsequent <see cref="SetProvider"/> calls, but never override a permanent provider.
///     </description>
///   </item>
/// </list>
/// </remarks>
internal sealed class AuthenticationProviderRegistry
{
    #region Private Fields

    /// <summary>
    /// The singleton instance backing the public static
    /// <see cref="SqlAuthenticationProvider.GetProvider"/> and
    /// <see cref="SqlAuthenticationProvider.SetProvider"/> accessors.
    /// </summary>
    /// <remarks>
    /// Production code uses this shared instance. Tests can instead construct an isolated
    /// instance via the internal constructor to avoid mutating global state.
    /// </remarks>
    internal static AuthenticationProviderRegistry Instance { get; } = new();

    /// <summary>
    /// A registered provider together with whether it was registered as permanent (via
    /// <see cref="SetPermanentProvider"/>) and therefore not overridable by <see cref="SetProvider"/>.
    /// </summary>
    /// <param name="Provider">The registered provider.  Never <see langword="null"/>.</param>
    /// <param name="IsPermanent">Whether the provider must not be overridden by <see cref="SetProvider"/>.</param>
    private readonly record struct ProviderEntry(SqlAuthenticationProvider Provider, bool IsPermanent);

    /// <summary>
    /// The registered providers keyed by authentication method.  Each entry records whether the
    /// provider was registered as permanent (e.g. application specified); permanent providers are
    /// not overridable via <see cref="SetProvider"/>.
    /// </summary>
    private readonly ConcurrentDictionary<SqlAuthenticationMethod, ProviderEntry> _providers = new();

    /// <summary>
    /// Control-flow sentinel used to abort <see cref="SetProvider"/>'s update factory when it
    /// would replace a permanent provider.  Never escapes <see cref="SetProvider"/>.
    /// </summary>
    private sealed class PermanentProviderException : Exception
    {
    }

    /// <summary>
    /// Sentinel thrown from <see cref="SetProvider"/>'s update factory to abort the
    /// <c>AddOrUpdate</c> call when it would replace a permanent provider.  It never escapes
    /// <see cref="SetProvider"/>.
    /// </summary>
    private static readonly PermanentProviderException s_permanentProviderException = new();

    #endregion

    #region Construction

    /// <summary>
    /// Initializes a new, empty registry. Production code uses the shared <see cref="Instance"/>;
    /// the constructor is exposed to tests so they can exercise registry behavior in isolation.
    /// </summary>
    internal AuthenticationProviderRegistry()
    {
    }

    #endregion

    #region Internal API

    /// <summary>
    /// Gets the provider registered for the given authentication method, or <see langword="null"/>
    /// if none is registered.
    /// </summary>
    internal SqlAuthenticationProvider? GetProvider(SqlAuthenticationMethod authenticationMethod)
    {
        return _providers.TryGetValue(authenticationMethod, out ProviderEntry entry)
            ? entry.Provider
            : null;
    }

    /// <summary>
    /// Registers an overridable provider for the given authentication method.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the provider was registered; <see langword="false"/> if a
    /// permanent provider is already registered for the authentication method.
    /// </returns>
    /// <exception cref="NotSupportedException">
    /// The provider does not support the given authentication method.
    /// </exception>
    internal bool SetProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider)
    {
        if (!provider.IsSupported(authenticationMethod))
        {
            throw new NotSupportedException(
                string.Format(
                    AbstractionsStrings.SQL_UnsupportedAuthenticationByProvider,
                    provider.GetType().Name,
                    authenticationMethod.ToString()));
        }

        try
        {
            _providers.AddOrUpdate(
                authenticationMethod,
                // addValueFactory: no provider is registered for this method yet.
                (SqlAuthenticationMethod key) =>
                {
                    InvokeProviderCallback(provider, provider.BeforeLoad, key, nameof(SqlAuthenticationProvider.BeforeLoad));

                    SqlClientEventSource.Log.TryTraceEvent(
                        "AuthenticationProviderRegistry.SetProvider | Added auth provider {0} for authentication {1}.",
                        GetProviderType(provider),
                        key);

                    return new ProviderEntry(provider, IsPermanent: false);
                },
                // updateValueFactory: a provider is already registered for this method.
                (SqlAuthenticationMethod key, ProviderEntry existing) =>
                {
                    // Permanent providers cannot be replaced.
                    if (existing.IsPermanent)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            "AuthenticationProviderRegistry.SetProvider | Failed to add provider {0} because a " +
                            "permanent provider with type {1} already existed for authentication {2}.",
                            GetProviderType(provider),
                            GetProviderType(existing.Provider),
                            key);

                        // A permanent provider was specified for this authentication method, so we
                        // won't override it.  Abort the update; SetProvider catches this below.
                        throw s_permanentProviderException;
                    }

                    InvokeProviderCallback(existing.Provider, existing.Provider.BeforeUnload, key, nameof(SqlAuthenticationProvider.BeforeUnload));
                    InvokeProviderCallback(provider, provider.BeforeLoad, key, nameof(SqlAuthenticationProvider.BeforeLoad));

                    SqlClientEventSource.Log.TryTraceEvent(
                        "AuthenticationProviderRegistry.SetProvider | Added auth provider {0}, overriding " +
                        "existing provider {1} for authentication {2}.",
                        GetProviderType(provider),
                        GetProviderType(existing.Provider),
                        key);

                    return new ProviderEntry(provider, IsPermanent: false);
                });
        }
        catch (PermanentProviderException)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Registers a permanent provider for the given authentication method. Permanent providers
    /// take precedence and cannot be overridden by <see cref="SetProvider"/>.
    /// </summary>
    /// <remarks>
    /// Callers are responsible for verifying that the provider supports the authentication method
    /// before registering it.
    /// <para>
    /// This is a last-in-wins operation: a later <see cref="SetPermanentProvider"/> call for the
    /// same authentication method unconditionally replaces any previously registered provider
    /// (permanent or not). Only <see cref="SetProvider"/> is blocked by an existing permanent
    /// provider; <see cref="SetPermanentProvider"/> itself always overwrites.
    /// </para>
    /// </remarks>
    internal void SetPermanentProvider(SqlAuthenticationMethod authenticationMethod, SqlAuthenticationProvider provider)
    {
        _providers[authenticationMethod] = new ProviderEntry(provider, IsPermanent: true);
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Returns a human-readable type name for the given provider, for use in trace messages.
    /// </summary>
    /// <param name="provider">The provider to describe, or <see langword="null"/>.</param>
    /// <returns>
    /// The provider's full type name; <c>"null"</c> if <paramref name="provider"/> is
    /// <see langword="null"/>; or <c>"unknown"</c> if the type name is unavailable.
    /// </returns>
    private static string GetProviderType(SqlAuthenticationProvider? provider)
    {
        if (provider is null)
        {
            return "null";
        }
        return provider.GetType().FullName ?? "unknown";
    }

    /// <summary>
    /// Invokes a provider lifecycle callback (<see cref="SqlAuthenticationProvider.BeforeLoad"/> or
    /// <see cref="SqlAuthenticationProvider.BeforeUnload"/>), isolating the registry from a
    /// misbehaving provider: any exception the callback throws is logged and swallowed so it
    /// cannot corrupt registration.
    /// </summary>
    /// <param name="provider">The provider whose callback is being invoked (used for logging).</param>
    /// <param name="callback">The callback to invoke.</param>
    /// <param name="authenticationMethod">The authentication method passed to the callback.</param>
    /// <param name="callbackName">The callback name, used in trace messages.</param>
    private static void InvokeProviderCallback(
        SqlAuthenticationProvider provider,
        Action<SqlAuthenticationMethod> callback,
        SqlAuthenticationMethod authenticationMethod,
        string callbackName)
    {
        try
        {
            callback(authenticationMethod);
        }
        catch (Exception ex)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "AuthenticationProviderRegistry.SetProvider | {0} threw for provider {1} with " +
                "authentication {2}; ignoring: {3}",
                callbackName,
                GetProviderType(provider),
                authenticationMethod,
                ex);
        }
    }

    #endregion
}
