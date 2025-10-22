// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Security;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConnectionBasicTests
    {
        [Fact]
        public void ConnectionTest()
        {
            using TestTdsServer server = TestTdsServer.StartTestServer();
            using SqlConnection connection = new SqlConnection(server.ConnectionString);
            connection.Open();
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsNotArmProcess))]
        [ActiveIssue(4830, TestPlatforms.AnyUnix)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void IntegratedAuthConnectionTest()
        {
            using TestTdsServer server = TestTdsServer.StartTestServer();
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(server.ConnectionString);
            builder.IntegratedSecurity = true;
            using SqlConnection connection = new SqlConnection(builder.ConnectionString);
            connection.Open();
        }

        /// <summary>
        /// Runs a test where TDS Server doesn't send encryption info during pre-login response.
        /// The driver is expected to fail when that happens, and terminate the connection during pre-login phase 
        /// when client enables encryption using Encrypt=true or uses default encryption setting.
        /// </summary>
        [Fact]
        public async Task PreLoginEncryptionExcludedTest()
        {
            using TestTdsServer server = TestTdsServer.StartTestServer(false, false, 5, excludeEncryption: true);
            SqlConnectionStringBuilder builder = new(server.ConnectionString)
            {
                IntegratedSecurity = true
            };

            using SqlConnection connection = new(builder.ConnectionString);
            Exception ex = await Assert.ThrowsAsync<SqlException>(async () => await connection.OpenAsync());
            Assert.Contains("The instance of SQL Server you attempted to connect to does not support encryption.", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [ConditionalTheory(typeof(PlatformDetection), nameof(PlatformDetection.IsNotArmProcess))]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TransientFaultTest(uint errorCode)
        {
            using TransientFaultTDSServer server = TransientFaultTDSServer.StartTestServer(true, true, errorCode);
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.Port,
                IntegratedSecurity = true,
                Encrypt = SqlConnectionEncryptOption.Optional
            };

            using SqlConnection connection = new(builder.ConnectionString);
            try
            {
                connection.Open();
                Assert.Equal(ConnectionState.Open, connection.State);
            }
            catch (Exception e)
            {
                if (null != connection)
                {
                    Assert.Equal(ConnectionState.Closed, connection.State);
                }
                Assert.False(true, e.Message);
            }
        }

        [Fact]
        public void SqlConnectionDbProviderFactoryTest()
        {
            SqlConnection con = new();
            PropertyInfo dbProviderFactoryProperty = con.GetType().GetProperty("DbProviderFactory", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(dbProviderFactoryProperty);
            DbProviderFactory factory = dbProviderFactoryProperty.GetValue(con) as DbProviderFactory;
            Assert.NotNull(factory);
            Assert.Same(typeof(SqlClientFactory), factory.GetType());
            Assert.Same(SqlClientFactory.Instance, factory);
        }

        [Fact]
        public void SqlConnectionValidParameters()
        {
            var con = new SqlConnection("Timeout=1234;packet Size=5678 ;;; ;");
            Assert.Equal(1234, con.ConnectionTimeout);
            Assert.Equal(5678, con.PacketSize);
        }

        [Fact]
        public void SqlConnectionEmptyParameters()
        {
            var con = new SqlConnection("Timeout=;packet Size= ;Integrated Security=;");
            //default values are defined in internal class DbConnectionStringDefaults
            Assert.Equal(15, con.ConnectionTimeout);
            Assert.Equal(8000, con.PacketSize);
            Assert.False(new SqlConnectionStringBuilder(con.ConnectionString).IntegratedSecurity);
        }

        [Theory]
        [InlineData("Timeout=null;")]
        [InlineData("Timeout= null;")]
        [InlineData("Timeout=1 1;")]
        [InlineData("Timeout=1a;")]
        [InlineData("Integrated Security=truee")]
        public void SqlConnectionInvalidParameters(string connString)
        {
            Assert.Throws<ArgumentException>(() => new SqlConnection(connString));
        }

        [Fact]
        public void ClosedConnectionSchemaRetrieval()
        {
            using SqlConnection connection = new(string.Empty);
            Assert.Throws<InvalidOperationException>(() => connection.GetSchema());
        }

        [Theory]
        [InlineData("RandomStringForTargetServer", false, true)]
        [InlineData("RandomStringForTargetServer", true, false)]
        [InlineData(null, false, false)]
        [InlineData("", false, false)]
        public void RetrieveWorkstationId(string workstation, bool withDispose, bool shouldMatchSetWorkstationId)
        {
            string connectionString = $"Workstation Id={workstation}";
            SqlConnection conn = new(connectionString);
            if (withDispose)
            {
                conn.Dispose();
            }
            string expected = shouldMatchSetWorkstationId ? workstation : Environment.MachineName;
            Assert.Equal(expected, conn.WorkstationId);
        }

        [OuterLoop("Can take up to 4 seconds")]
        [Fact]
        public void ExceptionsWithMinPoolSizeCanBeHandled()
        {
            string connectionString = $"Data Source={Guid.NewGuid()};uid=random;pwd=asd;Connect Timeout=2; Min Pool Size=3";
            for (int i = 0; i < 2; i++)
            {
                using SqlConnection connection = new(connectionString);
                Exception exception = Record.Exception(() => connection.Open());
                Assert.True(exception is InvalidOperationException || exception is SqlException, $"Unexpected exception: {exception}");
            }
        }

        [Fact]
        public void ConnectionTestInvalidCredentialCombination()
        {
            var cleartextCredsConnStr = "User=test;Password=test;";
            var sspiConnStr = "Integrated Security=true;";
            var testPassword = new SecureString();
            testPassword.MakeReadOnly();
            var sqlCredential = new SqlCredential(string.Empty, testPassword);

            // Verify that SSPI and cleartext username/password are not in the connection string.
            Assert.Throws<ArgumentException>(() => { new SqlConnection(cleartextCredsConnStr, sqlCredential); });

            Assert.Throws<ArgumentException>(() => { new SqlConnection(sspiConnStr, sqlCredential); });

            // Verify that credential may not be set with cleartext username/password or SSPI.
            using (var conn = new SqlConnection(cleartextCredsConnStr))
            {
                Assert.Throws<InvalidOperationException>(() => { conn.Credential = sqlCredential; });
            }

            using (var conn = new SqlConnection(sspiConnStr))
            {
                Assert.Throws<InvalidOperationException>(() => { conn.Credential = sqlCredential; });
            }

            // Verify that connection string with username/password or SSPI may not be set with credential present.
            using (var conn = new SqlConnection(string.Empty, sqlCredential))
            {
                Assert.Throws<InvalidOperationException>(() => { conn.ConnectionString = cleartextCredsConnStr; });

                Assert.Throws<InvalidOperationException>(() => { conn.ConnectionString = sspiConnStr; });
            }
        }

        [Fact]
        public void ConnectionTestValidCredentialCombination()
        {
            var testPassword = new SecureString();
            testPassword.MakeReadOnly();
            var sqlCredential = new SqlCredential(string.Empty, testPassword);
            var conn = new SqlConnection(string.Empty, sqlCredential);

            Assert.Equal(sqlCredential, conn.Credential);
        }

        [Fact]
        public void TransientFault_IgnoreServerProvidedFailoverPartner_ShouldConnectToUserProvidedPartner()
        {
            // Arrange
            using LocalAppContextSwitchesHelper switchesHelper = new();
            switchesHelper.IgnoreServerProvidedFailoverPartnerField = LocalAppContextSwitchesHelper.Tristate.True;

            using TdsServer failoverServer = new(
                new TdsServerArguments
                {
                    // Doesn't need to point to a real endpoint, just needs a value specified
                    FailoverPartner = "localhost,1234",
                });
            failoverServer.Start();

            using TdsServer server = new(
                new TdsServerArguments()
                {
                    // Set an invalid failover partner to ensure that the connection fails if the
                    // server provided failover partner is used.
                    FailoverPartner = $"invalidhost",
                });
            server.Start();

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                InitialCatalog = "master",
                Encrypt = false,
                FailoverPartner = $"localhost,{failoverServer.EndPoint.Port}",
                // Ensure pooling is enabled so that the failover partner information
                // is persisted in the pool group. If pooling is disabled, the server
                // provided failover partner will never be used.
                Pooling = true
            };
            SqlConnection connection = new(builder.ConnectionString);

            // Connect once to the primary to trigger it to send the failover partner
            connection.Open();
            Assert.Equal("invalidhost", (connection.InnerConnection as SqlInternalConnectionTds)!.ServerProvidedFailoverPartner);

            // Close the connection to return it to the pool
            connection.Close();


            // Act
            // Dispose of the server to trigger a failover
            server.Dispose();

            // Opening a new connection will use the failover partner stored in the pool group.
            // This will fail if the server provided failover partner was stored to the pool group.
            using SqlConnection failoverConnection = new(builder.ConnectionString);
            failoverConnection.Open();

            // Assert
            Assert.Equal(ConnectionState.Open, failoverConnection.State);
            Assert.Equal($"localhost,{failoverServer.EndPoint.Port}", failoverConnection.DataSource);
            // 1 for the initial connection
            Assert.Equal(1, server.PreLoginCount);
            // 1 for the failover connection
            Assert.Equal(1, failoverServer.PreLoginCount);
        }
    }
}
