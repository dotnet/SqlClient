// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConnectionBasicTests
    {
        // Reflection
        public static Assembly systemData = Assembly.GetAssembly(typeof(SqlConnection));
        
        public static Type sqlConnection = systemData.GetType("Microsoft.Data.SqlClient.SqlConnection");
        public static PropertyInfo innerConnectionProperty = sqlConnection.GetProperty("InnerConnection", BindingFlags.NonPublic | BindingFlags.Instance);
        public static Type sqlInternalConnectionTds = systemData.GetType("Microsoft.Data.SqlClient.SqlInternalConnectionTds");
        public static PropertyInfo serverProvidedFailoverPartnerProperty = sqlInternalConnectionTds.GetProperty("ServerProvidedFailoverPartner", BindingFlags.NonPublic | BindingFlags.Instance);
        public static Type localAppContextSwitches = systemData.GetType("Microsoft.Data.SqlClient.LocalAppContextSwitches");
        public static FieldInfo ignoreServerProvidedFailoverPartnerField = localAppContextSwitches.GetField("s_ignoreServerProvidedFailoverPartner", BindingFlags.NonPublic | BindingFlags.Static);


        [Fact]
        public void ConnectionTest()
        {
            using TestTdsServer server = TestTdsServer.StartTestServer();
            using SqlConnection connection = new SqlConnection(server.ConnectionString);
            connection.Open();
        }

        [ConditionalFact(typeof(TestUtility), nameof(TestUtility.IsNotArmProcess))]
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

        [ConditionalTheory(typeof(TestUtility), nameof(TestUtility.IsNotArmProcess))]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public async Task TransientFaultTestAsync(uint errorCode)
        {
            using TransientFaultTDSServer server = TransientFaultTDSServer.StartTestServer(true, true, errorCode);
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.Port,
                IntegratedSecurity = true,
                Encrypt = SqlConnectionEncryptOption.Optional
            };

            using SqlConnection connection = new(builder.ConnectionString);
            await connection.OpenAsync();
            Assert.Equal(ConnectionState.Open, connection.State);
        }

        [ConditionalTheory(typeof(TestUtility), nameof(TestUtility.IsNotArmProcess))]
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
                Assert.Fail(e.Message);
            }
        }

        [ConditionalTheory(typeof(TestUtility), nameof(TestUtility.IsNotArmProcess))]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TransientFaultDisabledTestAsync(uint errorCode)
        {
            using TransientFaultTDSServer server = TransientFaultTDSServer.StartTestServer(true, true, errorCode);
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.Port,
                IntegratedSecurity = true,
                ConnectRetryCount = 0,
                Encrypt = SqlConnectionEncryptOption.Optional
            };

            using SqlConnection connection = new(builder.ConnectionString);
            Task<SqlException> e = Assert.ThrowsAsync<SqlException>(async () => await connection.OpenAsync());
            Assert.Equal(20, e.Result.Class);
            Assert.Equal(ConnectionState.Closed, connection.State);
        }

        [ConditionalTheory(typeof(TestUtility), nameof(TestUtility.IsNotArmProcess))]
        [InlineData(40613)]
        [InlineData(42108)]
        [InlineData(42109)]
        [PlatformSpecific(TestPlatforms.Windows)]
        public void TransientFaultDisabledTest(uint errorCode)
        {
            using TransientFaultTDSServer server = TransientFaultTDSServer.StartTestServer(true, true, errorCode);
            SqlConnectionStringBuilder builder = new()
            {
                DataSource = "localhost," + server.Port,
                IntegratedSecurity = true,
                ConnectRetryCount = 0,
                Encrypt = SqlConnectionEncryptOption.Optional
            };

            using SqlConnection connection = new(builder.ConnectionString);
            SqlException e = Assert.Throws<SqlException>(() => connection.Open());
            Assert.Equal(20, e.Class);
            Assert.Equal(ConnectionState.Closed, connection.State);
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

        [Theory]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryIntegrated)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryInteractive)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryMSI)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryDefault)]
        [InlineData(SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity)]
        public void ConnectionTestInvalidCredentialAndAuthentication(SqlAuthenticationMethod authentication)
        {
            var connectionString = $"Authentication={authentication}";

            using var testPassword = new SecureString();
            testPassword.MakeReadOnly();
            var credential = new SqlCredential(string.Empty, testPassword);

            Assert.Throws<ArgumentException>(() => new SqlConnection(connectionString, credential));

            // Attempt to set the credential after creation
            using var connection = new SqlConnection(connectionString);
            Assert.Throws<InvalidOperationException>(() => connection.Credential = credential);
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
            ignoreServerProvidedFailoverPartnerField.SetValue(null, true);

            try
            {
                using TestTdsServer failoverServer = TestTdsServer.StartTestServer();
                // Doesn't need to point to a real endpoint, just needs a value specified
                failoverServer.Arguments.FailoverPartner = "localhost,1234";

                var failoverBuilder = new SqlConnectionStringBuilder(failoverServer.ConnectionString);

                using TestTdsServer server = TestTdsServer.StartTestServer();
                // Set an invalid failover partner to ensure that the connection fails if the
                // server provided failover partner is used.
                server.Arguments.FailoverPartner = $"invalidhost";

                SqlConnectionStringBuilder builder = new(server.ConnectionString)
                {
                    InitialCatalog = "master",
                    Encrypt = false,
                    FailoverPartner = failoverBuilder.DataSource,
                    // Ensure pooling is enabled so that the failover partner information
                    // is persisted in the pool group. If pooling is disabled, the server
                    // provided failover partner will never be used.
                    Pooling = true,
                    ConnectTimeout = 10000
                };
                SqlConnection connection = new(builder.ConnectionString);

                // Connect once to the primary to trigger it to send the failover partner
                connection.Open();

                var innerConnection = innerConnectionProperty.GetValue(connection);
                var serverProvidedFailoverPartner = serverProvidedFailoverPartnerProperty.GetValue(innerConnection);
                Assert.Equal("invalidhost", serverProvidedFailoverPartner);

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
                Assert.Equal(failoverBuilder.DataSource, failoverConnection.DataSource);
                // 1 for the initial connection
                Assert.Equal(1, server.PreLoginCount);
                // 1 for the failover connection
                Assert.Equal(1, failoverServer.PreLoginCount);
            }
            finally
            {
                ignoreServerProvidedFailoverPartnerField.SetValue(null, false);
            }
        }


        [Theory]
        [InlineData(60)]
        [InlineData(30)]
        [InlineData(15)]
        [InlineData(10)]
        [InlineData(5)]
        [InlineData(1)]
        public void ConnectionTimeoutTest(int timeout)
        {
            // Start a server with connection timeout from the inline data.
            using TestTdsServer server = TestTdsServer.StartTestServer(false, false, timeout);
            using SqlConnection connection = new SqlConnection(server.ConnectionString);

            // Dispose the server to force connection timeout 
            server.Dispose();

            // Measure the actual time it took to timeout and compare it with configured timeout
            Stopwatch timer = new();
            Exception ex = null;

            // Open a connection with the server disposed.
            try
            {
                timer.Start();
                connection.Open();
            }
            catch (Exception e)
            {
                timer.Stop();
                ex = e;
            }

            Assert.False(timer.IsRunning, "Timer must be stopped.");
            Assert.NotNull(ex);
            Assert.True(timer.Elapsed.TotalSeconds <= timeout + 3,
                $"The actual timeout {timer.Elapsed.TotalSeconds} is expected to be less than {timeout} plus 3 seconds additional threshold." +
                $"{Environment.NewLine}{ex}");
        }

        [Theory]
        [InlineData(60)]
        [InlineData(30)]
        [InlineData(15)]
        [InlineData(10)]
        [InlineData(5)]
        [InlineData(1)]
        public async Task ConnectionTimeoutTestAsync(int timeout)
        {
            // Start a server with connection timeout from the inline data.
            using TestTdsServer server = TestTdsServer.StartTestServer(false, false, timeout);
            using SqlConnection connection = new SqlConnection(server.ConnectionString);

            // Dispose the server to force connection timeout 
            server.Dispose();

            // Measure the actual time it took to timeout and compare it with configured timeout
            Stopwatch timer = new();
            Exception ex = null;

            // Open a connection with the server disposed.
            try
            {
                //an asyn call with a timeout token to cancel the operation after the specific time
                using CancellationTokenSource cts = new CancellationTokenSource(timeout * 1000);
                timer.Start();
                await connection.OpenAsync(cts.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                timer.Stop();
                ex = e;
            }

            Assert.False(timer.IsRunning, "Timer must be stopped.");
            Assert.NotNull(ex);
            Assert.True(timer.Elapsed.TotalSeconds <= timeout + 3,
                $"The actual timeout {timer.Elapsed.TotalSeconds} is expected to be less than {timeout} plus 3 seconds additional threshold." +
                $"{Environment.NewLine}{ex}");
        }

        [Fact]
        public void ConnectionInvalidTimeoutTest()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using TestTdsServer server = TestTdsServer.StartTestServer(false, false, -5);
            });

        }

        [Fact]
        public void ConnectionTestWithCultureTH()
        {
            CultureInfo savedCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo savedUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("th-TH");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("th-TH");

                using TestTdsServer server = TestTdsServer.StartTestServer();
                using SqlConnection connection = new SqlConnection(server.ConnectionString);
                connection.Open();
                Assert.Equal(ConnectionState.Open, connection.State);
            }
            finally
            {
                // Restore saved cultures if necessary
                if (Thread.CurrentThread.CurrentCulture != savedCulture)
                    Thread.CurrentThread.CurrentCulture = savedCulture;
                if (Thread.CurrentThread.CurrentUICulture != savedUICulture)
                    Thread.CurrentThread.CurrentUICulture = savedUICulture;
            }
        }

        [Fact]
        public void ConnectionTestAccessTokenCallbackCombinations()
        {
            var cleartextCredsConnStr = "User=test;Password=test;";
            var sspiConnStr = "Integrated Security=true;";
            var authConnStr = "Authentication=ActiveDirectoryPassword";
            var testPassword = new SecureString();
            testPassword.MakeReadOnly();
            var sqlCredential = new SqlCredential(string.Empty, testPassword);
            Func<SqlAuthenticationParameters, CancellationToken, Task<SqlAuthenticationToken>> callback = (ctx, token) =>
                    Task.FromResult(new SqlAuthenticationToken("invalid", DateTimeOffset.MaxValue));

            // Successes
            using (var conn = new SqlConnection(cleartextCredsConnStr))
            {
                conn.AccessTokenCallback = callback;
                conn.AccessTokenCallback = null;
            }

            using (var conn = new SqlConnection(string.Empty, sqlCredential))
            {
                conn.AccessTokenCallback = null;
                conn.AccessTokenCallback = callback;
            }

            using (var conn = new SqlConnection()
            {
                AccessTokenCallback = callback
            })
            {
                conn.Credential = sqlCredential;
            }

            using (var conn = new SqlConnection()
            {
                AccessTokenCallback = callback
            })
            {
                conn.ConnectionString = cleartextCredsConnStr;
            }

            //Failures
            using (var conn = new SqlConnection(sspiConnStr))
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    conn.AccessTokenCallback = callback;
                });
            }

            using (var conn = new SqlConnection(authConnStr))
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    conn.AccessTokenCallback = callback;
                });
            }

            using (var conn = new SqlConnection()
            {
                AccessTokenCallback = callback
            })
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    conn.ConnectionString = sspiConnStr;
                });
            }

            using (var conn = new SqlConnection()
            {
                AccessTokenCallback = callback
            })
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    conn.ConnectionString = authConnStr;
                });
            }
        }
    }
}
