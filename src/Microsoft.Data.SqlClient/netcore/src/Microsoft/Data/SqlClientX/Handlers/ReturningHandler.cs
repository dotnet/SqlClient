// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Base class for handlers that will return if they can handle the parameters passed into
    /// them. These handlers are expected to not modify the passed in parameters and instead return
    /// the result of processing, or throw on error conditions. This pattern follows the more
    /// traditional Chain of Responsibility (CoR) pattern.
    /// </summary>
    /// <typeparam name="TParameters">Type of the parameters expected as input to the chain.</typeparam>
    /// <typeparam name="TOutput">Type of the output expected by the chain.</typeparam>
    internal abstract class ReturningHandler<TParameters, TOutput>
    {
        /// <summary>
        /// Gets or sets the next handler in the chain of responsibility.
        /// </summary>
        public ReturningHandler<TParameters, TOutput> NextHandler { get; set; }

        /// <summary>
        /// Handles the provided parameters.
        /// </summary>
        /// <remarks>
        /// Implementations of this method should read and modify the <paramref name="parameters"/>
        /// provided to it. The implementation must call <see cref="Handle"/> on
        /// <see cref="NextHandler"/> to continue the chain as appropriate. If the handler can
        /// handle the parameters, it should return (or throw).
        /// </remarks>
        /// <param name="parameters">Parameters to handle.</param>
        /// <param name="isAsync">
        /// If <c>true</c>, asynchronous code paths should be used. If <c>false</c>, only
        /// synchronous code paths should be used.
        /// </param>
        /// <param name="ct">Cancellation token to signal cancellation of the request.</param>
        /// <returns>Result of the handler.</returns>
        public abstract ValueTask<TOutput> Handle(TParameters parameters, bool isAsync, CancellationToken ct);

        /// <summary>
        /// Method available for handlers that checks for a next handler and if one exists, passes
        /// the call on to the next handler, returning the result. If no next handler exists, then
        /// an exception is thrown.
        /// </summary>
        /// <param name="parameters">Parameters to handle.</param>
        /// <param name="isAsync">
        /// If <c>true</c>, asynchronous code paths should be used. If <c>false</c>, only
        /// synchronous code paths should be used.
        /// </param>
        /// <param name="ct">Cancellation token to signal cancellation of the request.</param>
        /// <returns>Result of calling the next handler to handle the request.</returns>
        protected ValueTask<TOutput> HandleNext(TParameters parameters, bool isAsync, CancellationToken ct)
        {
            if (NextHandler is null)
            {
                throw new NoSuitableHandlerFoundException();
            }

            return NextHandler.Handle(parameters, isAsync, ct);
        }
    }
}
