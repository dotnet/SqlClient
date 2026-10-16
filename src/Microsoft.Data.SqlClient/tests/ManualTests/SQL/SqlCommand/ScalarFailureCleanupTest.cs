// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Xunit;
using DiagnosticListener = System.Diagnostics.DiagnosticListener;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Prevents other test collections' ClearAllPools calls from invalidating pool reuse assertions.
    /// </summary>
    [CollectionDefinition(nameof(ScalarFailureCleanupTest), DisableParallelization = true)]
    public sealed class ScalarFailureCleanupCollection
    {
    }

    /// <summary>
    /// Verifies scalar cleanup preserves fatal failures and quarantines the interrupted connection,
    /// while ordinary SQL errors retain normal draining and pooling.
    /// </summary>
    [Collection(nameof(ScalarFailureCleanupTest))]
    [Trait("Set", "3")]
    public sealed class ScalarFailureCleanupTest
    {
        /// <summary>
        /// A preallocated fatal exception from an InfoMessage callback must not be replaced by
        /// a later SQL error during cleanup. Exercise both scalar implementations with and without MARS.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public async Task ScalarFailure_PreservesExceptionAndPoolSafety(bool async, bool mars, bool fatal)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                ApplicationName = nameof(ScalarFailureCleanupTest) + Guid.NewGuid(),
                Pooling = true,
                MaxPoolSize = 1,
                MinPoolSize = 0,
                ConnectRetryCount = 0,
                MultipleActiveResultSets = mars,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                await OpenConnection(connection, async);
                var original = new InternalConnectionWrapper(connection);
                Guid originalId = connection.ClientConnectionId;
                connection.Close();
                await OpenConnection(connection, async);
                Assert.True(original.IsInternalConnectionOf(connection));
                Assert.Equal(originalId, connection.ClientConnectionId);
                object parser = ConnectionHelper.GetParser(connection.GetInternalConnection());

                var failure = new OutOfMemoryException("Scalar cleanup sentinel");
                int notifications = 0;
                connection.InfoMessage += (_, args) =>
                {
                    Assert.Contains("Scalar cleanup sentinel", args.Message);
                    notifications++;
                    if (fatal)
                    {
                        throw failure;
                    }
                };

                // The first result binds the reader before the callback interrupts NextResult.
                // Normal cleanup would encounter the second error and could mask the fatal one.
                using SqlCommand command = new(
                    "SELECT 1; RAISERROR ('Scalar cleanup sentinel', 10, 1); " +
                    "RAISERROR ('Secondary cleanup error', 16, 1);", connection);
                Exception actual = await Record.ExceptionAsync(async () =>
                {
                    if (async)
                    {
                        await command.ExecuteScalarAsync();
                    }
                    else
                    {
                        command.ExecuteScalar();
                    }
                });

                Assert.Equal(1, notifications);
                if (fatal)
                {
                    Assert.Same(failure, actual);
                    Assert.Equal("Broken", parser.GetType()
                        .GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(parser).ToString());
                }
                else
                {
                    SqlException sqlError = Assert.IsType<SqlException>(actual);
                    Assert.Contains("Secondary cleanup error", sqlError.Message);
                }

                connection.Close();
                using SqlConnection next = new(builder.ConnectionString);
                await OpenConnection(next, async);
                Assert.Equal(!fatal, original.IsInternalConnectionOf(next));
                if (fatal)
                {
                    Assert.NotEqual(originalId, next.ClientConnectionId);
                }
                using SqlCommand probe = new("SELECT 42", next);
                Assert.Equal(42, async ? await probe.ExecuteScalarAsync() : probe.ExecuteScalar());
            }
            finally
            {
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>
        /// Fatal settings restoration must honor reader connection ownership and release the pool
        /// checkout, without replacing the initiating failure if a connection-close subscriber throws.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void ReaderClose_FatalRestorationHonorsConnectionOwnership(bool mars, bool closeConnection, bool throwOnClose)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                ApplicationName = nameof(ReaderClose_FatalRestorationHonorsConnectionOwnership) + Guid.NewGuid(),
                Pooling = true,
                MaxPoolSize = 1,
                MinPoolSize = 0,
                ConnectTimeout = 5,
                ConnectRetryCount = 0,
                MultipleActiveResultSets = mars,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            int closedEvents = 0;
            StateChangeEventHandler onStateChange = (_, args) =>
            {
                if (args.CurrentState == ConnectionState.Closed)
                {
                    closedEvents++;
                    if (throwOnClose)
                    {
                        throw new InvalidOperationException("Secondary close failure");
                    }
                }
            };
            try
            {
                connection.Open();
                var original = new InternalConnectionWrapper(connection);
                using SqlCommand command = new("SELECT 1", connection);
                using SqlDataReader reader = command.ExecuteReader(
                    closeConnection ? CommandBehavior.CloseConnection : CommandBehavior.Default);
                var failure = new OutOfMemoryException("Fatal restoration sentinel");
                int notifications = 0;
                connection.InfoMessage += (_, _) =>
                {
                    notifications++;
                    throw failure;
                };
                connection.StateChange += onStateChange;

                // Replace the existing restoration batch, not the cleanup implementation, so
                // Close encounters a deterministic fatal callback after draining the result.
                typeof(SqlDataReader).GetField("_resetOptionsString", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(reader, "RAISERROR ('Fatal restoration sentinel', 10, 1);");

                Assert.Same(failure, Record.Exception(() => reader.Close()));
                Assert.Equal(1, notifications);
                Assert.True(reader.IsClosed);
                Assert.Equal(closeConnection ? 1 : 0, closedEvents);
                if (!closeConnection)
                {
                    // Without CloseConnection, releasing the checkout remains the caller's responsibility.
                    connection.Close();
                }
                Assert.Equal(ConnectionState.Closed, connection.State);
                reader.Close();
                Assert.Equal(1, notifications);
                Assert.Equal(1, closedEvents);

                // With CloseConnection, this borrow must succeed without explicitly closing the
                // original connection or clearing the one-slot pool.
                using SqlConnection next = new(builder.ConnectionString);
                next.Open();
                Assert.False(original.IsInternalConnectionOf(next));
                using SqlCommand probe = new("SELECT 42", next);
                Assert.Equal(42, probe.ExecuteScalar());
            }
            finally
            {
                connection.StateChange -= onStateChange;
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>
        /// Fatal cleanup must wait for the session monitor held by cancellation before retiring
        /// transport resources. Holding that monitor models an in-flight Cancel without racing native I/O.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false)]
        [InlineData(true)]
        public void FatalCleanup_WaitsForCancellationLock(bool mars)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = false,
                MultipleActiveResultSets = mars,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            connection.Open();
            using SqlCommand command = new("SELECT 1", connection);
            using SqlDataReader reader = command.ExecuteReader();
            object stateObj = typeof(SqlDataReader)
                .GetField("_stateObj", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader);
            MethodInfo closeOnError = typeof(SqlDataReader)
                .GetMethod("CloseOnError", BindingFlags.Instance | BindingFlags.NonPublic);
            var failure = new OutOfMemoryException("Fatal retirement sentinel");
            Exception actual = null;
            int completed = 0;
            var worker = new Thread(() =>
            {
                try
                {
                    actual = Record.Exception(() => closeOnError.Invoke(reader, new object[] { failure }));
                }
                finally
                {
                    Volatile.Write(ref completed, 1);
                }
            }) { IsBackground = true };
            bool reachedCleanup;
            bool completedWhileLocked;
            lock (stateObj)
            {
                worker.Start();
                reachedCleanup = SpinWait.SpinUntil(
                    () => Volatile.Read(ref completed) != 0 ||
                        (reader.IsClosed && (worker.ThreadState & ThreadState.WaitSleepJoin) != 0),
                    TimeSpan.FromSeconds(10));
                completedWhileLocked = Volatile.Read(ref completed) != 0;
            }

            Assert.True(worker.Join(TimeSpan.FromSeconds(10)), "Fatal cleanup did not finish after releasing the cancellation lock.");
            Assert.True(reachedCleanup, "Worker did not reach fatal cleanup.");
            Assert.False(completedWhileLocked, "Session retirement bypassed the cancellation lock.");
            Assert.Same(failure, Assert.IsType<TargetInvocationException>(actual).InnerException);
            Assert.True(reader.IsClosed);
        }

        /// <summary>
        /// Synchronous error subscribers must observe an already quarantined connection, even if
        /// they close and borrow from its pool or throw a secondary exception.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task ScalarFatalError_QuarantinesBeforeDiagnosticCallback(bool mars, bool throwFromSubscriber)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                ApplicationName = nameof(ScalarFatalError_QuarantinesBeforeDiagnosticCallback) + Guid.NewGuid(),
                Pooling = true,
                MaxPoolSize = 1,
                MinPoolSize = 0,
                ConnectTimeout = 5,
                ConnectRetryCount = 0,
                MultipleActiveResultSets = mars,
                Enlist = false
            };
            using SqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            var original = new InternalConnectionWrapper(connection);
            object parser = ConnectionHelper.GetParser(connection.GetInternalConnection());
            using SqlCommand command = new(
                "SELECT 1; RAISERROR ('Fatal diagnostic sentinel', 10, 1); SELECT 2;", connection);
            var failure = new OutOfMemoryException("Fatal diagnostic sentinel");
            connection.InfoMessage += (_, _) => throw failure;
            int errorEvents = 0;
            bool parserWasBroken = false;
            bool reusedInterruptedConnection = true;
            object reportedException = null;
            object probeResult = null;
            var observer = new FakeDiagnosticListenerObserver(value =>
            {
                if (value.Key != "Microsoft.Data.SqlClient.WriteCommandError" ||
                    !ReferenceEquals(command, value.Value.GetType().GetProperty("Command").GetValue(value.Value)))
                {
                    return;
                }
                errorEvents++;
                reportedException = value.Value.GetType().GetProperty("Exception").GetValue(value.Value);
                parserWasBroken = parser.GetType().GetProperty("State", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(parser).ToString() == "Broken";
                connection.Close();
                using SqlConnection next = new(builder.ConnectionString);
                next.Open();
                reusedInterruptedConnection = original.IsInternalConnectionOf(next);
                using SqlCommand probe = new("SELECT 42", next);
                probeResult = probe.ExecuteScalar();
                if (throwFromSubscriber)
                {
                    throw new InvalidOperationException("Secondary diagnostic failure");
                }
            });
            observer.Enable();
            try
            {
                using (DiagnosticListener.AllListeners.Subscribe(observer))
                {
                    Assert.Same(failure, await Record.ExceptionAsync(() => command.ExecuteScalarAsync()));
                }
                Assert.Equal(1, errorEvents);
                Assert.Same(failure, reportedException);
                Assert.True(parserWasBroken, "Diagnostic subscriber ran before quarantine.");
                Assert.False(reusedInterruptedConnection);
                Assert.Equal(42, probeResult);
            }
            finally
            {
                observer.Disable();
                SqlConnection.ClearPool(connection);
            }
        }

        /// <summary>Opens a connection using the API variant under test.</summary>
        /// <param name="connection">Connection to open.</param>
        /// <param name="async">Whether to exercise asynchronous opening.</param>
        /// <returns>A task completing when the connection is open.</returns>
        private static async Task OpenConnection(SqlConnection connection, bool async)
        {
            if (async)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }
        }
    }
}
