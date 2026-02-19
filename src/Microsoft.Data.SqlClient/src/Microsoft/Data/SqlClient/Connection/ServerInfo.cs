// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Globalization;

namespace Microsoft.Data.SqlClient.Connection
{
    internal sealed class ServerInfo
    {
        #region Constructors

        /// <summary>
        /// Initialize server info from connection options alone.
        /// </summary>
        internal ServerInfo(SqlConnectionString userOptions)
            : this(userOptions, userOptions.DataSource, userOptions.ServerSPN)
        { }

        /// <summary>
        /// Initialize server info from connection options, but override DataSource and ServerSPN
        /// with given server name and server SPN.
        /// </summary>
        internal ServerInfo(SqlConnectionString userOptions, string serverName, string serverSpn)
            : this(userOptions, serverName)
        {
            ServerSPN = serverSpn;
        }

        //
        /// <summary>
        /// Initialize server info from connection options, but override DataSource with given
        /// server name.
        /// </summary>
        private ServerInfo(SqlConnectionString userOptions, string serverName)
        {
            Debug.Assert(userOptions != null);
            Debug.Assert(serverName != null, "server name should never be null");

            // Ensure user server name is not null
            UserServerName = serverName ?? string.Empty;

            #if NET
            UserProtocol = string.Empty;
            #else
            UserProtocol = userOptions.NetworkLibrary;
            #endif

            ResolvedDatabaseName = userOptions.InitialCatalog;
            PreRoutingServerName = null;
        }

        /// <summary>
        /// Initialize server info from connection options, but override DataSource with given
        /// server name.
        /// </summary>
        internal ServerInfo(
            SqlConnectionString userOptions,
            RoutingInfo routing,
            string preRoutingServerName,
            string serverSpn)
        {
            Debug.Assert(userOptions != null && routing != null);
            Debug.Assert(routing.ServerName != null, "server name should never be null");

            // Ensure user server name is not null
            // NOTE: string.Format should be used here to ensure invariant culture is used for port number.
            UserServerName = routing == null || routing.ServerName == null
                ? string.Empty
                : string.Format(CultureInfo.InvariantCulture, "{0},{1}", routing.ServerName, routing.Port);

            PreRoutingServerName = preRoutingServerName;
            UserProtocol = TdsEnums.TCP;
            SetDerivedNames(UserProtocol, UserServerName);
            ResolvedDatabaseName = routing?.DatabaseName ?? userOptions.InitialCatalog;
            ServerSPN = serverSpn;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Resolved servername with protocol
        /// </summary>
        internal string ExtendedServerName { get; private set; }

        internal string PreRoutingServerName { get; private set; }

        /// <summary>
        /// Name of target database after resolution
        /// </summary>
        internal string ResolvedDatabaseName { get; private set; }

        /// <summary>
        /// Resolved servername only
        /// </summary>
        internal string ResolvedServerName { get; private set; }

        /// <summary>
        /// Server SPN
        /// </summary>
        internal string ServerSPN { get; private set; }

        /// <summary>
        /// Original user-supplied server name from the connection string. If connection string
        /// has no Data Source, the value is set to string.Empty. In case of routing, will be
        /// changed to routing destination.
        /// </summary>
        internal string UserServerName { get; private set; }

        /// <summary>
        /// User specified protocol
        /// </summary>
        internal string UserProtocol { get; private set; }

        #endregion

        internal void SetDerivedNames(string protocol, string serverName)
        {
            // The following concatenates the specified netlib network protocol to the host string,
            // if netlib is not null and the flag is on. This allows the user to specify the
            // network protocol for the connection - but only when using the Dbnetlib dll. If the
            // protocol is not specified, the netlib will try all protocols in the order listed in
            // the Client Network Utility. Connect will then fail if all protocols fail.
            ExtendedServerName = !string.IsNullOrEmpty(protocol)
                ? $"{protocol}:{serverName}"
                : serverName;
            ResolvedServerName = serverName;
        }
    }
}
