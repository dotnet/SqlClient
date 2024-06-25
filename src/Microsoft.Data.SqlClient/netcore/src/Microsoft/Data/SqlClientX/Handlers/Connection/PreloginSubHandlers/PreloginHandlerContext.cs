// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler context for Prelogin.
    /// </summary>
    internal class PreLoginHandlerContext : HandlerRequest
    {
        public SqlConnectionEncryptOption ConnectionEncryptionOption { get; private set; }
        public bool IsTlsFirst => (ConnectionEncryptionOption == SqlConnectionEncryptOption.Strict);
        public bool TrustServerCert { get; private set; }
        public bool IntegratedSecurity { get; private set; }
        public SqlAuthenticationMethod AuthType { get; private set; }
        public string HostNameInCertificate { get; private set; }
        public string ServerCertificateFilename { get; private set; }

        public EncryptionOptions InternalEncryptionOption { get; set; } = EncryptionOptions.OFF;

        public ConnectionHandlerContext ConnectionContext { get; private set; }

        public SNIError SniError { get; internal set; }
        public bool ServerSupportsEncryption { get; internal set; }
        public PreLoginHandshakeStatus HandshakeStatus { get; internal set; }

        public PreLoginHandlerContext(ConnectionHandlerContext connectionContext)
        {
            ConnectionContext = connectionContext;
            SqlConnectionString connectionOptions = connectionContext.ConnectionString;
            ConnectionEncryptionOption = connectionOptions.Encrypt;
            TrustServerCert = connectionOptions.TrustServerCertificate;
            IntegratedSecurity = connectionOptions.IntegratedSecurity;
            AuthType = connectionOptions.Authentication;
            HostNameInCertificate = connectionOptions.HostNameInCertificate;
            ServerCertificateFilename = connectionOptions.ServerCertificate;
        }

        /// <summary>
        /// Checks if the client should validate the server certificate.
        /// </summary>
        /// <returns></returns>
        public bool ShouldValidateCertificate()
        {
            if (IsTlsFirst)
            {
                return true;
            }
            else
            {
                return (InternalEncryptionOption == EncryptionOptions.ON && !TrustServerCert)
                || (ConnectionContext.AccessTokenInBytes != null && !TrustServerCert);
            }
        }

        /// <summary>
        /// Checks if the client needs encryption.
        /// </summary>
        /// <returns></returns>
        public bool DoesClientNeedEncryption() =>
                InternalEncryptionOption == EncryptionOptions.ON || InternalEncryptionOption == EncryptionOptions.LOGIN;

    }
}
