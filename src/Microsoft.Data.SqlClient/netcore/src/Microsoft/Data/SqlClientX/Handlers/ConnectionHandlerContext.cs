// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;

namespace Microsoft.Data.SqlClientX.Handlers
{    /// <summary>
     /// Class that contains all context data needed for various handlers.
     /// </summary>
    internal class ConnectionHandlerContext : HandlerRequest, ICloneable
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
        /// Method needed to clone ConnectionHandlerContext as part of history along the chain.
        /// </summary>
        public object Clone() 
        {
            ConnectionHandlerContext clonedContext = new ConnectionHandlerContext();

            if (this.ConnectionStream != null)
            {
                MemoryStream clonedStream = new MemoryStream();
                this.ConnectionStream.CopyTo(clonedStream);
                clonedStream.Position = 0;
                clonedContext.ConnectionStream = clonedStream;
            }

            if (this.ConnectionString != null)
            {
                clonedContext.ConnectionString = new SqlConnectionString(this.ConnectionString.ToString());
            }

            if (this.DataSource != null)
            {
                //TODO: Move DataSource class into SqlClientX and implement clone
                clonedContext.DataSource = (DataSource)this.DataSource;
            }

            if (this.Error != null)
            {
                clonedContext.Error = new Exception(this.Error.Message, this.Error.InnerException);
            }

            return clonedContext;
        }
    }
}

