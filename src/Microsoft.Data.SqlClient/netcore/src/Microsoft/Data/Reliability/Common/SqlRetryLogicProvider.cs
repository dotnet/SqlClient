// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// 
    public abstract class SqlRetryLogicProvider : ISqlRetryLogicProvider
    {
        /// 
        public ISqlRetryLogic RetryLogic { get; protected set; }

        /// 
        public TResult Execute<TResult>(Func<TResult> function)
        {
            var exceptions = new List<Exception>();
        retry:
            try
            {
                return function.Invoke();
            }
            catch (Exception e)
            {
                if (RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out int intervalTime))
                    {
                        // TODO: log the retried execution and the throttled exception
                        Thread.Sleep(intervalTime);
                        goto retry;
                    }
                    else
                    {
                        throw CreateException(exceptions);
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        /// 
        public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> function, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
        retry:
            try
            {
                return await function.Invoke();
            }
            catch (Exception e)
            {
                if (RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out int intervalTime))
                    {
                        // TODO: log the retried execution and the throttled exception
                        await Task.Delay(intervalTime, cancellationToken);
                        goto retry;
                    }
                    else
                    {
                        throw CreateException(exceptions);
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        /// 
        public async Task ExecuteAsync<TResult>(Func<Task> function, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
        retry:
            try
            {
                await function.Invoke();
            }
            catch (Exception e)
            {
                if (RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out int intervalTime))
                    {
                        // TODO: log the retried execution and the throttled exception
                        await Task.Delay(intervalTime, cancellationToken);
                        goto retry;
                    }
                    else
                    {   
                        throw CreateException(exceptions);
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        private Exception CreateException(IList<Exception> exceptions)
        {
            // TODO: load the error message from resource file.
            var message = $"The number of retries has been exceeded from the maximum {RetryLogic.NumberOfTries} retry count.";
            RetryLogic.Reset();
            return new AggregateException(message, exceptions);
        }
    }
}
