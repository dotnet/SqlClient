// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class IntegratedAuthenticationTest
    {
        private static bool IsIntegratedSecurityEnvironmentSet() => DataTestUtility.IsIntegratedSecuritySetup();

        private static bool AreConnectionStringsSetup() => DataTestUtility.AreConnStringsSetup();

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTestWithConnectionPooling()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            builder.Pooling = true;
            TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString);
        }

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTestWithOutConnectionPooling()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            builder.Pooling = false;
            TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString);
        }

        [ActiveIssue("21707")]
        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTest_InvalidServerSPN()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            builder.ServerSPN = "InvalidServerSPN";
            SqlException ex = Assert.Throws<SqlException>(() => TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString));
            Assert.Contains("generate SSPI context.", ex.Message);
        }

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void IntegratedAuthenticationTest_ServerSPN()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out int port, out string instanceName));
            // Build the SPN for the server we are connecting to
            builder.ServerSPN = $"MSSQLSvc/{DataTestUtility.GetMachineFQDN(hostname)}";
            if (!string.IsNullOrWhiteSpace(instanceName))
            {
                builder.ServerSPN += ":" + instanceName;
            }
            TryOpenConnectionWithIntegratedAuthentication(builder.ConnectionString);
        }

        [ConditionalFact(nameof(IsIntegratedSecurityEnvironmentSet), nameof(AreConnectionStringsSetup))]
        public static void CustomSspiContextGeneratorTest()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.IntegratedSecurity = true;
            Assert.True(DataTestUtility.ParseDataSource(builder.DataSource, out string hostname, out int port, out string instanceName));
            // Build the SPN for the server we are connecting to
            builder.ServerSPN = $"MSSQLSvc/{DataTestUtility.GetMachineFQDN(hostname)}";
            if (!string.IsNullOrWhiteSpace(instanceName))
            {
                builder.ServerSPN += ":" + instanceName;
            }

            var provider = new TestSspiContextProvider();
            using SqlConnection conn = new(builder.ConnectionString)
            {
                SspiContextProvider = provider,
            };

            // The custom provider captures auth params and returns false,
            // causing an SSPI generation failure wrapped in SqlException.
            Assert.ThrowsAny<SqlException>(() => conn.Open());

            Assert.NotNull(provider.CapturedAuthParams);
            Assert.Equal(provider.CapturedAuthParams.ServerName, builder.DataSource);
            Assert.Equal(provider.CapturedAuthParams.DatabaseName, builder.InitialCatalog);
            Assert.Equal(provider.CapturedAuthParams.UserId, builder.UserID);
            Assert.Equal(provider.CapturedAuthParams.Password, builder.Password);
        }

        private static void TryOpenConnectionWithIntegratedAuthentication(string connectionString)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
            }
        }

        private sealed class TestSspiContextProvider : SspiContextProvider
        {
            public SspiAuthenticationParameters CapturedAuthParams { get; private set; }

            protected override bool GenerateContext(ReadOnlySpan<byte> incomingBlob, IBufferWriter<byte> outgoingBlobWriter, SspiAuthenticationParameters authParams)
            {
                CapturedAuthParams = authParams;
                return false;
            }
        }
    }
}
