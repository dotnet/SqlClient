// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Interface for handlers that will return if they can handle the parameters passed into
    /// them. These handlers are expected to not modify the passed in parameters and instead return
    /// the result of processing, or throw on error conditions. This pattern follows the more
    /// traditional Chain of Responsibility (CoR) pattern and is orchestrated by
    /// <see cref="ReturningHandlerChain{TParameters,TOutput}"/>.
    /// </summary>
    /// <typeparam name="TParameters">Type of the input parameters this handler can handle.</typeparam>
    /// <typeparam name="TOutput">Type of the output this handler will return on successful handling.</typeparam>
    public interface IReturningHandler<in TParameters, TOutput>
        where TOutput : class
    {
        /// <summary>
        /// Handles the provided parameters.
        /// </summary>
        /// <remarks>
        /// Implementations of this method should only read the parameters and act on them as
        /// appropriate.
        ///   * If the handler can handle the input, the result should be returned.
        ///   * If the handler cannot handle the input, <c>null</c> should be returned.
        ///   * If the handler encounters an exception, the exception should be thrown. Whether
        ///     this halts the chain or the chain continues is determined by the exception behavior
        ///     provided to the orchestrator.
        /// </remarks>
        /// <param name="parameters">Parameters to handle.</param>
        /// <param name="isAsync">
        /// If <c>true</c>, asynchronous code paths should be used. If <c>false</c>, only
        /// synchronous code paths should be used.
        /// </param>
        /// <param name="ct">Cancellation token to signal cancellation of the request.</param>
        /// <returns>Result of the handler.</returns>
        ValueTask<TOutput> Handle(TParameters parameters, bool isAsync, CancellationToken ct);
    }
}
