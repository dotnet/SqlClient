// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Options for how exceptions should be handled in the <see cref="ReturningHandlerChain{TParameters,TOutput}"/>.
    /// </summary>
    internal enum ReturningHandlerChainExceptionBehavior
    {
        /// <summary>
        /// Indicated that the first exception thrown by a handler should be thrown. This implies
        /// that on exception, the next handler will not be tried.
        /// </summary>
        Halt,

        /// <summary>
        /// Indicates that any and all exceptions thrown by handlers in the chain should be
        /// collected and thrown at the end. This implies on exception, the next handler will be
        /// tried.
        /// </summary>
        ThrowCollected,

        /// <summary>
        /// Indicates that the first exception thrown by the handlers should be thrown. This
        /// implies that on exception, the next handler will be tried. If no handler in the chain
        /// can handle the input, then the first encountered exception will be thrown.
        /// </summary>
        ThrowFirst,

        /// <summary>
        /// Indicates that the last exception thrown by the handler should be thrown. This implies
        /// that on exception, the next handler will be tried. If no handler in the chain can
        /// successfully handle the input, then the last encountered exception will be thrown.
        /// </summary>
        ThrowLast,
    }
}
