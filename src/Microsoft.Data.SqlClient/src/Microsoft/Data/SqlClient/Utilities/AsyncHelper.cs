// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Microsoft.Data.SqlClient.Utilities
{
    /// <summary>
    /// Provides helpers for interacting with asynchronous tasks.
    /// </summary>
    /// <remarks>
    /// These helpers mainly provide continuation and timeout functionality. They utilize
    /// <see cref="TaskCompletionSource{TResult}"/> at their core, and as such are fairly antiquated
    /// implementations. If possible these methods should be utilized less and async/await native
    /// constructs should be used.
    /// </remarks>
    internal static class AsyncHelper
    {
        /// <summary>
        /// Continues a task and signals failure of the continuation via the provided
        /// <paramref name="taskCompletionSource"/>.
        /// </summary>
        /// <remarks>
        /// If the <paramref name="taskToContinue"/> completes in the following states, these
        /// actions will be taken:
        /// * With exception
        ///   * <paramref name="onFailure"/> is called (if provided)
        ///   * Will try to set exception on <paramref name="taskCompletionSource"/>
        /// * Cancelled
        ///   * <paramref name="onCancellation"/> is called (if provided)
        ///   * Will try to set cancelled on <paramref name="taskCompletionSource"/>
        /// * Successfully
        ///   * <paramref name="onSuccess"/> is called
        ///   * IF an exception is thrown during execution of <paramref name="onSuccess"/>, the
        ///     helper will try to set an exception on the <paramref name="taskCompletionSource"/>.
        ///   * <paramref name="taskCompletionSource"/> is *not* with result on success. This
        ///     is to allow the task completion source to be continued even more after this current
        ///     continuation.
        /// </remarks>
        /// <param name="taskToContinue">Task to continue with provided callbacks</param>
        /// <param name="taskCompletionSource">
        /// Completion source used to track completion of the continuation, see remarks for details
        /// </param>
        /// <param name="onSuccess">Callback to execute on successful completion of the task</param>
        /// <param name="onFailure">Callback to execute on failure of the task (optional)</param>
        /// <param name="onCancellation">Callback to execute on cancellation of the task (optional)</param>
        internal static void ContinueTask(
            Task taskToContinue,
            TaskCompletionSource<object?> taskCompletionSource,
            Action onSuccess,
            Action<Exception>? onFailure = null,
            Action? onCancellation = null)
        {
            ContinuationState continuationState = new ContinuationState(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                TaskCompletionSource: taskCompletionSource);

            Task continuationTask = taskToContinue.ContinueWith(
                static (tsk, continuationState2) =>
                {
                    ContinuationState typedState = (ContinuationState)continuationState2!;

                    if (tsk.Exception != null)
                    {
                        Exception innerException = tsk.Exception.InnerException ?? tsk.Exception;
                        try
                        {
                            typedState.OnFailure?.Invoke(innerException);
                        }
                        finally
                        {
                            typedState.TaskCompletionSource.TrySetException(innerException);
                        }
                    }
                    else if (tsk.IsCanceled)
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
                        }
                        // @TODO: CER Exception Handling was removed here (see GH#3581)
                        catch (Exception e)
                        {
                            typedState.TaskCompletionSource.TrySetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);
        }

        /// <summary>
        /// Continues a task and signals failure of the continuation via the provided
        /// <paramref name="taskCompletionSource"/>. This overload provides a single state object
        /// to the callbacks.
        /// </summary>
        /// <remarks>
        /// When possible, use static lambdas for callbacks.
        ///
        /// If the <paramref name="taskToContinue"/> completes in the following states, these
        /// actions will be taken:
        /// * With exception
        ///   * <paramref name="onFailure"/> is called (if provided)
        ///   * Will try to set exception on <paramref name="taskCompletionSource"/>
        /// * Cancelled
        ///   * <paramref name="onCancellation"/> is called (if provided)
        ///   * Will try to set cancelled on <paramref name="taskCompletionSource"/>
        /// * Successfully
        ///   * <paramref name="onSuccess"/> is called
        ///   * IF an exception is thrown during execution of <paramref name="onSuccess"/>, the
        ///     helper will try to set an exception on the <paramref name="taskCompletionSource"/>.
        ///   * <paramref name="taskCompletionSource"/> is *not* with result on success. This
        ///     is to allow the task completion source to be continued even more after this current
        ///     continuation.
        /// </remarks>
        /// <typeparam name="TState">Type of the state object to provide to the callbacks</typeparam>
        /// <param name="taskToContinue">Task to continue with provided callbacks</param>
        /// <param name="taskCompletionSource">
        /// Completion source used to track completion of the continuation, see remarks for details
        /// </param>
        /// <param name="state">State object to provide to callbacks</param>
        /// <param name="onSuccess">Callback to execute on successful completion of the task</param>
        /// <param name="onFailure">Callback to execute on failure of the task (optional)</param>
        /// <param name="onCancellation">Callback to execute on cancellation of the task (optional)</param>
        internal static void ContinueTaskWithState<TState>(
            Task taskToContinue,
            TaskCompletionSource<object?> taskCompletionSource,
            TState state,
            Action<TState> onSuccess,
            Action<TState, Exception>? onFailure = null,
            Action<TState>? onCancellation = null)
        {
            ContinuationState<TState> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State: state,
                TaskCompletionSource: taskCompletionSource);

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, continuationState2) =>
                {
                    ContinuationState<TState> typedState2 = (ContinuationState<TState>)continuationState2!;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException ?? task.Exception;
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
                            typedState2.TaskCompletionSource.TrySetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);
        }

        /// <summary>
        /// Continues a task and signals failure of the continuation via the provided
        /// <paramref name="taskCompletionSource"/>. This overload provides two state objects to
        /// the callbacks.
        /// </summary>
        /// <remarks>
        /// When possible, use static lambdas for callbacks.
        ///
        /// If the <paramref name="taskToContinue"/> completes in the following states, these
        /// actions will be taken:
        /// * With exception
        ///   * <paramref name="onFailure"/> is called (if provided)
        ///   * Will try to set exception on <paramref name="taskCompletionSource"/>
        /// * Cancelled
        ///   * <paramref name="onCancellation"/> is called (if provided)
        ///   * Will try to set cancelled on <paramref name="taskCompletionSource"/>
        /// * Successfully
        ///   * <paramref name="onSuccess"/> is called
        ///   * IF an exception is thrown during execution of <paramref name="onSuccess"/>, the
        ///     helper will try to set an exception on the <paramref name="taskCompletionSource"/>.
        ///   * <paramref name="taskCompletionSource"/> is *not* with result on success. This
        ///     is to allow the task completion source to be continued even more after this
        ///     current continuation.
        /// </remarks>
        /// <param name="taskToContinue">Task to continue with provided callbacks</param>
        /// <param name="taskCompletionSource">
        /// Completion source used to track completion of the continuation, see remarks for details
        /// </param>
        /// <typeparam name="TState1">Type of the first state object to provide to callbacks</typeparam>
        /// <typeparam name="TState2">Type of the second state object to provide to callbacks</typeparam>
        /// <param name="state1">First state object to provide to callbacks</param>
        /// <param name="state2">Second state object to provide to callbacks</param>
        /// <param name="onSuccess">Callback to execute on successful completion of the task</param>
        /// <param name="onFailure">Callback to execute on failure of the task (optional)</param>
        /// <param name="onCancellation">Callback to execute on cancellation of the task (optional)</param>
        internal static void ContinueTaskWithState<TState1, TState2>(
            Task taskToContinue,
            TaskCompletionSource<object?> taskCompletionSource,
            TState1 state1,
            TState2 state2,
            Action<TState1, TState2> onSuccess,
            Action<TState1, TState2, Exception>? onFailure = null,
            Action<TState1, TState2>? onCancellation = null)
        {
            ContinuationState<TState1, TState2> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State1: state1,
                State2: state2,
                TaskCompletionSource: taskCompletionSource);

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, continuationState2) =>
                {
                    ContinuationState<TState1, TState2> typedState2 =
                        (ContinuationState<TState1, TState2>)continuationState2!;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException ?? task.Exception;
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
                            typedState2.TaskCompletionSource.TrySetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);
        }

        /// <summary>
        /// Continues a task and returns the continuation task.
        /// </summary>
        /// <remarks>
        /// When possible, use static lambdas for callbacks.
        ///
        /// If the <paramref name="taskToContinue"/> completes in the following states, these
        /// actions will be taken:
        /// * With exception
        ///   * <paramref name="onFailure"/> is called (if provided)
        ///   * The task will be completed with an exception.
        /// * Cancelled
        ///   * <paramref name="onCancellation"/> is called (if provided)
        ///   * The task will be completed as cancelled.
        /// * Successfully
        ///   * <paramref name="onSuccess"/> is called
        ///   * IF an exception is thrown during execution of <paramref name="onSuccess"/>, the
        ///     task will be completed with the exception.
        ///   * The task will be completed as successful.
        /// </remarks>
        /// <param name="taskToContinue">
        /// Task to continue with provided callbacks, if <c>null</c>, <c>null</c> will be returned.
        /// </param>
        /// <param name="onSuccess">Callback to execute on successful completion of the task</param>
        /// <param name="onFailure">Callback to execute on failure of the task (optional)</param>
        /// <param name="onCancellation">Callback to execute on cancellation of the task (optional)</param>
        internal static Task? CreateContinuationTask(
            Task? taskToContinue,
            Action onSuccess,
            Action<Exception>? onFailure = null,
            Action? onCancellation = null)
        {
            if (taskToContinue is null)
            {
                // This is a remnant of ye olde async/sync code that return null tasks when
                // executing synchronously. It's still desirable that the onSuccess executes
                // regardless of whether the preceding action was synchronous or asynchronous.
                onSuccess();
                return null;
            }

            // @TODO: Can totally use a non-generic TaskCompletionSource
            TaskCompletionSource<object?> taskCompletionSource = new();
            ContinuationState continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                TaskCompletionSource: taskCompletionSource);

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, continuationState2) =>
                {
                    ContinuationState typedState = (ContinuationState)continuationState2!;
                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException ?? task.Exception;
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
                            typedState.TaskCompletionSource.TrySetResult(null);
                        }
                        catch (Exception e)
                        {
                            typedState.TaskCompletionSource.TrySetException(e);
                        }
                    }
                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Continues a task and returns the continuation task. This overload allows a state object
        /// to be passed into the callbacks.
        /// </summary>
        /// <remarks>
        /// When possible, use static lambdas for callbacks.
        ///
        /// If the <paramref name="taskToContinue"/> completes in the following states, these
        /// actions will be taken:
        /// * With exception
        ///   * <paramref name="onFailure"/> is called (if provided)
        ///   * The task will be completed with an exception.
        /// * Cancelled
        ///   * <paramref name="onCancellation"/> is called (if provided)
        ///   * The task will be completed as cancelled.
        /// * Successfully
        ///   * <paramref name="onSuccess"/> is called
        ///   * IF an exception is thrown during execution of <paramref name="onSuccess"/>, the
        ///     task will be completed with the exception.
        ///   * The task will be completed as successful.
        /// </remarks>
        /// <typeparam name="TState">Type of the state object to pass to callbacks</typeparam>
        /// <param name="taskToContinue">
        /// Task to continue with provided callbacks, if <c>null</c>, <c>null</c> will be returned.
        /// </param>
        /// <param name="state">State object to pass to the callbacks</param>
        /// <param name="onSuccess">Callback to execute on successful completion of the task</param>
        /// <param name="onFailure">Callback to execute on failure of the task (optional)</param>
        /// <param name="onCancellation">Callback to execute on cancellation of the task (optional)</param>
        internal static Task? CreateContinuationTaskWithState<TState>(
            Task? taskToContinue,
            TState state,
            Action<TState> onSuccess,
            Action<TState, Exception>? onFailure = null,
            Action<TState>? onCancellation = null)
        {
            if (taskToContinue is null)
            {
                // This is a remnant of ye olde async/sync code that return null tasks when
                // executing synchronously. It's still desirable that the onSuccess executes
                // regardless of whether the preceding action was synchronous or asynchronous.
                onSuccess(state);
                return null;
            }

            // @TODO: Can totally use a non-generic TaskCompletionSource
            TaskCompletionSource<object?> taskCompletionSource = new();
            ContinuationState<TState> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State: state,
                TaskCompletionSource: taskCompletionSource);

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, continuationState2) =>
                {
                    ContinuationState<TState> typedState2 = (ContinuationState<TState>)continuationState2!;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException ?? task.Exception;
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
                            typedState2.TaskCompletionSource.TrySetResult(null);
                        }
                        catch (Exception e)
                        {
                            typedState2.TaskCompletionSource.TrySetException(e);
                        }
                    }

                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Continues a task and returns the continuation task. This overload allows two state
        /// objects to be passed into the callbacks.
        /// </summary>
        /// <remarks>
        /// When possible, use static lambdas for callbacks.
        ///
        /// If the <paramref name="taskToContinue"/> completes in the following states, these
        /// actions will be taken:
        /// * With exception
        ///   * <paramref name="onFailure"/> is called (if provided)
        ///   * The task will be completed with an exception.
        /// * Cancelled
        ///   * <paramref name="onCancellation"/> is called (if provided)
        ///   * The task will be completed as cancelled.
        /// * Successfully
        ///   * <paramref name="onSuccess"/> is called
        ///   * IF an exception is thrown during execution of <paramref name="onSuccess"/>, the
        ///     task will be completed with the exception.
        ///   * The task will be completed as successful.
        /// </remarks>
        /// <typeparam name="TState1">Type of the first state object to pass to callbacks</typeparam>
        /// <typeparam name="TState2">Type of the second state object to pass to callbacks</typeparam>
        /// <param name="taskToContinue">
        /// Task to continue with provided callbacks, if <c>null</c>, <c>null</c> will be returned.
        /// </param>
        /// <param name="state1">First state object to pass to the callbacks</param>
        /// <param name="state2">Second state object to pass to the callbacks</param>
        /// <param name="onSuccess">Callback to execute on successful completion of the task</param>
        /// <param name="onFailure">Callback to execute on failure of the task (optional)</param>
        /// <param name="onCancellation">Callback to execute on cancellation of the task (optional)</param>
        internal static Task? CreateContinuationTaskWithState<TState1, TState2>(
            Task? taskToContinue,
            TState1 state1,
            TState2 state2,
            Action<TState1, TState2> onSuccess,
            Action<TState1, TState2, Exception>? onFailure = null,
            Action<TState1, TState2>? onCancellation = null)
        {
            if (taskToContinue is null)
            {
                // This is a remnant of ye olde async/sync code that return null tasks when
                // executing synchronously. It's still desirable that the onSuccess executes
                // regardless of whether the preceding action was synchronous or asynchronous.
                onSuccess(state1, state2);
                return null;
            }

            // @TODO: Can totally use a non-generic TaskCompletionSource
            TaskCompletionSource<object?> taskCompletionSource = new();
            ContinuationState<TState1, TState2> continuationState = new(
                OnCancellation: onCancellation,
                OnFailure: onFailure,
                OnSuccess: onSuccess,
                State1: state1,
                State2: state2,
                TaskCompletionSource: taskCompletionSource);

            Task continuationTask = taskToContinue.ContinueWith(
                static (task, continuationState2) =>
                {
                    ContinuationState<TState1, TState2> typedState2 =
                        (ContinuationState<TState1, TState2>)continuationState2!;

                    if (task.Exception is not null)
                    {
                        Exception innerException = task.Exception.InnerException ?? task.Exception;
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
                            typedState2.TaskCompletionSource.TrySetResult(null);
                        }
                        catch (Exception e)
                        {
                            typedState2.TaskCompletionSource.TrySetException(e);
                        }
                    }

                },
                state: continuationState,
                scheduler: TaskScheduler.Default);

            // Explicitly follow up by observing any exception thrown during continuation
            ObserveContinuationException(continuationTask);

            return taskCompletionSource.Task;
        }

        /// <summary>
        /// Executes a timeout task in parallel with the provided
        /// <paramref name="taskCompletionSource"/>. If the timeout completes before the task
        /// completion source, the provided <paramref name="onTimeout"/> is executed and the
        /// exception returned is set as the exception that completes the task completion source.
        /// </summary>
        /// <param name="taskCompletionSource">Task to execute with a timeout</param>
        /// <param name="timeoutInSeconds">Number of seconds to wait until timing out the task</param>
        /// <param name="onTimeout">
        /// Callback to execute when the task does not complete within the allotted time. The
        /// exception returned by the callback is set on the <paramref name="taskCompletionSource"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token to prematurely cancel timeout</param>
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

        /// <summary>
        /// Executes a timeout task in parallel with the provided
        /// <paramref name="taskCompletionSource"/>. If the timeout completes before the task
        /// completion source, the provided <paramref name="onTimeout"/> is executed and the
        /// exception returned is set as the exception that completes the task completion source.
        /// This overload provides a state object to the timeout callback.
        /// </summary>
        /// <param name="taskCompletionSource">Task to execute with a timeout</param>
        /// <param name="timeoutInSeconds">Number of seconds to wait until timing out the task</param>
        /// <param name="state">State object to pass to the callback</param>
        /// <param name="onTimeout">
        /// Callback to execute when the task does not complete within the allotted time. The
        /// exception returned by the callback is set on the <paramref name="taskCompletionSource"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token to prematurely cancel timeout</param>
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
                            taskCompletionSource.TrySetException(onTimeout((TState)state2!));
                        }
                    },
                    state: state,
                    cancellationToken: CancellationToken.None);
        }

        /// <summary>
        /// Waits for a maximum of <paramref name="timeoutInSeconds"/> seconds for completion of
        /// the provided <paramref name="task"/>.
        /// </summary>
        /// <param name="task">Task to execute with a timeout</param>
        /// <param name="timeoutInSeconds">Number of seconds to wait until timing out the task</param>
        /// <param name="onTimeout">
        /// Callback to execute when the task does not complete within the allotted time.
        /// </param>
        /// <param name="rethrowExceptions">
        /// If <c>true</c>, the inner exception of any <see cref="AggregateException"/> raised
        /// during execution, including timeout of the task, will be rethrown.
        /// </param>
        internal static void WaitForCompletion(
            Task task,
            int timeoutInSeconds,
            Action? onTimeout = null,
            bool rethrowExceptions = true)
        {
            try
            {
                TimeSpan timeout = timeoutInSeconds > 0
                    ? TimeSpan.FromSeconds(timeoutInSeconds)
                    : Timeout.InfiniteTimeSpan;
                task.Wait(timeout);
            }
            catch (AggregateException ae)
            {
                if (rethrowExceptions)
                {
                    Debug.Assert(ae.InnerException is not null, "Inner exception is null");
                    Debug.Assert(ae.InnerExceptions.Count == 1, "There is more than one exception in AggregateException");
                    ExceptionDispatchInfo.Capture(ae.InnerException!).Throw();
                }
            }

            if (!task.IsCompleted)
            {
                // Ensure the task does not leave an unobserved exception
                task.ContinueWith(static t => { _ = t.Exception; });
                onTimeout?.Invoke();
            }
        }

        /// <remarks>
        /// This method is intended to be used within the above helpers to ensure that any
        /// exceptions thrown during callbacks do not go unobserved. If these exceptions were
        /// to go unobserved, they will trigger events to be raised by the default task scheduler.
        /// Neither situation is ideal:
        /// * If an application assigns a listener to this event, it will generate events that
        ///   should be reported to us. But, because it happens outside the stack that caused the
        ///   exception, most of the context of the exception is lost. Furthermore, the event is
        ///   triggered when the GC runs, so the event happens asynchronous to the action that
        ///   caused it.
        /// * Adding this forced observation of the exception prevents applications from receiving
        ///   the event, effectively swallowing it.
        /// * However, if we log the exception when we observe it, we can still log that the
        ///   unobserved exception happened without causing undue disruption to the application
        ///   or leaking resources and causing overhead by raising the event.
        /// </remarks>
        private static void ObserveContinuationException(Task continuationTask)
        {
            continuationTask.ContinueWith(
                static task =>
                {
                    SqlClientEventSource.Log.TraceEvent($"Unobserved task exception: {task.Exception}");
                    return _ = task.Exception;
                },
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        }

        private record ContinuationState(
            Action? OnCancellation,
            Action<Exception>? OnFailure,
            Action OnSuccess,
            TaskCompletionSource<object?> TaskCompletionSource);

        private record ContinuationState<TState>(
            Action<TState>? OnCancellation,
            Action<TState, Exception>? OnFailure,
            Action<TState> OnSuccess,
            TState State,
            TaskCompletionSource<object?> TaskCompletionSource);

        private record ContinuationState<TState1, TState2>(
            Action<TState1, TState2>? OnCancellation,
            Action<TState1, TState2, Exception>? OnFailure,
            Action<TState1, TState2> OnSuccess,
            TState1 State1,
            TState2 State2,
            TaskCompletionSource<object?> TaskCompletionSource);
    }
}
