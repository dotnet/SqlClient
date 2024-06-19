// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers;

namespace Microsoft.Data.SqlClientX.Handlers
{    /// <summary>
     /// Class that contains all context data needed for various handlers.
     /// </summary>
    internal class ConnectionHandlerContext : HandlerRequest
    {
        /// <summary>
        /// Class that contains data required to handle a connection request.
        /// </summary>
        public SqlConnectionString ConnectionString { get; set; }

        /// <summary>
        /// Stream used by readers.
        /// </summary>
        public Stream ConnectionStream { get; set; }

        /// <summary>
        /// Class required by DataSourceParser and Transport layer.
        /// </summary>
        public DataSource DataSource { get; set; }

        /// <summary>
        /// Class used by orchestrator while chaining handlers.
        /// </summary>
        public Exception Error { get; set; }
    }
}
