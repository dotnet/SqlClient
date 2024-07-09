// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers.TransportCreation;

namespace Microsoft.Data.SqlClientX.Handlers
{
    internal class HandlerOrchestrator
    {
        /// <summary>
        /// Class responsible for creation of chain of handlers, initiate chain, retain handler-context, maintain 
        /// routing count and history of all handler contexts for all handlers in the chain
        /// </summary>
        private  IHandler<HandlerRequest> _initialHandler;
        private ConnectionHandlerContext _initialConnectionHandlerContext;
        private InternalConnectionContext _internalConnectionContext;

        public HandlerOrchestrator(IHandler<HandlerRequest> initialHandler, ConnectionHandlerContext initialConnectionHandlerContext, InternalConnectionContext internalConnectionContext, List<ConnectionHandlerContext> history)
        {
            _initialHandler = initialHandler;
            _initialConnectionHandlerContext = initialConnectionHandlerContext;
            _internalConnectionContext = internalConnectionContext;
        }

        /// <summary>
        /// Intiates the pre-defined chain of handlers
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="HandlerNotFoundException"></exception>
        public async ValueTask ProcessRequestAsync(bool isAsync, CancellationToken ct) 
        {
            CreateHandlerChain();
            if (_initialHandler is not null)
            {
                //TODO: Deal with missing handler exception in the connector and populate relevant SqlException in future.
                throw new HandlerNotFoundException();
                
            }
            await _initialHandler.Handle(_initialConnectionHandlerContext, isAsync, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates the chain of handlers in the order definition
        /// </summary>
        private void CreateHandlerChain()
        {
            // List of handlers to be added to the chain, ex : DSH -> TCH -> Prelogin -> Login -> null
            List<IHandler<ConnectionHandlerContext>> handlers = new List<IHandler<ConnectionHandlerContext>>
            {
                new DataSourceParsingHandler(),
                new TransportCreationHandler(),
                //new PreLoginHandler(),
            };

            for (int i = 0; i < handlers.Count - 1; i++)
            {
                handlers[i].NextHandler = handlers[i + 1];
            }

            // Set the first handler in the chain
            if (handlers.Count > 0)
            {
                // explicit cast because handlers currently use ConnectionHandlerContext
                _initialHandler = (IHandler<HandlerRequest>)handlers[0];
            }
        }
    }
}
