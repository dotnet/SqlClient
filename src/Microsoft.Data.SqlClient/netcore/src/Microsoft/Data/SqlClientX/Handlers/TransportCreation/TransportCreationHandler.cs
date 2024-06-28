﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers.TransportCreation
{
    /// <summary>
    /// Handler for creating the connection to the server. This handler passes the context through
    /// its own chain of responsibilities to determine what protocol to connect with.
    /// </summary>
    internal sealed class TransportCreationHandler : ContextHandler<ConnectionHandlerContext>
    {
        private readonly ReturningHandlerChain<ConnectionHandlerContext, Stream> _streamCreationChain;

        public TransportCreationHandler()
        {
            // Construct the chain of responsibility for handling the connections
            _streamCreationChain = new ReturningHandlerChain<ConnectionHandlerContext, Stream>(
                new IReturningHandler<ConnectionHandlerContext, Stream>[]
                {
                    new SharedMemoryTransportCreationHandler(),
                    new TcpTransportCreationHandler(),
                    new NamedPipeTransportCreationHandler(),
                },
                ReturningHandlerChainExceptionBehavior.ThrowLast);
        }

        /// <inheritdoc />
        public override async ValueTask Handle(ConnectionHandlerContext context, bool isAsync, CancellationToken ct)
        {
            try
            {
                context.ConnectionStream = await _streamCreationChain.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                context.Error = e;
                return;
            }

            if (NextHandler is not null)
            {
                await NextHandler.Handle(context, isAsync, ct).ConfigureAwait(false);
            }
        }
    }
}
