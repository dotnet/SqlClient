// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers.Connection.TransportCreation
{
    /// <summary>
    /// Handler for connecting via shared memory.
    /// </summary>
    internal sealed class SharedMemoryTransportCreationHandler : IReturningHandler<ConnectionHandlerContext, Stream>
    {
        /// <inheritdoc />
        public ValueTask<Stream> Handle(ConnectionHandlerContext parameters, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(parameters is not null, "Parameters must not be null");
            Debug.Assert(parameters.DataSource is not null, "Data source must not be null");

            // This handler can only handle connection attempts if named pipes are included
            if (parameters.DataSource.Protocol is DataSourceProtocol.SharedMemory)
            {
                // @TODO: Support shared memory connection
                return ValueTask.FromException<Stream>(new NotImplementedException());
            }

            // For now, we just pass it along to the next handler
            return ValueTask.FromResult<Stream>(null);
        }
    }
}
