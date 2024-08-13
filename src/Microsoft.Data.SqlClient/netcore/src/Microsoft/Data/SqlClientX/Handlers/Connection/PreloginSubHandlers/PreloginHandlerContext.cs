// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET8_0_OR_GREATER

using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers
{
    /// <summary>
    /// Handler context for Prelogin.
    /// </summary>
    internal class PreloginHandlerContext : HandlerRequest
    {
        /// <summary>
        /// Encryption options for the connection.
        /// </summary>
        public SqlConnectionEncryptOption ConnectionEncryptionOption { get; private set; }
        
        /// <summary>
        /// If the client should do TLS first.
        /// </summary>
        public bool IsTlsFirst => (ConnectionEncryptionOption == SqlConnectionEncryptOption.Strict);
        
        /// <summary>
        /// If the client should trust the server certificate.
        /// </summary>
        private bool TrustServerCertificate { get; set; }

        /// <summary>
        /// Is integrated security enabled? 
        /// </summary>
        public bool IntegratedSecurity { get; private set; }

        /// <summary>
        /// The Sqlauthentication method being used.
        /// </summary>
        public SqlAuthenticationMethod AuthType { get; private set; }

        /// <summary>
        /// The hostname in certificate to validate during TLS handshake.
        /// </summary>
        public string HostNameInCertificate { get; private set; }

        /// <summary>
        /// The filename with the server certificate to be used for validating the server certificate.
        /// </summary>
        public string ServerCertificateFilename { get; private set; }

        /// <summary>
        /// The server information.
        /// </summary>
        public ServerInfo ServerInfo { get; private set; }

        /// <summary>
        /// The encryption option for the client, who state is maintained internally.
        /// </summary>
        public EncryptionOptions InternalEncryptionOption { get; set; } = EncryptionOptions.OFF;

        /// <summary>
        /// The original connection context. 
        /// </summary>
        public ConnectionHandlerContext ConnectionContext { get; private set; }

        /// <summary>
        /// Does the server support encryption?
        /// </summary>
        public bool ServerSupportsEncryption { get; internal set; }

        /// <summary>
        /// The Prelogin handshake status.
        /// </summary>
        public PreLoginHandshakeStatus HandshakeStatus { get; internal set; }
        
        /// <summary>
        /// The SNI context in which the operation is being performed.
        /// This is used for exception reporting.
        /// </summary>
        public static SniContext SniContext => SniContext.Snix_PreLogin;

        /// <summary>
        /// Constructs the Prelogin context from the connection context.
        /// </summary>
        /// <param name="connectionContext"></param>
        public PreloginHandlerContext(ConnectionHandlerContext connectionContext)
        {
            ConnectionContext = connectionContext;
            SqlConnectionString connectionOptions = connectionContext.ConnectionString;
            ConnectionEncryptionOption = connectionOptions.Encrypt;
            TrustServerCertificate = connectionOptions.TrustServerCertificate;
            IntegratedSecurity = connectionOptions.IntegratedSecurity;
            AuthType = connectionOptions.Authentication;
            HostNameInCertificate = connectionOptions.HostNameInCertificate;
            ServerCertificateFilename = connectionOptions.ServerCertificate;
            ServerInfo = connectionContext.ServerInfo;
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
                return (InternalEncryptionOption == EncryptionOptions.ON && !TrustServerCertificate)
                || (ConnectionContext.AccessTokenInBytes != null && !TrustServerCertificate);
            }
        }

        /// <summary>
        /// Checks if the client needs encryption.
        /// </summary>
        /// <returns></returns>
        public bool DoesClientNeedEncryption() =>
                InternalEncryptionOption is (EncryptionOptions.ON or EncryptionOptions.LOGIN);

    }
}
#endif
