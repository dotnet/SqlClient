﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.Reliability
{
    /// provide retry information on each attemp
    public class SqlRetryingEventArgs : EventArgs
    {

        /// 
        public SqlRetryingEventArgs(int retryCount, TimeSpan delay, IList<Exception> exceptions)
        {
            RetryCount = retryCount;
            Delay = delay;
            Exceptions = exceptions;
        }

        /// retry-attempt-number, after the fisrt exception occurrence
        public int RetryCount { get; private set; }

        /// current waiting time in millisecond
        public TimeSpan Delay { get; private set; }

        /// if set to true retry will intruppted immidiately
        public bool Cancel { get; set; } = false;

        /// list of exceptions since first happening
        public IList<Exception> Exceptions { get; private set; }
    }

    /// 
    public abstract class SqlRetryLogicProvider : ISqlRetryLogicProvider
    {
        // safety switch for the preview version
        private const string EnableRetryLogicSwitch = "Switch.Microsoft.Data.SqlClient.EnableRetryLogic";
        private bool EnableRetryLogic = false;

        /// 
        public EventHandler<SqlRetryingEventArgs> Retrying { set; get; }

        /// 
        public SqlRetryLogicProvider()
        {
            AppContext.TryGetSwitch(EnableRetryLogicSwitch, out EnableRetryLogic);
        }

        private void OnRetrying(SqlRetryingEventArgs eventArgs)
        {
            Retrying?.Invoke(this, eventArgs);
        }

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
                if (EnableRetryLogic && RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        ApplyRetryEvent(RetryLogic.Current, intervalTime, exceptions);

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
                if (EnableRetryLogic && RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        ApplyRetryEvent(RetryLogic.Current, intervalTime, exceptions);

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
                if (EnableRetryLogic && RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        ApplyRetryEvent(RetryLogic.Current, intervalTime, exceptions);

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

        private Exception CreateException(IList<Exception> exceptions, bool manualCancellation = false)
        {
            // TODO: load the error message from resource file.
            string message;
            if (manualCancellation)
            {
                message = $"The retry manually has been canceled by user after {RetryLogic.Current - 1} attempt(s).";
            }
            else
            {
                message = $"The number of retries has been exceeded from the maximum {RetryLogic.Current} attempt(s).";
            }

            RetryLogic.Reset();
            return new AggregateException(message, exceptions);
        }

        private void ApplyRetryEvent(int retryCount, TimeSpan intervalTime, List<Exception> exceptions)
        {
            if (Retrying != null)
            {
                var retryEventArgs = new SqlRetryingEventArgs(retryCount - 1, intervalTime, exceptions);
                OnRetrying(retryEventArgs);
                if (retryEventArgs.Cancel)
                {
                    throw CreateException(exceptions, true);
                }
            }
        }
    }
}
