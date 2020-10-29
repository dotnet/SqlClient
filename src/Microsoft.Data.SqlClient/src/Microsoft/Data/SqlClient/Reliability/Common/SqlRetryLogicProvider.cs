// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Apply a retry logic on an operation.
    /// </summary>
    internal class SqlRetryLogicProvider : SqlRetryLogicBaseProvider
    {
        private readonly string _typeName = nameof(SqlRetryLogicProvider);

        // safety switch for the preview version
        private const string EnableRetryLogicSwitch = "Switch.Microsoft.Data.SqlClient.EnableRetryLogic";
        private readonly bool enableRetryLogic = false;

        ///
        public SqlRetryLogicProvider(SqlRetryLogicBase retryLogic)
        {
            Debug.Assert(retryLogic != null, $"The '{nameof(retryLogic)}' cannot be null.");
            AppContext.TryGetSwitch(EnableRetryLogicSwitch, out enableRetryLogic);
            RetryLogic = retryLogic;
        }

        ///
        public override TResult Execute<TResult>(object sender, Func<TResult> function)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

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
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.Execute<TResult>|INFO> Found the action eligible to apply the retry policy (retried attempts = {1}).", _typeName, RetryLogic.Current);
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        // The retrying event raises on each retry.
                        ApplyRetryingEvent(sender, RetryLogic.Current, intervalTime, exceptions, e);

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
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.Execute<TResult>|INFO> Found the action not eligible to apply the retry policy (retried attempts = {1}).", _typeName, RetryLogic.Current);
                    throw;
                }
            }
        }

        ///
        public override async Task<TResult> ExecuteAsync<TResult>(object sender, Func<Task<TResult>> function, CancellationToken cancellationToken = default)
        {
            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

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
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.ExecuteAsync<TResult>|INFO> Found the action eligible to apply the retry policy (retried attempts = {1}).", _typeName, RetryLogic.Current);
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        // The retrying event raises on each retry.
                        ApplyRetryingEvent(sender, RetryLogic.Current, intervalTime, exceptions, e);

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
            if(function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

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
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.ExecuteAsync|INFO> Found the action eligible to apply the retry policy (retried attempts = {1}).", _typeName, RetryLogic.Current);
                    exceptions.Add(e);
                    if (RetryLogic.TryNextInterval(out TimeSpan intervalTime))
                    {
                        // The retrying event raises on each retry.
                        ApplyRetryingEvent(sender, RetryLogic.Current, intervalTime, exceptions, e);

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
            string message;
            if (manualCancellation)
            {
                message = StringsHelper.GetString(Strings.SqlRetryLogic_RetryCanceled, RetryLogic.Current);
            }
            else
            {
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|ERR|THROW> Exiting the retry scope (has been exceeded the allowed attempts = {2}).", _typeName, MethodBase.GetCurrentMethod().Name, RetryLogic.NumberOfTries);
                message = StringsHelper.GetString(Strings.SqlRetryLogic_RetryExceeded, RetryLogic.NumberOfTries);
            }
            return new AggregateException(message, exceptions);
        }

        private void OnRetrying(object sender, SqlRetryingEventArgs eventArgs) => Retrying?.Invoke(sender, eventArgs);

        private void ApplyRetryingEvent(object sender, int retryCount, TimeSpan intervalTime, List<Exception> exceptions, Exception lastException)
        {
            if (Retrying != null)
            {
                var retryEventArgs = new SqlRetryingEventArgs(retryCount, intervalTime, exceptions);
                SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Running the retrying event.", _typeName, MethodBase.GetCurrentMethod().Name);
                OnRetrying(sender, retryEventArgs);
                if (retryEventArgs.Cancel)
                {
                    SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Retry attempt is canceled manually by user (current retry number = {2}).", _typeName, MethodBase.GetCurrentMethod().Name, retryCount);
                    throw CreateException(exceptions, true);
                }
            }
            SqlClientEventSource.Log.TryTraceEvent("<sc.{0}.{1}|INFO> Wait '{2}' and run the action for the retry number {3}.", _typeName, MethodBase.GetCurrentMethod().Name, intervalTime, retryCount);
        }
        #endregion
    }
}
