// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.Data.SqlClientX.Handlers.TransportCreation;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Class that contains data required to handle a connection request.
    /// </summary>
    internal class ConnectionRequest : HandlerRequest
    {
        /// <summary>
        /// Stream that is created during connection.
        /// </summary>
        public Stream ConnectionStream { get; set; }

        /// <summary>
        /// Parameters parsed from the connection string to be used by the transport creation handler.
        /// </summary>
        public TransportCreationParams TransportCreationParams { get; set; }
    }
}
