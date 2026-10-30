// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

#nullable enable

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/SqlConnectionEncryptionInfo/*' />
    public sealed class SqlConnectionEncryptionInfo
    {
        /// <summary>
        /// Shared snapshot with every field unavailable (<see langword="null"/>). Safe to share
        /// because instances are immutable.
        /// </summary>
        internal static SqlConnectionEncryptionInfo Empty { get; } = new();

        private readonly byte[]? _presentedServerCertificate;

        private SqlConnectionEncryptionInfo()
        {
        }

        /// <summary>
        /// Creates a populated snapshot. Each argument is <see langword="null"/> when the
        /// corresponding value was not captured for the connection attempt.
        /// </summary>
        /// <param name="negotiatedEncryptionMode">The negotiated encryption mode.</param>
        /// <param name="negotiatedEncryptionProtocol">The negotiated TLS protocol version.</param>
        /// <param name="presentedServerCertificate">
        /// The DER encoding of the leaf certificate presented by the server. The array is copied;
        /// an empty array is treated as <see langword="null"/>.
        /// </param>
        /// <param name="validationErrors">The certificate validation errors observed by the driver.</param>
        /// <param name="chainStatus">The aggregated X.509 chain status flags.</param>
        /// <param name="negotiatedCipherSuite">
        /// The IANA TLS cipher suite identifier selected by the server (for example, 0x1302), not a
        /// Windows ALG_ID. Must be in the range 0 to 0xFFFF when not <see langword="null"/>.
        /// </param>
        internal SqlConnectionEncryptionInfo(
            SqlConnectionEncryptionMode? negotiatedEncryptionMode,
            SslProtocols? negotiatedEncryptionProtocol,
            byte[]? presentedServerCertificate,
            SqlServerCertificateValidationErrors? validationErrors,
            X509ChainStatusFlags? chainStatus,
            int? negotiatedCipherSuite = null)
        {
            if (negotiatedCipherSuite is < 0 or > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(negotiatedCipherSuite),
                    negotiatedCipherSuite,
                    "A TLS cipher suite identifier must be in the range 0 to 0xFFFF.");
            }

            NegotiatedEncryptionMode = negotiatedEncryptionMode;
            NegotiatedEncryptionProtocol = negotiatedEncryptionProtocol;
            NegotiatedCipherSuiteId = negotiatedCipherSuite;
            if (presentedServerCertificate is { Length: > 0 })
            {
                _presentedServerCertificate = (byte[])presentedServerCertificate.Clone();
            }
            ValidationErrors = validationErrors;
            ChainStatus = chainStatus;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/NegotiatedEncryptionMode/*' />
        public SqlConnectionEncryptionMode? NegotiatedEncryptionMode { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/NegotiatedEncryptionProtocol/*' />
        public SslProtocols? NegotiatedEncryptionProtocol { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/NegotiatedCipherSuiteId/*' />
        public int? NegotiatedCipherSuiteId { get; }

#if NET
        // Strongly typed view of NegotiatedCipherSuiteId. TlsCipherSuite is ushort-backed, matching
        // SslStream.NegotiatedCipherSuite, so this member is not CLS-compliant. Values with no named
        // enum member are preserved.
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/NegotiatedCipherSuite/*' />
        [CLSCompliant(false)]
        public System.Net.Security.TlsCipherSuite? NegotiatedCipherSuite =>
            NegotiatedCipherSuiteId is int value
                ? (System.Net.Security.TlsCipherSuite)value
                : null;
#endif

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/GetServerCertificate/*' />
        public X509Certificate2? GetServerCertificate()
        {
            if (_presentedServerCertificate is null)
            {
                return null;
            }

            try
            {
                return X509CertificateLoader.LoadCertificate(_presentedServerCertificate);
            }
            catch (Exception)
            {
                // Diagnostics only: bytes that cannot be parsed are reported as unavailable.
                return null;
            }
        }

        /// <summary>
        /// Gets the captured DER encoding of the server-presented leaf certificate, or
        /// <see langword="null"/> if none was captured. The memory is read-only and owned by
        /// the snapshot.
        /// </summary>
        internal ReadOnlyMemory<byte>? PresentedServerCertificateRawData =>
            _presentedServerCertificate is null
                ? (ReadOnlyMemory<byte>?)null
                : new ReadOnlyMemory<byte>(_presentedServerCertificate);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/ValidationErrors/*' />
        public SqlServerCertificateValidationErrors? ValidationErrors { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/ChainStatus/*' />
        public X509ChainStatusFlags? ChainStatus { get; }

        /// <summary>
        /// Gets a value indicating whether every field is unavailable.
        /// </summary>
        internal bool IsEmpty =>
            NegotiatedEncryptionMode is null &&
            NegotiatedEncryptionProtocol is null &&
            NegotiatedCipherSuiteId is null &&
            _presentedServerCertificate is null &&
            ValidationErrors is null &&
            ChainStatus is null;
    }
}
