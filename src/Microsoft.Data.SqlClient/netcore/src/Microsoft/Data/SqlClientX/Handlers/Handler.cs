using System;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// The abstract class representing the handler.
    /// 
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    internal abstract class Handler<TRequest> : IHandler<TRequest> where TRequest : HandlerRequest
    {
        /// <summary>
        /// Stores the internal handler to be executed next.
        /// </summary>
        private IHandler<TRequest> _nextHandler;

        /// <summary>
        /// Property for interacting with the Next Handler.
        /// </summary>
        public IHandler<TRequest> NextHandler { get => _nextHandler; set => _nextHandler = value; }

        /// <inheritdoc/>
        public abstract ValueTask Handle(TRequest request, bool isAsync, CancellationToken ct);


        /// <inheritdoc/>
        public virtual void SetNext(IHandler<TRequest> handler)
        {
            NextHandler = handler;
        }
    }


}
