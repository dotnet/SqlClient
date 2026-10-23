// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Guards cancellation cleanup and retry handling during asynchronous parameter encryption.
    /// </summary>
    public class SqlCommandAsyncShould
    {
        /// <summary>
        /// Retryable enclave package faults before the read stage must invalidate cached metadata and
        /// retry once, preserving the retry's result, failure, or cancellation.
        /// </summary>
        [Theory]
        [InlineData("Success")]
        [InlineData("Failure")]
        [InlineData("Cancellation")]
        public async Task RetryableEnclaveWriteFailureRetriesCachedExecution(string retryOutcome)
        {
            using SqlConnection connection = new("Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            using SqlCommand command = new($"SELECT @p -- {Guid.NewGuid()}", connection);
            command.Parameters.AddWithValue("@p", 1);
            SqlQueryMetadataCache cache = SqlQueryMetadataCache.GetInstance();
            cache.AddQueryMetadata(command, ignoreQueriesWithReturnValueParams: true);
            Assert.True(cache.TryGetCachedQueryMetadata(command, out _));

            TaskCompletionSource<object> localCompletion = new();
            TaskCompletionSource<object> globalCompletion = new();
            TaskCompletionSource<object> retryCompletion = new();
            object expectedResult = new();
            var retryFailure = new EnclaveDelegate.RetryableEnclaveQueryExecutionException("retry failed", null);
            switch (retryOutcome)
            {
                case "Success":
                    retryCompletion.SetResult(expectedResult);
                    break;
                case "Failure":
                    retryCompletion.SetException(retryFailure);
                    break;
                case "Cancellation":
                    retryCompletion.SetCanceled();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(retryOutcome));
            }

            int retryCalls = 0;
            AttachCachedExecutionRetry(command, localCompletion, globalCompletion,
                (retryCommand, behavior, callback, state, timeout, isRetry, asyncWrite) =>
                {
                    retryCalls++;
                    Assert.Same(command, retryCommand);
                    Assert.Equal(CommandBehavior.Default, behavior);
                    Assert.Null(callback);
                    Assert.Null(state);
                    Assert.InRange(timeout, 0, 30);
                    Assert.True(isRetry);
                    Assert.True(asyncWrite);
                    Assert.False(cache.TryGetCachedQueryMetadata(command, out _));
                    return retryCompletion.Task;
                });

            localCompletion.SetException(
                new EnclaveDelegate.RetryableEnclaveQueryExecutionException("package generation failed", null));

            if (retryOutcome == "Success")
            {
                Assert.Same(expectedResult, await globalCompletion.Task);
            }
            else if (retryOutcome == "Failure")
            {
                var exception = await Assert.ThrowsAsync<EnclaveDelegate.RetryableEnclaveQueryExecutionException>(
                    () => globalCompletion.Task);
                Assert.Same(retryFailure, exception);
            }
            else
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => globalCompletion.Task);
                Assert.True(globalCompletion.Task.IsCanceled);
            }

            Assert.Equal(1, retryCalls);
        }

        /// <summary>
        /// Ordinary write failures and cancellation must retain their existing completion behavior
        /// without triggering enclave retries.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task NonRetryableWriteCompletionDoesNotRetry(bool canceled)
        {
            using SqlConnection connection = new("Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            using SqlCommand command = new("SELECT @p", connection);
            TaskCompletionSource<object> localCompletion = new();
            TaskCompletionSource<object> globalCompletion = new();
            InvalidOperationException failure = new("write failed");
            int retryCalls = 0;
            AttachCachedExecutionRetry(command, localCompletion, globalCompletion,
                (retryCommand, behavior, callback, state, timeout, isRetry, asyncWrite) =>
                {
                    retryCalls++;
                    return Task.FromResult<object>(command);
                });

            if (canceled)
            {
                localCompletion.SetCanceled();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => globalCompletion.Task);
                Assert.True(globalCompletion.Task.IsCanceled);
            }
            else
            {
                localCompletion.SetException(failure);
                InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() => globalCompletion.Task);
                Assert.Same(failure, exception);
            }

            Assert.Equal(0, retryCalls);
        }

        /// <summary>
        /// Wires the command's cache-hit completion path with observable read-stage and retry delegates.
        /// </summary>
        /// <param name="command">Command whose cached metadata may need invalidating.</param>
        /// <param name="localCompletion">Completion representing the initial write stage.</param>
        /// <param name="globalCompletion">Completion returned to the caller.</param>
        /// <param name="retry">Delegate observing the single retry attempt.</param>
        private static void AttachCachedExecutionRetry(
            SqlCommand command,
            TaskCompletionSource<object> localCompletion,
            TaskCompletionSource<object> globalCompletion,
            Func<SqlCommand, CommandBehavior, AsyncCallback, object, int, bool, bool, IAsyncResult> retry)
        {
            MethodInfo method = typeof(SqlCommand).GetMethod(
                "CreateLocalCompletionTask", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Async retry method not found.");
            Func<SqlCommand, IAsyncResult, bool, string, object> end =
                (_, _, _, _) => throw new InvalidOperationException("A failed write must not enter the read stage.");
            method.Invoke(command, new object?[]
            {
                CommandBehavior.Default, null, 30, true, true,
                globalCompletion, localCompletion, end, retry, nameof(SqlCommand.EndExecuteReader),
                ADP.TimerCurrent(),
            });
        }

        /// <summary>
        /// Key-store cancellation after the describe phase releases the parser state must not be
        /// replaced by a null-lock exception in the asynchronous End methods.
        /// </summary>
        [Theory]
        [InlineData("EndExecuteReaderAsync")]
        [InlineData("EndExecuteNonQueryAsync")]
        [InlineData("EndExecuteXmlReaderAsync")]
        public void CanceledKeyStoreOperationDoesNotLockReleasedState(string endMethod)
        {
            using SqlConnection connection = new("Data Source=async-ae-unit-test;Column Encryption Setting=Enabled");
            using SqlCommand command = new("SELECT 1", connection);
            TaskCompletionSource<object> completion = new();
            completion.SetCanceled();

            MethodInfo method = typeof(SqlCommand).GetMethod(endMethod, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Async End method not found.");
            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => method.Invoke(command, new object[] { completion.Task }));

            Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
        }

        /// <summary>
        /// Missing or closed connections must fault execution without retaining the caller's token,
        /// including when setup fails before the normal cleanup continuation is registered.
        /// </summary>
        [Theory]
        [InlineData("Reader", false)]
        [InlineData("Reader", true)]
        [InlineData("NonQuery", false)]
        [InlineData("NonQuery", true)]
        [InlineData("XmlReader", false)]
        [InlineData("XmlReader", true)]
        public async Task FailedSetupClearsCancellationToken(string execution, bool hasConnection)
        {
            using SqlConnection connection = new();
            using SqlCommand command = new("SELECT 1", hasConnection ? connection : null);
            using CancellationTokenSource source = new();

            await Assert.ThrowsAsync<InvalidOperationException>(() => execution switch
            {
                "Reader" => (Task)command.ExecuteReaderAsync(source.Token),
                "NonQuery" => command.ExecuteNonQueryAsync(source.Token),
                "XmlReader" => command.ExecuteXmlReaderAsync(source.Token),
                _ => throw new ArgumentOutOfRangeException(nameof(execution)),
            });

            FieldInfo tokenField = typeof(SqlCommand).GetField(
                "_asyncExecutionCancellationToken", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Execution cancellation token field not found.");
            Assert.Equal(CancellationToken.None, Assert.IsType<CancellationToken>(tokenField.GetValue(command)));
        }
    }
}
