// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Base class for handlers that pass a context between them. These handlers are expected to
    /// modify the context and pass them onto the next handler in the chain. This pattern is
    /// colloquially referred to a chain of handlers (CoH).
    /// </summary>
    /// <typeparam name="TContext">Type of the context that will be passed through the chain of handlers.</typeparam>
    internal abstract class ContextHandler<TContext>
    {
        /// <summary>
        /// Gets or sets the next handler in the chain of handlers.
        /// </summary>
        public ContextHandler<TContext> NextHandler { get; set; }

        /// <summary>
        /// Handles the context provided.
        /// </summary>
        /// <remarks>
        /// Implementations of this method should read and modify the <paramref name="context"/>
        /// provided to it. The implementation must call <see cref="Handle"/> on
        /// <see cref="NextHandler"/> to continue the chain as appropriate.
        /// </remarks>
        /// <param name="context">
        /// Context object that contains all input, output, and intermediate values that apply to
        /// this chain of handlers.
        /// </param>
        /// <param name="isAsync">
        /// If <c>true</c>, asynchronous code paths should be used. If <c>false</c>, only
        /// synchronous code paths should be used.
        /// </param>
        /// <param name="ct">Cancellation token to signal cancellation of the request.</param>
        public abstract ValueTask Handle(TContext context, bool isAsync, CancellationToken ct);
    }
}
