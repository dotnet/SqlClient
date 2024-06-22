// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.Handlers.Connection.PreloginSubHandlers;
using Microsoft.Data.SqlClientX.IO;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    /// <summary>
    /// Handler to send and receive the prelogin request.
    /// This handler will send the prelogin based on the features requested in the connection string.
    /// It will consume the prelogin handshake and pass the control to the next handler.
    /// </summary>
    internal class PreloginHandler : IHandler<ConnectionHandlerContext>
    {
       
        // EventSource counter
        private static int s_objectTypeCount;

        internal readonly int _objectID = Interlocked.Increment(ref s_objectTypeCount);

        internal int ObjectID => _objectID;

        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        /// <inheritdoc />
        public async ValueTask Handle(ConnectionHandlerContext connectionContext, bool isAsync, CancellationToken ct)
        {
            PreLoginHandlerContext context = new PreLoginHandlerContext(connectionContext);

            TlsBeginHandler tlsBeginHandler = new TlsBeginHandler();
            PreloginPacketHandler preloginPacketHandler = new PreloginPacketHandler();
            TlsEndHandler tlsEndHandler = new TlsEndHandler();

            tlsBeginHandler.NextHandler = preloginPacketHandler;
            preloginPacketHandler.NextHandler = tlsEndHandler;
            tlsEndHandler.NextHandler = null;

            await tlsBeginHandler.Handle(context, isAsync, ct).ConfigureAwait(false);

            if (NextHandler is not null)
            {
                await NextHandler.Handle(connectionContext, isAsync, ct).ConfigureAwait(false);
            }
        }

        
         
    }
}
