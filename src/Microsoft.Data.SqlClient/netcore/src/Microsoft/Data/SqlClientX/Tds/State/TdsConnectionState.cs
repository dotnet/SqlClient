// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClientX.Tds.Tokens.EnvChange;

namespace Microsoft.Data.SqlClientX.Tds.State
{
    /// <summary>
    /// TDS Connection State information
    /// </summary>
    internal class TdsConnectionState
    {
        /// <summary>
        /// Server name
        /// </summary>
        public string Server {  get; set; }

        /// <summary>
        /// Server version string
        /// </summary>
        public string ServerVersion { get; set; }

        /// <summary>
        /// Database information for parser's connection
        /// </summary>
        public string CurrentDatabase {  get; set; }

        /// <summary>
        /// Current language
        /// </summary>
        public string CurrentLanguage { get; set; }

        /// <summary>
        /// Current Packet size
        /// </summary>
        public int CurrentPacketSize { get; set; }

        /// <summary>
        /// Database Mirroring Partner for connection
        /// </summary>
        public string CurrentFailoverPartner { get; set; }

        /// <summary>
        /// User Instance Name
        /// </summary>
        public string UserInstanceName { get; set; }

        /// <summary>
        /// Client connection Id
        /// </summary>
        public Guid ClientConnectionId { get; set; }

        /// <summary>
        /// Original client connection id before routing info is received.
        /// </summary>
        public Guid OriginalClientConnectionId { get; set; }

        /// <summary>
        /// Routing destination server
        /// </summary>
        public string RoutingDestination { get; set; }

        /// <summary>
        /// Routing information as received from server.
        /// </summary>
        public RoutingInfo RoutingInfo { get; set; }
    }
}
