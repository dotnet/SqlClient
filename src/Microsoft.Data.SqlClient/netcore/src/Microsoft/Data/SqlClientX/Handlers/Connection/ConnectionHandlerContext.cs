// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers.Connection.Login;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{    /// <summary>
     /// Class that contains all context data needed for various handlers.
     /// </summary>
    internal class ConnectionHandlerContext : HandlerRequest, ICloneable
    {
        // TODO: Decide if we need a default constructor depending on the latest design 
        /// <summary>
        /// Stream used by readers.
        /// </summary>
        public Stream ConnectionStream { get; set; }

        /// <summary>
        /// A timer representing the timeout for the connection.
        /// TODO: This might require rethinking.
        /// </summary>
        internal TimeoutTimer TimeoutTimer { get; set; }

        /// <summary>
        /// Class that contains data required to handle a connection request.
        /// </summary>
        public SqlConnectionString ConnectionString { get; set; }

        /// <summary>
        /// Class required by DataSourceParser and Transport layer.
        /// </summary>
        public DataSource DataSource { get; set; }

        /// <summary>
        /// The Guid of the Connection.
        /// </summary>
        public Guid ConnectionId { get; internal set; } = Guid.Empty;

        /// <summary>
        /// The SslStream used by the connection.
        /// </summary>
        public SslStream SslStream { get; internal set; }

        /// <summary>
        /// The SslOverTdsStream used by the connection in case of Tds below 7.4.
        /// </summary>
        public SslOverTdsStream SslOverTdsStream { get; internal set; }

        /// <summary>
        /// The TdsStream to write Tds Packets to.
        /// </summary>
        public TdsStream TdsStream { get; internal set; }

        /// <summary>
        /// Whether the connection is capable of MARS
        /// This is negotiated after pre-login.
        /// </summary>
        public bool IsMarsCapable { get; internal set; }

        /// <summary>
        /// Indicates if fed auth needed for this connection.
        /// </summary>
        public bool IsFedAuthNegotiatedInPrelogin { get; internal set; }

        /// <summary>
        /// The access token in bytes.
        /// </summary>
        public byte[] AccessTokenInBytes { get; internal set; }

        /// <summary>
        /// The server information created for the connection.
        /// </summary>
        public ServerInfo ServerInfo { get; internal set; }

        /// <summary>
        /// The history of previous <see cref="ConnectionHandlerContext"/> in case of reroute
        /// </summary>
        public List<ConnectionHandlerContext> RoutingHistory { get; set; }

        /// <summary>
        /// An error collection for the handlers to add errors to.
        /// </summary>
        public SqlErrorCollection ErrorCollection { get; internal set; } = new();

        /// <summary>
        /// Logger.
        /// </summary>
        public static SqlClientLogger Logger { get; } = new();

        /// <summary>
        /// The callback for Access Token Retrieval.
        /// </summary>
        internal Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> AccessTokenCallback { get; set; }

        /// <summary>
        /// Represents a password change request on this connection.
        /// </summary>
        public PasswordChangeRequest PasswordChangeRequest { get; internal set; }

        /// <summary>
        /// Features in the login request.
        /// This needn't be cloned, since after routing, the feature extensions need to be 
        /// re-requested like it was for a fresh connection.
        /// </summary>
        public FeatureExtensions Features { get; } = new();

        /// <summary>
        /// Clone <see cref="ConnectionHandlerContext"/> as part of history along the chain.
        /// </summary>
        /// <returns>A new instance of ConnectionHandlerContext with copied values.</returns>
        public object Clone()
        {
            return new ConnectionHandlerContext
            {
                ConnectionStream = this.ConnectionStream, 
                ConnectionString = this.ConnectionString, 
                DataSource = this.DataSource, 
                ConnectionId = this.ConnectionId,
                SslStream = this.SslStream, 
                SslOverTdsStream = this.SslOverTdsStream, 
                TdsStream = this.TdsStream, 
                IsMarsCapable = this.IsMarsCapable,
                IsFedAuthNegotiatedInPrelogin = this.IsFedAuthNegotiatedInPrelogin,
                AccessTokenInBytes = this.AccessTokenInBytes,
                ServerInfo = this.ServerInfo, 
                ErrorCollection = this.ErrorCollection, 
                AccessTokenCallback = this.AccessTokenCallback, // Assuming delegate cloning is not required
                PasswordChangeRequest = this.PasswordChangeRequest,
            };
        }

        /// <summary>
        /// Adds the context of previous handler into the routingHistory object in case
        /// of a reroute
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="ArgumentNullException"></exception>
        private void AddToRoutingHistory(ConnectionHandlerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), "Context cannot be null");
            }
            RoutingHistory.Add(context);
        }

    }
}
