// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO(ADO-39873): Re-enable these tests after addressing their flakiness.
//
// Note that xUnit v2 has no built-in way to skip an entire test class, so we
// use the preprocessor instead.
#if false

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.Servers;
using Microsoft.SqlServer.TDS.SQLBatch;
using Xunit;
using System.Runtime.CompilerServices;
using System;
using System.Data;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.DotNet.RemoteExecutor;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class DiagnosticTest
    {
        private const string BadConnectionString = "data source = bad; initial catalog = bad; integrated security = true; connection timeout = 1;";

        private const string WriteCommandBefore = "Microsoft.Data.SqlClient.WriteCommandBefore";
        private const string WriteCommandAfter = "Microsoft.Data.SqlClient.WriteCommandAfter";
        private const string WriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";
        private const string WriteConnectionOpenBefore = "Microsoft.Data.SqlClient.WriteConnectionOpenBefore";
        private const string WriteConnectionOpenAfter = "Microsoft.Data.SqlClient.WriteConnectionOpenAfter";
        private const string WriteConnectionOpenError = "Microsoft.Data.SqlClient.WriteConnectionOpenError";
        private const string WriteConnectionCloseBefore = "Microsoft.Data.SqlClient.WriteConnectionCloseBefore";
        private const string WriteConnectionCloseAfter = "Microsoft.Data.SqlClient.WriteConnectionCloseAfter";
        private const string WriteConnectionCloseError = "Microsoft.Data.SqlClient.WriteConnectionCloseError";

        [Fact]
        public void ExecuteScalarTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        cmd.ExecuteScalar();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteScalarErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT 1 / 0;";

                        conn.Open();
                        Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteNonQueryTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        cmd.ExecuteNonQuery();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteNonQueryErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT 1 / 0;";

                        // Limiting the command timeout to 3 seconds. This should be lower than the Process timeout.
                        cmd.CommandTimeout = 3;
                        conn.Open();

                        Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteReaderTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        reader.FlushResultSet();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteReaderErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT 1 / 0;";

                        conn.Open();
                        // @TODO: TestTdsServer should not throw on ExecuteReader, it should throw on reader.Read
                        Assert.Throws<SqlException>(() => cmd.ExecuteReader());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteReaderWithCommandBehaviorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default);
                        reader.FlushResultSet();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        // Synapse: Parse error at line: 1, column: 27: Incorrect syntax near 'for'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void ExecuteXmlReaderTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(_ =>
                {
                    // @TODO: Test TDS server doesn't support ExecuteXmlReader, so connect to real server as workaround
                    using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT TOP 10 * FROM sys.objects FOR xml auto, xmldata;";

                        conn.Open();
                        XmlReader reader = cmd.ExecuteXmlReader();
                        while (reader.Read())
                        {
                            // Flush results
                        }
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteXmlReaderErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT *, baddata = 1 / 0 FROM sys.objects FOR xml auto, xmldata;";

                        conn.Open();
                        // @TODO: TestTdsServer should not throw on ExecuteXmlReader, should throw on reader.Read
                        Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteScalarAsyncTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(connectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        await cmd.ExecuteScalarAsync();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteScalarAsyncErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(connectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT 1 / 0;";

                        conn.Open();
                        await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteNonQueryAsyncTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(connectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        await cmd.ExecuteNonQueryAsync();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteNonQueryAsyncErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(connectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT 1 / 0;";

                        conn.Open();
                        await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteReaderAsyncTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(connectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();";

                        conn.Open();
                        SqlDataReader reader = await cmd.ExecuteReaderAsync();
                        reader.FlushResultSet();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteReaderAsyncErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(connectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT 1 / 0;";

                        conn.Open();
                        // @TODO: TestTdsServer should not throw on ExecuteReader, should throw on reader.Read
                        await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteReaderAsync());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        // Synapse:  Parse error at line: 1, column: 27: Incorrect syntax near 'for'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void ExecuteXmlReaderAsyncTest()
        {
            // @TODO: TestTdsServer does not handle xml reader, so connect to a real server as a workaround
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async _ =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "SELECT TOP 10 * FROM sys.objects FOR xml auto, xmldata;";

                        conn.Open();
                        XmlReader reader = await cmd.ExecuteXmlReaderAsync();
                        while (reader.Read())
                        {
                            // Flush results
                        }
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ExecuteXmlReaderAsyncErrorTest()
        {
            // @TODO: TestTdsServer does not handle xml reader, so connect to a real server as a workaround
            RemoteExecutor.Invoke(() =>
            {

                CollectStatisticsDiagnosticsAsync(async _ =>
                {
#if NET
                    await using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
                    await using (SqlCommand cmd = new SqlCommand())
#else
                    using (SqlConnection conn = new SqlConnection(DataTestUtility.TCPConnectionString))
                    using (SqlCommand cmd = new SqlCommand())
#endif
                    {
                        cmd.Connection = conn;
                        cmd.CommandText = "select *, baddata = 1 / 0 from sys.objects for xml auto, xmldata;";

                        // @TODO: Since this test uses a real database connection, the exception is
                        //     thrown during reader.Read. (ie, TestTdsServer does not obey proper
                        //     exception behavior)
                        // NB: As a result of the exception being thrown during reader.Read, 
                        // cmd.ExecuteXmlReaderAsync returns successfully. This means that we receive
                        // a WriteCommandAfter event rather than WriteCommandError.
                        await conn.OpenAsync();
                        XmlReader reader = await cmd.ExecuteXmlReaderAsync();
                        await Assert.ThrowsAsync<SqlException>(() => reader.ReadAsync());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ConnectionOpenTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(connectionString =>
                {
                    using (SqlConnection sqlConnection = new SqlConnection(connectionString))
                    {
                        sqlConnection.Open();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ConnectionOpenErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnostics(_ =>
                {
                    using (SqlConnection sqlConnection = new SqlConnection(BadConnectionString))
                    {
                        Assert.Throws<SqlException>(() => sqlConnection.Open());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenError]);
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ConnectionOpenAsyncTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async connectionString =>
                {
#if NET
                    await using (SqlConnection sqlConnection = new SqlConnection(connectionString))
#else
                    using (SqlConnection sqlConnection = new SqlConnection(connectionString))
#endif
                    {
                        await sqlConnection.OpenAsync();
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        [Fact]
        public void ConnectionOpenAsyncErrorTest()
        {
            RemoteExecutor.Invoke(() =>
            {
                CollectStatisticsDiagnosticsAsync(async _ =>
                {
#if NET
                    await using (SqlConnection sqlConnection = new SqlConnection(BadConnectionString))
#else
                    using (SqlConnection sqlConnection = new SqlConnection(BadConnectionString))
#endif
                    {
                        await Assert.ThrowsAsync<SqlException>(() => sqlConnection.OpenAsync());
                    }
                }, [WriteConnectionOpenBefore, WriteConnectionOpenError]).Wait();
                return RemoteExecutor.SuccessExitCode;
            }).Dispose();
        }

        private static void CollectStatisticsDiagnostics(Action<string> sqlOperation, string[] expectedDiagnostics, [CallerMemberName] string methodName = "")
        {
            bool statsLogged = false;
            bool operationHasError = false;
            Guid beginOperationId = Guid.Empty;

            FakeDiagnosticListenerObserver diagnosticListenerObserver = new FakeDiagnosticListenerObserver(kvp =>
                {
                    IDictionary statistics;

                    if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteCommandBefore"))
                    {
                        Assert.NotNull(kvp.Value);

                        Guid retrievedOperationId = GetPropertyValueFromType<Guid>(kvp.Value, "OperationId");
                        Assert.NotEqual(retrievedOperationId, Guid.Empty);

                        SqlCommand sqlCommand = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                        Assert.NotNull(sqlCommand);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        if (sqlCommand.Connection.State == ConnectionState.Open)
                        {
                            Assert.NotEqual(connectionId, Guid.Empty);
                        }

                        beginOperationId = retrievedOperationId;

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteCommandAfter"))
                    {
                        Assert.NotNull(kvp.Value);

                        Guid retrievedOperationId = GetPropertyValueFromType<Guid>(kvp.Value, "OperationId");
                        Assert.NotEqual(retrievedOperationId, Guid.Empty);

                        SqlCommand sqlCommand = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                        Assert.NotNull(sqlCommand);

                        statistics = GetPropertyValueFromType<IDictionary>(kvp.Value, "Statistics");
                        if (!operationHasError)
                        {
                            Assert.NotNull(statistics);
                        }

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        if (sqlCommand.Connection.State == ConnectionState.Open)
                        {
                            Assert.NotEqual(connectionId, Guid.Empty);
                        }

                        // if we get to this point, then statistics exist and this must be the "end" 
                        // event, so we need to make sure the operation IDs match
                        Assert.Equal(retrievedOperationId, beginOperationId);
                        beginOperationId = Guid.Empty;

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteCommandError"))
                    {
                        operationHasError = true;
                        Assert.NotNull(kvp.Value);

                        SqlCommand sqlCommand = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                        Assert.NotNull(sqlCommand);

                        Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                        Assert.NotNull(ex);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        if (sqlCommand.Connection.State == ConnectionState.Open)
                        {
                            Assert.NotEqual(connectionId, Guid.Empty);
                        }

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionOpenBefore"))
                    {
                        Assert.NotNull(kvp.Value);

                        SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                        Assert.NotNull(sqlConnection);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionOpenAfter"))
                    {
                        Assert.NotNull(kvp.Value);

                        SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                        Assert.NotNull(sqlConnection);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        statistics = GetPropertyValueFromType<IDictionary>(kvp.Value, "Statistics");
                        Assert.NotNull(statistics);

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        Assert.NotEqual(connectionId, Guid.Empty);

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionOpenError"))
                    {
                        Assert.NotNull(kvp.Value);

                        SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                        Assert.NotNull(sqlConnection);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                        Assert.NotNull(ex);

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionCloseBefore"))
                    {
                        Assert.NotNull(kvp.Value);

                        SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                        Assert.NotNull(sqlConnection);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        Assert.NotEqual(connectionId, Guid.Empty);

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionCloseAfter"))
                    {
                        Assert.NotNull(kvp.Value);

                        SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                        Assert.NotNull(sqlConnection);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        statistics = GetPropertyValueFromType<IDictionary>(kvp.Value, "Statistics");

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        Assert.NotEqual(connectionId, Guid.Empty);

                        statsLogged = true;
                    }
                    else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionCloseError"))
                    {
                        Assert.NotNull(kvp.Value);

                        SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                        Assert.NotNull(sqlConnection);

                        string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                        Assert.False(string.IsNullOrWhiteSpace(operation));

                        Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                        Assert.NotNull(ex);

                        Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                        Assert.NotEqual(connectionId, Guid.Empty);

                        statsLogged = true;
                    }
                });

            diagnosticListenerObserver.Enable();
            using (DiagnosticListener.AllListeners.Subscribe(diagnosticListenerObserver))
            {

                Console.WriteLine(string.Format("Test: {0} Enabled Listeners", methodName));

                using (var server = new TdsServer(new DiagnosticsQueryEngine(), new TdsServerArguments()))
                {
                    server.Start(methodName);
                    Console.WriteLine(string.Format("Test: {0} Started Server", methodName));

                    var connectionString = new SqlConnectionStringBuilder
                    {
                        DataSource = $"localhost,{server.EndPoint.Port}",
                        Encrypt = SqlConnectionEncryptOption.Optional
                    }.ConnectionString;

                    sqlOperation(connectionString);

                    Console.WriteLine(string.Format("Test: {0} SqlOperation Successful", methodName));

                    Assert.True(statsLogged);

                    diagnosticListenerObserver.Disable();
                    foreach (string expected in expectedDiagnostics)
                    {
                        Assert.True(diagnosticListenerObserver.HasReceivedDiagnostic(expected), $"Missing diagnostic '{expected}'");
                    }

                    Console.WriteLine(string.Format("Test: {0} Listeners Disabled", methodName));
                }
                Console.WriteLine(string.Format("Test: {0} Server Disposed", methodName));
            }
            Console.WriteLine(string.Format("Test: {0} Listeners Disposed Successfully", methodName));
        }

        private static async Task CollectStatisticsDiagnosticsAsync(Func<string, Task> sqlOperation, string[] expectedDiagnostics, [CallerMemberName] string methodName = "")
        {
            bool statsLogged = false;
            bool operationHasError = false;
            Guid beginOperationId = Guid.Empty;

            FakeDiagnosticListenerObserver diagnosticListenerObserver = new FakeDiagnosticListenerObserver(kvp =>
            {
                IDictionary statistics;

                if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteCommandBefore"))
                {
                    Assert.NotNull(kvp.Value);

                    Guid retrievedOperationId = GetPropertyValueFromType<Guid>(kvp.Value, "OperationId");
                    Assert.NotEqual(retrievedOperationId, Guid.Empty);

                    SqlCommand sqlCommand = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                    Assert.NotNull(sqlCommand);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    beginOperationId = retrievedOperationId;

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteCommandAfter"))
                {
                    Assert.NotNull(kvp.Value);

                    Guid retrievedOperationId = GetPropertyValueFromType<Guid>(kvp.Value, "OperationId");
                    Assert.NotEqual(retrievedOperationId, Guid.Empty);

                    SqlCommand sqlCommand = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                    Assert.NotNull(sqlCommand);

                    statistics = GetPropertyValueFromType<IDictionary>(kvp.Value, "Statistics");
                    if (!operationHasError)
                    {
                        Assert.NotNull(statistics);
                    }

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    // if we get to this point, then statistics exist and this must be the "end" 
                    // event, so we need to make sure the operation IDs match
                    Assert.Equal(retrievedOperationId, beginOperationId);
                    beginOperationId = Guid.Empty;

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteCommandError"))
                {
                    operationHasError = true;
                    Assert.NotNull(kvp.Value);

                    SqlCommand sqlCommand = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                    Assert.NotNull(sqlCommand);

                    Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                    Assert.NotNull(ex);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionOpenBefore"))
                {
                    Assert.NotNull(kvp.Value);

                    SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                    Assert.NotNull(sqlConnection);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionOpenAfter"))
                {
                    Assert.NotNull(kvp.Value);

                    SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                    Assert.NotNull(sqlConnection);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    statistics = GetPropertyValueFromType<IDictionary>(kvp.Value, "Statistics");
                    Assert.NotNull(statistics);

                    Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                    if (sqlConnection.State == ConnectionState.Open)
                    {
                        Assert.NotEqual(connectionId, Guid.Empty);
                    }

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionOpenError"))
                {
                    Assert.NotNull(kvp.Value);

                    SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                    Assert.NotNull(sqlConnection);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                    Assert.NotNull(ex);

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionCloseBefore"))
                {
                    Assert.NotNull(kvp.Value);

                    SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                    Assert.NotNull(sqlConnection);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                    Assert.NotEqual(connectionId, Guid.Empty);

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionCloseAfter"))
                {
                    Assert.NotNull(kvp.Value);

                    SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                    Assert.NotNull(sqlConnection);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    statistics = GetPropertyValueFromType<IDictionary>(kvp.Value, "Statistics");

                    Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                    Assert.NotEqual(connectionId, Guid.Empty);

                    statsLogged = true;
                }
                else if (kvp.Key.Equals("Microsoft.Data.SqlClient.WriteConnectionCloseError"))
                {
                    Assert.NotNull(kvp.Value);

                    SqlConnection sqlConnection = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                    Assert.NotNull(sqlConnection);

                    string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
                    Assert.False(string.IsNullOrWhiteSpace(operation));

                    Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                    Assert.NotNull(ex);

                    Guid connectionId = GetPropertyValueFromType<Guid>(kvp.Value, "ConnectionId");
                    Assert.NotEqual(connectionId, Guid.Empty);

                    statsLogged = true;
                }
            });

            diagnosticListenerObserver.Enable();
            using (DiagnosticListener.AllListeners.Subscribe(diagnosticListenerObserver))
            {
                Console.WriteLine(string.Format("Test: {0} Enabled Listeners", methodName));
                using (var server = new TdsServer(new DiagnosticsQueryEngine(), new TdsServerArguments()))
                {
                    server.Start(methodName);
                    Console.WriteLine(string.Format("Test: {0} Started Server", methodName));

                    var connectionString = new SqlConnectionStringBuilder
                    {
                        DataSource = $"localhost,{server.EndPoint.Port}",
                        Encrypt = SqlConnectionEncryptOption.Optional
                    }.ConnectionString;
                    await sqlOperation(connectionString);

                    Console.WriteLine(string.Format("Test: {0} SqlOperation Successful", methodName));

                    Assert.True(statsLogged);

                    diagnosticListenerObserver.Disable();
                    foreach (string expected in expectedDiagnostics)
                    {
                        Assert.True(diagnosticListenerObserver.HasReceivedDiagnostic(expected), $"Missing diagnostic '{expected}'");
                    }

                    Console.WriteLine(string.Format("Test: {0} Listeners Disabled", methodName));
                }
                Console.WriteLine(string.Format("Test: {0} Server Disposed", methodName));
            }
            Console.WriteLine(string.Format("Test: {0} Listeners Disposed Successfully", methodName));
        }

        private static T GetPropertyValueFromType<T>(object obj, string propName)
        {
            Type type = obj.GetType();
            PropertyInfo pi = type.GetRuntimeProperty(propName);

            var propertyValue = pi.GetValue(obj);
            return (T)propertyValue;
        }
    }

    public class DiagnosticsQueryEngine : QueryEngine
    {
        public DiagnosticsQueryEngine() : base(new TdsServerArguments())
        {
        }

        protected override TDSMessageCollection CreateQueryResponse(ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            string lowerBatchText = batchRequest.Text.ToLowerInvariant();

            if (lowerBatchText.Contains("1 / 0")) // SELECT 1/0 
            {
                TDSErrorToken errorToken = new TDSErrorToken(8134, 1, 16, "Divide by zero error encountered.");
                TDSDoneToken doneToken = new TDSDoneToken(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1);
                TDSMessage responseMessage = new TDSMessage(TDSMessageType.Response, errorToken, doneToken);
                return new TDSMessageCollection(responseMessage);
            }
            else
            {
                return base.CreateQueryResponse(session, batchRequest);
            }
        }
    }
}

#endif
