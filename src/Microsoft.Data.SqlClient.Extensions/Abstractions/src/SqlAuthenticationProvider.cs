// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
public abstract partial class SqlAuthenticationProvider
{
    /// <summary>
    /// Providers registered via <see cref="SetProvider"/> before the core
    /// SqlClient assembly has registered its provider manager callbacks
    /// via <see cref="RegisterProviderManager"/>. Replayed once the
    /// manager registers.
    /// </summary>
    private static Dictionary<SqlAuthenticationMethod,
        SqlAuthenticationProvider>? s_pendingProviders;

    private static Func<SqlAuthenticationMethod,
        SqlAuthenticationProvider?>? s_getProviderCallback;

    private static Func<SqlAuthenticationMethod,
        SqlAuthenticationProvider, bool>? s_setProviderCallback;

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeLoad/*'/>
    public virtual void BeforeLoad(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/BeforeUnload/*'/>
    public virtual void BeforeUnload(SqlAuthenticationMethod authenticationMethod) { }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/IsSupported/*'/>
    public abstract bool IsSupported(SqlAuthenticationMethod authenticationMethod);

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/AcquireTokenAsync/*'/>
    public abstract Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters);

    /// <summary>
    /// Registers the core SqlClient provider management callbacks.
    /// This method is called by
    /// <c>SqlAuthenticationProviderManager</c> during its static
    /// initialization to wire up the AOT-safe provider bridge between
    /// the Abstractions layer and the core SqlClient assembly.
    ///
    /// Any providers that were registered via
    /// <see cref="SetProvider"/> before this method was called are
    /// replayed through <paramref name="setProvider"/> so they are
    /// not lost.
    ///
    /// This is infrastructure -- application code should not call
    /// this method.
    /// </summary>
    /// <param name="getProvider">
    /// Callback that retrieves a registered provider for a given
    /// authentication method.
    /// </param>
    /// <param name="setProvider">
    /// Callback that registers a provider for a given authentication
    /// method.
    /// </param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void RegisterProviderManager(
        Func<SqlAuthenticationMethod,
            SqlAuthenticationProvider?> getProvider,
        Func<SqlAuthenticationMethod,
            SqlAuthenticationProvider, bool> setProvider)
    {
        s_getProviderCallback = getProvider;
        s_setProviderCallback = setProvider;

        // Replay providers that were registered before the core
        // assembly initialized (e.g. via RegisterAsDefault() called
        // early in Main).
        if (s_pendingProviders is not null)
        {
            foreach (var kvp in s_pendingProviders)
            {
                setProvider(kvp.Key, kvp.Value);
            }

            s_pendingProviders = null;
        }
    }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/GetProvider/*'/>
    //
    // We would like to deprecate this method in favour of
    // SqlAuthenticationProviderManager.GetProvider().
    //
    public static SqlAuthenticationProvider? GetProvider(
        SqlAuthenticationMethod authenticationMethod)
    {
        // Prefer the direct callback registered by the core SqlClient
        // assembly (AOT-safe). Fall back to the reflection-based
        // approach for backwards compatibility with older versions of
        // Microsoft.Data.SqlClient that do not register the callback.
        return s_getProviderCallback is not null
            ? s_getProviderCallback(authenticationMethod)
            : Internal.GetProvider(authenticationMethod);
    }

    /// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SetProvider/*'/>
    //
    // We would like to deprecate this method in favour of
    // SqlAuthenticationProviderManager.SetProvider().
    //
    public static bool SetProvider(
        SqlAuthenticationMethod authenticationMethod,
        SqlAuthenticationProvider provider)
    {
        // Prefer the direct callback registered by the core SqlClient
        // assembly (AOT-safe).
        if (s_setProviderCallback is not null)
        {
            return s_setProviderCallback(
                authenticationMethod, provider);
        }

        // The core SqlClient assembly has not initialized its
        // callbacks yet. Buffer the registration and replay it once
        // RegisterProviderManager is called.
        s_pendingProviders ??= new();
        s_pendingProviders[authenticationMethod] = provider;
        return true;
    }
}
