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
    /// <typeparam name="TRequest"></typeparam>
    internal interface IHandler<TRequest> where TRequest : HandlerRequest
    {
        /// <summary>
        /// The call to handler to execute the request.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="isAsync"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public abstract ValueTask Handle(TRequest request, bool isAsync, CancellationToken ct);

        /// <summary>
        /// If a next handler is to be executed in the chain, then SetNext is used for those purposes.
        /// </summary>
        /// <param name="handler"></param>
        public void SetNext(IHandler<TRequest> handler);
    }
}
