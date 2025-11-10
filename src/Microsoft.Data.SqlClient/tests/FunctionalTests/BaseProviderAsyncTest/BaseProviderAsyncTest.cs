// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class BaseProviderAsyncTest
    {
        private static void AssertTaskFaults(Task t)
        {
            Assert.ThrowsAny<Exception>(() => t.Wait(TimeSpan.FromMilliseconds(1)));
        }

        [Fact]
        public static async Task TestDbConnection()
        {
            MockConnection connection = new MockConnection();
            CancellationTokenSource source = new CancellationTokenSource();

            // ensure OpenAsync() calls OpenAsync(CancellationToken.None)
            AssertEqualsWithDescription(ConnectionState.Closed, connection.State, "Connection state should have been marked as Closed");
            await connection.OpenAsync();
            Assert.False(connection.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            AssertEqualsWithDescription(ConnectionState.Open, connection.State, "Connection state should have been marked as Open");
            connection.Close();

            // Verify cancellationToken over-ride
            AssertEqualsWithDescription(ConnectionState.Closed, connection.State, "Connection state should have been marked as Closed");
            await connection.OpenAsync(source.Token);
            AssertEqualsWithDescription(ConnectionState.Open, connection.State, "Connection state should have been marked as Open");
            connection.Close();

            // Verify exceptions are routed through task
            MockConnection connectionFail = new MockConnection()
            {
                Fail = true
            };
            AssertTaskFaults(connectionFail.OpenAsync());
            AssertTaskFaults(connectionFail.OpenAsync(source.Token));

            // Verify base implementation does not call Open when passed an already cancelled cancellation token
            source.Cancel();
            AssertEqualsWithDescription(ConnectionState.Closed, connection.State, "Connection state should have been marked as Closed");
            await connection.OpenAsync(source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription(ConnectionState.Closed, connection.State, "Connection state should have been marked as Closed");
        }

        [Fact]
        public static async Task TestDbCommand()
        {
            MockCommand command = new MockCommand()
            {
                ScalarResult = 1,
                Results = Enumerable.Range(1, 5).Select((x) => new object[] { x, x.ToString() })
            };
            CancellationTokenSource source = new CancellationTokenSource();

            // Verify parameter routing and correct synchronous implementation is called
            await command.ExecuteNonQueryAsync();
            Assert.False(command.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            AssertEqualsWithDescription("ExecuteNonQuery", command.LastCommand, "Last command was not as expected");
            await command.ExecuteReaderAsync();
            AssertEqualsWithDescription(CommandBehavior.Default, command.CommandBehavior, "Command behavior should have been marked as Default");
            Assert.False(command.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            AssertEqualsWithDescription("ExecuteReader", command.LastCommand, "Last command was not as expected");
            await command.ExecuteScalarAsync();
            Assert.False(command.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            AssertEqualsWithDescription("ExecuteScalar", command.LastCommand, "Last command was not as expected");

            await command.ExecuteNonQueryAsync(source.Token);
            AssertEqualsWithDescription("ExecuteNonQuery", command.LastCommand, "Last command was not as expected");
            await command.ExecuteReaderAsync(source.Token);
            AssertEqualsWithDescription("ExecuteReader", command.LastCommand, "Last command was not as expected");
            await command.ExecuteScalarAsync(source.Token);
            AssertEqualsWithDescription("ExecuteScalar", command.LastCommand, "Last command was not as expected");

            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);
            AssertEqualsWithDescription(CommandBehavior.SequentialAccess, command.CommandBehavior, "Command behavior should have been marked as SequentialAccess");
            Assert.False(command.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            AssertEqualsWithDescription("ExecuteReader", command.LastCommand, "Last command was not as expected");

            await command.ExecuteReaderAsync(CommandBehavior.SingleRow, source.Token);
            AssertEqualsWithDescription(CommandBehavior.SingleRow, command.CommandBehavior, "Command behavior should have been marked as SingleRow");
            AssertEqualsWithDescription("ExecuteReader", command.LastCommand, "Last command was not as expected");

            // Verify exceptions are routed through task
            MockCommand commandFail = new MockCommand
            {
                Fail = true
            };
            AssertTaskFaults(commandFail.ExecuteNonQueryAsync());
            AssertTaskFaults(commandFail.ExecuteNonQueryAsync(source.Token));
            AssertTaskFaults(commandFail.ExecuteReaderAsync());
            AssertTaskFaults(commandFail.ExecuteReaderAsync(CommandBehavior.SequentialAccess));
            AssertTaskFaults(commandFail.ExecuteReaderAsync(source.Token));
            AssertTaskFaults(commandFail.ExecuteReaderAsync(CommandBehavior.SequentialAccess, source.Token));
            AssertTaskFaults(commandFail.ExecuteScalarAsync());
            AssertTaskFaults(commandFail.ExecuteScalarAsync(source.Token));

            // Verify base implementation does not call Open when passed an already cancelled cancellation token
            source.Cancel();
            command.LastCommand = "Nothing";
            await command.ExecuteNonQueryAsync(source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", command.LastCommand, "Expected last command to be 'Nothing'");
            await command.ExecuteReaderAsync(source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", command.LastCommand, "Expected last command to be 'Nothing'");
            await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", command.LastCommand, "Expected last command to be 'Nothing'");
            await command.ExecuteScalarAsync(source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", command.LastCommand, "Expected last command to be 'Nothing'");

            // Verify cancellation
            command.WaitForCancel = true;
            source = new CancellationTokenSource();
            var task = Task.Factory.StartNew(() => { command.WaitForWaitingForCancel(); source.Cancel(); });
            Task result = command.ExecuteNonQueryAsync(source.Token);
            Assert.True(result.Exception != null, "Task result should be faulted");
            await task;

            source = new CancellationTokenSource();
            task = Task.Factory.StartNew(() => { command.WaitForWaitingForCancel(); source.Cancel(); });
            result = command.ExecuteReaderAsync(source.Token);
            Assert.True(result.Exception != null, "Task result should be faulted");
            await task;

            source = new CancellationTokenSource();
            task = Task.Factory.StartNew(() => { command.WaitForWaitingForCancel(); source.Cancel(); });
            result = command.ExecuteScalarAsync(source.Token);
            Assert.True(result.Exception != null, "Task result should be faulted");
            await task;
        }

        [Fact]
        public static async Task TestDbDataReader()
        {
            var query = Enumerable.Range(1, 2).Select((x) => new object[] { x, x.ToString(), DBNull.Value });
            MockDataReader reader = new MockDataReader { Results = query.GetEnumerator() };
            CancellationTokenSource source = new CancellationTokenSource();

            var result = await reader.ReadAsync();
            AssertEqualsWithDescription("Read", reader.LastCommand, "Last command was not as expected");
            Assert.True(result, "Should have received a Result from the ReadAsync");
            Assert.False(reader.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");

            GetFieldValueAsync(reader, 0, 1);
            Assert.False(reader.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            GetFieldValueAsync(reader, source.Token, 1, "1");

            result = await reader.ReadAsync(source.Token);
            AssertEqualsWithDescription("Read", reader.LastCommand, "Last command was not as expected");
            Assert.True(result, "Should have received a Result from the ReadAsync");

            GetFieldValueAsync<object>(reader, 2, DBNull.Value);
            GetFieldValueAsync<DBNull>(reader, 2, DBNull.Value);
            AssertTaskFaults(reader.GetFieldValueAsync<int?>(2));
            AssertTaskFaults(reader.GetFieldValueAsync<string>(2));
            AssertTaskFaults(reader.GetFieldValueAsync<bool>(2));
            AssertEqualsWithDescription("GetValue", reader.LastCommand, "Last command was not as expected");

            result = await reader.ReadAsync();
            AssertEqualsWithDescription("Read", reader.LastCommand, "Last command was not as expected");
            Assert.False(result, "Should NOT have received a Result from the ReadAsync");

            result = await reader.NextResultAsync();
            AssertEqualsWithDescription("NextResult", reader.LastCommand, "Last command was not as expected");
            Assert.False(result, "Should NOT have received a Result from NextResultAsync");
            Assert.False(reader.CancellationToken.CanBeCanceled, "Default cancellation token should not be cancellable");
            result = await reader.NextResultAsync(source.Token);
            AssertEqualsWithDescription("NextResult", reader.LastCommand, "Last command was not as expected");
            Assert.False(result, "Should NOT have received a Result from NextResultAsync");

            MockDataReader readerFail = new MockDataReader { Results = query.GetEnumerator(), Fail = true };
            AssertTaskFaults(readerFail.ReadAsync());
            AssertTaskFaults(readerFail.ReadAsync(source.Token));
            AssertTaskFaults(readerFail.NextResultAsync());
            AssertTaskFaults(readerFail.NextResultAsync(source.Token));
            AssertTaskFaults(readerFail.GetFieldValueAsync<object>(0));
            AssertTaskFaults(readerFail.GetFieldValueAsync<object>(0, source.Token));

            source.Cancel();
            reader.LastCommand = "Nothing";
            await reader.ReadAsync(source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", reader.LastCommand, "Expected last command to be 'Nothing'");
            await reader.NextResultAsync(source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", reader.LastCommand, "Expected last command to be 'Nothing'");
            await reader.GetFieldValueAsync<object>(0, source.Token).ContinueWith((t) => { }, TaskContinuationOptions.OnlyOnCanceled);
            AssertEqualsWithDescription("Nothing", reader.LastCommand, "Expected last command to be 'Nothing'");
        }

        private static void GetFieldValueAsync<T>(MockDataReader reader, int ordinal, T expected)
        {
            Task<T> result = reader.GetFieldValueAsync<T>(ordinal);
            result.Wait();
            AssertEqualsWithDescription("GetValue", reader.LastCommand, "Last command was not as expected");
            AssertEqualsWithDescription(expected, result.Result, "GetFieldValueAsync did not return expected value");
        }

        private static void GetFieldValueAsync<T>(MockDataReader reader, CancellationToken cancellationToken, int ordinal, T expected)
        {
            Task<T> result = reader.GetFieldValueAsync<T>(ordinal, cancellationToken);
            result.Wait();
            AssertEqualsWithDescription("GetValue", reader.LastCommand, "Last command was not as expected");
            AssertEqualsWithDescription(expected, result.Result, "GetFieldValueAsync did not return expected value");
        }

        private static void AssertEqualsWithDescription(object expectedValue, object actualValue, string failMessage)
        {
            if (expectedValue == null || actualValue == null)
            {
                var msg = string.Format("{0}\nExpected: {1}\nActual: {2}", failMessage, expectedValue, actualValue);
                Assert.True(expectedValue == actualValue, msg);
            }
            else
            {
                var msg = string.Format("{0}\nExpected: {1} ({2})\nActual: {3} ({4})", failMessage, expectedValue, expectedValue.GetType(), actualValue, actualValue.GetType());
                Assert.True(expectedValue.Equals(actualValue), msg);
            }
        }
    }
}
