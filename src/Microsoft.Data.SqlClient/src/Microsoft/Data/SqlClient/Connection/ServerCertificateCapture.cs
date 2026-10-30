// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    /// <summary>
    /// Immutable record of the server certificate details observed by the transport during one TLS
    /// handshake. Produced by the transport layer and consumed by <see cref="EncryptionInfoCapture"/>.
    /// </summary>
    /// <remarks>
    /// Each value is <see langword="null"/> when it is unavailable: no certificate was presented
    /// or it was larger than <see cref="MaxRawCertificateSize"/> (<see cref="RawCertificate"/>),
    /// SqlClient did not evaluate the certificate (<see cref="ValidationErrors"/>), or the original
    /// chain was not consulted or reported no usable status (<see cref="ChainStatus"/>).
    /// </remarks>
    internal sealed class ServerCertificateCapture
    {
        /// <summary>
        /// The largest DER-encoded certificate, in bytes, that is captured. A larger certificate is
        /// not captured; the connection is unaffected.
        /// </summary>
        internal const int MaxRawCertificateSize = 64 * 1024;

        internal ServerCertificateCapture(
            byte[]? rawCertificate,
            SqlServerCertificateValidationErrors? validationErrors,
            X509ChainStatusFlags? chainStatus,
            SslProtocols? protocol = null,
            int? cipherSuite = null)
        {
            RawCertificate = rawCertificate;
            ValidationErrors = validationErrors;
            ChainStatus = chainStatus;
            Protocol = protocol;
            CipherSuite = cipherSuite;
        }

        /// <summary>
        /// Gets the DER encoding of the presented leaf certificate, or <see langword="null"/> if no
        /// certificate was presented or it was larger than <see cref="MaxRawCertificateSize"/>.
        /// The array is owned by this capture and must not be modified.
        /// </summary>
        public byte[]? RawCertificate { get; }

        /// <summary>
        /// Gets SqlClient's effective validation verdict, or <see langword="null"/> if SqlClient did
        /// not evaluate the certificate.
        /// </summary>
        public SqlServerCertificateValidationErrors? ValidationErrors { get; }

        /// <summary>
        /// Gets the combined status flags of the chain supplied to the certificate validation
        /// callback, or <see langword="null"/> if the chain was not consulted or is unavailable.
        /// </summary>
        public X509ChainStatusFlags? ChainStatus { get; }

        /// <summary>
        /// Gets the TLS protocol the transport reported for this handshake, or <see langword="null"/>
        /// if the transport did not report one. Used only when the protocol was not otherwise
        /// determined for the attempt (for example, when the handshake failed).
        /// </summary>
        public SslProtocols? Protocol { get; }

        /// <summary>
        /// Gets the IANA identifier of the cipher suite the transport reported for this handshake,
        /// or <see langword="null"/> if the transport did not report one. Used only when the cipher
        /// suite was not otherwise determined for the attempt (for example, when the handshake
        /// failed).
        /// </summary>
        public int? CipherSuite { get; }
    }
}
