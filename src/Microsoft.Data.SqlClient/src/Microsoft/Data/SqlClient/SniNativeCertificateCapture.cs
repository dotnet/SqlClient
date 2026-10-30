// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Data.SqlClient.NativeSni;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Reads the server certificate details that native SNI recorded for the most recent client TLS
    /// handshake on a connection and maps them to a <see cref="ServerCertificateCapture"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The native exports used here (<c>SNIGetCertValidationInfoWrapper</c> and
    /// <c>SNIGetServerCertificateWrapper</c>) are not present in older SNI libraries. The first call
    /// that finds an export missing records that fact, and subsequent captures skip it, leaving the
    /// corresponding fields unavailable. Connections are never affected.
    /// </para>
    /// <para>
    /// Capture is diagnostic only: it never throws, never changes the connection outcome or the
    /// exception that is reported, and never synthesizes a value that native SNI did not mark as
    /// available.
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
        /// Queries <c>SNIGetServerCertificateWrapper</c>. An empty <paramref name="certificate"/> buffer
        /// requests only the size.
        /// </summary>
        internal delegate uint ServerCertificateQuery(SNIHandle? handle, Span<byte> certificate, out uint certificateSize);

        /// <summary>
        /// The instance used by <see cref="Parser.TdsParserStateObjectNative"/>, bound to the loaded SNI library.
        /// </summary>
        internal static SniNativeCertificateCapture Default { get; } = new SniNativeCertificateCapture(
            static (SNIHandle? handle, ref SniCertValidationInfo info) => SniNativeWrapper.SniGetCertValidationInfo(handle!, ref info),
            static (SNIHandle? handle, Span<byte> certificate, out uint certificateSize) => SniNativeWrapper.SniGetServerCertificate(handle!, certificate, out certificateSize));

        private readonly CertValidationInfoQuery _validationInfoQuery;
        private readonly ServerCertificateQuery _certificateQuery;

        private volatile bool _validationInfoUnavailable;
        private volatile bool _certificateUnavailable;

        internal SniNativeCertificateCapture(CertValidationInfoQuery validationInfoQuery, ServerCertificateQuery certificateQuery)
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
        /// Reads the certificate details native SNI recorded for <paramref name="handle"/>.
        /// </summary>
        /// <returns>
        /// The captured details, or <see langword="null"/> when native SNI reported nothing usable,
        /// the exports are not available, or the query failed.
        /// </returns>
        internal ServerCertificateCapture? TryCapture(SNIHandle? handle)
        {
            if (_validationInfoUnavailable)
            {
                return null;
            }

            try
            {
                SniCertValidationInfo info = SniCertValidationInfo.Create();
                uint result;
                try
                {
                    result = _validationInfoQuery(handle, ref info);
                }
                catch (EntryPointNotFoundException)
                {
                    _validationInfoUnavailable = true;
                    SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SniNativeCertificateCapture.TryCapture|ADV> SNIGetCertValidationInfoWrapper is not exported by the loaded SNI library; certificate details are unavailable.");
                    return null;
                }

                if (result != ERROR_SUCCESS || !IsValid(info))
                {
                    return null;
                }

                byte[]? rawCertificate = (info.dwAvailable & (uint)SniCertInfoAvailability.Certificate) != 0
                    ? TryReadCertificate(handle)
                    : null;

                return Map(info, rawCertificate);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SniNativeCertificateCapture.TryCapture|ADV> Certificate details unavailable; capture failed with {0}.", e.GetType().Name);
                return null;
            }
        }

        /// <summary>
        /// Reads the presented certificate with a size query followed by a copy. Any failure leaves
        /// the certificate unavailable without affecting the other captured fields.
        /// </summary>
        private byte[]? TryReadCertificate(SNIHandle? handle)
        {
            if (_certificateUnavailable)
            {
                return null;
            }

            try
            {
                uint result = _certificateQuery(handle, Span<byte>.Empty, out uint size);
                if (result != ERROR_INSUFFICIENT_BUFFER || size == 0 || size > ServerCertificateCapture.MaxRawCertificateSize)
                {
                    return null;
                }

                byte[] certificate = new byte[size];
                result = _certificateQuery(handle, certificate, out uint written);
                return result == ERROR_SUCCESS && written == size ? certificate : null;
            }
            catch (EntryPointNotFoundException)
            {
                _certificateUnavailable = true;
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SniNativeCertificateCapture.TryCapture|ADV> SNIGetServerCertificateWrapper is not exported by the loaded SNI library; the presented certificate is unavailable.");
                return null;
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SniNativeCertificateCapture.TryCapture|ADV> Presented certificate unavailable; read failed with {0}.", e.GetType().Name);
                return null;
            }
        }

        private static bool IsValid(SniCertValidationInfo info) =>
            info.cbSize >= SniCertValidationInfo.Size && info.dwVersion >= SniCertValidationInfo.CurrentVersion;

        /// <summary>
        /// Maps a native <see cref="SniCertValidationInfo"/> and the separately read presented
        /// certificate to a <see cref="ServerCertificateCapture"/>. Only fields whose availability
        /// flag is set are populated; all others remain <see langword="null"/>.
        /// </summary>
        /// <param name="info">The structure returned by native SNI.</param>
        /// <param name="rawCertificate">
        /// The DER encoding read with <c>SNIGetServerCertificateWrapper</c>, or <see langword="null"/>.
        /// Ignored unless <see cref="SniCertInfoAvailability.Certificate"/> is set.
        /// </param>
        /// <returns>
        /// The mapped details, or <see langword="null"/> if the structure is malformed or reports no
        /// usable field.
        /// </returns>
        internal static ServerCertificateCapture? Map(SniCertValidationInfo info, byte[]? rawCertificate = null)
        {
            if (!IsValid(info))
            {
                return null;
            }

            SniCertInfoAvailability available = (SniCertInfoAvailability)info.dwAvailable;

            byte[]? certificate = (available & SniCertInfoAvailability.Certificate) != 0 &&
                rawCertificate is { Length: > 0 and <= ServerCertificateCapture.MaxRawCertificateSize }
                ? rawCertificate
                : null;

            // Bit values are defined to be identical to the managed enums; unknown bits are preserved.
            SqlServerCertificateValidationErrors? validationErrors = (available & SniCertInfoAvailability.ValidationErrors) != 0
                ? (SqlServerCertificateValidationErrors)info.dwValidationErrors
                : null;

            X509ChainStatusFlags? chainStatus = (available & SniCertInfoAvailability.ChainStatus) != 0
                ? (X509ChainStatusFlags)info.dwChainStatus
                : null;

            SslProtocols? protocol = null;
            if ((available & SniCertInfoAvailability.Protocol) != 0)
            {
                SslProtocols mapped = SniNativeWrapper.ToSslProtocols((SniSslProtocols)info.dwProtocol);
                if (mapped != SslProtocols.None)
                {
                    protocol = mapped;
                }
            }

            int? cipherSuite = null;
            if ((available & SniCertInfoAvailability.CipherSuite) != 0 &&
                info.dwCipherSuite != 0 &&
                info.dwCipherSuite <= ushort.MaxValue)
            {
                cipherSuite = (int)info.dwCipherSuite;
            }

            if (certificate is null && validationErrors is null && chainStatus is null && protocol is null && cipherSuite is null)
            {
                return null;
            }

            return new ServerCertificateCapture(certificate, validationErrors, chainStatus, protocol, cipherSuite);
        }
    }
}
