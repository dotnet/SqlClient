// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
#if NET
using System.Net.Security;
#endif
using System.Security.Cryptography.X509Certificates;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.NativeSni;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Reads what native SNI recorded for the most recent client TLS handshake on a connection and
    /// records it into the login attempt's <see cref="EncryptionInfoAccumulator"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The native exports used here (<c>SNIGetCertValidationInfoWrapper</c> and
    /// <c>SNIGetServerCertificateWrapper</c>) are not present in older SNI libraries.
    /// <see cref="SniNativeWrapper"/> detects a missing export once per process and then returns
    /// <see cref="SniNativeWrapper.ErrorProcNotFound"/>, which this class treats like any other
    /// error code: the corresponding values stay unavailable. Connections are never affected.
    /// </para>
    /// <para>
    /// A value is reported only when native SNI marked it as available; nothing is synthesized from
    /// error codes or messages.
    /// </para>
    /// </remarks>
    internal sealed class SniNativeCertificateCapture
    {
        /// <summary>Win32 <c>ERROR_SUCCESS</c>: the query succeeded.</summary>
        internal const uint ERROR_SUCCESS = 0;

        /// <summary>Win32 <c>ERROR_INVALID_PARAMETER</c>: returned for a missing handle.</summary>
        internal const uint ERROR_INVALID_PARAMETER = 87;

        /// <summary>
        /// Win32 <c>ERROR_INSUFFICIENT_BUFFER</c>: the certificate buffer is too small. The size
        /// query (an empty buffer) returns this code together with the certificate size.
        /// </summary>
        internal const uint ERROR_INSUFFICIENT_BUFFER = 122;

        /// <summary>Win32 <c>ERROR_NOT_FOUND</c>: native SNI captured no certificate.</summary>
        internal const uint ERROR_NOT_FOUND = 1168;

        /// <summary>Queries <c>SNIGetCertValidationInfoWrapper</c>.</summary>
        internal delegate uint CertValidationInfoQuery(
            SNIHandle? handle,
            ref SniCertValidationInfo info);

        /// <summary>
        /// Queries <c>SNIGetServerCertificateWrapper</c>. An empty <paramref name="certificate"/>
        /// buffer requests only the size.
        /// </summary>
        internal delegate uint ServerCertificateQuery(
            SNIHandle? handle,
            Span<byte> certificate,
            out uint certificateSize);

        /// <summary>Reads the validation details; native SNI in production.</summary>
        private readonly CertValidationInfoQuery _validationInfoQuery;

        /// <summary>Reads the presented certificate; native SNI in production.</summary>
        private readonly ServerCertificateQuery _certificateQuery;

        /// <summary>
        /// Creates an instance bound to the loaded SNI library through
        /// <see cref="SniNativeWrapper"/>. A missing handle is rejected with
        /// <see cref="ERROR_INVALID_PARAMETER"/> without calling native code.
        /// </summary>
        internal SniNativeCertificateCapture()
            : this(QueryNativeValidationInfo, QueryNativeCertificate)
        {
        }

        /// <summary>
        /// Creates an instance that reads through the given delegates. Used by tests to substitute
        /// native SNI.
        /// </summary>
        /// <param name="validationInfoQuery">Replaces <c>SNIGetCertValidationInfoWrapper</c>.</param>
        /// <param name="certificateQuery">Replaces <c>SNIGetServerCertificateWrapper</c>.</param>
        internal SniNativeCertificateCapture(
            CertValidationInfoQuery validationInfoQuery,
            ServerCertificateQuery certificateQuery)
        {
            _validationInfoQuery = validationInfoQuery;
            _certificateQuery = certificateQuery;
        }

        /// <summary>
        /// Records what native SNI kept for the most recent TLS handshake on
        /// <paramref name="handle"/> into <paramref name="accumulator"/>.
        /// </summary>
        /// <remarks>
        /// Native SNI is the single source of these values, so a successful query replaces all five
        /// of them, including with <see langword="null"/>. When the validation details cannot be
        /// read (the export is missing or the query returns an error code), the accumulator is left
        /// unchanged.
        /// </remarks>
        internal void Record(SNIHandle? handle, EncryptionInfoAccumulator? accumulator)
        {
            // Nothing to record into (a code path that does not track encryption info).
            if (accumulator is null)
            {
                return;
            }

            // The validation details are the anchor: they say which values native SNI captured.
            // Without them nothing is known, so keep whatever the accumulator already holds.
            SniCertValidationInfo info = default;
            if (_validationInfoQuery(handle, ref info) != ERROR_SUCCESS)
            {
                return;
            }

            // Read the certificate only when native SNI says it captured one, and only when its
            // size is reportable. This is the single size check; it runs before any buffer is
            // rented so an oversized certificate is never copied.
            if ((info.dwAvailable & (uint)SniCertInfoAvailability.Certificate) == 0
                || !TryQueryCertificateSize(handle, out int size))
            {
                Apply(info, derBytes: null, accumulator);
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
                // The certificate is used only if it was read in full and its size did not change
                // between the two calls; otherwise it is reported as unavailable.
                uint result = _certificateQuery(handle, buffer.AsSpan(0, size), out uint written);
                ReadOnlyMemory<byte>? derBytes = result == ERROR_SUCCESS && written == (uint)size
                    ? new ReadOnlyMemory<byte>(buffer, 0, size)
                    : null;

                // The accumulator stores its own copy, so the pooled buffer can be returned.
                Apply(info, derBytes, accumulator);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Queries the size of the presented certificate.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if native SNI reported a certificate whose size is accepted by
        /// <see cref="EncryptionInfoAccumulator.IsReportableServerCertificateSize"/>.
        /// </returns>
        private bool TryQueryCertificateSize(SNIHandle? handle, out int size)
        {
            // An empty buffer is a size query; native SNI answers with ERROR_INSUFFICIENT_BUFFER and
            // the size. Any other code (including a missing export) means there is no certificate.
            size = 0;
            uint result = _certificateQuery(handle, Span<byte>.Empty, out uint reported);
            if (result != ERROR_INSUFFICIENT_BUFFER)
            {
                return false;
            }

            // A size above int.MaxValue becomes negative here and is rejected by the size check.
            int candidate = unchecked((int)reported);
            if (!EncryptionInfoAccumulator.IsReportableServerCertificateSize(candidate))
            {
                return false;
            }

            size = candidate;
            return true;
        }

        /// <summary>
        /// Records a native <see cref="SniCertValidationInfo"/> and the separately read presented
        /// certificate into <paramref name="accumulator"/>. Only values whose availability flag is
        /// set are reported; all others are set to <see langword="null"/>.
        /// </summary>
        /// <param name="info">The structure returned by native SNI.</param>
        /// <param name="derBytes">
        /// The DER encoding read by <see cref="Record"/>, which has already checked availability and
        /// size, or <see langword="null"/> if no certificate is reported.
        /// </param>
        /// <param name="accumulator">The accumulator of the in-progress login attempt.</param>
        private static void Apply(
            SniCertValidationInfo info,
            ReadOnlyMemory<byte>? derBytes,
            EncryptionInfoAccumulator accumulator)
        {
            SniCertInfoAvailability available = (SniCertInfoAvailability)info.dwAvailable;

            // Record has already applied the availability and size checks. The accumulator still
            // enforces its own cap, which is its contract for every transport.
            accumulator.ServerCertificateDerBytes = derBytes;

            // Bit values are defined to be identical to the managed enums; unknown bits are preserved.
            accumulator.ValidationErrors = (available & SniCertInfoAvailability.ValidationErrors) != 0
                ? (SqlServerCertificateValidationErrors)info.dwValidationErrors
                : null;

            // ChainStatusLeafTimeOnly means only the pinned leaf's validity period was checked and no
            // chain was built. ChainStatus describes a built chain, so it stays unknown, as with
            // Managed SNI for a pinned certificate.
            accumulator.ChainStatus = (available & SniCertInfoAvailability.ChainStatus) != 0
                && (available & SniCertInfoAvailability.ChainStatusLeafTimeOnly) == 0
                ? (X509ChainStatusFlags)info.dwChainStatus
                : null;

            // Native SNI reads the protocol from the Schannel connection info after the server's
            // ServerHello, so this is the version the server selected, not merely the one offered.
            // An unmapped protocol is stored as null by the accumulator.
            accumulator.Protocol = (available & SniCertInfoAvailability.Protocol) != 0
                ? SniNativeWrapper.ToSslProtocols((SniSslProtocols)info.dwProtocol)
                : null;

            accumulator.CipherSuite = MapCipherSuite(info);
        }

        /// <summary>
        /// Maps the cipher suite, or <see langword="null"/> if it is not available, is the null
        /// cipher suite (TLS_NULL_WITH_NULL_NULL, 0), or is not a valid IANA identifier.
        /// </summary>
#if NET
        private static TlsCipherSuite? MapCipherSuite(SniCertValidationInfo info)
#else
        private static int? MapCipherSuite(SniCertValidationInfo info)
#endif
        {
            // IANA cipher suite identifiers are 16-bit; anything else is not a real suite.
            if ((info.dwAvailable & (uint)SniCertInfoAvailability.CipherSuite) == 0
                || info.dwCipherSuite == 0
                || info.dwCipherSuite > ushort.MaxValue)
            {
                return null;
            }

#if NET
            return (TlsCipherSuite)(ushort)info.dwCipherSuite;
#else
            return (int)info.dwCipherSuite;
#endif
        }

        /// <summary>Production validation info query; rejects a missing handle.</summary>
        private static uint QueryNativeValidationInfo(
            SNIHandle? handle,
            ref SniCertValidationInfo info) =>
            handle is null
                ? ERROR_INVALID_PARAMETER
                : SniNativeWrapper.SniGetCertValidationInfo(handle, ref info);

        /// <summary>Production certificate query; rejects a missing handle.</summary>
        private static uint QueryNativeCertificate(
            SNIHandle? handle,
            Span<byte> certificate,
            out uint certificateSize)
        {
            if (handle is null)
            {
                certificateSize = 0;
                return ERROR_INVALID_PARAMETER;
            }

            return SniNativeWrapper.SniGetServerCertificate(handle, certificate, out certificateSize);
        }
    }
}
