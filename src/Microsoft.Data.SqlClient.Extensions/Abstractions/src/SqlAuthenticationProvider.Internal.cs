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
        /// Static construction performs the reflection lookups.
        /// </summary>
        static Internal()
        {
            const string assemblyName = "Microsoft.Data.SqlClient";

            // If the SqlClient assembly is present, load its
            // SqlAuthenticationProviderManager class and get/set methods.
            try
            {
                // Try to load the SqlClient assembly.

                #if STRONG_NAME_SIGNING

                // The expected public key token of the SqlClient assembly, used to avoid invoking
                // APIs from imposter assemblies.  This is the same token used by all assemblies in
                // this repository when strong-name signed.
                byte[] expectedPublicKeyToken =
                    [ 0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5 ];

                // When strong-name signing is enabled, build a fully-qualified AssemblyName that
                // includes the expected public key token.
                Log($"Attempting to load SqlClient assembly={assemblyName} with " +
                    "expected public key token=" +
                    BitConverter.ToString(expectedPublicKeyToken).Replace("-", ""));

                var qualifiedName = new AssemblyName(assemblyName);
                qualifiedName.SetPublicKeyToken(expectedPublicKeyToken);

                // The .NET Framework runtime enforces the token during binding, causing Load() to
                // throw if it doesn't match. The .NET (Core) runtime ignores the token, so we
                // verify it ourselves below.
                var assembly = Assembly.Load(qualifiedName);

                // Defense-in-depth: verify the public key token after loading.  This is necessary
                // on .NET Core where the runtime does not enforce the token. It is harmless on .NET
                // Framework.
                if (assembly is not null)
                {
                    byte[]? actualToken = assembly.GetName().GetPublicKeyToken();

                    if (actualToken is null ||
                        !actualToken.AsSpan().SequenceEqual(expectedPublicKeyToken))
                    {
                        Log($"SqlClient assembly={assembly.GetName()} has an " +
                            "unexpected public key token; " +
                            "Get/SetProvider() will not function");
                        return;
                    }
                }

                #else

                // Strong-name signing is disabled, so we cannot verify the public key token.
                Log($"Loading SqlClient assembly={assemblyName} without strong-name identity " +
                    "verification; ensure this assembly is from a trusted source");

                var assembly = Assembly.Load(assemblyName);

                #endif

                if (assembly is null)
                {
                    Log($"SqlClient assembly={assemblyName} not found; " +
                        "Get/SetProvider() will not function");
                    return;
                }

                // Look for the manager class.
                const string className = "Microsoft.Data.SqlClient.SqlAuthenticationProviderManager";
                Type? manager = assembly.GetType(className);

                if (manager is null)
                {
                    Log($"SqlClient auth manager class={className} not found; " +
                        "Get/SetProvider() will not function");
                    return;
                }

                // Get handles to the get/set static methods.
                _getProvider = manager.GetMethod(
                    "GetProvider",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_getProvider is null)
                {
                    Log($"SqlClient GetProvider() method not found; " +
                        "GetProvider() will not function");
                }

                _setProvider = manager.GetMethod(
                    "SetProvider",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_setProvider is null)
                {
                    Log($"SqlClient SetProvider() method not found; " +
                        "SetProvider() will not function");
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
                Log($"SqlClient assembly={assemblyName} not found or not usable; " +
                    $"Get/SetProvider() will not function: {ex} ");
            }
            // Any other exceptions are fatal.
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

        private static void Log(string message)
        {
            SqlClientEventSource.Log.TryTraceEvent("SqlAuthenticationProvider.Internal | {0}", message);
        }
    }
}
