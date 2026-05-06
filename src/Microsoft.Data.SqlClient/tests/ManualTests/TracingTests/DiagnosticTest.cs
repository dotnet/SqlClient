// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Error;
using Microsoft.SqlServer.TDS.SQLBatch;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // Serialized execution: DiagnosticListener is global state, so these tests
    // must not run in parallel with each other.
    [Collection("DiagnosticTests")]
    public class DiagnosticTest
    {
        private const string WriteCommandBefore = "Microsoft.Data.SqlClient.WriteCommandBefore";
        private const string WriteCommandAfter = "Microsoft.Data.SqlClient.WriteCommandAfter";
        private const string WriteCommandError = "Microsoft.Data.SqlClient.WriteCommandError";
        private const string WriteConnectionOpenBefore = "Microsoft.Data.SqlClient.WriteConnectionOpenBefore";
        private const string WriteConnectionOpenAfter = "Microsoft.Data.SqlClient.WriteConnectionOpenAfter";
        private const string WriteConnectionOpenError = "Microsoft.Data.SqlClient.WriteConnectionOpenError";
        private const string WriteConnectionCloseBefore = "Microsoft.Data.SqlClient.WriteConnectionCloseBefore";
        private const string WriteConnectionCloseAfter = "Microsoft.Data.SqlClient.WriteConnectionCloseAfter";
        private const string BadConnectionString = "data source = bad; initial catalog = bad; integrated security = true; connection timeout = 1;";

        #region Sync tests

        [Fact]
        public void ExecuteScalarTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
                conn.Open();
                cmd.ExecuteScalar();
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteScalarErrorTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT 1 / 0;", conn);
                conn.Open();
                Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteNonQueryTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
                conn.Open();
                cmd.ExecuteNonQuery();
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteNonQueryErrorTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT 1 / 0;", conn);
                cmd.CommandTimeout = 3;
                conn.Open();
                Assert.Throws<SqlException>(() => cmd.ExecuteNonQuery());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteReaderTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
                conn.Open();
                using SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read()) { }
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteReaderErrorTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT 1 / 0;", conn);
                conn.Open();
                Assert.Throws<SqlException>(() => cmd.ExecuteReader());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteReaderWithCommandBehaviorTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
                conn.Open();
                using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior.Default);
                while (reader.Read()) { }
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteXmlReaderTest()
        {
            // The TDS test server does not return XML-formatted results, so
            // ExecuteXmlReader will throw. We verify the error diagnostic path.
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT TOP 10 * FROM sys.objects FOR xml auto, xmldata;", conn);
                conn.Open();
                Assert.Throws<InvalidOperationException>(() => cmd.ExecuteXmlReader());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ExecuteXmlReaderErrorTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT *, baddata = 1 / 0 FROM sys.objects FOR xml auto, xmldata;", conn);
                conn.Open();
                Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ConnectionOpenTest()
        {
            CollectStatisticsDiagnostics(connectionString =>
            {
                using SqlConnection conn = new(connectionString);
                conn.Open();
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public void ConnectionOpenErrorTest()
        {
            CollectStatisticsDiagnostics(_ =>
            {
                using SqlConnection conn = new(BadConnectionString);
                Assert.Throws<SqlException>(() => conn.Open());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenError]);
        }

        #endregion

        #region Async tests

        [Fact]
        public async Task ExecuteScalarAsyncTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
#endif
                conn.Open();
                await cmd.ExecuteScalarAsync();
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteScalarAsyncErrorTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT 1 / 0;", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT 1 / 0;", conn);
#endif
                conn.Open();
                await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteNonQueryAsyncTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
#endif
                conn.Open();
                await cmd.ExecuteNonQueryAsync();
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteNonQueryAsyncErrorTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT 1 / 0;", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT 1 / 0;", conn);
#endif
                conn.Open();
                await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteNonQueryAsync());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteReaderAsyncTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT [name], [state] FROM [sys].[databases] WHERE [name] = db_name();", conn);
#endif
                conn.Open();
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();
                while (reader.Read()) { }
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteReaderAsyncErrorTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT 1 / 0;", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT 1 / 0;", conn);
#endif
                conn.Open();
                await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteReaderAsync());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteXmlReaderAsyncTest()
        {
            // The TDS test server does not return XML-formatted results, so
            // ExecuteXmlReaderAsync will throw. We verify the error diagnostic path.
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT TOP 10 * FROM sys.objects FOR xml auto, xmldata;", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT TOP 10 * FROM sys.objects FOR xml auto, xmldata;", conn);
#endif
                conn.Open();
                await Assert.ThrowsAsync<InvalidOperationException>(() => cmd.ExecuteXmlReaderAsync());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ExecuteXmlReaderAsyncErrorTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
                await using SqlCommand cmd = new("SELECT *, baddata = 1 / 0 FROM sys.objects FOR xml auto, xmldata;", conn);
#else
                using SqlConnection conn = new(connectionString);
                using SqlCommand cmd = new("SELECT *, baddata = 1 / 0 FROM sys.objects FOR xml auto, xmldata;", conn);
#endif
                conn.Open();
                Assert.Throws<SqlException>(() => cmd.ExecuteXmlReader());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteCommandBefore, WriteCommandError, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ConnectionOpenAsyncTest()
        {
            await CollectStatisticsDiagnosticsAsync(async connectionString =>
            {
#if NET
                await using SqlConnection conn = new(connectionString);
#else
                using SqlConnection conn = new(connectionString);
#endif
                await conn.OpenAsync();
            }, [WriteConnectionOpenBefore, WriteConnectionOpenAfter, WriteConnectionCloseBefore, WriteConnectionCloseAfter]);
        }

        [Fact]
        public async Task ConnectionOpenAsyncErrorTest()
        {
            await CollectStatisticsDiagnosticsAsync(async _ =>
            {
#if NET
                await using SqlConnection conn = new(BadConnectionString);
#else
                using SqlConnection conn = new(BadConnectionString);
#endif
                await Assert.ThrowsAsync<SqlException>(() => conn.OpenAsync());
            }, [WriteConnectionOpenBefore, WriteConnectionOpenError]);
        }

        #endregion

        #region Helpers

        private static void CollectStatisticsDiagnostics(
            Action<string> sqlOperation,
            string[] expectedDiagnostics,
            [CallerMemberName] string methodName = "")
        {
            bool statsLogged = false;
            FakeDiagnosticListenerObserver observer = new(kvp =>
            {
                ValidateDiagnosticPayload(kvp);
                statsLogged = true;
            });

            observer.Enable();
            using (DiagnosticListener.AllListeners.Subscribe(observer))
            using (var server = new TdsServer(new DiagnosticsQueryEngine(), new TdsServerArguments()))
            {
                server.Start(methodName);
                string connectionString = new SqlConnectionStringBuilder
                {
                    DataSource = $"localhost,{server.EndPoint.Port}",
                    Encrypt = SqlConnectionEncryptOption.Optional
                }.ConnectionString;

                sqlOperation(connectionString);

                Assert.True(statsLogged, "Expected at least one diagnostic event");
                observer.Disable();

                foreach (string expected in expectedDiagnostics)
                {
                    Assert.True(observer.HasReceivedDiagnostic(expected), $"Missing diagnostic '{expected}'");
                }
            }
        }

        private static async Task CollectStatisticsDiagnosticsAsync(
            Func<string, Task> sqlOperation,
            string[] expectedDiagnostics,
            [CallerMemberName] string methodName = "")
        {
            bool statsLogged = false;
            FakeDiagnosticListenerObserver observer = new(kvp =>
            {
                ValidateDiagnosticPayload(kvp);
                statsLogged = true;
            });

            observer.Enable();
            using (DiagnosticListener.AllListeners.Subscribe(observer))
            using (var server = new TdsServer(new DiagnosticsQueryEngine(), new TdsServerArguments()))
            {
                server.Start(methodName);
                string connectionString = new SqlConnectionStringBuilder
                {
                    DataSource = $"localhost,{server.EndPoint.Port}",
                    Encrypt = SqlConnectionEncryptOption.Optional
                }.ConnectionString;

                await sqlOperation(connectionString);

                Assert.True(statsLogged, "Expected at least one diagnostic event");
                observer.Disable();

                foreach (string expected in expectedDiagnostics)
                {
                    Assert.True(observer.HasReceivedDiagnostic(expected), $"Missing diagnostic '{expected}'");
                }
            }
        }

        private static void ValidateDiagnosticPayload(KeyValuePair<string, object> kvp)
        {
            Assert.NotNull(kvp.Value);

            if (kvp.Key.Contains("Command"))
            {
                SqlCommand cmd = GetPropertyValueFromType<SqlCommand>(kvp.Value, "Command");
                Assert.NotNull(cmd);
            }
            else if (kvp.Key.Contains("Connection"))
            {
                SqlConnection conn = GetPropertyValueFromType<SqlConnection>(kvp.Value, "Connection");
                Assert.NotNull(conn);
            }

            if (kvp.Key.Contains("Error"))
            {
                Exception ex = GetPropertyValueFromType<Exception>(kvp.Value, "Exception");
                Assert.NotNull(ex);
            }

            string operation = GetPropertyValueFromType<string>(kvp.Value, "Operation");
            Assert.False(string.IsNullOrWhiteSpace(operation));
        }

        private static T GetPropertyValueFromType<T>(object obj, string propName)
        {
            Type type = obj.GetType();
            PropertyInfo pi = type.GetRuntimeProperty(propName);
            return (T)pi.GetValue(obj);
        }

        #endregion
    }

    /// <summary>
    /// Query engine that handles error queries (division by zero).
    /// </summary>
    public class DiagnosticsQueryEngine : QueryEngine
    {
        public DiagnosticsQueryEngine() : base(new TdsServerArguments()) { }

        protected override TDSMessageCollection CreateQueryResponse(ITDSServerSession session, TDSSQLBatchToken batchRequest)
        {
            string lowerBatchText = batchRequest.Text.ToLowerInvariant();
            if (lowerBatchText.Contains("1 / 0"))
            {
                TDSErrorToken errorToken = new(8134, 1, 16, "Divide by zero error encountered.");
                TDSDoneToken doneToken = new(TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count, TDSDoneTokenCommandType.Select, 1);
                TDSMessage responseMessage = new(TDSMessageType.Response, errorToken, doneToken);
                return new TDSMessageCollection(responseMessage);
            }
            return base.CreateQueryResponse(session, batchRequest);
        }
    }
}
