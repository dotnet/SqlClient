// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>Loads the optional Azure assembly for authentication discovery in Abstractions.</summary>
/// <remarks>
/// SqlClient owns an independent attestation loader so attestation does not depend on
/// authentication configuration, initializers or discovery switches. Keep their assembly
/// identity checks consistent.
/// </remarks>
internal static class AzureExtensionLoader
{
    internal const string AssemblyName = "Microsoft.Data.SqlClient.Extensions.Azure";

    private static readonly byte[] s_publicKeyToken = [0x23, 0xec, 0x7f, 0xc2, 0xd6, 0xea, 0xa4, 0xa5];

    /// <summary>Loads the extension and checks its identity in signed builds.</summary>
    /// <param name="caller">The caller name for diagnostics.</param>
    /// <returns>The trusted assembly, or null when missing, unloadable or untrusted.</returns>
    [RequiresUnreferencedCode("Azure authentication discovery loads an assembly by name.")]
    internal static Assembly? Load(string caller) => Load(caller, static assemblyName =>
    {
#if STRONG_NAME_SIGNING
        return Assembly.Load(assemblyName);
#else
        return Assembly.Load(assemblyName.Name!);
#endif
    });

    [RequiresUnreferencedCode("Azure authentication discovery loads an assembly by name.")]
    internal static Assembly? Load(string caller, Func<AssemblyName, Assembly> assemblyLoader)
    {
        try
        {
            var qualifiedName = new AssemblyName(AssemblyName);
#if STRONG_NAME_SIGNING
            SqlClientEventSource.Log.TryTraceEvent(
                "{0}: Attempting to load Azure extension assembly={1} with expected public key token={2}",
                caller, AssemblyName, BitConverter.ToString(s_publicKeyToken).Replace("-", ""));

            qualifiedName.SetPublicKeyToken(s_publicKeyToken);
            var assembly = assemblyLoader(qualifiedName);

#if !NETFRAMEWORK
            // Framework binding enforces the token; .NET and .NET Standard need this check.
            byte[]? actualToken = assembly.GetName().GetPublicKeyToken();
            if (actualToken is null || !actualToken.SequenceEqual(s_publicKeyToken))
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    "{0}: Azure extension assembly={1} has an unexpected public key token",
                    caller, assembly.GetName());
                return null;
            }
#endif
#else
            SqlClientEventSource.Log.TryTraceEvent(
                "{0}: Attempting to load Azure extension assembly={1} without strong name " +
                "verification; ensure this assembly is from a trusted source",
                caller, AssemblyName);
            var assembly = assemblyLoader(qualifiedName);
#endif
            return assembly;
        }
        catch (Exception ex)
        when ((ex is FileNotFoundException or FileLoadException or BadImageFormatException)
            && ExceptionHelpers.IsRecoverableException(ex))
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "{0}: Azure extension assembly={1} not found or not loadable; {2}: {3}",
                caller, AssemblyName, ex.GetType().Name, ex.Message);
            return null;
        }
    }
}
