// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class MARSSessionPoolingTest
    {
        private const int ConcurrentCommands = 5;
        private static readonly string TestConnString =
            new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                ApplicationName = "SqlClientMarsPoolingTests", // Specify application name to make these tests unique
                                                               // for pooling purposes.
                PacketSize = 512,
                MaxPoolSize = 1,
                MultipleActiveResultSets = true
            }.ConnectionString;

        // Synapse: Catalog view 'dm_exec_connections' is not supported in this version.

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteScalar(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command
                command.ExecuteScalar();

                // Assert
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteNonQuery(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command
                command.ExecuteScalar();

                // Assert
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_CloseReader(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, close reader
                using SqlDataReader reader = command.ExecuteReader();
                reader.Close();

                // Assert
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_DisposeReader(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, dispose reader
                SqlDataReader reader = command.ExecuteReader();
                reader.Dispose();

                // Assert
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_CloseConnection(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, suppress finalization of reader
                using SqlDataReader reader = command.ExecuteReader();
                GC.SuppressFinalize(reader);

                // Assert
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_GarbageCollection_Wait(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command and get weak reference to reader
                // Note: This must happen in another scope otherwise the reader will not be marked
                // for garbage collection
                WeakReference weakReader = OpenReaderThenNullify(command);

                // Run the garbage collector to force cleanup of reader
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Assert
                // Reader should be garbage collected by now
                Assert.False(weakReader.IsAlive);
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_GarbageCollection_NoWait(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command
                // Note: This must happen in another scope otherwise the reader will not be marked
                // for garbage collection
                _ = OpenReaderThenNullify(command);

                // Run the garbage collector, but do not wait for finalization
                GC.Collect();

                // Assert
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteReader_NoCloses(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(TestConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                // Act
                // Run command, close nothing!
                readers[i] = commands[i].ExecuteReader();
                GC.SuppressFinalize(readers[i]);

                // Assert
                // MARS session for all previous commands should still be open
                // Sessions: 1 for connection, i+1 for previous commands (with 0-index offset)
                // Requests: i+1 for previous commands (with 0-index offset)
                AssertSessionsAndRequests(connection, expectedSessions: i + 2, expectedRequests: i + 1);
            }

            // @TODO: THIS POISONS THE POOL. IS THIS A BUG???
        }

        private static DisposableArray<SqlCommand> GetCommands(SqlConnection connection, CommandType commandType)
        {
            SqlCommand[] result = new SqlCommand[ConcurrentCommands];
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

            return new DisposableArray<SqlCommand>(result);
        }

        private void AssertSessionsAndRequests(SqlConnection connection, int expectedSessions, int expectedRequests)
        {
            using SqlCommand verificationCommand = new SqlCommand();
            verificationCommand.CommandText =
                "select count(*) as MarsSessionCount from sys.dm_exec_connections where session_id=@@spid and net_transport='Session'; " +
                "select count(*) as ActiveRequestCount from sys.dm_exec_requests where session_id=@@spid and (status='running' or status='suspended')";
            verificationCommand.CommandType = CommandType.Text;
            verificationCommand.Connection = connection;

            using SqlDataReader reader = verificationCommand.ExecuteReader();

            // Result 1) Count of active MARS sessions from sys.dm_exec_connections
            if (!reader.Read())
            {
                throw new Exception("Expected dm_exec_connections results from verification command");
            }

            // Add 1 for the verification command executing
            Assert.Equal(expectedSessions + 1, reader.GetInt32(0));

            // Result 2) Count of active requests from sys.dm_exec_requests
            if (!reader.NextResult() || !reader.Read())
            {
                throw new Exception("Expected dm_exec_requests results from verification command");
            }

            // Add 1 for the verification command executing
            Assert.Equal(expectedRequests + 1, reader.GetInt32(0));
        }

        private static WeakReference OpenReaderThenNullify(SqlCommand command)
        {
            SqlDataReader reader = command.ExecuteReader();
            WeakReference weak = new WeakReference(reader);
            reader = null;
            return weak;
        }
    }
}
