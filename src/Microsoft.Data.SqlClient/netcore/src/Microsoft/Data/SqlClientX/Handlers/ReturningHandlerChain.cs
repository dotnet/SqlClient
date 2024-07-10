// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClientX.Handlers
{
    /// <summary>
    /// Class that builds and executes a chain of responsibility.
    /// </summary>
    /// <typeparam name="TParameters">Type of the parameters for the handlers.</typeparam>
    /// <typeparam name="TOutput">Type of the output expected from the chain.</typeparam>
    internal sealed class ReturningHandlerChain<TParameters, TOutput>
        where TOutput : class
    {
        private readonly ReturningHandlerChainExceptionBehavior _exceptionBehavior;
        private readonly ICollection<IReturningHandler<TParameters, TOutput>> _handlerChain;

        /// <summary>
        /// Constructs and initializes a new instance.
        /// </summary>
        /// <param name="handlers">
        /// Collection of handlers that make up the chain. The first handler in the collection will
        /// be the first handler tried to handle the input.
        /// </param>
        /// <param name="exceptionBehavior">
        /// Behavior to observe when an exception occurs during execution of the chain. See
        /// <see cref="ReturningHandlerChainExceptionBehavior"/> for explanation of how each option
        /// affects the execution of the chain.
        /// </param>
        public ReturningHandlerChain(
            ICollection<IReturningHandler<TParameters, TOutput>> handlers,
            ReturningHandlerChainExceptionBehavior exceptionBehavior)
        {
            Debug.Assert(handlers is not null, "Collection of handlers must not be null.");

            _exceptionBehavior = exceptionBehavior;
            _handlerChain = handlers;
        }

        /// <summary>
        /// Executes the chain of responsibility, while observing the desired exception behavior.
        /// </summary>
        /// <param name="parameters">Input to the chain of responsibility.</param>
        /// <param name="isAsync">Whether async pathways should be used.</param>
        /// <param name="ct">Cancellation token that will be passed to each handler.</param>
        /// <returns>
        /// If a handler could successfully handle the input, the result from that handler is
        /// returned. If no handler could successfully handle the input, an exception is thrown.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if an invalid exception behavior enum value is provided (which should never happen).
        /// </exception>
        public ValueTask<TOutput> Handle(TParameters parameters, bool isAsync, CancellationToken ct) =>
            _exceptionBehavior switch
            {
                ReturningHandlerChainExceptionBehavior.Halt => HandleHalt(parameters, isAsync, ct),
                ReturningHandlerChainExceptionBehavior.ThrowCollected => HandleThrowCollected(parameters, isAsync, ct),
                ReturningHandlerChainExceptionBehavior.ThrowFirst => HandleThrowFirst(parameters, isAsync, ct),
                ReturningHandlerChainExceptionBehavior.ThrowLast => HandleThrowLast(parameters, isAsync, ct),
                _ => throw new InvalidOperationException(),
            };

        private async ValueTask<TOutput> HandleHalt(TParameters parameters, bool isAsync, CancellationToken ct)
        {
            foreach (IReturningHandler<TParameters, TOutput> handler in _handlerChain)
            {
                // If handle throws an exception, we let it bubble up higher.
                TOutput result = await handler.Handle(parameters, isAsync, ct);
                if (result is not null)
                {
                    return result;
                }
            }

            // If we make it to here, none of the handlers successfully handled the message. Throw
            // that no handlers found.
            throw new NoSuitableHandlerFoundException();
        }

        private async ValueTask<TOutput> HandleThrowCollected(TParameters parameters, bool isAsync, CancellationToken ct)
        {
            List<Exception> collectedExceptions = new List<Exception>(_handlerChain.Count);
            foreach (IReturningHandler<TParameters, TOutput> handler in _handlerChain)
            {
                try
                {
                    TOutput result = await handler.Handle(parameters, isAsync, ct);
                    if (result is not null)
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    collectedExceptions.Add(e);
                }
            }

            // If we make it to here, none of the handlers successfully handled the message. If we
            // collected any exceptions, throw those, otherwise throw that no handlers were found.
            throw collectedExceptions.Count switch
            {
                0 => new NoSuitableHandlerFoundException(),
                1 => collectedExceptions[0],
                _ => new AggregateException(collectedExceptions),
            };
        }

        private async ValueTask<TOutput> HandleThrowFirst(TParameters parameters, bool isAsync, CancellationToken ct)
        {
            Exception firstException = null;
            foreach (IReturningHandler<TParameters, TOutput> handler in _handlerChain)
            {
                try
                {
                    TOutput result = await handler.Handle(parameters, isAsync, ct);
                    if (result is not null)
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    firstException ??= e;
                }
            }

            // If we make it to here, none of the handlers successfully handled the message. If we
            // received an exception, the first one will be thrown. Otherwise, no handlers found
            // exception will be thrown.
            throw firstException ?? new NoSuitableHandlerFoundException();
        }

        private async ValueTask<TOutput> HandleThrowLast(TParameters parameters, bool isAsync, CancellationToken ct)
        {
            Exception lastException = null;
            foreach (IReturningHandler<TParameters, TOutput> handler in _handlerChain)
            {
                try
                {
                    TOutput result = await handler.Handle(parameters, isAsync, ct);
                    if (result is not null)
                    {
                        return result;
                    }
                }
                catch (Exception e)
                {
                    lastException = e;
                }
            }

            // If we make it to here, none of the handlers successfully handled the message. Throw
            // the last received exception or no handler found if no exception received.
            throw lastException ?? new NoSuitableHandlerFoundException();
        }
    }
}
