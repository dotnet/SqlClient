// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    internal static class ConnectionHandlerOrchestrator
    {
        /// <summary>
        /// Initiates the pre-defined chain of handlers
        /// </summary>
        /// <param name="context">Connection Handler Context</param>
        /// <param name="isAsync">Whether the calling method is executing asynchronously.</param>
        /// <param name="ct">Cancellation Token</param>
        /// <returns></returns>
        public static ValueTask ProcessRequestAsync(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            //TODO: replace with a static line of creating the chain
            DataSourceParsingHandler dataSourceParsingHandler = new DataSourceParsingHandler()
            {
                NextHandler = new TransportCreationHandler()
                {
                    NextHandler = new PreLoginHandler()
                }
            };
            return dataSourceParsingHandler.Handle(context, isAsync, ct);
        }
    }
}
