// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System.Net.Security;
#endif
using System.Security.Authentication;

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
    /// This type only holds values. The rules that decide which values are recorded live with the
    /// code that observes them, in <see cref="Parser.TdsParser"/>. Values are only written during
    /// login, never on the per-command hot path. Callers convert the accumulated values into an
    /// immutable <see cref="SqlConnectionEncryptionInfo"/> snapshot via <see cref="ToSnapshot"/>.
    /// </para>
    /// </remarks>
    internal sealed class EncryptionInfoAccumulator
    {
        /// <summary>
        /// Gets or sets the TLS cipher suite negotiated for the current attempt, or
        /// <see langword="null"/> if the transport did not report one.
        /// </summary>
#if NET
        public TlsCipherSuite? CipherSuite { get; set; }
#else
        public int? CipherSuite { get; set; }
#endif

        /// <summary>
        /// Gets a value indicating whether any value has been recorded for the current attempt.
        /// </summary>
        public bool HasValues => Mode is not null || Protocol is not null || CipherSuite is not null;

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
        public SslProtocols? Protocol { get; set; }

        /// <summary>
        /// Clears all accumulated values. Called at the start of each physical login attempt.
        /// </summary>
        public void Reset()
        {
            Mode = null;
            Protocol = null;
            CipherSuite = null;
        }

        /// <summary>
        /// Creates an immutable snapshot of the accumulated values. Returns
        /// <see cref="SqlConnectionEncryptionInfo.Empty"/> when nothing was recorded.
        /// </summary>
        /// <remarks>
        /// Certificate fields are never inferred here; they remain unavailable.
        /// </remarks>
        public SqlConnectionEncryptionInfo ToSnapshot() =>
            HasValues
                ? new SqlConnectionEncryptionInfo(
                    negotiatedEncryptionMode: Mode,
                    negotiatedEncryptionProtocol: Protocol,
                    presentedServerCertificate: null,
                    validationErrors: null,
                    chainStatus: null,
                    negotiatedCipherSuite: CipherSuite)
                : SqlConnectionEncryptionInfo.Empty;
    }
}