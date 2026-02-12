// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using System.Threading;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class MarsSessionPoolingTest
    {
        private const int ConcurrentCommands = 5;

        // Synapse: Catalog view 'dm_exec_connections' is not supported in this version.

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteScalar_DisposeCommand(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // - Run command
                command.ExecuteScalar();

                // - Dispose command
                command.Dispose();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Each request runs to completion, none are running concurrently, so additional
                //   MARS sessions do not need to be opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteScalar_CloseConnection(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // - Run command
                command.ExecuteScalar();

                // - Close and reopen connection (return to pool)
                connection.Close();
                connection.Open();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Each request runs to completion, none are running concurrently, so additional
                //   MARS sessions do not need to be opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteNonQuery_DisposeCommand(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // - Run command
                command.ExecuteNonQuery();

                // - Dispose command
                command.Dispose();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Each request runs to completion, none are running concurrently, so additional
                //   MARS sessions do not need to be opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteNonQuery_CloseConnection(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // - Run command
                command.ExecuteNonQuery();

                // - Close and reopen connection (return to pool)
                connection.Close();
                connection.Open();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Each request runs to completion, none are running concurrently, so additional
                //   MARS sessions do not need to be opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_CloseReader(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // - Run command
                readers[i] = commands[i].ExecuteReader();

                // - Close reader
                readers[i].Close();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Closing the reader completes the request, so no requests are running
                //   concurrently, so no additional MARS sessions should have been opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_DisposeReader(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // - Run command
                readers[i] = commands[i].ExecuteReader();

                // - Dispose reader
                readers[i].Dispose();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Disposing the reader completes the request, so no requests are running
                //   concurrently, so no additional MARS sessions should have been opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [Trait("Category", "flaky")]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_GarbageCollectReader(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // - Run command and get weak reference to reader.
                //   This must happen in another scope otherwise the reader will not be marked for
                //   garbage collection.
                WeakReference readerWeakReference = OpenReaderThenNullify(commands[i]);

                // - Run the garbage collector
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Assert
                // - Make sure reader has been collected by now, otherwise results are invalid
                Assert.False(readerWeakReference.IsAlive);

                // - Count of open sessions/requests will increase with each iteration
                //   Finalizing a data reader does *not* close it, meaning the MARS session is left
                //   in an incomplete state. As such, with each command that's executed, a new
                //   session is opened.
                AssertSessionsAndRequests(connection, openMarsSessions: i + 1, openRequests: i + 1);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_DisposeCommand(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // - Run command
                readers[i] = commands[i].ExecuteReader();

                // - Dispose the command
                commands[i].Dispose();

                // Assert
                // - Count of open sessions/requests will increase with each iteration
                //   Disposing of the command does *not* close the reader, meaning the MARS session
                //   is left in an incomplete state. As such, with each command that's executed, a
                //   new session is opened.
                AssertSessionsAndRequests(connection, openMarsSessions: i + 1, openRequests: i + 1);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_CloseConnection(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // - Run command
                readers[i] = commands[i].ExecuteReader();
                // - Close and reopen connection (return to pool)
                connection.Close();
                connection.Open();

                // Assert
                // - Count of sessions/requests should stay the same
                //   Closing the connection completes any pending requests, so no requests are
                //   running concurrently, so no additional MARS sessions should have been opened.
                AssertSessionsAndRequests(connection, openMarsSessions: 0, openRequests: 0);
            }
        }

        [Trait("Category", "flaky")]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_NoCloses(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = GetConnection();
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // - Run command, close nothing!
                readers[i] = commands[i].ExecuteReader();

                // Assert
                // - Count of open sessions/requests will increase with each iteration
                //   Leaving a data reader open leaves the MARS session in an incomplete state. As
                //   such, with each command that's executed, a new session is opened.
                AssertSessionsAndRequests(connection, openMarsSessions: i + 1, openRequests: i + 1);
            }
        }

        /// <summary>
        /// Asserts the number of open sessions and pending requests on the connection.
        /// </summary>
        /// <param name="connection">Connection to check open sessions and pending requests on</param>
        /// <param name="openMarsSessions">
        /// Number of MARS sessions expected to be open/in use by the test. The sessions for the
        /// main connection and validation query will be added before assertion.
        /// </param>
        /// <param name="openRequests">
        /// Number of open requests expected to be pending by use of the test. The request for the
        /// validation query will be added before assertion.
        /// </param>
        /// <exception cref="Exception">
        /// Thrown if any of the validation result sets are missing or empty.
        /// </exception>
        private static void AssertSessionsAndRequests(
            SqlConnection connection,
            int openMarsSessions,
            int openRequests)
        {
            const int maxAttempts = 5;

            // For these tests, the expected session count will always be at least 2 in MARS mode:
            // 1 for the main connection
            // 1 for the verification command we just executed
            int? observedSessions = null;
            int expectedSessions = openMarsSessions + 2;

            // For these tests, the expected request count will always be at least 1:
            // 1 for the verification command we just executed
            int? observedRequests = null;
            int expectedRequests = openRequests + 1;

            // There is a race between opening new sessions and them appearing in the DMV tables.
            // As such, we want to poll the DMV a few times before declaring the wrong behavior was
            // observed.
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                (observedSessions, observedRequests) = QuerySessionCounters(connection);
                if (observedSessions == expectedSessions && observedRequests == expectedRequests)
                {
                    // We observed the expected values.
                    return;
                }

                // Back off and wait before trying again
                Thread.SpinWait(20 << attempt);
            }

            // If we make it to here, we never saw the expected numbers, so fail the test with the
            // last value we observed.
            Assert.Equal(expectedSessions, observedSessions);
            Assert.Equal(expectedRequests, observedRequests);
        }

        private static DisposableArray<SqlCommand> GetCommands(SqlConnection connection, CommandType commandType)
        {
            DisposableArray<SqlCommand> result = new(ConcurrentCommands);
            for (int i = 0; i < result.Length; i++)
            {
                switch (commandType)
                {
                    case CommandType.Text:
                        string commandText = string.Join(" ", Enumerable.Repeat(@"SELECT * FROM sys.databases;", 20));
                        commandText += @" PRINT 'THIS IS THE END!'";

                        result[i] = new SqlCommand
                        {
                            CommandText = commandText,
                            CommandTimeout = 120,
                            CommandType = CommandType.Text,
                            Connection = connection
                        };
                        break;

                    case CommandType.StoredProcedure:
                        result[i] = new SqlCommand
                        {
                            CommandText = "sp_who",
                            CommandTimeout = 120,
                            CommandType = CommandType.StoredProcedure,
                            Connection = connection
                        };
                        break;

                    default:
                        throw new InvalidOperationException("Not supported test type");
                }
            }

            return result;
        }

        private static SqlConnection GetConnection()
        {
            // Generate a unique name for the application to ensure pool isolation between tests
            string applicationName = $"SqlClientMarsPoolingTests:{Guid.NewGuid()}";
            string connectionString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                ApplicationName = applicationName,
                PacketSize = 512,
                MaxPoolSize = 1,
                MultipleActiveResultSets = true
            }.ConnectionString;

            return new SqlConnection(connectionString);
        }

        private static WeakReference OpenReaderThenNullify(SqlCommand command)
        {
            SqlDataReader? reader = command.ExecuteReader();
            WeakReference weak = new WeakReference(reader);
            reader = null;
            return weak;
        }

        private static (int sessions, int requests) QuerySessionCounters(SqlConnection connection)
        {
            using SqlCommand verificationCommand = new SqlCommand();
            verificationCommand.CommandText =
                @"SELECT COUNT(*) AS SessionCount " +
                @"FROM sys.dm_exec_connections " +
                @"WHERE session_id=@@spid AND net_transport='Session'; " +
                @"SELECT COUNT(*) AS RequestCount " +
                @"FROM sys.dm_exec_requests " +
                @"WHERE session_id=@@spid AND (status='running' OR status='suspended')";
            verificationCommand.CommandType = CommandType.Text;
            verificationCommand.Connection = connection;

            // Result 1) Count of active sessions from sys.dm_exec_connections
            using SqlDataReader reader = verificationCommand.ExecuteReader();
            if (!reader.Read())
            {
                throw new Exception("Expected dm_exec_connections results from verification command");
            }

            int sessions = reader.GetInt32(0);

            // Result 2) Count of active requests from sys.dm_exec_requests
            if (!reader.NextResult() || !reader.Read())
            {
                throw new Exception("Expected dm_exec_requests results from verification command");
            }

            int requests = reader.GetInt32(0);

            return (sessions, requests);
        }
    }
}
