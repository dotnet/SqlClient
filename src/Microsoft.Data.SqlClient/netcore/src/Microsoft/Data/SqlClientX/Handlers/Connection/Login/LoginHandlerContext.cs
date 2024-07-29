// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        public FeatureExtensions Features { get; } = new();

        /// <summary>
        /// The login record.
        /// This is generted internally and passed around in the Login Handler.
        /// </summary>
        //public SqlLogin Login { get; internal set; }

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
        /// The access token in bytes, from teh SqlConnection API.
        /// </summary>
        public byte[] AccessTokenInBytes => ConnectionContext.AccessTokenInBytes;

        /// <summary>
        /// If fed auth is negotiated in prelogin.
        /// </summary>
        public bool FedAuthNegotiatedInPrelogin => ConnectionContext.FedAuthNegotiatedInPrelogin;

        /// <summary>
        /// The interface name for this driver.
        /// </summary>
        public string ClientInterfaceName => TdsEnums.SQL_PROVIDER_NAME;
        /// <summary>
        /// Get the workstation Id.
        /// </summary>
        public string WorkstationId => ConnectionOptions.ObtainWorkstationId();

        public string CurrentDatabase => !ConnectionOptions.UserInstance ? ServerInfo.ResolvedDatabaseName : string.Empty;

        public int CalculateLoginRecordLength()
        {
            return ConnectionOptions.ObtainWorkstationId().Length 
                + ConnectionOptions.ApplicationName.Length +
                            ServerInfo.UserServerName.Length + 
                            ClientInterfaceName.Length +
                            ConnectionOptions.CurrentLanguage.Length + 
                            CurrentDatabase.Length +
                            ConnectionOptions.AttachDBFilename.Length;
        }
    }
}
