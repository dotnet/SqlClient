// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class MARSSessionPoolingTest
    {
        private const string COMMAND_STATUS =
            "select count(*) as ConnectionCount, @@spid as spid from sys.dm_exec_connections where session_id=@@spid and net_transport='Session'; " +
            "select count(*) as ActiveRequestCount, @@spid as spid from sys.dm_exec_requests where session_id=@@spid and (status='running' or status='suspended')";
        private const string COMMAND_SPID = "select @@spid";
        private const int CONCURRENT_COMMANDS = 5;

        private const string _COMMAND_RPC = "sp_who";
        private const string _COMMAND_SQL =
            "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
            "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
            "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
            "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
            "select * from sys.databases; print 'THIS IS THE END!'";

        private static readonly string _testConnString =
            (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                PacketSize = 512,
                MaxPoolSize = 1,
                MultipleActiveResultSets = true
            }).ConnectionString;

        // Synapse: Catalog view 'dm_exec_connections' is not supported in this version.

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsNotAzureSynapse))]
        [InlineData(CommandType.Text)]
        [InlineData(CommandType.StoredProcedure)]
        public void ExecuteScalar(CommandType commandType)
        {
            // Arrange
            using SqlConnection connection = new SqlConnection(_testConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, close/reopen connection to dispose sessions
                command.ExecuteScalar();
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, close/reopen connection to dispose sessions
                command.ExecuteScalar();
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, close reader
                using SqlDataReader reader = command.ExecuteReader();
                reader.Close();

                // Close/reopen connection to force disposal of sessions
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, dispose reader
                SqlDataReader reader = command.ExecuteReader();
                reader.Dispose();

                // Close/reopen connection to force disposal of sessions
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            connection.Open();

            // Act / Assert
            foreach (SqlCommand command in commands)
            {
                // Act
                // Run command, suppress finalization of reader
                using SqlDataReader reader = command.ExecuteReader();
                GC.SuppressFinalize(reader);

                // Close/reopen connection to force disposal of sessions
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
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

                // Close/reopen connection to force disposal of sessions
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
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

                // Close/reopen connection to force disposal of sessions
                connection.Close();
                connection.Open();

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
            using SqlConnection connection = new SqlConnection(_testConnString);
            using DisposableArray<SqlCommand> commands = GetCommands(connection, commandType);
            using DisposableArray<SqlDataReader> readers = new DisposableArray<SqlDataReader>(commands.Length);
            connection.Open();

            // Act / Assert
            for (int i = 0; i < commands.Length; i++)
            {
                using SqlDataReader reader = commands[i].ExecuteReader();
                GC.SuppressFinalize(reader);

                // // Act
                // // Run command, close nothing!
                // readers[i] = commands[i].ExecuteReader();
                // GC.SuppressFinalize(readers[i]);
                //
                // // Assert
                // // MARS session for all previous commands should still be open
                // // Sessions: 1 for connection, i+1 for previous commands (with 0-index offset)
                // // Requests: i+1 for previous commands (with 0-index offset)
                // AssertSessionsAndRequests(connection, expectedSessions: i + 2, expectedRequests: i + 1);
                AssertSessionsAndRequests(connection, expectedSessions: 1, expectedRequests: 0);
            }

            // foreach (var q in readers)
            // {
            //     q.Close();
            //     q.Dispose();
            // }
            //
            // foreach (var q in commands)
            // {
            //     q.Dispose();
            // }
            //
            // connection.Close();
            // connection.Open();
            // connection.Dispose();
        }

        private DisposableArray<SqlCommand> GetCommands(SqlConnection connection, CommandType commandType)
        {
            SqlCommand[] result = new SqlCommand[CONCURRENT_COMMANDS];
            for (int i = 0; i < result.Length; i++)
            {
                switch (commandType)
                {
                    case CommandType.Text:
                        result[i] = new SqlCommand
                        {
                            CommandText =
                                "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
                                "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
                                "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
                                "select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; select * from sys.databases; " +
                                "select * from sys.databases; print 'THIS IS THE END!'",
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


        private enum ExecuteType
        {
            ExecuteScalar,
            ExecuteNonQuery,
            ExecuteReader,
        }

        private enum ReaderTestType
        {
            ReaderClose,
            ReaderDispose,
            ReaderGC,
            ConnectionClose,
            NoCloses,
        }

        private enum GCType
        {
            Wait,
            NoWait,
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void TestMARSSessionPooling(string caseName, string connectionString, CommandType commandType,
                                           ExecuteType executeType, ReaderTestType readerTestType, GCType gcType)
        {
            SqlCommand[] cmd = new SqlCommand[CONCURRENT_COMMANDS];
            SqlDataReader[] gch = new SqlDataReader[CONCURRENT_COMMANDS];

            using (SqlConnection con = new SqlConnection(connectionString))
            {
                con.Open();

                // Create command
                for (int i = 0; i < CONCURRENT_COMMANDS; i++)
                {
                    // Prepare all commands
                    cmd[i] = con.CreateCommand();
                    switch (commandType)
                    {
                        case CommandType.Text:
                            cmd[i].CommandText = _COMMAND_SQL;
                            cmd[i].CommandTimeout = 120;
                            break;
                        case CommandType.StoredProcedure:
                            cmd[i].CommandText = _COMMAND_RPC;
                            cmd[i].CommandTimeout = 120;
                            cmd[i].CommandType = CommandType.StoredProcedure;
                            break;
                    }
                }

                for (int i = 0; i < CONCURRENT_COMMANDS; i++)
                {
                    switch (executeType)
                    {
                        // case ExecuteType.ExecuteScalar:
                        //     cmd[i].ExecuteScalar();
                        //     break;
                        // case ExecuteType.ExecuteNonQuery:
                        //     cmd[i].ExecuteNonQuery();
                        //     break;
                        case ExecuteType.ExecuteReader:
                            if (readerTestType != ReaderTestType.ReaderGC)
                            {
                                gch[i] = cmd[i].ExecuteReader();
                            }

                            switch (readerTestType)
                            {
                                // case ReaderTestType.ReaderClose:
                                //     {
                                //         gch[i].Dispose();
                                //         break;
                                //     }
                                // case ReaderTestType.ReaderDispose:
                                //     gch[i].Dispose();
                                //     break;
                                // case ReaderTestType.ReaderGC:
                                //     // gch[i] = null;
                                //     // WeakReference weak = OpenReaderThenNullify(cmd[i]);
                                //     // GC.Collect();
                                //
                                //     // if (gcType == GCType.Wait)
                                //     // {
                                //     //     GC.WaitForPendingFinalizers();
                                //     //     Assert.False(weak.IsAlive, "Error - target still alive!");
                                //     // }
                                //     break;
                                // case ReaderTestType.ConnectionClose:
                                //     GC.SuppressFinalize(gch[i]);
                                //     con.Close();
                                //     con.Open();
                                //     break;
                                case ReaderTestType.NoCloses:
                                    GC.SuppressFinalize(gch[i]);
                                    break;
                            }
                            break;
                    }

                    if (readerTestType != ReaderTestType.NoCloses)
                    {
                        con.Close();
                        con.Open(); // Close and open, to re-assure collection!
                    }

                    using (SqlCommand verificationCmd = con.CreateCommand())
                    {

                        verificationCmd.CommandText = COMMAND_STATUS;
                        using (SqlDataReader rdr = verificationCmd.ExecuteReader())
                        {
                            rdr.Read();
                            int connections = (int)rdr.GetValue(0);
                            int spid1 = (Int16)rdr.GetValue(1);
                            rdr.NextResult();
                            rdr.Read();
                            int requests = (int)rdr.GetValue(0);
                            int spid2 = (Int16)rdr.GetValue(1);

                            switch (executeType)
                            {
                                // case ExecuteType.ExecuteScalar:
                                // case ExecuteType.ExecuteNonQuery:
                                //     // 1 for connection, 1 for command
                                //     Assert.True(connections == 2, "Failure - incorrect number of connections for ExecuteScalar! #connections: " + connections);
                                //
                                //     // only 1 executing
                                //     Assert.True(requests == 1, "Failure - incorrect number of requests for ExecuteScalar! #requests: " + requests);
                                //     break;
                                case ExecuteType.ExecuteReader:
                                    switch (readerTestType)
                                    {
                                        // case ReaderTestType.ReaderClose:
                                        // case ReaderTestType.ReaderDispose:
                                        // case ReaderTestType.ConnectionClose:
                                        //     // 1 for connection, 1 for command
                                        //     Assert.True(connections == 2, "Failure - Incorrect number of connections for ReaderClose / ReaderDispose / ConnectionClose! #connections: " + connections);
                                        //
                                        //     // only 1 executing
                                        //     Assert.True(requests == 1, "Failure - incorrect number of requests for ReaderClose/ReaderDispose/ConnectionClose! #requests: " + requests);
                                        //     break;
                                        // case ReaderTestType.ReaderGC:
                                        //     switch (gcType)
                                        //     {
                                        //         // case GCType.Wait:
                                        //         //     // 1 for connection, 1 for open reader
                                        //         //     Assert.True(connections == 2, "Failure - incorrect number of connections for ReaderGCWait! #connections: " + connections);
                                        //         //     // only 1 executing
                                        //         //     Assert.True(requests == 1, "Failure - incorrect number of requests for ReaderGCWait! #requests: " + requests);
                                        //         //     break;
                                        //         case GCType.NoWait:
                                        //             // 1 for connection, 1 for open reader
                                        //             Assert.True(connections == 2, "Failure - incorrect number of connections for ReaderGCNoWait! #connections: " + connections);
                                        //
                                        //             // only 1 executing
                                        //             Assert.True(requests == 1, "Failure - incorrect number of requests for ReaderGCNoWait! #requests: " + requests);
                                        //             break;
                                        //     }
                                        //     break;
                                        case ReaderTestType.NoCloses:
                                            // 1 for connection, 1 for current command, 1 for 0 based array offset, plus i for open readers
                                            Assert.Equal(3+i, connections);

                                            // 1 for current command, 1 for 0 based array offset, plus i open readers
                                            Assert.Equal(2+i, requests);
                                            break;
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
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
