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
        /// Class responsible to create the chain of handlers, initiate chain, retain handler-context, maintain 
        /// routing count and history of all handler contexts for all handlers in the chain
        /// </summary>
        private  IHandler<HandlerRequest> _initialHandler;
        private ConnectionHandlerContext _initialConnectionHandlerContext;
        private InternalConnectionContext _internalConnectionContext;
        private int _routingCount;
        private List<ConnectionHandlerContext> _routingHistory;

        public HandlerOrchestrator(IHandler<HandlerRequest> initialHandler, ConnectionHandlerContext initialConnectionHandlerContext, InternalConnectionContext internalConnectionContext, List<ConnectionHandlerContext> history)
        {
            _initialHandler = initialHandler;
            _initialConnectionHandlerContext = initialConnectionHandlerContext;
            _internalConnectionContext = internalConnectionContext;
            _routingHistory = history;
            _routingCount = 0;
        }

        /// <summary>
        /// Intiates the pre-defined chain of handlers
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        /// <exception cref="HandlerNotFoundException"></exception>
        public async Task ProcessRequestAsync(bool isAsync, CancellationToken ct) 
        {
            CreateHandlerChain();
            if (_initialHandler != null)
            {
                await _initialHandler.Handle(_initialConnectionHandlerContext, isAsync, ct).ConfigureAwait(false);
            }
            else
            {
                //TODO: Deal with missing handler exception in the connector and populate relevant SqlException in future.
                throw new HandlerNotFoundException();
            }
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
                _initialHandler = (IHandler<HandlerRequest>)handlers[0];
            }
        }

        /// <summary>
        /// Adds the context of previous handler into the routingHistory object in case
        /// of a reroute, exception etc.
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="ArgumentNullException"></exception>
        private void AddToHistory(ConnectionHandlerContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context), "Context cannot be null");
            }
            _routingHistory.Add((ConnectionHandlerContext)context.Clone());
            _routingCount++;
        }
    }
}
