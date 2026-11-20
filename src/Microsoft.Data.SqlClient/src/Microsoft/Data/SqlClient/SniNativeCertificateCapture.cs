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
using Microsoft.Data.SqlClient.Internal;
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
    /// <c>SNIGetServerCertificateWrapper</c>) are not present in older SNI libraries. The first call
    /// that finds an export missing records that fact, and subsequent reads skip it, leaving the
    /// corresponding values unavailable. Connections are never affected by a missing export.
    /// </para>
    /// <para>
    /// A value is reported only when native SNI marked it as available; nothing is synthesized from
    /// error codes or messages.
    /// </para>
    /// </remarks>
    internal sealed class SniNativeCertificateCapture
    {
        internal const uint ERROR_SUCCESS = 0;
        internal const uint ERROR_INVALID_PARAMETER = 87;
        internal const uint ERROR_INSUFFICIENT_BUFFER = 122;
        internal const uint ERROR_NOT_FOUND = 1168;

        /// <summary>Queries <c>SNIGetCertValidationInfoWrapper</c>.</summary>
        internal delegate uint CertValidationInfoQuery(SNIHandle? handle, ref SniCertValidationInfo info);

        /// <summary>
        /// Queries <c>SNIGetServerCertificateWrapper</c>. An empty <paramref name="certificate"/>
        /// buffer requests only the size.
        /// </summary>
        internal delegate uint ServerCertificateQuery(
            SNIHandle? handle,
            Span<byte> certificate,
            out uint certificateSize);

        /// <summary>
        /// The instance used by <see cref="Parser.TdsParserStateObjectNative"/>, bound to the loaded
        /// SNI library. A missing handle is rejected without calling native code.
        /// </summary>
        internal static SniNativeCertificateCapture Default { get; } = new SniNativeCertificateCapture(
            static (SNIHandle? handle, ref SniCertValidationInfo info) =>
                handle is null
                    ? ERROR_INVALID_PARAMETER
                    : SniNativeWrapper.SniGetCertValidationInfo(handle, ref info),
            static (SNIHandle? handle, Span<byte> certificate, out uint certificateSize) =>
            {
                if (handle is null)
                {
                    certificateSize = 0;
                    return ERROR_INVALID_PARAMETER;
                }

                return SniNativeWrapper.SniGetServerCertificate(handle, certificate, out certificateSize);
            });

        private readonly CertValidationInfoQuery _validationInfoQuery;
        private readonly ServerCertificateQuery _certificateQuery;

        private volatile bool _validationInfoUnavailable;
        private volatile bool _certificateUnavailable;

        internal SniNativeCertificateCapture(
            CertValidationInfoQuery validationInfoQuery,
            ServerCertificateQuery certificateQuery)
        {
            _validationInfoQuery = validationInfoQuery;
            _certificateQuery = certificateQuery;
        }

        /// <summary>
        /// Gets a value indicating whether <c>SNIGetCertValidationInfoWrapper</c> was found to be
        /// missing from the loaded SNI library.
        /// </summary>
        internal bool IsValidationInfoUnavailable => _validationInfoUnavailable;

        /// <summary>
        /// Gets a value indicating whether <c>SNIGetServerCertificateWrapper</c> was found to be
        /// missing from the loaded SNI library.
        /// </summary>
        internal bool IsCertificateUnavailable => _certificateUnavailable;

        /// <summary>
        /// Records what native SNI kept for the most recent TLS handshake on
        /// <paramref name="handle"/> into <paramref name="accumulator"/>.
        /// </summary>
        /// <remarks>
        /// Native SNI is the single source of these values, so a usable report replaces all five of
        /// them, including with <see langword="null"/>. When native SNI reports nothing usable, the
        /// export is not available, or the query returns an error code, the accumulator is left
        /// unchanged.
        /// </remarks>
        internal void Record(SNIHandle? handle, EncryptionInfoAccumulator? accumulator)
        {
            if (accumulator is null || !TryQueryValidationInfo(handle, out SniCertValidationInfo info))
            {
                return;
            }

            if ((info.dwAvailable & (uint)SniCertInfoAvailability.Certificate) == 0
                || !TryQueryCertificateSize(handle, out int size))
            {
                Apply(info, derBytes: null, accumulator);
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            try
            {
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
        /// Queries <c>SNIGetCertValidationInfoWrapper</c>, caching a missing export.
        /// </summary>
        /// <returns><see langword="true"/> if the query succeeded and returned a valid structure.</returns>
        private bool TryQueryValidationInfo(SNIHandle? handle, out SniCertValidationInfo info)
        {
            info = default;
            if (_validationInfoUnavailable)
            {
                return false;
            }

            info = SniCertValidationInfo.Create();
            uint result;
            try
            {
                result = _validationInfoQuery(handle, ref info);
            }
            catch (EntryPointNotFoundException)
            {
                _validationInfoUnavailable = true;
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    "<sc.SniNativeCertificateCapture.Record|ADV> " +
                    "SNIGetCertValidationInfoWrapper is not exported by the loaded SNI library; " +
                    "handshake details are unavailable.");
                return false;
            }

            return result == ERROR_SUCCESS && IsValid(info);
        }

        /// <summary>
        /// Queries the size of the presented certificate, caching a missing export.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if native SNI reported a certificate whose size is within
        /// <see cref="EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize"/>.
        /// </returns>
        private bool TryQueryCertificateSize(SNIHandle? handle, out int size)
        {
            size = 0;
            if (_certificateUnavailable)
            {
                return false;
            }

            uint result;
            uint reported;
            try
            {
                result = _certificateQuery(handle, Span<byte>.Empty, out reported);
            }
            catch (EntryPointNotFoundException)
            {
                _certificateUnavailable = true;
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    "<sc.SniNativeCertificateCapture.Record|ADV> " +
                    "SNIGetServerCertificateWrapper is not exported by the loaded SNI library; " +
                    "the presented certificate is unavailable.");
                return false;
            }

            if (result != ERROR_INSUFFICIENT_BUFFER
                || reported > EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize
                || !EncryptionInfoAccumulator.IsReportableServerCertificateSize((int)reported))
            {
                return false;
            }

            size = (int)reported;
            return true;
        }

        private static bool IsValid(SniCertValidationInfo info) =>
            info.cbSize >= SniCertValidationInfo.Size && info.dwVersion >= SniCertValidationInfo.CurrentVersion;

        /// <summary>
        /// Records a native <see cref="SniCertValidationInfo"/> and the separately read presented
        /// certificate into <paramref name="accumulator"/>. Only values whose availability flag is
        /// set are reported; all others are set to <see langword="null"/>. A malformed structure
        /// leaves the accumulator unchanged.
        /// </summary>
        /// <param name="info">The structure returned by native SNI.</param>
        /// <param name="derBytes">
        /// The DER encoding read with <c>SNIGetServerCertificateWrapper</c>, or
        /// <see langword="null"/>. Ignored unless <see cref="SniCertInfoAvailability.Certificate"/>
        /// is set.
        /// </param>
        /// <param name="accumulator">The accumulator of the in-progress login attempt.</param>
        internal static void Apply(
            SniCertValidationInfo info,
            ReadOnlyMemory<byte>? derBytes,
            EncryptionInfoAccumulator accumulator)
        {
            if (!IsValid(info))
            {
                return;
            }

            SniCertInfoAvailability available = (SniCertInfoAvailability)info.dwAvailable;

            // The accumulator stores a copy only when the encoding is within its cap.
            accumulator.ServerCertificateDerBytes = (available & SniCertInfoAvailability.Certificate) != 0
                ? derBytes
                : null;

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
        /// Maps the cipher suite of a valid structure, or <see langword="null"/> if it is not
        /// available, is the null cipher suite (TLS_NULL_WITH_NULL_NULL, 0), or is not a valid
        /// IANA identifier.
        /// </summary>
#if NET
        private static TlsCipherSuite? MapCipherSuite(SniCertValidationInfo info)
#else
        private static int? MapCipherSuite(SniCertValidationInfo info)
#endif
        {
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
    }
}
