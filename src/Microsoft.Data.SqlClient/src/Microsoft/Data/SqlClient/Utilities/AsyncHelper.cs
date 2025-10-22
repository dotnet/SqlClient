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
        internal static void ContinueTask(
            Task taskToContinue,
            TaskCompletionSource<object> taskCompletionSource,
            Action onSuccess,
            Action<Exception> onFailure = null,
            Action onCancellation = null)
        {
            Task continuationTask = taskToContinue.ContinueWith(
                tsk =>
                {
                    if (tsk.Exception != null)
                    {
                        Exception innerException = tsk.Exception.InnerException;
                        try
                        {
                            onFailure?.Invoke(innerException);
                        }
                        finally
                        {
                            taskCompletionSource.TrySetException(innerException);
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
                            taskCompletionSource.TrySetCanceled();
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
                            taskCompletionSource.SetException(e);
                        }
                    }
                },
                TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);
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

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState> typedState2 =
                        (ContinuationState<TState>)state2;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException;
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State, innerException);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(innerException);
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

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);
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

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState1, TState2> typedState2 = (ContinuationState<TState1, TState2>)state2;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException;
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State1, typedState2.State2, innerException);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(innerException);
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

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);
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

            Task continuationTask = taskToContinue.ContinueWith(static (task, continuationState2) =>
                {
                    ContinuationState typedState = (ContinuationState)continuationState2;
                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException;
                        try
                        {
                            typedState.OnFailure?.Invoke(innerException);
                        }
                        finally
                        {
                            typedState.TaskCompletionSource.TrySetException(innerException);
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

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);

            return taskCompletionSource.Task;
        }

        internal static Task CreateContinuationTaskWithState<TState>(
            Task taskToContinue,
            TState state,
            Action<TState> onSuccess,
            Action<TState, Exception> onFailure = null,
            Action<TState> onCancellation = null)
        {
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

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState> typedState2 = (ContinuationState<TState>)state2;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException;
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State, innerException);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(innerException);
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

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);

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

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, state2) =>
                {
                    ContinuationState<TState1, TState2> typedState2 = (ContinuationState<TState1, TState2>)state2;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException;
                        try
                        {
                            typedState2.OnFailure?.Invoke(typedState2.State1, typedState2.State2, innerException);
                        }
                        finally
                        {
                            typedState2.TaskCompletionSource.TrySetException(innerException);
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

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);

            return taskCompletionSource.Task;
        }

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
                    cancellationToken: CancellationToken.None);
        }

        internal static void SetTimeoutExceptionWithState<TState>(
            TaskCompletionSource<object> taskCompletionSource,
            int timeoutInSeconds,
            TState state,
            Func<TState, Exception> onTimeout,
            CancellationToken cancellationToken)
        {
            if (timeoutInSeconds <= 0)
            {
                return;
            }

            Task.Delay(TimeSpan.FromSeconds(timeoutInSeconds), cancellationToken)
                .ContinueWith(
                    (task, state2) =>
                    {
                        // If the timeout ran to completion AND the task to complete did not complete
                        // then the timeout expired first, run the timeout handler
                        if (!task.IsCanceled && !taskCompletionSource.Task.IsCompleted)
                        {
                            taskCompletionSource.TrySetException(onTimeout((TState)state2));
                        }
                    },
                    state: state,
                    cancellationToken: CancellationToken.None);
        }

        internal static void WaitForCompletion(
            Task task,
            int timeoutInSeconds,
            Action onTimeout = null,
            bool rethrowExceptions = true)
        {
            try
            {
                task.Wait(timeoutInSeconds > 0 ? 1000 * timeoutInSeconds : Timeout.Infinite);
            }
            catch (AggregateException ae)
            {
                if (rethrowExceptions)
                {
                    Debug.Assert(ae.InnerException is not null, "Inner exception is null");
                    Debug.Assert(ae.InnerExceptions.Count == 1, "There is more than one exception in AggregateException");
                    ExceptionDispatchInfo.Capture(ae.InnerException).Throw();
                }
            }

            if (!task.IsCompleted)
            {
                // Ensure the task does not leave an unobserved exception
                task.ContinueWith(static t => { _ = t.Exception; });
                onTimeout?.Invoke();
            }
        }

        private static void ObserveContinuationException(Task continuationTask)
        {
            continuationTask.ContinueWith(
                static task => _ = task.Exception,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
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
    }
}
