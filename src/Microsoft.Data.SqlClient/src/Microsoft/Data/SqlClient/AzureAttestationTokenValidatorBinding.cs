// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
        // The token signature is valid.
        internal const int TokenValid = 0;

        // The token has expired.
        internal const int TokenExpired = 1;

        // The token failed validation, possibly because the cached signing keys are stale.
        internal const int TokenValidationFailed = 2;

        // The token is invalid and retrying will not help.
        internal const int TokenInvalid = 3;

        private const string ValidatorTypeName = "Microsoft.Data.SqlClient.Extensions.Azure.AzureAttestationTokenValidator";

        private static readonly Lazy<AzureAttestationTokenValidatorBinding?> s_instance =
            new(Bind, LazyThreadSafetyMode.ExecutionAndPublication);

        internal delegate int ValidateTokenSignatureDelegate(string attestationToken, string tokenIssuerUrl, object signingKeys, out Exception? error);

        internal AzureAttestationTokenValidatorBinding(
            Func<string, bool, object> getSigningKeys,
            Func<string, bool, CancellationToken, Task<object>> getSigningKeysAsync,
            ValidateTokenSignatureDelegate validateTokenSignature,
            Func<string, Dictionary<string, string>> readClaims)
        {
            GetSigningKeys = getSigningKeys;
            GetSigningKeysAsync = getSigningKeysAsync;
            ValidateTokenSignature = validateTokenSignature;
            ReadClaims = readClaims;
        }

        /// <summary>
        /// The binding to the Azure extension's validator, or null if the Azure extension is not
        /// available.
        /// </summary>
        internal static AzureAttestationTokenValidatorBinding? Instance => s_instance.Value;

        /// <summary>
        /// Downloads (or returns cached) token signing keys for an attestation instance url.
        /// Returns an opaque handle to pass to <see cref="ValidateTokenSignature"/>.
        /// </summary>
        internal Func<string, bool, object> GetSigningKeys { get; }

        /// <summary>
        /// Asynchronous counterpart of <see cref="GetSigningKeys"/>.
        /// </summary>
        internal Func<string, bool, CancellationToken, Task<object>> GetSigningKeysAsync { get; }

        /// <summary>
        /// Validates the token signature, lifetime and issuer. Returns one of the Token* constants.
        /// </summary>
        internal ValidateTokenSignatureDelegate ValidateTokenSignature { get; }

        /// <summary>
        /// Reads the claims of a token without validating it. Throws <see cref="ArgumentException"/>
        /// if the token cannot be parsed.
        /// </summary>
        internal Func<string, Dictionary<string, string>> ReadClaims { get; }

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

                return new AzureAttestationTokenValidatorBinding(
                    CreateDelegate<Func<string, bool, object>>(type, "GetSigningKeys"),
                    CreateDelegate<Func<string, bool, CancellationToken, Task<object>>>(type, "GetSigningKeysAsync"),
                    CreateDelegate<ValidateTokenSignatureDelegate>(type, "ValidateTokenSignature"),
                    CreateDelegate<Func<string, Dictionary<string, string>>>(type, "ReadClaims"));
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
