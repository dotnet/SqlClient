// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.Utilities
{
    internal static class AsyncHelper
    {
        internal static void ContinueTask(Task task,
            TaskCompletionSource<object> completion,
            Action onSuccess,
            Action<Exception> onFailure = null,
            Action onCancellation = null,
            Func<Exception, Exception> exceptionConverter = null)
        {
            task.ContinueWith(
                tsk =>
                {
                    if (tsk.Exception != null)
                    {
                        Exception exc = tsk.Exception.InnerException;
                        if (exceptionConverter != null)
                        {
                            exc = exceptionConverter(exc);
                        }
                        try
                        {
                            onFailure?.Invoke(exc);
                        }
                        finally
                        {
                            completion.TrySetException(exc);
                        }
                    }
                    else if (tsk.IsCanceled)
                    {
                        try
                        {
                            onCancellation?.Invoke();
                        }
                        finally
                        {
                            completion.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            onSuccess();
                        }
                        // @TODO: CER Exception Handling was removed here (see GH#3581)
                        catch (Exception e)
                        {
                            completion.SetException(e);
                        }
                    }
                }, TaskScheduler.Default
            );
        }

        // the same logic as ContinueTask but with an added state parameter to allow the caller to avoid the use of a closure
        // the parameter allocation cannot be avoided here and using closure names is clearer than Tuple numbered properties
        internal static void ContinueTaskWithState(Task task,
            TaskCompletionSource<object> completion,
            object state,
            Action<object> onSuccess,
            Action<Exception, object> onFailure = null,
            Action<object> onCancellation = null,
            Func<Exception, Exception> exceptionConverter = null)
        {
            task.ContinueWith(
                (Task tsk, object state2) =>
                {
                    if (tsk.Exception != null)
                    {
                        Exception exc = tsk.Exception.InnerException;
                        if (exceptionConverter != null)
                        {
                            exc = exceptionConverter(exc);
                        }

                        try
                        {
                            onFailure?.Invoke(exc, state2);
                        }
                        finally
                        {
                            completion.TrySetException(exc);
                        }
                    }
                    else if (tsk.IsCanceled)
                    {
                        try
                        {
                            onCancellation?.Invoke(state2);
                        }
                        finally
                        {
                            completion.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            onSuccess(state2);
                        }
                        // @TODO: CER Exception Handling was removed here (see GH#3581)
                        catch (Exception e)
                        {
                            completion.SetException(e);
                        }
                    }
                },
                state: state,
                scheduler: TaskScheduler.Default
            );
        }

        internal static Task CreateContinuationTask(
            Task task,
            Action onSuccess,
            Action<Exception> onFailure = null)
        {
            if (task == null)
            {
                onSuccess();
                return null;
            }
            else
            {
                TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
                ContinueTaskWithState(
                    task,
                    completion,
                    state: Tuple.Create(onSuccess, onFailure, completion),
                    onSuccess: static (object state) =>
                    {
                        var parameters = (Tuple<Action, Action<Exception>, TaskCompletionSource<object>>)state;
                        Action success = parameters.Item1;
                        TaskCompletionSource<object> taskCompletionSource = parameters.Item3;
                        success();
                        taskCompletionSource.SetResult(null);
                    },
                    onFailure: static (Exception exception, object state) =>
                    {
                        var parameters = (Tuple<Action, Action<Exception>, TaskCompletionSource<object>>)state;
                        Action<Exception> failure = parameters.Item2;
                        failure?.Invoke(exception);
                    }
                );
                return completion.Task;
            }
        }

        internal static Task CreateContinuationTask<T1, T2>(
            Task task,
            Action<T1, T2> onSuccess,
            T1 arg1,
            T2 arg2,
            Action<Exception> onFailure = null)
        {
            return CreateContinuationTask(task, () => onSuccess(arg1, arg2), onFailure);
        }

        internal static Task CreateContinuationTaskWithState(Task task, object state, Action<object> onSuccess, Action<Exception, object> onFailure = null)
        {
            if (task == null)
            {
                onSuccess(state);
                return null;
            }
            else
            {
                var completion = new TaskCompletionSource<object>();
                ContinueTaskWithState(task, completion, state,
                    onSuccess: (object continueState) =>
                    {
                        onSuccess(continueState);
                        completion.SetResult(null);
                    },
                    onFailure: onFailure
                );
                return completion.Task;
            }
        }

        internal static void SetTimeoutException(TaskCompletionSource<object> completion, int timeout, Func<Exception> onFailure, CancellationToken ctoken)
        {
            if (timeout > 0)
            {
                Task.Delay(timeout * 1000, ctoken).ContinueWith(
                    (Task task) =>
                    {
                        if (!task.IsCanceled && !completion.Task.IsCompleted)
                        {
                            completion.TrySetException(onFailure());
                        }
                    }
                );
            }
        }

        internal static void SetTimeoutExceptionWithState(
            TaskCompletionSource<object> completion,
            int timeout,
            object state,
            Func<object, Exception> onFailure,
            CancellationToken cancellationToken)
        {
            if (timeout <= 0)
            {
                return;
            }

            Task.Delay(timeout * 1000, cancellationToken).ContinueWith(
                (task, innerState) =>
                {
                    if (!task.IsCanceled && !completion.Task.IsCompleted)
                    {
                        completion.TrySetException(onFailure(innerState));
                    }
                },
                state: state,
                cancellationToken: CancellationToken.None);
        }

        internal static void WaitForCompletion(Task task, int timeout, Action onTimeout = null, bool rethrowExceptions = true)
        {
            try
            {
                task.Wait(timeout > 0 ? (1000 * timeout) : Timeout.Infinite);
            }
            catch (AggregateException ae)
            {
                if (rethrowExceptions)
                {
                    Debug.Assert(ae.InnerExceptions.Count == 1, "There is more than one exception in AggregateException");
                    ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
                }
            }
            if (!task.IsCompleted)
            {
                task.ContinueWith(static t => { var ignored = t.Exception; }); //Ensure the task does not leave an unobserved exception
                onTimeout?.Invoke();
            }
        }
    }
}
