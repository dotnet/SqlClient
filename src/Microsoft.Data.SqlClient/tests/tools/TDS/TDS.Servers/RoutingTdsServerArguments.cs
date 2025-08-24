// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.SqlServer.TDS.Servers
{
    /// <summary>
    /// Arguments for routing TDS Server
    /// </summary>
    public class RoutingTdsServerArguments : TdsServerArguments
    {
        /// <summary>
        /// Routing destination protocol.
        /// </summary>
        public int RoutingProtocol = 0;

        /// <summary>
        /// Routing TCP port
        /// </summary>
        public ushort RoutingTCPPort = 0;

        /// <summary>
        /// Routing TCP host name
        /// </summary>
        public string RoutingTCPHost = string.Empty;

        /// <summary>
        /// Packet on which routing should occur
        /// </summary>
        public TDSMessageType RouteOnPacket = TDSMessageType.TDS7Login;

        /// <summary>
        /// Indicates that routing should only occur on read-only connections
        /// </summary>
        public bool RequireReadOnly = true;
    }
}
