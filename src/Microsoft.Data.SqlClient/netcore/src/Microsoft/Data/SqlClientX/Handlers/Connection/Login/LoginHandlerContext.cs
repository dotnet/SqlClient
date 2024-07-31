// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.Login
{
    /// <summary>
    /// Contains the context which needs to be passed around for login handler.
    /// </summary>
    internal class LoginHandlerContext : HandlerRequest
    {
        /// <summary>
        /// Constructor to instantiate using the ConnectionHandlerContext.
        /// </summary>
        /// <param name="context"></param>
        public LoginHandlerContext(ConnectionHandlerContext context)
        {
            ConnectionContext = context;
        }

        /// <summary>
        /// The Connection context.
        /// </summary>
        private ConnectionHandlerContext ConnectionContext { get; }

        /// <summary>
        /// Server info.
        /// </summary>
        public ServerInfo ServerInfo => this.ConnectionContext.ServerInfo;

        /// <summary>
        /// The connection string representation as SqlConnectionString.
        /// </summary>
        public SqlConnectionString ConnectionOptions => this.ConnectionContext.ConnectionString;

        /// <summary>
        /// Features in the login request.
        /// </summary>
        public FeatureExtensions Features => this.ConnectionContext.Features;

        /// <summary>
        /// If feature extensions being used.
        /// </summary>
        public bool UseFeatureExt => Features.RequestedFeatures != TdsEnums.FeatureExtension.None;

        /// <summary>
        /// If there was a password change requested using APIs.
        /// </summary>
        public PasswordChangeRequest PasswordChangeRequest => ConnectionContext.PasswordChangeRequest;

        /// <summary>
        /// The TdsStream in the connection context.
        /// </summary>
        public TdsStream TdsStream => ConnectionContext.TdsStream;

        public Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> AccessTokenCallback => ConnectionContext.AccessTokenCallback;

        /// <summary>
        /// The access token in bytes, from the SqlConnection API.
        /// </summary>
        public byte[] AccessTokenInBytes => ConnectionContext.AccessTokenInBytes;

        /// <summary>
        /// If fed auth is negotiated in prelogin.
        /// </summary>
        public bool FedAuthNegotiatedInPrelogin => ConnectionContext.IsFedAuthNegotiatedInPrelogin;

        /// <summary>
        /// The interface name for this driver.
        /// </summary>
        public string ClientInterfaceName => TdsEnums.SQL_PROVIDER_NAME;
        /// <summary>
        /// Get the workstation Id.
        /// </summary>
        public string WorkstationId => ConnectionOptions.ObtainWorkstationId();

        /// <summary>
        /// The current database.
        /// </summary>
        public string Database => !ConnectionOptions.UserInstance ? ServerInfo.ResolvedDatabaseName : string.Empty;

        /// <summary>
        /// Get the hostname of the client driver.
        /// </summary>
        public string HostName => ConnectionOptions.ObtainWorkstationId();

        /// <summary>
        /// Is User Instance.
        /// </summary>
        public bool IsUserInstance => ConnectionOptions.UserInstance;

        /// <summary>
        /// Whether to use SSPI or not.
        /// </summary>
        public bool UseSspi => ConnectionOptions.IntegratedSecurity  // Treat AD Integrated like Windows integrated when against a non-FedAuth endpoint
                                     || (ConnectionOptions.Authentication == SqlAuthenticationMethod.ActiveDirectoryIntegrated
                                     && !FedAuthNegotiatedInPrelogin);

        /// <summary>
        /// Provides the user id for the connection.
        /// </summary>
        public string UserName => PasswordChangeRequest?.Credential != null ? PasswordChangeRequest?.Credential.UserId : ConnectionOptions.UserID;

        /// <summary>
        /// The byte array of obfuscated password according to TDS spec.
        /// If SqlCredential is provided, then don't populate this property.
        /// </summary>
        public byte[] EncryptedPassword => PasswordChangeRequest?.Credential != null ? null : TdsParserStaticMethods.ObfuscatePassword(ConnectionOptions.Password);

        /// <summary>
        /// The packet size to be negotiated.
        /// </summary>
        public int PacketSize => ConnectionOptions.PacketSize;

        /// <summary>
        /// Application name in the connection string.
        /// </summary>
        public string ApplicationName => ConnectionOptions.ApplicationName;

        /// <summary>
        /// The language on the connection string.
        /// </summary>
        public string Language => ConnectionOptions.CurrentLanguage;

        /// <summary>
        /// The attach DB filename in the connection string.
        /// </summary>
        public string AttachedDbFileName => ConnectionOptions.AttachDBFilename;

        /// <summary>
        /// The server name being connected to.
        /// </summary>
        public string ServerName => ConnectionContext.ServerInfo.UserServerName;

        /// <summary>
        /// The credential to be used for login.
        /// </summary>
        public SqlCredential Credential => PasswordChangeRequest?.Credential;

        /// <summary>
        /// Whether replication is being used, as specified in the connection string.
        /// </summary>
        public bool UseReplication => ConnectionOptions.Replication;

        /// <summary>
        /// Whether the connection is read-only.
        /// </summary>
        public bool ReadOnlyIntent => ConnectionOptions.ApplicationIntent == ApplicationIntent.ReadOnly;

        /// <summary>
        /// New password to be changed, for the login.
        /// </summary>
        public string NewPassword => PasswordChangeRequest?.NewPassword;

        /// <summary>
        /// New secure password as SecureString.
        /// </summary>
        public SecureString NewSecurePassword => PasswordChangeRequest?.NewSecurePassword;

        public int CalculateLoginRecordLength()
        {
            checked
            { 
                int loginRecordLength = HostName.Length + 
                    ConnectionOptions.ApplicationName.Length +
                    ServerInfo.UserServerName.Length + 
                    ClientInterfaceName.Length +
                    ConnectionOptions.CurrentLanguage.Length + 
                    Database.Length +
                    ConnectionOptions.AttachDBFilename.Length;

                loginRecordLength *= 2;

                if (UseFeatureExt)
                {
                    loginRecordLength += 4;
                }

                return loginRecordLength;
            }
        }
    }
}
