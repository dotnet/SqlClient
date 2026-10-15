// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Authentication;

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
        public bool HasValues => Mode is not null || Protocol is not null;

        /// <summary>
        /// Clears all captured values. Called at the start of each physical login attempt.
        /// </summary>
        public void Reset()
        {
            Mode = null;
            Protocol = null;
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
        /// protocol was not reported and is recorded as unavailable.
        /// </param>
        public void Record(SqlConnectionEncryptionMode mode, SslProtocols protocol)
        {
            Mode = mode;
            Protocol = protocol == SslProtocols.None ? null : protocol;
        }

        /// <summary>
        /// Creates an immutable snapshot of the captured values. Returns
        /// <see cref="SqlConnectionEncryptionInfo.Empty"/> when nothing was captured.
        /// </summary>
        /// <remarks>
        /// Certificate fields are never inferred here; they remain unavailable.
        /// </remarks>
        public SqlConnectionEncryptionInfo ToSnapshot() =>
            HasValues
                ? new SqlConnectionEncryptionInfo(
                    negotiatedEncryptionMode: Mode,
                    negotiatedEncryptionProtocol: Protocol,
                    presentedServerCertificateHash256: null,
                    validationErrors: null,
                    chainStatus: null)
                : SqlConnectionEncryptionInfo.Empty;
    }
}
