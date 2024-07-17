// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers.Connection;

namespace Microsoft.Data.SqlClientX.Handlers
{
    internal sealed class HandlerOrchestrator
    {
        /// <summary>
        /// Class responsible for creation of chain of handlers and initiate the chain
        /// </summary>
        private ConnectionHandlerContext _initialConnectionHandlerContext;

        public HandlerOrchestrator(ConnectionHandlerContext initialConnectionHandlerContext)
        {
            _initialConnectionHandlerContext = initialConnectionHandlerContext;
        }

        /// <summary>
        /// Intiates the pre-defined chain of handlers
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async ValueTask ProcessRequestAsync(bool isAsync, CancellationToken ct) 
        {
            //TODO: replace with a static line of creating the chain
            DataSourceParsingHandler dataSourceParsingHandler = new DataSourceParsingHandler();
            TransportCreationHandler transportCreationHandler = new TransportCreationHandler();
            PreloginHandler preloginHandler = new PreloginHandler();
            dataSourceParsingHandler.NextHandler = transportCreationHandler;
            transportCreationHandler.NextHandler = preloginHandler;

            await dataSourceParsingHandler.Handle(_initialConnectionHandlerContext, isAsync, ct).ConfigureAwait(false);
        }
    }
}
