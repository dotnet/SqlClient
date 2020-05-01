// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlExceptionTest
    {
        private const string badServer = "92B96911A0BD43E8ADA4451031F7E7CF";

        [Fact]
        [ActiveIssue("12161", TestPlatforms.AnyUnix)]
        public void SerializationTest()
        {
            SqlException e = CreateException();
            string json = JsonConvert.SerializeObject(e);

            var settings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.All,
            };

            // TODO: Deserialization fails on Unix with "Member 'ClassName' was not found."
            var sqlEx = JsonConvert.DeserializeObject<SqlException>(json, settings);

            Assert.Equal(e.ClientConnectionId, sqlEx.ClientConnectionId);
            Assert.Equal(e.StackTrace, sqlEx.StackTrace);
        }

        [Fact]
        public void JSONSerializationTest()
        {
            string clientConnectionId = "90cdab4d-2145-4c24-a354-c8ccff903542";
            string json = @"{""ClassName"":""Microsoft.Data.SqlClient.SqlException"","
                        + @"""Message"":""A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: TCP Provider, error: 40 - Could not open a connection to SQL Server)"","
                        + @"""Data"":{""HelpLink.ProdName"":""Microsoft SQL Server"","
                        + @"""HelpLink.EvtSrc"":""MSSQLServer"","
                        + @"""HelpLink.EvtID"":""0"","
                        + @"""HelpLink.BaseHelpUrl"":""http://go.microsoft.com/fwlink"","
                        + @"""HelpLink.LinkId"":""20476"","
                        + @"""SqlError 1"":""Microsoft.Data.SqlClient.SqlError: A network-related or instance-specific error occurred while establishing a connection to SQL Server. The server was not found or was not accessible. Verify that the instance name is correct and that SQL Server is configured to allow remote connections. (provider: TCP Provider, error: 40 - Could not open a connection to SQL Server)"","
                        + @"""$type"":""System.Collections.ListDictionaryInternal, System.Private.CoreLib""},"
                        + @"""InnerException"":null,"
                        + @"""HelpURL"":null,"
                        + @"""StackTraceString"":""   at Microsoft.Data.SqlClient.SqlInternalConnectionTds..ctor(DbConnectionPoolIdentity identity, SqlConnectionString connectionOptions, SqlCredential credential, Object providerInfo, String newPassword, SecureString newSecurePassword, Boolean redirectedUserInstance, SqlConnectionString userConnectionOptions, SessionData reconnectSessionData, Boolean applyTransientFaultHandling, String accessToken)\\n   at Microsoft.Data.SqlClient.SqlConnectionFactory.CreateConnection(DbConnectionOptions options, DbConnectionPoolKey poolKey, Object poolGroupProviderInfo, DbConnectionPool pool, DbConnection owningConnection, DbConnectionOptions userOptions)\\n   at System.Data.ProviderBase.DbConnectionFactory.CreatePooledConnection(DbConnectionPool pool, DbConnection owningObject, DbConnectionOptions options, DbConnectionPoolKey poolKey, DbConnectionOptions userOptions)\\n   at System.Data.ProviderBase.DbConnectionPool.CreateObject(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)\\n   at System.Data.ProviderBase.DbConnectionPool.UserCreateRequest(DbConnection owningObject, DbConnectionOptions userOptions, DbConnectionInternal oldConnection)\\n   at System.Data.ProviderBase.DbConnectionPool.TryGetConnection(DbConnection owningObject, UInt32 waitForMultipleObjectsTimeout, Boolean allowCreate, Boolean onlyOneCheckConnection, DbConnectionOptions userOptions, DbConnectionInternal& connection)\\n   at System.Data.ProviderBase.DbConnectionPool.WaitForPendingOpen()\\n"","
                        + @"""RemoteStackTraceString"":null,"
                        + @"""RemoteStackIndex"":0,"
                        + @"""ExceptionMethod"":null,"
                        + @"""HResult"":-2146232060,"
                        + @"""Source"":""Core .Net SqlClient Data Provider"","
                        + @"""WatsonBuckets"":null,"
                        + @"""Errors"":null,"
                        + @"""ClientConnectionId"":""90cdab4d-2145-4c24-a354-c8ccff903542""}";

            var settings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.All,
            };

            var sqlEx = JsonConvert.DeserializeObject<SqlException>(json, settings);
            Assert.IsType<SqlException>(sqlEx);
            Assert.Equal(clientConnectionId, sqlEx.ClientConnectionId.ToString());
        }

        private static SqlException CreateException()
        {
            var builder = new SqlConnectionStringBuilder()
            {
                DataSource = badServer,
                ConnectTimeout = 1,
                Pooling = false
            };

            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                try
                {
                    connection.Open();
                }
                catch (SqlException ex)
                {
                    Assert.NotNull(ex.Errors);
                    Assert.Single(ex.Errors);

                    return ex;
                }
            }
            throw new InvalidOperationException("SqlException should have been returned.");
        }
    }
}
