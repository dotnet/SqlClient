// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System.Net.Security;
#endif
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Microsoft.Data.SqlClient.Connection
{
    /// <summary>
    /// Immutable record of what the transport observed during one TLS handshake: the negotiated
    /// protocol and cipher suite, the presented server certificate, and SqlClient's validation
    /// verdict. Produced by the transport layer from within the certificate validation callback
    /// and copied into <see cref="EncryptionInfoAccumulator"/>, which is the authority for these
    /// values for the login attempt.
    /// </summary>
    /// <remarks>
    /// Each value is <see langword="null"/> when it is unavailable: no certificate was presented
    /// or it was larger than <see cref="MaxDerBytesSize"/> (<see cref="DerBytes"/>), SqlClient
    /// did not evaluate the certificate (<see cref="ValidationErrors"/>), the original chain was
    /// not consulted or reported no usable status (<see cref="ChainStatus"/>), or the transport
    /// did not report a protocol or cipher suite.
    /// </remarks>
    internal sealed class TlsHandshakeInfo
    {
        /// <summary>
        /// The largest DER-encoded certificate that is captured: 64 KiB (65,536 bytes). A larger
        /// certificate is not captured; the connection is unaffected.
        /// </summary>
        /// <remarks>
        /// Bounds the memory retained per connection, pooled connection and exception when a
        /// hostile or misconfigured server presents an oversized certificate. Real leaf
        /// certificates are a few KiB.
        /// </remarks>
        internal const int MaxDerBytesSize = 64 * 1024;

        internal TlsHandshakeInfo(
            X509ChainStatusFlags? chainStatus,
#if NET
            TlsCipherSuite? cipherSuite,
#else
            int? cipherSuite,
#endif
            byte[]? derBytes,
            SslProtocols? protocol,
            SqlServerCertificateValidationErrors? validationErrors)
        {
            ChainStatus = chainStatus;
            CipherSuite = cipherSuite;
            DerBytes = derBytes;
            Protocol = protocol;
            ValidationErrors = validationErrors;
        }

        /// <summary>
        /// Gets the combined status flags of the chain supplied to the certificate validation
        /// callback, or <see langword="null"/> if the chain was not consulted or is unavailable.
        /// </summary>
        public X509ChainStatusFlags? ChainStatus { get; }

#if NET
        /// <summary>
        /// Gets the cipher suite the transport reported for this handshake, or
        /// <see langword="null"/> if the transport did not report one.
        /// </summary>
        public TlsCipherSuite? CipherSuite { get; }
#else
        /// <summary>
        /// Gets the IANA identifier of the cipher suite the transport reported for this handshake,
        /// or <see langword="null"/> if the transport did not report one.
        /// </summary>
        public int? CipherSuite { get; }
#endif

        /// <summary>
        /// Gets the DER encoding of the presented leaf certificate, or <see langword="null"/> if no
        /// certificate was presented or it was larger than <see cref="MaxDerBytesSize"/>.
        /// The array is owned by this record and must not be modified.
        /// </summary>
        public byte[]? DerBytes { get; }

        /// <summary>
        /// Gets the TLS protocol the transport reported for this handshake, or
        /// <see langword="null"/> if the transport did not report one.
        /// </summary>
        public SslProtocols? Protocol { get; }

        /// <summary>
        /// Gets SqlClient's effective validation verdict, or <see langword="null"/> if SqlClient did
        /// not evaluate the certificate.
        /// </summary>
        public SqlServerCertificateValidationErrors? ValidationErrors { get; }
    }
}
