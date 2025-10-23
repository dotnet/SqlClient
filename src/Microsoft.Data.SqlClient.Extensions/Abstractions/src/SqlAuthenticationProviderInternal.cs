// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationProvider.xml' path='docs/members[@name="SqlAuthenticationProvider"]/SqlAuthenticationProvider/*'/>
public abstract partial class SqlAuthenticationProvider
{
    // This class implements the obsolete static GetProvider and SetProvider
    // methods by using reflection to call into the Microsoft.Data.SqlClient
    // package's SqlAuthenticationProviderManager class, if that assembly is
    // present.
    private static class Internal
    {
        // Handles to the reflected get/set methods.
        private static MethodInfo? _getProvider = null;
        private static MethodInfo? _setProvider = null;

        // Static construction performs the reflection lookups.
        static Internal()
        {
            // If the MDS package is present, load its
            // SqlAuthenticationProviderManager class and get/set methods.
            const string assemblyName = "Microsoft.Data.SqlClient";

            try
            {
                // Try to load the MDS assembly.
                var assembly = Assembly.Load(assemblyName);

                if (assembly is null)
                {
                    // TODO: Logging
                    // SqlClientEventSource.Log.TryTraceEvent(
                    //     nameof(SqlAuthenticationProviderManager) +
                    //     $": Azure extension assembly={assemblyName} not found; " +
                    //     "no default provider installed");
                    return;
                }

                // TODO(ADO-39845): Verify the assembly is signed by us?

                // TODO: Logging
                // SqlClientEventSource.Log.TryTraceEvent(
                //     nameof(SqlAuthenticationProviderManager) +
                //     $": Azure extension assembly={assemblyName} found; " +
                //     "attempting to set as default provider for all Active " +
                //     "Directory authentication methods");

                // Look for the manager class.
                const string className = "Microsoft.Data.SqlClient.SqlAuthenticationProviderManager";
                var manager = assembly.GetType(className);

                if (manager is null)
                {
                    // TODO: Logging
                    // SqlClientEventSource.Log.TryTraceEvent(
                    //     nameof(SqlAuthenticationProviderManager) +
                    //     $": Azure extension does not contain class={className}; " +
                    //     "no default Active Directory provider installed");

                    return;
                }

                // Get handles to the get/set static methods.
                _getProvider = manager.GetMethod(
                    "GetProvider",
                    BindingFlags.Public | BindingFlags.Static);
                _setProvider = manager.GetMethod(
                    "SetProvider",
                    BindingFlags.Public | BindingFlags.Static);

                // TODO: Logging
                // SqlClientEventSource.Log.TryTraceEvent(
                //     nameof(SqlAuthenticationProviderManager) +
                //     $": Azure extension class={className} installed as " +
                //     "provider for all Active Directory authentication methods");
            }
            // All of these exceptions mean we couldn't find the get/set
            // methods.
            catch (Exception ex)
            when (ex is AmbiguousMatchException ||
                  ex is BadImageFormatException ||
                  ex is FileLoadException ||
                  ex is FileNotFoundException)
            {
                // SqlClientEventSource.Log.TryTraceEvent(
                //     nameof(SqlAuthenticationProviderManager) +
                //     $": Azure extension assembly={assemblyName} not found or " +
                //     "not usable; no default provider installed; " +
                //     $"{ex.GetType().Name}: {ex.Message}");
            }
            // Any other exceptions are fatal.
        }

        // Call the reflected GetProvider method.
        //
        // Returns null if reflection failed or any exceptions occur.
        // Otherwise, returns as the reflected method does.
        //
        public static SqlAuthenticationProvider? GetProvider(
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
            when (ex is InvalidOperationException ||
                  ex is MemberAccessException ||
                  ex is MethodAccessException ||
                  ex is NotSupportedException ||
                  ex is TargetInvocationException)
            {
                return null;
            }
        }


        // Call the reflected SetProvider method.
        //
        // Returns false if reflection failed, invocation fails, or any
        // exceptions occur.  Otherwise, returns as the reflected method does.
        //
        public static bool SetProvider(
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
                    return false;
                }

                return result.Value;
            }
            catch (Exception ex)
            when (ex is InvalidOperationException ||
                  ex is MemberAccessException ||
                  ex is MethodAccessException ||
                  ex is NotSupportedException ||
                  ex is TargetInvocationException)
            {
                return false;
            }
        }
    }
}
