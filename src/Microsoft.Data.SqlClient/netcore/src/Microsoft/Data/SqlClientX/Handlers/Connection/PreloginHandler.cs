// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Handler to send and receive the prelogin request.
    /// This handler will send the prelogin based on the features requested in the connection string.
    /// It will consume the prelogin handshake and pass the control to the next handler.
    /// </summary>
    internal class PreloginHandler : IHandler<ConnectionHandlerContext>
    {
        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(ConnectionHandlerContext connectionContext, bool isAsync, CancellationToken ct)
        {
            PreLoginHandlerContext context = new PreLoginHandlerContext(connectionContext);

            var tlsBeginHandler = new TlsBeginHandler();
            var preloginPacketHandler = new PreloginPacketHandler();
            var tlsEndHandler = new TlsEndHandler();

            tlsBeginHandler.NextHandler = preloginPacketHandler;
            preloginPacketHandler.NextHandler = tlsEndHandler;
            tlsEndHandler.NextHandler = null;

            await tlsBeginHandler.Handle(context, isAsync, ct).ConfigureAwait(false);

            if (!context.HasError && NextHandler is not null)
            {
                await NextHandler.Handle(connectionContext, isAsync, ct).ConfigureAwait(false);
            }
        }
    }
}
