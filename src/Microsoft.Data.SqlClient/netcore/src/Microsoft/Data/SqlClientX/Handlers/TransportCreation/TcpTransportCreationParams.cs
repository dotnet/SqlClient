// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClientX.Handlers.TransportCreation
{
    /// <summary>
    /// Class that contains parameters required for a TCP network stream.
    /// </summary>
    internal class TcpTransportCreationParams : TransportCreationParams
    {
        /// <summary>
        /// Gets or sets the hostname to connect to.
        /// </summary>
        public string Hostname { get; set; }

        /// <summary>
        /// Gets or sets the preference for version of IP address to use for the connection.
        /// </summary>
        public SqlConnectionIPAddressPreference IpAddressPreference { get; set; }

        /// <summary>
        /// Gets or sets the port to connect to.
        /// </summary>
        public int Port { get; set; }
    }
}
