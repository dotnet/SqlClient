// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Apply a retry logic on an operation.
    /// </summary>
    internal class SqlRetryLogicProvider : SqlRetryLogicBaseProvider
    {
        // safety switch for the preview version
        private const string EnableRetryLogicSwitch = "Switch.Microsoft.Data.SqlClient.EnableRetryLogic";
        private readonly bool enableRetryLogic = false;

        ///
        public SqlRetryLogicProvider(SqlRetryLogicBase retryLogic)
        {
            AppContext.TryGetSwitch(EnableRetryLogicSwitch, out enableRetryLogic);
            RetryLogic = retryLogic;
        }

        ///
        public override TResult Execute<TResult>(object sender, Func<TResult> function)
        {
            var exceptions = new List<Exception>();
            RetryLogic.Reset();
        retry:
            try
            {
                return function.Invoke();
            }
            catch (Exception e)
            {
                if (enableRetryLogic && RetryLogic.RetryCondition(sender) && RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        // The retrying event raises on each retry.
                        ApplyRetryingEvent(sender, RetryLogic.Current, intervalTime, exceptions);

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
        public override async Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
            RetryLogic.Reset();
        retry:
            try
            {
                return await function.Invoke();
            }
            catch (Exception e)
            {
                if (enableRetryLogic && RetryLogic.RetryCondition(sender) && RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        // The retrying event raises on each retry.
                        ApplyRetryingEvent(sender, RetryLogic.Current, intervalTime, exceptions);

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
        public override async Task ExecuteAsync(object sender, Func<Task> function, CancellationToken cancellationToken = default)
        {
            var exceptions = new List<Exception>();
            RetryLogic.Reset();
        retry:
            try
            {
                await function.Invoke();
            }
            catch (Exception e)
            {
                if (enableRetryLogic && RetryLogic.RetryCondition(sender) && RetryLogic.TransientPredicate(e))
                {
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        // The retrying event raises on each retry.
                        ApplyRetryingEvent(sender, RetryLogic.Current, intervalTime, exceptions);

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
        
        #region private methods

        private Exception CreateException(IList<Exception> exceptions, bool manualCancellation = false)
        {
            // TODO: load the error message from resource file.
            string message;
            if (manualCancellation)
            {
                message = $"The retry manually has been canceled by user after {RetryLogic.Current} attempt(s).";
            }
            else
            {
                message = $"The number of retries has been exceeded from the maximum {RetryLogic.NumberOfTries} attempt(s).";
            }

            return new AggregateException(message, exceptions);
        }

        private void OnRetrying(object sender, SqlRetryingEventArgs eventArgs) => Retrying?.Invoke(sender, eventArgs);

        private void ApplyRetryingEvent(object sender, int retryCount, TimeSpan intervalTime, List<Exception> exceptions)
        {
            if (Retrying != null)
            {
                var retryEventArgs = new SqlRetryingEventArgs(retryCount - 1, intervalTime, exceptions);
                OnRetrying(sender, retryEventArgs);
                if (retryEventArgs.Cancel)
                {
                    throw CreateException(exceptions, true);
                }
            }
        }
        #endregion
    }
}
