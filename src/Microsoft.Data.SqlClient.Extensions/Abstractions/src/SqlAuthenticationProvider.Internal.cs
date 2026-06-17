// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
//
// This part of the SqlAuthenticationProvider class implements the static
// GetProvider and SetProvider methods by reflection into the Microsoft.Data.SqlClient
// package's SqlAuthenticationProviderManager class, if that assembly is present.
//
public abstract partial class SqlAuthenticationProvider
{
    /// <summary>
    /// This class implements the static GetProvider and SetProvider methods by
    /// using reflection to call into the Microsoft.Data.SqlClient package's
    /// SqlAuthenticationProviderManager class, if that assembly is present.
    /// </summary>
    private static class Internal
    {
        /// <summary>
        /// Our handle to the reflected GetProvider() method.
        /// </summary>
        private static readonly MethodInfo? _getProvider = null;

        /// <summary>
        /// Our handle to the reflected SetProvider() method.
        /// </summary>
        private static readonly MethodInfo? _setProvider = null;

        /// <summary>
        /// Our handle to the reflected ClearFederatedAuthenticationInformationCache() method.
        /// </summary>
        private static readonly MethodInfo? _clearFedAuthCache = null;

        /// <summary>
        /// Static construction performs the reflection lookups.
        /// </summary>
        static Internal()
        {
            const string assemblyName = "Microsoft.Data.SqlClient";

            // If the MDS package is present, load its
            // SqlAuthenticationProviderManager class and get/set methods.
            try
            {
                // Try to load the MDS assembly.
                var assembly = Assembly.Load(assemblyName);

                if (assembly is null)
                {
                    Log($"MDS assembly={assemblyName} not found; " +
                        "Get/SetProvider() will not function");
                    return;
                }

                // Defense-in-depth: only reflect into MDS if it carries the same strong-name
                // public key as this Extensions assembly. This is not a substitute for
                // Authenticode verification (which would require WinVerifyTrust and is
                // Windows-only) — it only catches an MDS built by a different publisher
                // dropped on the load path. On .NET Framework Assembly.Load already enforces
                // strong-name matching; on .NET (Core+) it does not, which is why we check
                // explicitly here. Throws on mismatch so that consumers see a hard failure
                // (surfaced as TypeInitializationException on first GetProvider/SetProvider
                // call) instead of silently falling back to a no-op provider table.
                if (!IsSiblingAssembly(assembly))
                {
                    throw new InvalidOperationException(
                        $"MDS assembly={assemblyName} is loaded but is not signed with the " +
                        "same strong-name key as Microsoft.Data.SqlClient.Extensions.Abstractions. " +
                        "Refusing to reflect into a foreign-signed MDS for security reasons.");
                }

                // Look for the manager class.
                const string className = "Microsoft.Data.SqlClient.SqlAuthenticationProviderManager";
                Type? manager = assembly.GetType(className);

                if (manager is null)
                {
                    Log($"MDS auth manager manager class={className} not found; " +
                        "Get/SetProvider() will not function");
                    return;
                }

                // Get handles to the get/set static methods.
                _getProvider = manager.GetMethod(
                    "GetProvider",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_getProvider is null)
                {
                    Log($"MDS GetProvider() method not found; " +
                        "GetProvider() will not function");
                }

                _setProvider = manager.GetMethod(
                    "SetProvider",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_setProvider is null)
                {
                    Log($"MDS SetProvider() method not found; " +
                        "SetProvider() will not function");
                }

                _clearFedAuthCache = manager.GetMethod(
                    "ClearFederatedAuthenticationInformationCache",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_clearFedAuthCache is null)
                {
                    Log($"MDS ClearFederatedAuthenticationInformationCache() method not found; " +
                        "ClearFederatedAuthenticationInformationCache() will not function");
                }
            }
            // All of these exceptions mean we couldn't find the get/set
            // methods.
            catch (Exception ex)
            when (ex is AmbiguousMatchException
                     or BadImageFormatException
                     or FileLoadException
                     or FileNotFoundException)
            {
                Log($"MDS assembly={assemblyName} not found or not usable; " +
                    $"Get/SetProvider() will not function: {ex} ");
            }
            // Any other exceptions are fatal.
        }

        /// <summary>
        /// Returns <see langword="true"/> when it is safe to reflect into <paramref name="assembly"/>.
        /// Policy: if this Extensions assembly is strong-name signed, the loaded MDS must carry
        /// the same public-key token; if Extensions itself is unsigned (e.g. local developer
        /// builds), no token comparison is possible, so we permit it.
        /// </summary>
        private static bool IsSiblingAssembly(Assembly assembly)
        {
            byte[]? expected = typeof(SqlAuthenticationProvider)
                .Assembly.GetName().GetPublicKeyToken();

            // Extensions itself isn't strong-name signed (local dev build) — no token to
            // compare against, so we can't make a meaningful authenticity claim either way.
            if (expected is null || expected.Length == 0)
            {
                return true;
            }

            byte[]? actual = assembly.GetName().GetPublicKeyToken();

            if (actual is null || actual.Length != expected.Length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Call the reflected GetProvider method.
        /// </summary>
        /// <param name="authenticationMethod">
        ///   The authentication method whose provider to get.
        /// </param>
        /// <returns>
        ///   Returns null if reflection failed or any exceptions occur.
        ///   Otherwise, returns as the reflected method does.
        /// </returns>
        internal static SqlAuthenticationProvider? GetProvider(
            SqlAuthenticationMethod authenticationMethod)
        {
            if (_getProvider is null)
            {
                return null;
            }

            try
            {
                return _getProvider.Invoke(null, [authenticationMethod])
                    as SqlAuthenticationProvider;
            }
            catch (Exception ex)
            when (ex is InvalidOperationException
                     or MemberAccessException
                     or MethodAccessException
                     or NotSupportedException
                     or TargetInvocationException)
            {
                Log($"GetProvider() invocation failed: " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Call the reflected SetProvider method.
        /// </summary>
        /// <param name="authenticationMethod">
        ///   The authentication method whose provider to set.
        /// </param>
        /// <param name="provider">
        ///   The provider to set.
        /// </param>
        /// <returns>
        ///   Returns false if reflection failed, invocation fails, or any
        ///   exceptions occur.  Otherwise, returns as the reflected method
        ///   does.
        /// </returns>
        internal static bool SetProvider(
            SqlAuthenticationMethod authenticationMethod,
            SqlAuthenticationProvider provider)
        {
            if (_setProvider is null)
            {
                return false;
            }

            try
            {
                bool? result =
                    _setProvider.Invoke(null, [authenticationMethod, provider])
                    as bool?;

                if (!result.HasValue)
                {
                    Log($"SetProvider() invocation returned null; " +
                        "translating to false");
                    return false;
                }

                return result.Value;
            }
            catch (Exception ex)
            when (ex is InvalidOperationException
                     or MemberAccessException
                     or MethodAccessException
                     or NotSupportedException
                     or TargetInvocationException)
            {
                Log($"SetProvider() invocation failed: " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Call the reflected ClearFederatedAuthenticationInformationCache method to
        /// evict any fed-auth tokens the driver has cached across its connection pools.
        /// </summary>
        /// <returns>
        ///   True if the reflected call ran successfully, false if reflection wasn't
        ///   available or the invocation threw a recognized exception.
        /// </returns>
        internal static bool ClearFederatedAuthenticationInformationCache()
        {
            if (_clearFedAuthCache is null)
            {
                return false;
            }

            try
            {
                _clearFedAuthCache.Invoke(null, null);
                return true;
            }
            catch (Exception ex)
            when (ex is InvalidOperationException
                     or MemberAccessException
                     or MethodAccessException
                     or NotSupportedException
                     or TargetInvocationException)
            {
                Log($"ClearFederatedAuthenticationInformationCache() invocation failed: " +
                    $"{ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static void Log(string message)
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProvider.Internal | {0}", message);
        }
    }
}
