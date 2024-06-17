// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Class that contains data required to handle a connection request.
    /// </summary>
    // TODO: This will be updated as more information is available.
    internal class ConnectionHandlerContext : HandlerRequest
    {
        /// <summary>
        /// Stream that is created during connection.
        /// </summary>
        public Stream ConnectionStream { get; set; }

        /// <summary>
        /// Gets or sets the data source as parsed from the connection string. It will be used by
        /// the transport creation handler to create the connection stream.
        /// </summary>
        public DataSource DataSource { get; set; }

        /// <summary>
        /// Gets or sets an exception that halted execution of the connection chain of handlers.
        /// </summary>
        public Exception Error { get; set; }

        public SqlConnectionIPAddressPreference IpAddressPreference { get; set; }
    }
}
