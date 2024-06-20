// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers.Connection
{
    internal class PreloginHandler : IHandler<ConnectionHandlerContext>
    {
        /// <inheritdoc />
        public IHandler<ConnectionHandlerContext> NextHandler { get; set; }

        public ValueTask Handle(ConnectionHandlerContext request, bool isAsync, CancellationToken ct)
        {
            throw new NotImplementedException();
        }
    }
}
