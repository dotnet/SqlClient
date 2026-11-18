// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
#if NET
using System.Net.Security;
#endif
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
    /// Reads what native SNI recorded for the most recent client TLS handshake on a connection and
    /// maps it to a <see cref="TlsHandshakeInfo"/>.
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
        /// SNI library.
        /// </summary>
        internal static SniNativeCertificateCapture Default { get; } = new SniNativeCertificateCapture(
            static (SNIHandle? handle, ref SniCertValidationInfo info) =>
                SniNativeWrapper.SniGetCertValidationInfo(handle!, ref info),
            static (SNIHandle? handle, Span<byte> certificate, out uint certificateSize) =>
                SniNativeWrapper.SniGetServerCertificate(handle!, certificate, out certificateSize));

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
        /// Reads the handshake record native SNI kept for <paramref name="handle"/>.
        /// </summary>
        /// <returns>
        /// The record, or <see langword="null"/> when native SNI reported nothing usable, the export
        /// is not available, or the query returned an error code.
        /// </returns>
        internal TlsHandshakeInfo? ReadTlsHandshakeInfo(SNIHandle? handle)
        {
            if (!TryQueryValidationInfo(handle, out SniCertValidationInfo info))
            {
                return null;
            }

            byte[]? derBytes = null;
            if ((info.dwAvailable & (uint)SniCertInfoAvailability.Certificate) != 0)
            {
                derBytes = TryReadCertificate(handle);
            }

            return CreateTlsHandshakeInfo(info, derBytes);
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
                    "<sc.SniNativeCertificateCapture.ReadTlsHandshakeInfo|ADV> " +
                    "SNIGetCertValidationInfoWrapper is not exported by the loaded SNI library; " +
                    "handshake details are unavailable.");
                return false;
            }

            return result == ERROR_SUCCESS && IsValid(info);
        }

        /// <summary>
        /// Reads the presented certificate with a size query followed by a copy, caching a missing
        /// export. An error code leaves the certificate unavailable without affecting the other
        /// values.
        /// </summary>
        private byte[]? TryReadCertificate(SNIHandle? handle)
        {
            if (_certificateUnavailable)
            {
                return null;
            }

            uint result;
            uint size;
            try
            {
                result = _certificateQuery(handle, Span<byte>.Empty, out size);
            }
            catch (EntryPointNotFoundException)
            {
                _certificateUnavailable = true;
                SqlClientEventSource.Log.TryAdvancedTraceEvent(
                    "<sc.SniNativeCertificateCapture.ReadTlsHandshakeInfo|ADV> " +
                    "SNIGetServerCertificateWrapper is not exported by the loaded SNI library; " +
                    "the presented certificate is unavailable.");
                return null;
            }

            if (result != ERROR_INSUFFICIENT_BUFFER || size == 0 || size > TlsHandshakeInfo.MaxDerBytesSize)
            {
                return null;
            }

            byte[] certificate = new byte[size];
            result = _certificateQuery(handle, certificate, out uint written);
            if (result != ERROR_SUCCESS || written != size)
            {
                return null;
            }

            return certificate;
        }

        private static bool IsValid(SniCertValidationInfo info) =>
            info.cbSize >= SniCertValidationInfo.Size && info.dwVersion >= SniCertValidationInfo.CurrentVersion;

        /// <summary>
        /// Maps a native <see cref="SniCertValidationInfo"/> and the separately read presented
        /// certificate to a <see cref="TlsHandshakeInfo"/>. Only values whose availability flag is
        /// set are populated; all others remain <see langword="null"/>.
        /// </summary>
        /// <param name="info">The structure returned by native SNI.</param>
        /// <param name="derBytes">
        /// The DER encoding read with <c>SNIGetServerCertificateWrapper</c>, or
        /// <see langword="null"/>. Ignored unless <see cref="SniCertInfoAvailability.Certificate"/>
        /// is set.
        /// </param>
        /// <returns>
        /// The record, or <see langword="null"/> if the structure is malformed or reports no usable
        /// value.
        /// </returns>
        internal static TlsHandshakeInfo? CreateTlsHandshakeInfo(SniCertValidationInfo info, byte[]? derBytes = null)
        {
            if (!IsValid(info))
            {
                return null;
            }

            SniCertInfoAvailability available = (SniCertInfoAvailability)info.dwAvailable;

            byte[]? certificate = null;
            if ((available & SniCertInfoAvailability.Certificate) != 0
                && derBytes is { Length: > 0 and <= TlsHandshakeInfo.MaxDerBytesSize })
            {
                certificate = derBytes;
            }

            // Bit values are defined to be identical to the managed enums; unknown bits are preserved.
            SqlServerCertificateValidationErrors? validationErrors = null;
            if ((available & SniCertInfoAvailability.ValidationErrors) != 0)
            {
                validationErrors = (SqlServerCertificateValidationErrors)info.dwValidationErrors;
            }

            // ChainStatusLeafTimeOnly means only the pinned leaf's validity period was checked and no
            // chain was built. ChainStatus describes a built chain, so it stays unknown, as with
            // Managed SNI for a pinned certificate.
            X509ChainStatusFlags? chainStatus = null;
            if ((available & SniCertInfoAvailability.ChainStatus) != 0
                && (available & SniCertInfoAvailability.ChainStatusLeafTimeOnly) == 0)
            {
                chainStatus = (X509ChainStatusFlags)info.dwChainStatus;
            }

            // Native SNI reads the protocol from the Schannel connection info after the server's
            // ServerHello, so this is the version the server selected, not merely the one offered.
            SslProtocols? protocol = null;
            if ((available & SniCertInfoAvailability.Protocol) != 0)
            {
                SslProtocols mapped = SniNativeWrapper.ToSslProtocols((SniSslProtocols)info.dwProtocol);
                if (mapped != SslProtocols.None)
                {
                    protocol = mapped;
                }
            }

#if NET
            TlsCipherSuite? cipherSuite = MapCipherSuite(info);
#else
            int? cipherSuite = MapCipherSuite(info);
#endif

            if (certificate is null
                && validationErrors is null
                && chainStatus is null
                && protocol is null
                && cipherSuite is null)
            {
                return null;
            }

            return new TlsHandshakeInfo(
                chainStatus,
                cipherSuite,
                certificate,
                protocol,
                validationErrors);
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
