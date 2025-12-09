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
            // - taskCompletionSource should have been cancelled, regardless of mockOnSuccess throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task ContinueTask_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue that is cancelled
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
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        #endregion

        #region ContinueTaskWithState<T1>

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCompletes()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;

            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnCancellation = new();

            // Note: We have to set up mockOnSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action<int>> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action(state1))
                .Callback<int>(_ => taskCompletionSource.SetResult(0));

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

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;

            // - mockOnSuccess handler throws
            Mock<Action<int>> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action(It.IsAny<int>())).Throws<Exception>();

            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnCancellation = new();

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_1Generic_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;

            Mock<Action<int>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<int, Exception>();
            }

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int, Exception>> mockOnFailure = new();

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

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int, Exception>> mockOnFailure = new();

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_1Generic_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;

            Mock<Action<int, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<int, Exception, Exception>();
            }

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int>> mockOnCancellation = new();

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
            // - taskCompletionSource should have been cancelled, regardless of mockOnSuccess throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(state1, It.IsAny<Exception>()), Times.Once);
            mockOnCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int>> mockOnCancellation = new();

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
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        #endregion

        #region ContinueTaskWithState<T1, T2>

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCompletes()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnCancellation = new();

            // Note: We have to set up mockOnSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action<int, int>> mockOnSuccess = new();
            mockOnSuccess.Setup(action => action(state1, state2))
                .Callback<int, int>((_, _) => taskCompletionSource.SetResult(0));

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

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue that completed successfully
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;
            const int state2 = 234;

            // - mockOnSuccess handler throws
            Mock<Action<int, int>> mockOnSuccess = new();
            mockOnSuccess.Setup(o => o(It.IsAny<int>(), It.IsAny<int>())).Throws<Exception>();

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnCancellation = new();

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_2Generics_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<int, int, Exception>();
            }

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int, Exception>> mockOnFailure = new();

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

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue that was cancelled
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int, Exception>> mockOnFailure = new();

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

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_2Generics_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<int, int, Exception, Exception>();
            }

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int>> mockOnCancellation = new();

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
            // - taskCompletionSource should have been cancelled, regardless of mockOnSuccess throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
            mockOnFailure.Verify(action => action(state1, state2, It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue that faulted
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object?> taskCompletionSource = GetTaskCompletionSource();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int>> mockOnCancellation = new();

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
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            mockOnSuccess.VerifyNeverCalled();
            mockOnCancellation.VerifyNeverCalled();
        }

        #endregion

        #region CreateContinuationTask

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

        [Fact]
        public async Task CreateContinuationTask_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue completed successfully
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

        #endregion

        #region CreateContinuationTaskWithState<T1>

        [Fact]
        public void CreateContinuationTaskWithState_1Generic_NullTask()
        {
            // Arrange
            const int state1 = 123;
            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnCancellation = new();

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

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCompletes()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnCancellation = new();

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

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;

            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnCancellation = new();

            // - mockOnSuccess handler throws
            Mock<Action<int>> mockOnSuccess = new();
            mockOnSuccess.SetupThrows<int, Exception>();

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;

            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnSuccess = new();

            Mock<Action<int>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<int, Exception>();
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

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;

            Mock<Action<int, Exception>> mockOnFailure = new();
            Mock<Action<int>> mockOnSuccess = new();

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_1Generic_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int>> mockOnCancellation = new();

            Mock<Action<int, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<int, Exception, Exception>();
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

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;

            Mock<Action<int>> mockOnSuccess = new();
            Mock<Action<int>> mockOnCancellation = new();

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

        #endregion

        #region CreateContinuationTaskWithState<T1, T2>

        [Fact]
        public void CreateContinuationTaskWithState_2Generics_NullTask()
        {
            // Arrange
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnCancellation = new();

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

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCompletes()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnCancellation = new();

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

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCompletesHandlerThrows()
        {
            // Arrange
            // - Task to continue completed successfully
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnCancellation = new();

            // - mockOnSuccess handler throws
            Mock<Action<int, int>> mockOnSuccess = new();
            mockOnSuccess.SetupThrows<int, int, Exception>();

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnSuccess = new();

            Mock<Action<int, int>> mockOnCancellation = new();
            if (handlerShouldThrow)
            {
                mockOnCancellation.SetupThrows<int, int, Exception>();
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

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCancelsNoHandler()
        {
            // Arrange
            // - Task to continue was cancelled
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            Mock<Action<int, int>> mockOnSuccess = new();

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

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_2Generics_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int>> mockOnCancellation = new();

            Mock<Action<int, int, Exception>> mockOnFailure = new();
            if (handlerShouldThrow)
            {
                mockOnFailure.SetupThrows<int, int, Exception, Exception>();
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

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskFaultsNoHandler()
        {
            // Arrange
            // - Task to continue faulted
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> mockOnSuccess = new();
            Mock<Action<int, int>> mockOnCancellation = new();

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

        #endregion

        #region WaitForCompletion

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
                TaskCompletionSource<object?> tcs = new();
                AsyncHelper.WaitForCompletion(
                    tcs.Task,
                    timeoutInSeconds: 1,
                    onTimeout: null,
                    rethrowExceptions: true);

                // - Force collection of unobserved task
                GC.Collect();
                GC.WaitForPendingFinalizers();

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
