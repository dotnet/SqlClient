// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers
{

    /// <summary>
    /// Interface to represent the handler.
    /// </summary>
    /// <typeparam name="TRequest">The Type of the request object.</typeparam>
    internal interface IHandler<TRequest>
    {
        /// <summary>
        /// Allows getting and setting the next handler.
        /// </summary>
        IHandler<TRequest> NextHandler { get; set; }

        /// <summary>
        /// The call to handler to execute the request.
        /// </summary>
        /// <param name="isAsync">True if the call needs to be made asynchronously.</param>
        /// <param name="request">The request object of the chain of responsibilities</param>
        /// <param name="ct">Cancellation token in case of async</param>
        /// <returns></returns>
        ValueTask Handle(TRequest request, bool isAsync, CancellationToken ct);
    }
}
