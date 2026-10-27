// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Parser
{
    internal class RoutingInfo
    {
        internal byte Protocol { get; private set; }
        internal ushort Port { get; private set; }
        internal string ServerName { get; private set; }

        internal RoutingInfo(byte protocol, ushort port, string servername)
        {
            Protocol = protocol;
            Port = port;
            ServerName = servername;
        }
    }
}
