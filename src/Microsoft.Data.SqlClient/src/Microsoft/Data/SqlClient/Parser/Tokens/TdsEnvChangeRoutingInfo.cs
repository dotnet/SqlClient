// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Represents the routing information related to environment changes in the TDS protocol. This
/// information is included within an SQLENVCHANGE token when the type is ENV_ROUTING or
/// ENV_ENHANCEDROUTING.
/// </summary>
internal class TdsEnvChangeRoutingInfo
{
    internal TdsEnvChangeRoutingInfo(byte protocol, ushort port, string serverName, string databaseName = null)
    {
        Protocol = protocol;
        Port = port;
        ServerName = serverName;
        DatabaseName = databaseName;
    }

    /// <summary>
    /// The DatabaseName property is only used when routing via an EnhancedRouting ENVCHANGE token.
    /// It is not used when routing via the normal Routing ENVCHANGE token.
    /// </summary>
    internal string DatabaseName { get; private set; }

    /// <summary>
    /// Represents the port used for establishing a connection in a TDS routing environment.
    /// </summary>
    internal ushort Port { get; private set; }

    /// <summary>
    /// Represents the protocol used for routing in a Routing or EnhancedRouting ENVCHANGE token.
    /// </summary>
    // @TODO: Should this be an enum of protocol types?
    internal byte Protocol { get; private set; }

    /// <summary>
    /// Specifies the name of the server involved in the routing process of the TDS environment.
    /// </summary>
    internal string ServerName { get; private set; }
}
