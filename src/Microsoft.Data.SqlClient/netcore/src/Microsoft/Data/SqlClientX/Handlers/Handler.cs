using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SNI;
using Microsoft.Data.SqlClientX.IO;


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

    /// <summary>
    /// The request type for the handler. This should be appended to contain any
    /// other requests types as well.
    /// </summary>
    internal enum HandlerRequestType
    {
        ConnectionRequest
    }

    internal abstract class HandlerRequest
    {
        public HandlerRequestType RequestType { get; internal set; }

        /// <summary>
        /// When the Exception is set, that means that the next handler knows about the exception,
        /// and it can choose to execute or perform any clean ups.
        /// </summary>
        public Exception Exception { get; set; }

    }

}
