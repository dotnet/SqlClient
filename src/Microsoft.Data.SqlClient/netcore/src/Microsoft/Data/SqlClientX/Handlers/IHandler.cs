using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClientX.Handlers;

namespace Microsoft.Data.SqlClientX.Handlers
{

    /// <summary>
    /// Interface to repreent the handler.
    /// </summary>
    /// <typeparam name="TRequest">The Type of the request object.</typeparam>
    internal interface IHandler<TRequest> where TRequest : HandlerRequest
    {
        /// <summary>
        /// Allows getting and setting the next handler.
        /// </summary>
        public IHandler<TRequest> NextHandler { get; set; }

        /// <summary>
        /// The call to handler to execute the request.
        /// </summary>
        /// <param name="isAsync">True if the call needs to be made asynchronously.</param>
        /// <param name="request">The request object of the chain of responsibilities</param>
        /// <param name="ct">Cancellation token in case of async</param>
        /// <returns></returns>
        public abstract ValueTask Handle(TRequest request, bool isAsync, CancellationToken ct);
    }
}
