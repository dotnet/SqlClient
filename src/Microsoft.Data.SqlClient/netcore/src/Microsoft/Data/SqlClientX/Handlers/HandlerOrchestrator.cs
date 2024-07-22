// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
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
        private static readonly Lazy<HandlerOrchestrator> lazyInstance = new Lazy<HandlerOrchestrator>(() => new HandlerOrchestrator());
        private HandlerOrchestrator()
        {
        }

        public static HandlerOrchestrator Instance => lazyInstance.Value;

        /// <summary>
        /// Intiates the pre-defined chain of handlers
        /// </summary>
        /// <param name="handlerContext"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async ValueTask ProcessRequestAsync(ConnectionHandlerContext handlerContext, bool isAsync, CancellationToken ct) 
        {
            DataSourceParsingHandler dataSourceParsingHandler = new DataSourceParsingHandler();
            TransportCreationHandler transportCreationHandler = new TransportCreationHandler();
            SSRPHandler ssrpHandler = new SSRPHandler();
            PreloginHandler preloginHandler = new PreloginHandler();
            dataSourceParsingHandler.NextHandler = ssrpHandler;
            ssrpHandler.NextHandler = transportCreationHandler;
            transportCreationHandler.NextHandler = preloginHandler;
            
            await dataSourceParsingHandler.Handle(handlerContext, isAsync, ct).ConfigureAwait(false);
        }
    }
}
