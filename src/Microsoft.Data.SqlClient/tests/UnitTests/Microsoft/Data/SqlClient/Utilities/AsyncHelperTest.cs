using System;
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
        #region ContinueTask

        [Fact]
        public async Task ContinueTask_TaskCompletes()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object> taskCompletionSource = new();
            Mock<Action<Exception>> onFailure = new();
            Mock<Action> onCancellation = new();

            // Note: We have to set up onSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action> onSuccess = new();
            onSuccess.Setup(action => action())
                .Callback(() => taskCompletionSource.SetResult(0));

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue: taskToContinue,
                taskCompletionSource: taskCompletionSource,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            onSuccess.Verify(action => action(), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task ContinueTask_TaskCompletesHandlerThrows()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object> taskCompletionSource = new();

            Mock<Action> onSuccess = new();
            onSuccess.SetupThrows<Exception>();

            Mock<Action<Exception>> onFailure = new();
            Mock<Action> onCancellation = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            onSuccess.Verify(action => action(), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTask_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object> taskCompletionSource = new();

            Mock<Action> onCancellation = new();
            if (handlerShouldThrow)
            {
                onCancellation.SetupThrows<Exception>();
            }

            Mock<Action> onSuccess = new();
            Mock<Action<Exception>> onFailure = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of onCancellation throwing
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
            onCancellation.Verify(action => action(), Times.Once);
        }

        [Fact]
        public async Task ContinueTask_TaskCancelsNoHandler()
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object> taskCompletionSource = new();
            Mock<Action> onSuccess = new();
            Mock<Action<Exception>> onFailure = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                onSuccess.Object,
                onFailure.Object,
                onCancellation: null);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTask_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object> taskCompletionSource = new();

            Mock<Action<Exception>> onFailure = new();
            if (handlerShouldThrow)
            {
                onFailure.SetupThrows<Exception, Exception>();
            }

            Mock<Action> onSuccess = new();
            Mock<Action> onCancellation = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of onSuccess throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
            onFailure.Verify(action => action(It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task ContinueTask_TaskFaultsNoHandler()
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object> taskCompletionSource = new();
            Mock<Action> onSuccess = new();
            Mock<Action> onCancellation = new();

            // Act
            AsyncHelper.ContinueTask(
                taskToContinue,
                taskCompletionSource,
                onSuccess.Object,
                onFailure: null,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        #endregion

        #region ContinueTaskWithState<T1>

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCompletes()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onCancellation = new();

            // Note: We have to set up onSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action<int>> onSuccess = new();
            onSuccess.Setup(action => action(state1))
                .Callback<int>(_ => taskCompletionSource.SetResult(0));

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue: taskToContinue,
                taskCompletionSource: taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            onSuccess.Verify(action => action(state1), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCompletesHandlerThrows()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            onSuccess.Setup(action => action(It.IsAny<int>())).Throws<Exception>();

            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            onSuccess.Verify(action => action(state1), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_1Generic_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int>> onCancellation = new();
            if (handlerShouldThrow)
            {
                onCancellation.SetupThrows<int, Exception>();
            }

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int, Exception>> onFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of onCancellation throwing
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
            onCancellation.Verify(action => action(state1), Times.Once);
        }

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskCancelsNoHandler()
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int, Exception>> onFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation: null);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_1Generic_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int, Exception>> onFailure = new();
            if (handlerShouldThrow)
            {
                onFailure.SetupThrows<int, Exception, Exception>();
            }

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int>> onCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of onSuccess throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
            onFailure.Verify(action => action(state1, It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task ContinueTaskWithState_1Generic_TaskFaultsNoHandler()
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int>> onCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure: null,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        #endregion

        #region ContinueTaskWithState<T1, T2>

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCompletes()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> onFailure = new();
            Mock<Action<int, int>> onCancellation = new();

            // Note: We have to set up onSuccess to set a result on the task completion source,
            //       since the AsyncHelper will not do it, and without that, we cannot reliably
            //       know when the continuation completed. We will use SetResult b/c it will throw
            //       if it has already been set.
            Mock<Action<int, int>> onSuccess = new();
            onSuccess.Setup(action => action(state1, state2))
                .Callback<int, int>((_, _) => taskCompletionSource.SetResult(0));

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue: taskToContinue,
                taskCompletionSource: taskCompletionSource,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            onSuccess.Verify(action => action(state1, state2), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCompletesHandlerThrows()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            onSuccess.Setup(o => o(It.IsAny<int>())).Throws<Exception>();

            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have faulted
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            // - onSuccess was called with state obj
            onSuccess.Verify(action => action(state1), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_2Generics_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> onCancellation = new();
            if (handlerShouldThrow)
            {
                onCancellation.SetupThrows<int, int, Exception>();
            }

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int, Exception>> onFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of onCancellation throwing
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
            onCancellation.Verify(action => action(state1, state2), Times.Once);
        }

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskCancelsNoHandler()
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int, Exception>> onFailure = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation: null);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Canceled, taskCompletionSource.Task.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ContinueTaskWithState_2Generics_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> onFailure = new();
            if (handlerShouldThrow)
            {
                onFailure.SetupThrows<int, int, Exception, Exception>();
            }

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int>> onCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled, regardless of onSuccess throwing
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);

            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
            onFailure.Verify(action => action(state1, state2, It.IsAny<Exception>()), Times.Once);
        }

        [Fact]
        public async Task ContinueTaskWithState_2Generics_TaskFaultsNoHandler()
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            TaskCompletionSource<object> taskCompletionSource = new();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int>> onCancellation = new();

            // Act
            AsyncHelper.ContinueTaskWithState(
                taskToContinue,
                taskCompletionSource,
                state1,
                state2,
                onSuccess.Object,
                onFailure: null,
                onCancellation.Object);
            await RunWithTimeout(taskCompletionSource.Task, TimeSpan.FromSeconds(1));

            // Assert
            // - taskCompletionSource should have been cancelled
            Assert.Equal(TaskStatus.Faulted, taskCompletionSource.Task.Status);
            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        #endregion

        #region CreateContinuationTask

        [Fact]
        public async Task CreateContinuationTask_TaskCompletes()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            Mock<Action> onSuccess = new();
            Mock<Action<Exception>> onFailure = new();
            Mock<Action> onCancellation = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, continuationTask.Status);
            onSuccess.Verify(action => action(), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task CreateContinuationTask_TaskCompletesHandlerThrows()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            Mock<Action<Exception>> onFailure = new();
            Mock<Action> onCancellation = new();

            Mock<Action> onSuccess = new();
            onSuccess.SetupThrows<Exception>();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.Verify(action => action(), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTask_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            Mock<Action<Exception>> onFailure = new();
            Mock<Action> onSuccess = new();

            Mock<Action> onCancellation = new();
            if (handlerShouldThrow)
            {
                onCancellation.SetupThrows<Exception>();
            }

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
            onCancellation.Verify(action => action(), Times.Once);
        }

        [Fact]
        public async Task CreateContinuationTask_TaskCancelsNoHandler()
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            Mock<Action<Exception>> onFailure = new();
            Mock<Action> onSuccess = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                onSuccess.Object,
                onFailure.Object,
                onCancellation: null);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTask_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            Mock<Action> onSuccess = new();
            Mock<Action> onCancellation = new();

            Mock<Action<Exception>> onFailure = new();
            if (handlerShouldThrow)
            {
                onFailure.SetupThrows<Exception, Exception>();
            }

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.Verify(action => action(It.IsAny<Exception>()), Times.Once);
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task CreateContinuationTask_TaskFaultsNoHandler()
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            Mock<Action> onSuccess = new();
            Mock<Action> onCancellation = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTask(
                taskToContinue,
                onSuccess.Object,
                onFailure: null,
                onCancellation: null);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        #endregion

        #region CreateContinuationTaskWithState<T1>

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCompletes()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onCancellation = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, continuationTask.Status);
            onSuccess.Verify(action => action(state1), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCompletesHandlerThrows()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;

            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onCancellation = new();

            Mock<Action<int>> onSuccess = new();
            onSuccess.SetupThrows<int, Exception>();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.Verify(action => action(state1), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;

            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onSuccess = new();

            Mock<Action<int>> onCancellation = new();
            if (handlerShouldThrow)
            {
                onCancellation.SetupThrows<int, Exception>();
            }

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
            onCancellation.Verify(action => action(state1), Times.Once);
        }

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskCancelsNoHandler()
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;

            Mock<Action<int, Exception>> onFailure = new();
            Mock<Action<int>> onSuccess = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation: null);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_1Generic_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int>> onCancellation = new();

            Mock<Action<int, Exception>> onFailure = new();
            if (handlerShouldThrow)
            {
                onFailure.SetupThrows<int, Exception, Exception>();
            }

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.Verify(action => action(state1, It.IsAny<Exception>()), Times.Once);
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task CreateContinuationTaskWithState_1Generic_TaskFaultsNoHandler()
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;

            Mock<Action<int>> onSuccess = new();
            Mock<Action<int>> onCancellation = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                onSuccess.Object,
                onFailure: null,
                onCancellation: null);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        #endregion

        #region CreateContinuationTaskWithState<T1, T2>

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCompletes()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int, Exception>> onFailure = new();
            Mock<Action<int, int>> onCancellation = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.RanToCompletion, continuationTask.Status);
            onSuccess.Verify(action => action(state1, state2), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCompletesHandlerThrows()
        {
            // Arrange
            Task taskToContinue = Task.CompletedTask;
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> onFailure = new();
            Mock<Action<int, int>> onCancellation = new();

            Mock<Action<int, int>> onSuccess = new();
            onSuccess.SetupThrows<int, int, Exception>();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.Verify(action => action(state1, state2), Times.Once);
            onFailure.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCancels(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> onFailure = new();
            Mock<Action<int, int>> onSuccess = new();

            Mock<Action<int, int>> onCancellation = new();
            if (handlerShouldThrow)
            {
                onCancellation.SetupThrows<int, int, Exception>();
            }

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
            onCancellation.Verify(action => action(state1, state2), Times.Once);
        }

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskCancelsNoHandler()
        {
            // Arrange
            Task taskToContinue = GetCancelledTask();
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int, Exception>> onFailure = new();
            Mock<Action<int, int>> onSuccess = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation: null);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Canceled, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.VerifyNeverCalled();
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CreateContinuationTaskWithState_2Generics_TaskFaults(bool handlerShouldThrow)
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int>> onCancellation = new();

            Mock<Action<int, int, Exception>> onFailure = new();
            if (handlerShouldThrow)
            {
                onFailure.SetupThrows<int, int, Exception, Exception>();
            }

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                onSuccess.Object,
                onFailure.Object,
                onCancellation.Object);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onFailure.Verify(action => action(state1, state2, It.IsAny<Exception>()), Times.Once);
            onCancellation.VerifyNeverCalled();
        }

        [Fact]
        public async Task CreateContinuationTaskWithState_2Generics_TaskFaultsNoHandler()
        {
            // Arrange
            Task taskToContinue = Task.FromException(new Exception());
            const int state1 = 123;
            const int state2 = 234;

            Mock<Action<int, int>> onSuccess = new();
            Mock<Action<int, int>> onCancellation = new();

            // Act
            Task continuationTask = AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue,
                state1,
                state2,
                onSuccess.Object,
                onFailure: null,
                onCancellation: null);
            await RunWithTimeout(continuationTask, TimeSpan.FromSeconds(1));

            // Assert
            Assert.Equal(TaskStatus.Faulted, continuationTask.Status);
            onSuccess.VerifyNeverCalled();
            onCancellation.VerifyNeverCalled();
        }

        #endregion

        private static Task GetCancelledTask()
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            return Task.FromCanceled(cts.Token);
        }

        private static async Task RunWithTimeout(Task taskToRun, TimeSpan timeout)
        {
            Task winner = await Task.WhenAny(taskToRun, Task.Delay(timeout));
            if (winner != taskToRun)
            {
                Assert.Fail("Timeout elapsed.");
            }
        }
    }
}
