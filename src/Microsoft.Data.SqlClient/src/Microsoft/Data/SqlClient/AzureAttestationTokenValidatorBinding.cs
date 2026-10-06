// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Binds to the Azure Attestation token validator in our Azure extension assembly,
    /// Microsoft.Data.SqlClient.Extensions.Azure.
    /// </summary>
    /// <remarks>
    /// Validating Azure Attestation tokens requires the Microsoft.IdentityModel packages. That code
    /// lives in the Azure extension so that applications which don't use Azure Attestation don't
    /// depend on those packages. The validator's members use only BCL types, so we bind to them by
    /// name and call them through delegates.
    /// </remarks>
    internal sealed class AzureAttestationTokenValidatorBinding
    {
        // The status values below duplicate the constants of the same names in the Azure
        // extension's AzureAttestationTokenValidator, and must be kept in sync with them.

        /// <summary>
        /// The token is valid. The error is null.
        /// </summary>
        internal const int TokenValid = 0;

        /// <summary>
        /// The token has expired. Retrying won't help.
        /// </summary>
        internal const int TokenExpired = 1;

        /// <summary>
        /// The token failed validation, possibly because the cached signing keys are stale.
        /// Retry once with refreshed signing keys.
        /// </summary>
        internal const int TokenValidationFailed = 2;

        /// <summary>
        /// The token couldn't be validated at all. Retrying won't help.
        /// </summary>
        internal const int TokenInvalid = 3;

        /// <summary>
        /// The signing keys couldn't be retrieved from the attestation instance.
        /// </summary>
        internal const int SigningKeysUnavailable = 4;

        /// <summary>
        /// The full name of the validator type in the Azure extension.
        /// </summary>
        private const string ValidatorTypeName = "Microsoft.Data.SqlClient.Extensions.Azure.AzureAttestationTokenValidator";

        /// <summary>
        /// The binding, created on first use. Binding failures are cached as a null value.
        /// </summary>
        private static readonly Lazy<AzureAttestationTokenValidatorBinding?> s_instance =
            new(Bind, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// The signature of the validator's ValidateToken method.
        /// </summary>
        /// <param name="attestationToken">The attestation token (JWT).</param>
        /// <param name="attestationInstanceUrl">The attestation instance url, which is also the expected issuer.</param>
        /// <param name="forceRefreshSigningKeys">True to download the signing keys even if cached.</param>
        /// <param name="error">The failure, as described by the status returned; null when valid.</param>
        /// <returns>One of the status constants.</returns>
        internal delegate int ValidateTokenDelegate(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, out Exception? error);

        /// <summary>
        /// Creates a binding from the validator's methods. Internal rather than private so that
        /// tests can bind the provider to a fake validator.
        /// </summary>
        /// <param name="validateToken">The validator's ValidateToken method.</param>
        /// <param name="validateTokenAsync">The validator's ValidateTokenAsync method.</param>
        /// <param name="readClaims">The validator's ReadClaims method.</param>
        internal AzureAttestationTokenValidatorBinding(
            ValidateTokenDelegate validateToken,
            Func<string, string, bool, CancellationToken, Task<Tuple<int, Exception?>>> validateTokenAsync,
            Func<string, Dictionary<string, string>> readClaims)
        {
            ValidateToken = validateToken;
            ValidateTokenAsync = validateTokenAsync;
            ReadClaims = readClaims;
        }

        /// <summary>
        /// The binding to the Azure extension's validator, or null if the Azure extension is not
        /// available.
        /// </summary>
        /// <remarks>
        /// The extension is loaded once per load of this assembly. If the application loads
        /// SqlClient into more than one AssemblyLoadContext, each copy loads the extension
        /// separately, as the authentication provider manager does.
        /// </remarks>
        internal static AzureAttestationTokenValidatorBinding? Instance => s_instance.Value;

        /// <summary>
        /// A function that validates an attestation token against the signing keys of the
        /// attestation instance that issued it, downloading the keys if they aren't cached.
        /// Failures are reported through the status and error, not thrown; the function throws
        /// only <see cref="ArgumentException"/> for invalid arguments.
        /// </summary>
        internal ValidateTokenDelegate ValidateToken { get; }

        /// <summary>
        /// A function that is the asynchronous counterpart of <see cref="ValidateToken"/>,
        /// returning the status and error together. It also throws
        /// <see cref="OperationCanceledException"/> when its cancellation token is canceled.
        /// </summary>
        internal Func<string, string, bool, CancellationToken, Task<Tuple<int, Exception?>>> ValidateTokenAsync { get; }

        /// <summary>
        /// A function that reads the claims of a token without validating it. It throws
        /// <see cref="ArgumentException"/> if the token can't be parsed or has a claim type with
        /// more than one value.
        /// </summary>
        internal Func<string, Dictionary<string, string>> ReadClaims { get; }

        /// <summary>
        /// Loads the Azure extension and binds to its validator.
        /// </summary>
        /// <returns>The binding, or null if the extension or validator isn't available.</returns>
#if NET
        // Nothing references the validator statically, so without this the trimmer would remove
        // its methods (or the whole Azure extension) and Azure Attestation would stop working in
        // trimmed and NativeAOT apps. The dependency only applies when the app includes the
        // Azure extension.
        [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, ValidatorTypeName, AzureExtensionLoader.AssemblyName)]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026",
            Justification = "The validator type is preserved by the DynamicDependency above.")]
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2035",
            Justification = "The Azure extension is optional; when the app doesn't include it, Bind returns null.")]
#endif
        private static AzureAttestationTokenValidatorBinding? Bind()
        {
            try
            {
                Assembly? assembly = AzureExtensionLoader.Load(nameof(AzureAttestationTokenValidatorBinding));
                if (assembly is null)
                {
                    return null;
                }

                Type? type = assembly.GetType(ValidatorTypeName);
                if (type is null)
                {
                    SqlClientEventSource.Log.TryTraceEvent(
                        nameof(AzureAttestationTokenValidatorBinding) +
                        ": Azure extension does not contain class={0}; " +
                        "Azure Attestation is not available",
                        ValidatorTypeName);
                    return null;
                }

                AzureAttestationTokenValidatorBinding binding = new AzureAttestationTokenValidatorBinding(
                    CreateDelegate<ValidateTokenDelegate>(type, "ValidateToken"),
                    CreateDelegate<Func<string, string, bool, CancellationToken, Task<Tuple<int, Exception?>>>>(type, "ValidateTokenAsync"),
                    CreateDelegate<Func<string, Dictionary<string, string>>>(type, "ReadClaims"));

                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(AzureAttestationTokenValidatorBinding) +
                    ": Bound to class={0} in Azure extension assembly={1}; " +
                    "Azure Attestation is available",
                    ValidatorTypeName,
                    assembly.GetName());

                return binding;
            }
            // All of these exceptions mean we couldn't find or bind to the Azure extension's
            // validator, in which case Azure Attestation is not available.
            catch (Exception ex)
            when (ex is
                      AmbiguousMatchException or
                      ArgumentException or
                      BadImageFormatException or
                      FileLoadException or
                      FileNotFoundException or
                      MemberAccessException or
                      MissingMethodException or
                      NotSupportedException or
                      TypeInitializationException or
                      TypeLoadException)
            {
                SqlClientEventSource.Log.TryTraceEvent(
                    nameof(AzureAttestationTokenValidatorBinding) +
                    ": Azure extension assembly={0} not found or not usable; " +
                    "Azure Attestation is not available; {1}: {2}",
                    AzureExtensionLoader.AssemblyName,
                    ex.GetType().Name,
                    ex.Message);
                return null;
            }
            // Any other exceptions are fatal.
        }

        /// <summary>
        /// Creates a delegate for a non-public static method of the validator.
        /// </summary>
        /// <typeparam name="T">The delegate type, matching the method's signature.</typeparam>
        /// <param name="type">The validator type.</param>
        /// <param name="methodName">The method name.</param>
        /// <returns>The delegate.</returns>
        /// <exception cref="MissingMethodException">The method doesn't exist.</exception>
        /// <exception cref="ArgumentException">The method's signature doesn't match the delegate.</exception>
#if NET
        [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2070",
            Justification = "Only called from Bind, whose DynamicDependency preserves the validator's non-public methods.")]
#endif
        private static T CreateDelegate<T>(Type type, string methodName)
            where T : Delegate
        {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(type.FullName, methodName);

            // Throws ArgumentException if the method's signature doesn't match the delegate.
            return (T)Delegate.CreateDelegate(typeof(T), method);
        }
    }
}
