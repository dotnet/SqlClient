// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// The request type for the handler. This should be appended to contain any
    /// other requests types as well.
    /// </summary>
    internal enum HandlerRequestType
    {
        /// <summary>
        /// Request type for connection request.
        /// </summary>
        ConnectionHandlerContext,
    }
}
