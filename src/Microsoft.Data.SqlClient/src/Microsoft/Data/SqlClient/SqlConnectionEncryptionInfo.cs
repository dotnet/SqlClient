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

        /// <summary>
        /// Length in bytes of a SHA-256 hash.
        /// </summary>
        internal const int CertificateHash256Length = 32;

        private readonly byte[]? _presentedServerCertificateHash256;

        private SqlConnectionEncryptionInfo()
        {
        }

        /// <summary>
        /// Creates a populated snapshot. Each argument is <see langword="null"/> when the
        /// corresponding value was not captured for the connection attempt.
        /// </summary>
        internal SqlConnectionEncryptionInfo(
            SqlConnectionEncryptionMode? negotiatedEncryptionMode,
            SslProtocols? negotiatedEncryptionProtocol,
            byte[]? presentedServerCertificateHash256,
            SqlServerCertificateValidationErrors? validationErrors,
            X509ChainStatusFlags? chainStatus)
        {
            NegotiatedEncryptionMode = negotiatedEncryptionMode;
            NegotiatedEncryptionProtocol = negotiatedEncryptionProtocol;
            if (presentedServerCertificateHash256 is not null)
            {
                if (presentedServerCertificateHash256.Length != CertificateHash256Length)
                {
                    throw new ArgumentException(
                        $"A SHA-256 certificate hash must be {CertificateHash256Length} bytes.",
                        nameof(presentedServerCertificateHash256));
                }

                _presentedServerCertificateHash256 = (byte[])presentedServerCertificateHash256.Clone();
            }
            ValidationErrors = validationErrors;
            ChainStatus = chainStatus;
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/NegotiatedEncryptionMode/*' />
        public SqlConnectionEncryptionMode? NegotiatedEncryptionMode { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/NegotiatedEncryptionProtocol/*' />
        public SslProtocols? NegotiatedEncryptionProtocol { get; }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlConnectionEncryptionInfo.xml' path='docs/members[@name="SqlConnectionEncryptionInfo"]/PresentedServerCertificateHash256/*' />
                public ReadOnlyMemory<byte>? PresentedServerCertificateHash256 =>
                    _presentedServerCertificateHash256 is null
                        ? (ReadOnlyMemory<byte>?)null
                        : new ReadOnlyMemory<byte>(_presentedServerCertificateHash256);

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
            _presentedServerCertificateHash256 is null &&
            ValidationErrors is null &&
            ChainStatus is null;
    }
}
