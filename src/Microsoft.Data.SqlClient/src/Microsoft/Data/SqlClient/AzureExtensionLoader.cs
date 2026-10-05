// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Loads our Azure extension assembly, Microsoft.Data.SqlClient.Extensions.Azure, if the
    /// application references it.
    /// </summary>
    internal static class AzureExtensionLoader
    {
        // The name of our Azure extension assembly.
        internal const string AssemblyName = "Microsoft.Data.SqlClient.Extensions.Azure";

        // The public key token of our Azure extension assembly, used to avoid loading imposter
        // assemblies.
        private static readonly byte[] s_publicKeyToken = [ 0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5 ];

        /// <summary>
        /// Loads the Azure extension assembly.
        /// </summary>
        /// <param name="caller">The name of the caller, for tracing.</param>
        /// <returns>
        /// The assembly, or null if it was not found or has an unexpected public key token.
        /// </returns>
        /// <exception cref="System.IO.FileNotFoundException">The assembly is not present.</exception>
        /// <exception cref="System.IO.FileLoadException">The assembly could not be loaded.</exception>
        /// <exception cref="BadImageFormatException">The assembly is not valid.</exception>
        internal static Assembly? Load(string caller)
        {
            #if STRONG_NAME_SIGNING

            // When strong-name signing is enabled, build a fully-qualified AssemblyName
            // that includes the expected public key token.

            SqlClientEventSource.Log.TryTraceEvent(
                "{0}: Attempting to load Azure extension assembly={1} with " +
                "expected public key token={2}",
                caller,
                AssemblyName,
                BitConverter.ToString(s_publicKeyToken).Replace("-", ""));

            var qualifiedName = new AssemblyName(AssemblyName);
            qualifiedName.SetPublicKeyToken(s_publicKeyToken);

            // The .NET Framework runtime will enforce the token during binding, causing Load()
            // to throw.  This prevents an untrusted assembly from being loaded and having its
            // module initializers run.  This will throw if the public key token doesn't match.
            //
            // The .NET runtime ignores the public key token and will happily load any assembly
            // with the same simple name.
            //
            var assembly = Assembly.Load(qualifiedName);

            #if NET
            // For the .NET runtime, we will check the public key token ourselves.
            //
            // Note that a null assembly is handled below.
            if (assembly is not null)
            {
                byte[]? actualToken = assembly.GetName().GetPublicKeyToken();

                if (actualToken is null || !actualToken.AsSpan().SequenceEqual(s_publicKeyToken))
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        "{0}: Azure extension assembly={1} has an " +
                        "unexpected public key token",
                        caller,
                        assembly.GetName());
                    return null;
                }
            }
            #endif

            #else

            SqlClientEventSource.Log.TryTraceEvent(
                "{0}: Attempting to load Azure extension assembly={1} without " +
                "strong name verification; ensure this assembly is from a trusted source",
                caller,
                AssemblyName);

            var assembly = Assembly.Load(AssemblyName);

            #endif

            if (assembly is null)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "{0}: Azure extension assembly={1} not found",
                    caller,
                    AssemblyName);
                return null;
            }

            return assembly;
        }
    }
}
