// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers.TransportCreation
{
    /// <summary>
    /// Handler for connecting via shared memory.
    /// </summary>
    internal class SharedMemoryTransportCreationHandler : ReturningHandler<ConnectionHandlerContext, Stream>
    {
        /// <inheritdoc />
        public override ValueTask<Stream> Handle(ConnectionHandlerContext parameters, bool isAsync, CancellationToken ct)
        {
            Debug.Assert(parameters is not null, "Parameters must not be null");
            Debug.Assert(parameters.DataSource is not null, "Data source must not be null");

            // @TODO: When we have an enum value for shared memory, we should check for it

            // For now, we just pass it along to the next handler
            return HandleNext(parameters, isAsync, ct);
        }
    }
}
