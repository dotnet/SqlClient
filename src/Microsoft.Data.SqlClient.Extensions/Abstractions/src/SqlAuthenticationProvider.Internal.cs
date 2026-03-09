// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Extensions.Abstractions;
using Microsoft.Data.SqlClient.Extensions.Abstractions.Logging;
using System.Reflection;

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

            // If the MDS package is present, load its
            // SqlAuthenticationProviderManager class and get/set methods.
            try
            {
                // Try to load the MDS assembly.
                var assembly = Assembly.Load(assemblyName);

                if (assembly is null)
                {
                    Logger.TraceLogger?.AssemblyNotFound(assemblyName);
                    return;
                }

                // TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39845):
                // Verify the assembly is signed by us?

                // Look for the manager class.
                const string className = "Microsoft.Data.SqlClient.SqlAuthenticationProviderManager";
                var manager = assembly.GetType(className);

                if (manager is null)
                {
                    Logger.TraceLogger?.AuthManagerClassNotFound(className);
                    return;
                }

                // Get handles to the get/set static methods.
                _getProvider = manager.GetMethod(
                    "GetProvider",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_getProvider is null)
                {
                    Logger.TraceLogger?.GetProviderMethodNotFound();
                }

                _setProvider = manager.GetMethod(
                    "SetProvider",
                    BindingFlags.NonPublic | BindingFlags.Static);

                if (_setProvider is null)
                {
                    Logger.TraceLogger?.SetProviderMethodNotFound();
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
                Logger.TraceLogger?.AssemblyNotFoundOrUsable(assemblyName, ex.ToString());
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
                Logger.TraceLogger?.GetProviderInvocationFailed(ex.GetType().Name, ex.ToString());
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
                    Logger.TraceLogger?.SetProviderInvocationReturnedNull();
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
                Logger.TraceLogger?.SetProviderInvocationFailed(ex.GetType().Name, ex.ToString());
                return false;
            }
        }
    }
}
