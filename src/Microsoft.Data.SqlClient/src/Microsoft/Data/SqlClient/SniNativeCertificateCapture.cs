// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Internal;

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
    /// <c>SNIGetServerCertificateHashWrapper</c>) are not present in older SNI libraries. The first
    /// call that finds an export missing records that fact, and subsequent captures skip it, leaving
    /// the certificate fields unavailable. Connections are never affected.
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

        /// <summary>Queries <c>SNIGetServerCertificateHashWrapper</c>.</summary>
        internal delegate uint ServerCertificateHashQuery(SNIHandle? handle, Span<byte> hash, out uint bytesWritten);

        /// <summary>
        /// The instance used by <see cref="TdsParserStateObjectNative"/>, bound to the loaded SNI library.
        /// </summary>
        internal static SniNativeCertificateCapture Default { get; } = new SniNativeCertificateCapture(
            static (SNIHandle? handle, ref SniCertValidationInfo info) => SniNativeWrapper.SniGetCertValidationInfo(handle!, ref info),
            static (SNIHandle? handle, Span<byte> hash, out uint bytesWritten) => SniNativeWrapper.SniGetServerCertificateHash(handle!, hash, out bytesWritten));

        private readonly CertValidationInfoQuery _validationInfoQuery;
        private readonly ServerCertificateHashQuery _hashQuery;

        private volatile bool _validationInfoUnavailable;
        private volatile bool _hashUnavailable;

        internal SniNativeCertificateCapture(CertValidationInfoQuery validationInfoQuery, ServerCertificateHashQuery hashQuery)
        {
            _validationInfoQuery = validationInfoQuery;
            _hashQuery = hashQuery;
        }

        /// <summary>
        /// Gets a value indicating whether <c>SNIGetCertValidationInfoWrapper</c> was found to be
        /// missing from the loaded SNI library.
        /// </summary>
        internal bool IsValidationInfoUnavailable => _validationInfoUnavailable;

        /// <summary>
        /// Gets a value indicating whether <c>SNIGetServerCertificateHashWrapper</c> was found to be
        /// missing from the loaded SNI library.
        /// </summary>
        internal bool IsHashUnavailable => _hashUnavailable;

        /// <summary>
        /// Reads the certificate details native SNI recorded for <paramref name="handle"/>.
        /// </summary>
        /// <returns>
        /// The captured details, or <see langword="null"/> when native SNI reported nothing usable,
        /// the exports are not available, or the query failed.
        /// </returns>
        internal ServerCertificateCapture? TryCapture(SNIHandle? handle)
        {
            try
            {
                if (!_validationInfoUnavailable)
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
                        result = uint.MaxValue;
                    }

                    if (!_validationInfoUnavailable)
                    {
                        return result == ERROR_SUCCESS ? Map(info) : null;
                    }
                }

                // The structured export is missing; a library exporting only the hash function can
                // still identify the presented certificate.
                return TryCaptureHashOnly(handle);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SniNativeCertificateCapture.TryCapture|ADV> Certificate details unavailable; capture failed with {0}.", e.GetType().Name);
                return null;
            }
        }

        private ServerCertificateCapture? TryCaptureHashOnly(SNIHandle? handle)
        {
            if (_hashUnavailable)
            {
                return null;
            }

            byte[] hash = new byte[SniCertValidationInfo.HashLength];
            uint result;
            uint written;
            try
            {
                result = _hashQuery(handle, hash, out written);
            }
            catch (EntryPointNotFoundException)
            {
                _hashUnavailable = true;
                SqlClientEventSource.Log.TryAdvancedTraceEvent("<sc.SniNativeCertificateCapture.TryCapture|ADV> SNIGetServerCertificateHashWrapper is not exported by the loaded SNI library; certificate hash is unavailable.");
                return null;
            }

            return result == ERROR_SUCCESS && written == SniCertValidationInfo.HashLength
                ? new ServerCertificateCapture(hash, validationErrors: null, chainStatus: null)
                : null;
        }

        /// <summary>
        /// Maps a native <see cref="SniCertValidationInfo"/> to a <see cref="ServerCertificateCapture"/>.
        /// Only fields whose availability flag is set are populated; all others remain
        /// <see langword="null"/>.
        /// </summary>
        /// <returns>
        /// The mapped details, or <see langword="null"/> if the structure is malformed or reports no
        /// usable field.
        /// </returns>
        internal static unsafe ServerCertificateCapture? Map(SniCertValidationInfo info)
        {
            if (info.cbSize < SniCertValidationInfo.Size || info.dwVersion < SniCertValidationInfo.MinimumVersion)
            {
                return null;
            }

            SniCertInfoAvailability available = (SniCertInfoAvailability)info.dwAvailable;

            byte[]? hash = null;
            if ((available & SniCertInfoAvailability.Hash) != 0 && info.cbHash == SniCertValidationInfo.HashLength)
            {
                hash = new ReadOnlySpan<byte>(info.rgbHash, SniCertValidationInfo.HashLength).ToArray();
            }

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

            if (hash is null && validationErrors is null && chainStatus is null && protocol is null)
            {
                return null;
            }

            return new ServerCertificateCapture(hash, validationErrors, chainStatus, protocol);
        }
    }
}
