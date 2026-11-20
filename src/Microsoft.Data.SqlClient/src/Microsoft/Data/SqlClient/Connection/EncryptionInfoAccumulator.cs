// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System.Net.Security;
#endif
using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    /// <summary>
    /// Mutable, per-physical-login-attempt accumulator for connection encryption details.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single instance is owned by a <see cref="SqlConnectionInternal"/> and is
    /// <see cref="Reset">reset</see> at the start of every physical login attempt (initial attempt,
    /// transient-error retry, routing redirect, and failover partner attempt), so only the values
    /// recorded by the terminal attempt are ever published.
    /// </para>
    /// <para>
    /// This type holds values and owns the rules for ones that cannot be reported (see
    /// <see cref="Protocol"/>, <see cref="CipherSuite"/> and
    /// <see cref="ServerCertificateDerBytes"/>); transports pass values through as observed. <see cref="Parser.TdsParser"/>
    /// records each value as soon as it becomes known. Values are only written during
    /// login, never on the per-command hot path. Callers convert the accumulated values into an
    /// immutable <see cref="SqlConnectionEncryptionInfo"/> snapshot via <see cref="ToSnapshot"/>.
    /// </para>
    /// </remarks>
    internal sealed class EncryptionInfoAccumulator
    {
        /// <summary>
        /// The largest DER-encoded server certificate that is reported: 64 KiB (65,536 bytes).
        /// </summary>
        /// <remarks>
        /// Bounds the memory retained per connection, pooled connection and exception when a
        /// hostile or misconfigured server presents an oversized certificate. Real leaf
        /// certificates are a few KiB. Matches native SNI's <c>SNI_CERT_MAX_CAPTURED_SIZE</c>.
        /// </remarks>
        internal const int MaxServerCertificateDerBytesSize = 64 * 1024;

        /// <summary>
        /// Gets or sets the combined chain status flags for the current attempt, or
        /// <see langword="null"/> if the chain was not consulted or is unavailable.
        /// </summary>
        public X509ChainStatusFlags? ChainStatus { get; set; }

#if NET
        /// <summary>
        /// Gets or sets the TLS cipher suite negotiated for the current attempt, or
        /// <see langword="null"/> if the transport did not report one.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="TlsCipherSuite.TLS_NULL_WITH_NULL_NULL"/> stores
        /// <see langword="null"/>: that suite is never negotiated, so it means none was reported.
        /// </remarks>
        public TlsCipherSuite? CipherSuite
        {
            get;
            set
            {
                if (value == TlsCipherSuite.TLS_NULL_WITH_NULL_NULL)
                {
                    value = null;
                }

                field = value;
            }
        }
#else
        /// <summary>
        /// Gets or sets the IANA TLS cipher suite identifier negotiated for the current attempt, or
        /// <see langword="null"/> if the transport did not report one.
        /// </summary>
        /// <remarks>
        /// Setting <c>0</c> (<c>TLS_NULL_WITH_NULL_NULL</c>) stores <see langword="null"/>: that
        /// suite is never negotiated, so it means none was reported. Values outside
        /// <c>0</c>..<c>0xFFFF</c> are not valid identifiers. Setting one is ignored rather than
        /// failing the login, and the property keeps its previous value.
        /// </remarks>
        public int? CipherSuite
        {
            get;
            set
            {
                if (value == 0)
                {
                    field = null;
                }
                else if (value is null or (> 0 and <= ushort.MaxValue))
                {
                    field = value;
                }
            }
        }
#endif

        /// <summary>
        /// Gets a value indicating whether any value has been recorded for the current attempt.
        /// </summary>
        public bool HasValues =>
            Mode is not null ||
            Protocol is not null ||
            CipherSuite is not null ||
            ServerCertificateDerBytes is not null ||
            ValidationErrors is not null ||
            ChainStatus is not null;

        /// <summary>
        /// Gets or sets the final negotiated encryption mode for the current attempt, or
        /// <see langword="null"/> if it has not been determined.
        /// </summary>
        public SqlConnectionEncryptionMode? Mode { get; set; }

        /// <summary>
        /// Gets or sets the negotiated TLS protocol for the current attempt, or
        /// <see langword="null"/> if no TLS handshake completed or the transport did not report a
        /// protocol.
        /// </summary>
        /// <remarks>
        /// Setting <see cref="SslProtocols.None"/> stores <see langword="null"/>: Native SNI reports
        /// <c>0</c> both when TLS was not used and when the protocol was not reported, so the two
        /// cannot be told apart.
        /// </remarks>
        public SslProtocols? Protocol
        {
            get;
            set => field = value == SslProtocols.None ? null : value;
        }

        /// <summary>
        /// Gets or sets the DER encoding of the presented server certificate for the current attempt,
        /// or <see langword="null"/> if the transport did not capture a presented certificate.
        /// </summary>
        /// <remarks>
        /// Setting an encoding that <see cref="IsReportableServerCertificateSize"/> rejects stores
        /// <see langword="null"/>; the certificate is never truncated.
        /// </remarks>
        public byte[]? ServerCertificateDerBytes
        {
            get;
            set
            {
                if (value is not null && !IsReportableServerCertificateSize(value.Length))
                {
                    value = null;
                }

                field = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether a DER-encoded server certificate of
        /// <paramref name="length"/> bytes is reported: it is not empty and does not exceed
        /// <see cref="MaxServerCertificateDerBytesSize"/>.
        /// </summary>
        /// <param name="length">The length of the DER encoding, in bytes.</param>
        /// <returns><see langword="true"/> if the certificate is reported.</returns>
        internal static bool IsReportableServerCertificateSize(int length) =>
            length > 0 && length <= MaxServerCertificateDerBytesSize;

        /// <summary>
        /// Sets <see cref="ServerCertificateDerBytes"/> to a copy of <paramref name="derBytes"/>,
        /// or to <see langword="null"/> without copying when
        /// <see cref="IsReportableServerCertificateSize"/> rejects its length.
        /// </summary>
        /// <param name="derBytes">The DER encoding of the presented server certificate.</param>
        internal void SetServerCertificateDerBytes(ReadOnlySpan<byte> derBytes) =>
            ServerCertificateDerBytes =
                IsReportableServerCertificateSize(derBytes.Length) ? derBytes.ToArray() : null;

        /// <summary>
        /// Gets or sets SqlClient's effective certificate validation verdict for the current attempt,
        /// or <see langword="null"/> if SqlClient did not evaluate the certificate.
        /// </summary>
        public SqlServerCertificateValidationErrors? ValidationErrors { get; set; }

        /// <summary>
        /// Clears all accumulated values. Called at the start of each physical login attempt.
        /// </summary>
        public void Reset()
        {
            Mode = null;
            Protocol = null;
            CipherSuite = null;
            ServerCertificateDerBytes = null;
            ValidationErrors = null;
            ChainStatus = null;
        }

        /// <summary>
        /// Creates an immutable snapshot of the accumulated values. Returns
        /// <see cref="SqlConnectionEncryptionInfo.Empty"/> when nothing was recorded.
        /// </summary>
        /// <remarks>
        /// Certificate fields are never inferred here; they are published only as recorded by the
        /// transport.
        /// </remarks>
        public SqlConnectionEncryptionInfo ToSnapshot() =>
            HasValues
                ? new SqlConnectionEncryptionInfo(
                    negotiatedEncryptionMode: Mode,
                    negotiatedEncryptionProtocol: Protocol,
                    presentedServerCertificate: ServerCertificateDerBytes,
                    validationErrors: ValidationErrors,
                    chainStatus: ChainStatus,
                    negotiatedCipherSuite: CipherSuite)
                : SqlConnectionEncryptionInfo.Empty;
    }
}