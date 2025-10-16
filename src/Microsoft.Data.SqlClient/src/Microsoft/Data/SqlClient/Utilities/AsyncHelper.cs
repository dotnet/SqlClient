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

        internal static void ContinueTaskWithState<TState>(
            Task taskToContinue,
            TaskCompletionSource<object> taskCompletionSource,
            TState state,
            Action<TState> onSuccess,
            Action<TState, Exception> onFailure = null,
            Action<TState> onCancellation = null)
        {
            ContinuationState<TState> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State: state,
                TaskCompletionSource: taskCompletionSource);

            taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState> typedState2 =
                        (ContinuationState<TState>)state2;

                    if (task.Exception is not null)
                    {
                        // @TODO: Exception converter?
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State, task.Exception);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(task.Exception);
                        }
                    }
                    else if (task.IsCanceled)
                    {
                        try
                        {
                            typedState2.OnCancellation?.Invoke(typedState2.State);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            typedState2.OnSuccess(typedState2.State);
                            // @TODO: The one unpleasant thing with this code is that the TCS is not set completed and left to the caller to do or not do (which is more unpleasant)
                        }
                        catch (Exception e)
                        {
                            typedState2.TaskCompletionSource.SetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);
        }

        internal static void ContinueTaskWithState<TState1, TState2>(
            Task taskToContinue,
            TaskCompletionSource<object> taskCompletionSource,
            TState1 state1,
            TState2 state2,
            Action<TState1, TState2> onSuccess,
            Action<TState1, TState2, Exception> onFailure = null,
            Action<TState1, TState2> onCancellation = null)
        {
            ContinuationState<TState1, TState2> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State1: state1,
                State2: state2,
                TaskCompletionSource: taskCompletionSource);

            taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState1, TState2> typedState2 = (ContinuationState<TState1, TState2>)state2;

                    if (task.Exception is not null)
                    {
                        // @TODO: Exception converter?
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State1, typedState2.State2, task.Exception);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(task.Exception);
                        }
                    }
                    else if (task.IsCanceled)
                    {
                        try
                        {
                            typedState2.OnCancellation?.Invoke(typedState2.State1, typedState2.State2);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            typedState2.OnSuccess(typedState2.State1, typedState2.State2);
                        }
                        catch (Exception e)
                        {
                            typedState2.TaskCompletionSource.SetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);
        }

        internal static Task CreateContinuationTask(
            Task taskToContinue,
            Action onSuccess,
            Action<Exception> onFailure = null,
            Action onCancellation = null)
        {
            if (taskToContinue is null)
            {
                onSuccess();
                return null;
            }

            // @TODO: Can totally use a non-generic TaskCompletionSource
            TaskCompletionSource<object> taskCompletionSource = new();
            ContinuationState continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                TaskCompletionSource: taskCompletionSource);

            taskToContinue.ContinueWith(static (task, continuationState2) =>
                {
                    ContinuationState typedState = (ContinuationState)continuationState2;
                    if (task.Exception is not null)
                    {
                        try
                        {
                            typedState.OnFailure?.Invoke(task.Exception);
                        }
                        finally
                        {
                            typedState.TaskCompletionSource.TrySetException(task.Exception);
                        }
                    }
                    else if (task.IsCanceled)
                    {
                        try
                        {
                            typedState.OnCancellation?.Invoke();
                        }
                        finally
                        {
                            typedState.TaskCompletionSource.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            typedState.OnSuccess();
                            typedState.TaskCompletionSource.SetResult(null);
                        }
                        catch (Exception e)
                        {
                            typedState.TaskCompletionSource.SetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            return taskCompletionSource.Task;
        }

        internal static Task CreateContinuationTaskWithState<TState>(
            Task taskToContinue,
            TState state,
            Action<TState> onSuccess,
            Action<TState, Exception> onFailure = null,
            Action<TState> onCancellation = null)
        {
            // Note: this code is almost identical to ContinueTaskWithState, but creates its own
            // task completion source and completes it on success.
            // Yes, we could just chain into the ContinueTaskWithState, but that requires wrapping
            // more state in a tuple and confusing the heck out of people. So, duplicating code
            // just makes things more clean. Besides, @TODO: We should get rid of these helpers and
            // just use async/await natives.

            if (taskToContinue is null)
            {
                onSuccess(state);
                return null;
            }

            // @TODO: Can totally use a non-generic TaskCompletionSource
            TaskCompletionSource<object> taskCompletionSource = new();
            ContinuationState<TState> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State: state,
                TaskCompletionSource: taskCompletionSource);

            taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState> typedState2 = (ContinuationState<TState>)state2;

                    if (task.Exception is not null)
                    {
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State, task.Exception);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(task.Exception);
                        }
                    }
                    else if (task.IsCanceled)
                    {
                        try
                        {
                            typedState2.OnCancellation?.Invoke(typedState2.State);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            typedState2.OnSuccess(typedState2.State);
                            typedState2.TaskCompletionSource.SetResult(null);
                        }
                        catch (Exception e)
                        {
                            typedState2.TaskCompletionSource.SetException(e);
                        }
                    }

                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            return taskCompletionSource.Task;
        }

        internal static Task CreateContinuationTaskWithState<TState1, TState2>(
            Task taskToContinue,
            TState1 state1,
            TState2 state2,
            Action<TState1, TState2> onSuccess,
            Action<TState1, TState2, Exception> onFailure = null,
            Action<TState1, TState2> onCancellation = null)
        {
            // Note: this code is almost identical to ContinueTaskWithState, but creates its own
            // task completion source and completes it on success.
            // Yes, we could just chain into the ContinueTaskWithState, but that requires wrapping
            // more state in a tuple and confusing the heck out of people. So, duplicating code
            // just makes things more clean. Besides, @TODO: We should get rid of these helpers and
            // just use async/await natives.

            if (taskToContinue is null)
            {
                onSuccess(state1, state2);
                return null;
            }

            // @TODO: Can totally use a non-generic TaskCompletionSource
            TaskCompletionSource<object> taskCompletionSource = new();
            ContinuationState<TState1, TState2> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State1: state1,
                State2: state2,
                TaskCompletionSource: taskCompletionSource);

            taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState1, TState2> typedState2 = (ContinuationState<TState1, TState2>)state2;

                    if (task.Exception is not null)
                    {
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State1, typedState2.State2, task.Exception);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(task.Exception);
                        }
                    }
                    else if (task.IsCanceled)
                    {
                        try
                        {
                            typedState2.OnCancellation?.Invoke(typedState2.State1, typedState2.State2);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetCanceled();
                        }
                    }
                    else
                    {
                        try
                        {
                            typedState2.OnSuccess(typedState2.State1, typedState2.State2);
                            typedState2.TaskCompletionSource.SetResult(null);
                        }
                        catch (Exception e)
                        {
                            typedState2.TaskCompletionSource.SetException(e);
                        }
                    }

                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            return taskCompletionSource.Task;
        }

        // @TODO: This is a pretty wonky way of doing timeouts, imo.
        internal static void SetTimeoutException(
            TaskCompletionSource<object> taskCompletionSource,
            int timeoutInSeconds,
            Func<Exception> onTimeout,
            CancellationToken cancellationToken)
        {
            if (timeoutInSeconds <= 0)
            {
                return;
            }

            Task.Delay(TimeSpan.FromSeconds(timeoutInSeconds), cancellationToken)
                .ContinueWith(
                    task =>
                    {
                        // If the timeout ran to completion AND the task to complete did not complete
                        // then the timeout expired first, run the timeout handler
                        if (!task.IsCanceled && !taskCompletionSource.Task.IsCompleted)
                        {
                            taskCompletionSource.TrySetException(onTimeout());
                        }
                    },
                    cancellationToken);
        }

        private record ContinuationState(
            Action OnCancellation,
            Action<Exception> OnFailure,
            Action OnSuccess,
            TaskCompletionSource<object> TaskCompletionSource);

        private record ContinuationState<TState>(
            Action<TState> OnCancellation,
            Action<TState, Exception> OnFailure,
            Action<TState> OnSuccess,
            TState State,
            TaskCompletionSource<object> TaskCompletionSource);

        private record ContinuationState<TState1, TState2>(
            Action<TState1, TState2> OnCancellation,
            Action<TState1, TState2, Exception> OnFailure,
            Action<TState1, TState2> OnSuccess,
            TState1 State1,
            TState2 State2,
            TaskCompletionSource<object> TaskCompletionSource);

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
