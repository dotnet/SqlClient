// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Microsoft.Data.SqlClient.Utilities;
using Moq;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient.Utilities
{
    public class AsyncHelperTest
    {
        // This timeout is set fairly high. The tests are expected to complete quickly, but are
        // dependent on congestion of the thread pool. If the thread pool is congested, like on a
        // full CI run, short timeouts may elapse even if the code under test would behave as
        // expected. As such, we set a long timeout to ride out reasonable congestion on the
        // thread pool, but still trigger a failure if the code under test hangs.
        // @TODO: If suite-level timeouts are added, these timeouts can likely be removed.
        private static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);

        #region ContinueTask

        /// <summary>
        /// Verifies that the asynchronous continuation process is correctly handled when the task
        /// completes successfully. Ensures that the success handler is invoked exactly once, and
        /// neither failure nor cancellation handlers are invoked.
        /// </summary>
        [Fact]
        public async Task ContinueTask_TaskCompletes()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnCancellation = new();

            // Note: We have to set up mockOnSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action())
                .Callback(() => taskCompletionSource.SetResult(0));

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue: taskToContinue,
                taskCompletionSource: taskCompletionSource,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            mockOnSuccess.Verify(action => action(), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }


        /// <summary>
        /// Validates that the continuation process correctly handles cases when the success handler
        /// throws an exception. Ensures the task is marked as faulted and the failure and
        /// cancellation handlers are not invoked.
        /// </summary>
        [Fact]
        public async Task ContinueTask_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();

            // - mockOnSuccess handler throws
            Mock<Action> mockOnSuccess = new();
            mockOnSuccess.SetupThrows<Exception>();

            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            mockOnSuccess.Verify(action => action(), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }


        /// <summary>
        /// Verifies that the asynchronous continuation process is correctly handled when the task
        /// is cancelled. Ensures the cancellation handler is invoked exactly once, while neither
        /// the success nor failure handlers are invoked. Validates that the resulting task is
        /// marked as cancelled.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Indicates whether the cancellation handler should throw an exception during its
        /// execution to validate that cancellation is properly reported, regardless of handler
        /// failures.
        /// </param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTask_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that is cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();

            Mock<Action> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<Exception>();
            }

            Mock<Action> mockOnSuccess = new();
            Mock<Action<Exception>> mockOnFailure = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of mockOnCancellation throwing
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.Verify(action => action(), Times.Once);
        }

        /// <summary>
        /// Verifies the behavior of the asynchronous continuation process when the task to continue
        /// is cancelled, and no specific cancellation handler is provided. Ensures that the task is
        /// marked as cancelled without invoking the success or failure handlers.
        /// </summary>
        [Fact]
        public async Task ContinueTask_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue that is cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            Mock<Action> mockOnSuccess = new();
            Mock<Action<Exception>> mockOnFailure = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                onCancellation: null);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
        }

        /// <summary>
        /// Tests the behavior of the asynchronous continuation process when the initial task is
        /// faulted. Verifies that the fault handler is invoked exactly once and neither success nor
        /// cancellation handlers are called. Ensures that the continuation task transitions to a
        /// faulted state.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Indicates whether the fault handler itself is expected to throw an exception during
        /// execution.
        /// </param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTask_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that is faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();

            Mock<Action<Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<Exception, Exception>();
            }

            Mock<Action> mockOnSuccess = new();
            Mock<Action> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted, regardless of mockOnFailure throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that when a task to continue faults and no fault handler is provided, the
        /// proper behavior is executed. Ensures that the TaskCompletionSource transitions to a
        /// faulted state and neither success nor cancellation handlers are invoked. Validates that
        /// the absence of a fault handler does not disrupt the flow or produce unexpected side
        /// effects.
        /// </summary>
        [Fact]
        public async Task ContinueTask_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue that is faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            Mock<Action> mockOnSuccess = new();
            Mock<Action> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                mockOnSuccess.Object,
                onFailure: null,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that an unobserved exception is not created when continuing a cancelled task
        /// using the AsyncHelper. Ensures proper handling of task continuation in scenarios where
        /// cancellation occurs and the provided cancellation handler is utilized correctly.
        /// </summary>
        [Fact]
        public async Task ContinueTask_DoesNotCreateUnobservedException()
        {
            await VerifyDoesNotCreateUnobservedException(async onCancellation =>
            {
                TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();

                AsyncHelper.ContinueTask(
                    GetCancelledTask(),
                    taskCompletionSource,
                    onSuccess: static () => { },
                    onCancellation: onCancellation);
                await RunWithTimeout(taskCompletionSource.Task, RunTimeout);
            });
        }

        #endregion

        #region ContinueTaskWithState<T1>

        /// <summary>
        /// Verifies that a continuation process is correctly executed when the associated task
        /// completes successfully. Ensures that the success handler is invoked once, while failure
        /// and cancellation handlers are not invoked. Also verifies synchronization and proper
        /// state propagation between the task and continuation handlers.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCompletes()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();

            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Note: We have to set up mockOnSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action<object>> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action(state1))
                .Callback<object>(_ => taskCompletionSource.SetResult(0));

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue: taskToContinue,
                taskCompletionSource: taskCompletionSource,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            mockOnSuccess.Verify(action => action(state1), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Tests the behavior of the asynchronous continuation method when the task completes
        /// successfully but the success handler throws an exception. Verifies that the
        /// TaskCompletionSource is properly faulted, the success handler is invoked exactly once
        /// with the provided state, and neither the failure nor cancellation handlers are invoked.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();

            // - mockOnSuccess handler throws
            Mock<Action<object>> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action(It.IsAny<object>())).Throws<Exception>();

            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.Verify(action => action(state1), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies the behavior of the asynchronous continuation process when a task is cancelled
        /// and includes a state object. Ensures that the appropriate cancellation handler is
        /// invoked exactly once, while success and failure handlers are not. Also validates that
        /// the task completion source is correctly transitioned to the cancelled state, regardless
        /// of whether the cancellation handler throws an exception or not.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// A boolean value indicating whether the cancellation handler is expected to throw an
        /// exception.
        /// </param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_1Generic_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();

            Mock<Action<object>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<object, Exception>();
            }

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object, Exception>> mockOnFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of mockOnCancellation throwing
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.Verify(action => action(state1), Times.Once);
        }

        /// <summary>
        /// Ensures that when a task with an attached state is cancelled, no success, failure, or
        /// cancellation handlers are invoked. Verifies that the associated TaskCompletionSource is
        /// properly cancelled to reflect the task's state. This test method checks behavior
        /// specifically when no cancellation handler is provided.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object, Exception>> mockOnFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                onCancellation: null);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that when a task with a generic state parameter faults, the fault handler is
        /// invoked correctly. Validates that the state is passed to the handler and that other
        /// handlers, such as success and cancellation handlers, are not triggered. Additionally,
        /// ensures the final task transitions to a faulted state as expected.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// A boolean flag indicating whether the fault handler should throw an exception when
        /// invoked.
        /// </param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_1Generic_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();

            Mock<Action<object, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<object, Exception, Exception>();
            }

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted, regardless of mockOnFailure throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(state1, It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Ensures a faulted task is handled properly when no failure handler is provided.
        /// Validates that the task's fault state is correctly passed to the continuation process,
        /// and no unintended success or cancellation handlers are invoked. This test verifies that
        /// the task completion source reflects the faulted status as expected, promoting reliable
        /// behavior in fault scenarios.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                mockOnSuccess.Object,
                onFailure: null,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Confirms that the generic task continuation mechanism, with state, does not result in
        /// any unobserved exceptions. Verifies that the continuation invocation handles all
        /// outcomes, including cancellation, without allowing exceptions to remain unobserved or
        /// propagate outside the test scope.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_1Generic_DoesNotCreateUnobservedException()
        {
            await VerifyDoesNotCreateUnobservedException(async onCancellation =>
            {
                TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();

                AsyncHelper.ContinueTaskWithState(
                    GetCancelledTask(),
                    taskCompletionSource,
                    new object(),
                    onSuccess: static _ => { },
                    onCancellation: _ => onCancellation());
                await RunWithTimeout(taskCompletionSource.Task, RunTimeout);
            });
        }

        #endregion

        #region ContinueTaskWithState<T1, T2>

        /// <summary>
        /// Validates that the continuation of a task, which completed successfully, is handled
        /// appropriately when state information and generic types are involved. Ensures that the
        /// success delegate is triggered exactly once while both failure and cancellation handlers
        /// are never invoked.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCompletes()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Note: We have to set up mockOnSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action<object, object>> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action(state1, state2))
                .Callback<object, object>((_, _) => taskCompletionSource.SetResult(0));

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue: taskToContinue,
                taskCompletionSource: taskCompletionSource,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            mockOnSuccess.Verify(action => action(state1, state2), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies the behavior of the `ContinueTaskWithState` method when a task completes
        /// successfully and the success handler throws an exception. Ensures the faulted status of
        /// the task completion source and that the success handler is invoked exactly once with the
        /// correct state objects. Also confirms that neither the failure handler nor the
        /// cancellation handler are triggered.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();
            object state2 = new object();

            // - mockOnSuccess handler throws
            Mock<Action<object, object>> mockOnSuccess = new();
            mockOnSuccess.Setup(o => o(It.IsAny<object>(), It.IsAny<object>())).Throws<Exception>();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            // - mockOnSuccess was called with state obj
            mockOnSuccess.Verify(action => action(state1, state2), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies the behavior of the ContinueTaskWithState method when the task to continue is
        /// canceled. Ensures that the cancellation handler is invoked exactly once with the
        /// specified state parameters, while both success and failure handlers are not called.
        /// Confirms that the task completion source reflects the canceled state properly,
        /// regardless of whether the cancellation handler throws an exception.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Indicates whether the cancellation handler should throw an exception during execution.
        /// </param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_2Generics_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<object, object, Exception>();
            }

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object, Exception>> mockOnFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of mockOnCancellation throwing
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.Verify(action => action(state1, state2), Times.Once);
        }

        /// <summary>
        /// Verifies that when a task to continue with two state objects is cancelled and no
        /// cancellation handler is provided, the associated TaskCompletionSource transitions to a
        /// cancelled state. Ensures that neither the success handler nor the failure handler is
        /// invoked.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object, Exception>> mockOnFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                onCancellation: null);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
        }

        /// <summary>
        /// Validates that when a faulted task continues in the presence of two state objects, the
        /// failure handler is invoked exactly once for the provided state objects and exception.
        /// Ensures no other handlers, such as the success or cancellation handlers, are called, and
        /// the task completion source transitions to a faulted state as expected.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// A boolean indicating whether the failure handler should throw an exception when invoked.
        /// If true, the failure handler will throw; otherwise, it will complete normally.
        /// </param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_2Generics_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<object, object, Exception, Exception>();
            }

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted, regardless of mockOnFailure throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(state1, state2, It.IsAny<Exception>()), Times.Once);
        }

        /// <summary>
        /// Verifies that when a task to continue faults and no failure handler is provided, the
        /// resulting task faults as expected. Ensures that the success and cancellation handlers
        /// are never invoked, and the task continuation completes with a faulted status.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                mockOnSuccess.Object,
                onFailure: null,
                mockOnCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, RunTimeout);

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Ensures that a task with state and two generic parameters, when cancelled or failed,
        /// does not create an unobserved exception. Verifies proper handling of unobserved task
        /// exceptions through registration of cancellation or failure handlers in the continuation
        /// process.
        /// </summary>
        [Fact]
        public async Task ContinueTaskWithState_2Generics_DoesNotCreateUnobservedException()
        {
            await VerifyDoesNotCreateUnobservedException(async onCancellation =>
            {
                TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();

                AsyncHelper.ContinueTaskWithState(
                    GetCancelledTask(),
                    taskCompletionSource,
                    new object(),
                    new object(),
                    onSuccess: static (_, _) => { },
                    onCancellation: (_, _) => onCancellation());
                await RunWithTimeout(taskCompletionSource.Task, RunTimeout);
            });
        }

        #endregion

        #region CreateContinuationTask

        /// <summary>
        /// Tests the behavior of the CreateContinuationTask method when the provided task to
        /// continue is null. Ensures that the method returns null and invokes the onSuccess action
        /// exactly once. Verifies that neither the onFailure nor the onCancellation actions are
        /// called.
        /// </summary>
        [Fact]
        public void CreateContinuationTask_NullTask()
        {
            // Arrange
            Mock<Action> mockOnSuccess = new();
            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue: null,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);

            // Assert
            Assert.Null(continuationTask);

            mockOnSuccess.Verify(action => action(), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Validates that a continuation task is successfully created and completed when the
        /// initial task finishes execution without any exceptions. Ensures the success handler is
        /// invoked exactly once, while neither the failure handler nor the cancellation handler are
        /// triggered during the process.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTask_TaskCompletes()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            Mock<Action> mockOnSuccess = new();
            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, continuationTask.Status);

            mockOnSuccess.Verify(action => action(), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Ensures that a continuation task invokes the appropriate handlers when the success
        /// handler throws after the primary task completes successfully. Verifies that the
        /// continuation task transitions to a faulted state and only the success handler is invoked
        /// once, while the failure and cancellation handlers are never called.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTask_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnCancellation = new();

            // - mockOnSuccess handler throws
            Mock<Action> mockOnSuccess = new();
            mockOnSuccess.SetupThrows<Exception>();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.Verify(action => action(), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Tests that a continuation task correctly handles a cancellation of the original task.
        /// Verifies that the cancellation handler is invoked exactly once while ensuring that
        /// neither the success handler nor the failure handler is called. If the cancellation
        /// handler is configured to throw, this behavior is also validated.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Indicates whether the cancellation handler is expected to throw an exception during
        /// execution.
        /// </param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTask_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnSuccess = new();

            Mock<Action> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<Exception>();
            }

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.Verify(action => action(), Times.Once);
        }

        /// <summary>
        /// Verifies that a continuation task created with a null cancellation handler properly
        /// handles task cancellation. Ensures that no success or failure handlers are invoked and
        /// the resulting task is marked as canceled.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTask_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            Mock<Action<Exception>> mockOnFailure = new();
            Mock<Action> mockOnSuccess = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                onCancellation: null);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
        }

        /// <summary>
        /// Ensures that a continuation task is correctly created and handled when the original task
        /// faults. Validates that the failure handler is invoked exactly once, while success and
        /// cancellation handlers are never invoked.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Indicates whether the failure handler should throw an exception when invoked, testing
        /// edge cases in handling throwing failures.
        /// </param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTask_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            Mock<Action> mockOnSuccess = new();
            Mock<Action> mockOnCancellation = new();

            Mock<Action<Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<Exception, Exception>();
            }

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that when a task faults and no failure or cancellation handlers are provided,
        /// the continuation task is created in a faulted state. Ensures that success and
        /// cancellation handlers are never invoked in this scenario.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTask_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.FromException(new Exception());
            Mock<Action> mockOnSuccess = new();
            Mock<Action> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                mockOnSuccess.Object,
                onFailure: null,
                onCancellation: null);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Ensures that creating a continuation task does not result in an unobserved exception
        /// being thrown, even when the preceding task is canceled. Verifies that the cancellation
        /// handler, when provided, properly handles this scenario, avoiding unobserved exceptions
        /// in all cases.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTask_DoesNotCreateUnobservedException()
        {
            await VerifyDoesNotCreateUnobservedException(async onCancellation =>
            {
                Task? continuationTask = AsyncHelper.CreateContinuationTask(
                    GetCancelledTask(),
                    onSuccess: static () => { },
                    onCancellation: onCancellation);
                await RunWithTimeout(continuationTask, RunTimeout);
            });
        }

        #endregion

        #region CreateContinuationTaskWithState<T1>

        /// <summary>
        /// Validates the behavior of the CreateContinuationTaskWithState method when provided with
        /// a null task. Ensures that the continuation task is not created, the success handler is
        /// invoked exactly once with the given state, and neither the failure nor the cancellation
        /// handlers are invoked.
        /// </summary>
        [Fact]
        public void CreateContinuationTaskWithState_1Generic_NullTask()
        {
            // Arrange
            object state1 = new object();
            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue: null,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);

            // Assert
            Assert.Null(continuationTask);

            mockOnSuccess.Verify(action => action(state1), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that the continuation task is correctly created and executed when the initial
        /// task completes successfully. Ensures that the success handler is triggered exactly once,
        /// while failure and cancellation handlers are not invoked, confirming their appropriate
        /// exclusion in successful completion scenarios.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCompletes()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            object state1 = new object();

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, continuationTask.Status);
            mockOnSuccess.Verify(action => action(state1), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that a continuation task is correctly created with state when the preceding
        /// task completes successfully but the success handler throws an exception. Ensures the
        /// continuation task transitions to a faulted state, triggering the exception. Confirms
        /// that the success handler is invoked exactly once, while neither the failure handler nor
        /// the cancellation handler are invoked.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            object state1 = new object();

            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnCancellation = new();

            // - mockOnSuccess handler throws
            Mock<Action<object>> mockOnSuccess = new();
            mockOnSuccess.SetupThrows<object, Exception>();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.Verify(action => action(state1), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Handles the creation of a continuation task with state when the task to continue is
        /// canceled. Ensures the cancellation handler is invoked successfully, while success and
        /// failure handlers remain uninvoked. Validates task status and the correct execution of
        /// the cancellation handler.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Determines whether the cancellation handler should throw an exception during execution.
        /// </param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            object state1 = new object();

            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnSuccess = new();

            Mock<Action<object>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<object, Exception>();
            }

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.Verify(action => action(state1), Times.Once);
        }

        /// <summary>
        /// Validates that when a task is cancelled and no cancellation handler is provided, the
        /// continuation task transitions to the cancelled state. Ensures that no success or failure
        /// handlers are invoked in this scenario.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            object state1 = new object();

            Mock<Action<object, Exception>> mockOnFailure = new();
            Mock<Action<object>> mockOnSuccess = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                onCancellation: null);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
        }

        /// <summary>
        /// Tests the behavior of the CreateContinuationTaskWithState method when the task to
        /// continue has faulted. Ensures that the failure handler is invoked exactly once with the
        /// correct state and exception, while other handlers (success and cancellation) are not
        /// invoked. Validates that the resulting continuation task ends with a faulted status.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// Indicates if the failure handler should throw an exception during execution. This is
        /// used for testing resilience to handler exceptions. True to simulate a throwing handler;
        /// otherwise, false.
        /// </param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_1Generic_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            object state1 = new object();

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object>> mockOnCancellation = new();

            Mock<Action<object, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<object, Exception, Exception>();
            }

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(state1, It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Validates that a continuation task is correctly created when the preceding task faults
        /// and no failure handler is provided. Ensures that the continuation task reflects the
        /// faulted status of the original task, and that neither the success nor cancellation
        /// handlers are invoked in this scenario.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            object state1 = new object();

            Mock<Action<object>> mockOnSuccess = new();
            Mock<Action<object>> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                mockOnSuccess.Object,
                onFailure: null,
                onCancellation: null);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Confirms that calling CreateContinuationTaskWithState with a generic state does not
        /// result in an unobserved exception, even when the original task is canceled. Verifies
        /// that the cancellation handler, if provided, correctly executes and that other handlers
        /// are not triggered unexpectedly.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_DoesNotCreateUnobservedException()
        {
            await VerifyDoesNotCreateUnobservedException(async onCancellation =>
            {
                Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                    GetCancelledTask(),
                    new object(),
                    onSuccess: static _ => { },
                    onCancellation: _ => onCancellation());
                await RunWithTimeout(continuationTask, RunTimeout);
            });
        }

        #endregion

        #region CreateContinuationTaskWithState<T1, T2>

        /// <summary>
        /// Tests the behavior of <see cref="AsyncHelper.CreateContinuationTaskWithState{TState1,
        /// TState2}"/> when the input task is null. Validates that the method correctly returns
        /// null and that the success handler is invoked with the provided state objects. Ensures
        /// neither the failure handler nor the cancellation handler is invoked.
        /// </summary>
        [Fact]
        public void CreateContinuationTaskWithState_2Generics_NullTask()
        {
            // Arrange
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue: null,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);

            // Assert
            Assert.Null(continuationTask);

            mockOnSuccess.Verify(action => action(state1, state2), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Tests that the asynchronous continuation task correctly executes when the original task
        /// completes successfully. Verifies that the success handler is triggered exactly once with
        /// the provided state parameters, while the failure and cancellation handlers are not
        /// invoked.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCompletes()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, continuationTask.Status);
            mockOnSuccess.Verify(action => action(state1, state2), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Validates that a continuation task created with two states invokes the success handler
        /// once when the original task completes successfully, even if the success handler throws
        /// an exception. Ensures that no failure or cancellation handlers are invoked and that the
        /// continuation task ends in a faulted state as a result of the thrown exception.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // - mockOnSuccess handler throws
            Mock<Action<object, object>> mockOnSuccess = new();
            mockOnSuccess.SetupThrows<object, object, Exception>();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.Verify(action => action(state1, state2), Times.Once);
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that the continuation task is correctly created and executed when the initial
        /// task is canceled. Ensures that the cancellation handler is invoked exactly once, while
        /// success and failure handlers are never called.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// A boolean parameter indicating whether the cancellation handler should throw an
        /// exception when invoked.
        /// </param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnSuccess = new();

            Mock<Action<object, object>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<object, object, Exception>();
            }

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
            mockOnCancellation.Verify(action => action(state1, state2), Times.Once);
        }

        /// <summary>
        /// Validates that a continuation task created with two generic state parameters cancels
        /// without invoking any success, failure, or cancellation handlers when the task being
        /// continued was already cancelled. Ensures that the continuation task is in a cancelled
        /// state and that none of the handlers are executed.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            Mock<Action<object, object>> mockOnSuccess = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                onCancellation: null);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.VerifyNeverCalled();
        }

        /// <summary>
        /// Verifies that a continuation task created with two generic state parameters correctly
        /// handles the scenario where the antecedent task faults. Ensures that the failure handler
        /// is invoked exactly once with the provided state and exception, while neither the success
        /// nor cancellation handlers are invoked.
        /// </summary>
        /// <param name="handlerShouldThrow">
        /// A flag indicating whether the failure handler should throw an exception when invoked.
        /// This parameter is used to test the behavior of the continuation task in the presence of
        /// handler failures.
        /// </param>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_2Generics_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            Mock<Action<object, object, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<object, object, Exception, Exception>();
            }

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                mockOnSuccess.Object,
                mockOnFailure.Object,
                mockOnCancellation.Object);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(state1, state2, It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Validates that a continuation task created for a faulted initial task does not invoke
        /// the success or cancellation handlers. Ensures the resulting task properly transitions to
        /// the Faulted state. The test confirms that the absence of a failure handler does not
        /// create unintended behavior or exceptions.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            object state1 = new object();
            object state2 = new object();

            Mock<Action<object, object>> mockOnSuccess = new();
            Mock<Action<object, object>> mockOnCancellation = new();

            // Act
            Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                mockOnSuccess.Object,
                onFailure: null,
                onCancellation: null);
            await RunWithTimeout(continuationTask, RunTimeout);

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        /// <summary>
        /// Validates that creating a continuation task with two generic parameters does not result
        /// in an unobserved exception. Ensures that the task handles cancellation scenarios without
        /// leaking exceptions into the unobserved exception handler.
        /// </summary>
        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_DoesNotCreateUnobservedException()
        {
            await VerifyDoesNotCreateUnobservedException(async onCancellation =>
            {
                Task? continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                    GetCancelledTask(),
                    new object(),
                    new object(),
                    onSuccess: static (_, _) => { },
                    onCancellation: (_, _) => onCancellation());
                await RunWithTimeout(continuationTask, RunTimeout);
            });
        }

        #endregion

        #region WaitForCompletion

        /// <summary>
        /// Verifies that the WaitForCompletion method does not create unobserved exceptions when a
        /// faulted task is garbage collected. Ensures that exceptions occurring after a task times
        /// out are properly handled and do not propagate as unobserved task exceptions.
        /// </summary>
        [Fact]
        public void WaitForCompletion_DoesNotCreateUnobservedException()
        {
            // Arrange
            // - Create a handler to capture any unhandled exception
            Exception? unhandledException = null;
            EventHandler<UnobservedTaskExceptionEventArgs> handleUnobservedException =
                (_, args) => unhandledException = args.Exception;

            // @TODO: Can we do this with a custom scheduler to avoid changing global state?
            TaskScheduler.UnobservedTaskException += handleUnobservedException;

            try
            {
                // Act
                // - Run task that will always time out
                TaskCompletionSource<object?>? tcs = new();
                AsyncHelper.WaitForCompletion(
                    tcs.Task,
                    timeoutInSeconds: 1,
                    onTimeout: null,
                    rethrowExceptions: true);

                // - Task has timed out, simulate faulting task completion source
                tcs.SetException(new Exception("late failure"));

                // - Drop the last strong reference so the faulted Task can be finalized
                tcs = null;

                // - Force collection of unobserved task
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                // Assert
                // - Make sure no unobserved tasks happened
                Assert.Null(unhandledException);
            }
            finally
            {
                // Cleanup
                // - Remove the unobserved task handler
                TaskScheduler.UnobservedTaskException -= handleUnobservedException;
            }
        }

        #endregion

        private static Task GetCancelledTask()
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            return Task.FromCanceled(cts.Token);
        }

        private static TaskCompletionSource<object?> GetTaskCompletionSource()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);

        private static async Task VerifyDoesNotCreateUnobservedException(Func<Action, Task> runContinuation)
        {
            Exception? unhandledException = null;
            EventHandler<UnobservedTaskExceptionEventArgs> handleUnobservedException =
                (_, args) =>
                {
                    unhandledException = args.Exception;
                    args.SetObserved();
                };

            // @TODO: Can we do this with a custom scheduler to avoid changing global state?
            TaskScheduler.UnobservedTaskException += handleUnobservedException;

            try
            {
                await runContinuation(static () => throw new Exception("callback failure"));

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Assert.Null(unhandledException);
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handleUnobservedException;
            }
        }

        private static async Task RunWithTimeout([NotNull] Task? taskToRun, TimeSpan timeout)
        {
            if (taskToRun is null)
            {
                Assert.Fail("Expected non-null task for timeout");
            }

            Task winner = await Task.WhenAny(taskToRun, Task.Delay(timeout));
            if (winner != taskToRun)
            {
                Assert.Fail("Timeout elapsed.");
            }

            // Force observation of any exception
            _ = taskToRun.Exception;
        }
    }
}
