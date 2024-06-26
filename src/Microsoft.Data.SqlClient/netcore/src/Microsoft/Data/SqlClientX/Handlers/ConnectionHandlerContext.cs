// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;
using System.Net.Security;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers
{    /// <summary>
     /// Class that contains all context data needed for various handlers.
     /// </summary>
    internal class ConnectionHandlerContext : HandlerRequest
    {
        /// <summary>
        /// Stream used by readers.
        /// </summary>
        public Stream ConnectionStream { get; set; }

        /// <summary>
        /// Class that contains data required to handle a connection request.
        /// </summary>
        public SqlConnectionString ConnectionString { get; set; }

        /// <summary>
        /// Class required by DataSourceParser and Transport layer.
        /// </summary>
        public DataSource DataSource { get; set; }

        /// <summary>
        /// Class used by orchestrator while chaining handlers.
        /// </summary>
        public Exception Error { get; set; }

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
        public bool MarsCapable { get; internal set; }

        /// <summary>
        /// Indicates if fed auth needed for this connection.
        /// </summary>
        public bool FedAuthRequired { get; internal set; }

        /// <summary>
        /// The access token in bytes.
        /// </summary>
        public byte[] AccessTokenInBytes { get; internal set; }

        /// <summary>
        /// The server information created for the connection.
        /// </summary>
        public ServerInfo SeverInfo { get; internal set; }
        public SqlErrorCollection ErrorCollection { get; internal set; } = new SqlErrorCollection();

        /// <summary>
        /// The callback for Access Token Retrieval.
        /// </summary>
        internal Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> AccessTokenCallback { get; set; }
    }
}
