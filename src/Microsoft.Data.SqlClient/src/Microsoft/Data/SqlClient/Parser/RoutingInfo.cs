// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser;

internal class RoutingInfo
{
    internal RoutingInfo(byte protocol, ushort port, string serverName, string databaseName = null)
    {
        Protocol = protocol;
        Port = port;
        ServerName = serverName;
        DatabaseName = databaseName;
    }

    internal byte Protocol { get; private set; }

    internal ushort Port { get; private set; }

    internal string ServerName { get; private set; }

    /// <summary>
    /// The DatabaseName property is only used when routing via an EnhancedRouting ENVCHANGE token.
    /// It is not used when routing via the normal Routing ENVCHANGE token.
    /// </summary>
    internal string DatabaseName { get; private set; }
}
