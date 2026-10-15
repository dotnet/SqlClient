// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    /// <summary>
    /// Mutable, per-physical-login-attempt collector for connection encryption details.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A single instance is owned by a <see cref="SqlConnectionInternal"/> and is
    /// <see cref="Reset">reset</see> at the start of every physical login attempt (initial attempt,
    /// transient-error retry, routing redirect, and failover partner attempt), so only the values
    /// recorded by the terminal attempt are ever published.
    /// </para>
    /// <para>
    /// Values are only written during login, never on the per-command hot path. Callers convert the
    /// captured values into an immutable <see cref="SqlConnectionEncryptionInfo"/> snapshot via
    /// <see cref="ToSnapshot"/>.
    /// </para>
    /// </remarks>
    internal sealed class EncryptionInfoCapture
    {
        /// <summary>
        /// Gets the final negotiated encryption mode for the current attempt, or
        /// <see langword="null"/> if it has not been determined.
        /// </summary>
        public SqlConnectionEncryptionMode? Mode { get; private set; }

        /// <summary>
        /// Gets the negotiated TLS protocol for the current attempt, or <see langword="null"/>
        /// if no TLS handshake completed or the transport did not report a protocol.
        /// </summary>
        public SslProtocols? Protocol { get; private set; }

        /// <summary>
        /// Gets a value indicating whether any value has been captured for the current attempt.
        /// </summary>
        public bool HasValues =>
            Mode is not null ||
            Protocol is not null ||
            CertificateHash256 is not null ||
            ValidationErrors is not null ||
            ChainStatus is not null;

        /// <summary>
        /// Gets the SHA-256 hash of the presented server certificate for the current attempt, or
        /// <see langword="null"/> if the transport did not observe a presented certificate.
        /// </summary>
        public byte[]? CertificateHash256 { get; private set; }

        /// <summary>
        /// Gets SqlClient's effective certificate validation verdict for the current attempt, or
        /// <see langword="null"/> if SqlClient did not evaluate the certificate.
        /// </summary>
        public SqlServerCertificateValidationErrors? ValidationErrors { get; private set; }

        /// <summary>
        /// Gets the combined chain status flags for the current attempt, or <see langword="null"/>
        /// if the chain was not consulted or is unavailable.
        /// </summary>
        public X509ChainStatusFlags? ChainStatus { get; private set; }

        /// <summary>
        /// Clears all captured values. Called at the start of each physical login attempt.
        /// </summary>
        public void Reset()
        {
            Mode = null;
            Protocol = null;
            CertificateHash256 = null;
            ValidationErrors = null;
            ChainStatus = null;
        }

        /// <summary>
        /// Records the server certificate details observed by the transport during the TLS
        /// handshake. A <see langword="null"/> <paramref name="capture"/> leaves the certificate
        /// fields unavailable.
        /// </summary>
        /// <param name="capture">The transport's certificate capture, if any.</param>
        public void RecordServerCertificate(ServerCertificateCapture? capture)
        {
            if (capture is null)
            {
                return;
            }

            CertificateHash256 = capture.Hash256;
            ValidationErrors = capture.ValidationErrors;
            ChainStatus = capture.ChainStatus;

            // A protocol already determined for this attempt takes precedence over one reported
            // alongside the certificate details.
            if (Protocol is null && capture.Protocol is SslProtocols protocol && protocol != SslProtocols.None)
            {
                Protocol = protocol;
            }
        }

        /// <summary>
        /// Applies the failure-path rule for a TLS failure on the current attempt: the negotiated
        /// mode and protocol are published only when they were actually observed. A TLS protocol
        /// version agreed for this attempt is the evidence of that; without it the mode is cleared,
        /// since requested settings and server pre-login responses are not substitutes. Certificate
        /// fields are never affected.
        /// </summary>
        public void DiscardUnobservedNegotiation()
        {
            if (Protocol is null)
            {
                Mode = null;
            }
        }

        /// <summary>
        /// Records the negotiated encryption mode for a failed TLS attempt, but only when a TLS
        /// protocol version was observed for this attempt (for example, reported by the transport's
        /// certificate validation callback before the certificate was rejected).
        /// </summary>
        /// <param name="mode">The encryption mode the attempt was negotiating.</param>
        public void RecordModeIfProtocolObserved(SqlConnectionEncryptionMode mode)
        {
            if (Protocol is not null)
            {
                Mode = mode;
            }
        }

        /// <summary>
        /// Records the negotiated encryption mode and, when known, the negotiated TLS protocol.
        /// </summary>
        /// <param name="mode">The negotiated encryption mode.</param>
        /// <param name="protocol">
        /// The negotiated TLS protocol. A value of <c>0</c> (<see cref="SslProtocols.None"/>) means the
        /// protocol was not reported; any protocol already reported by the transport for this attempt
        /// is kept, otherwise the protocol remains unavailable.
        /// </param>
        public void Record(SqlConnectionEncryptionMode mode, SslProtocols protocol)
        {
            Mode = mode;
            if (protocol != SslProtocols.None)
            {
                Protocol = protocol;
            }
        }

        /// <summary>
        /// Creates an immutable snapshot of the captured values. Returns
        /// <see cref="SqlConnectionEncryptionInfo.Empty"/> when nothing was captured.
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
                    presentedServerCertificateHash256: CertificateHash256,
                    validationErrors: ValidationErrors,
                    chainStatus: ChainStatus)
                : SqlConnectionEncryptionInfo.Empty;
    }
}
