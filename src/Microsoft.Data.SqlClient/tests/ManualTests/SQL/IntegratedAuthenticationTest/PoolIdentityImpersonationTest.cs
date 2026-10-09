// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Serializes tests that switch pool implementations and exercise real Windows credentials.
    /// </summary>
    [CollectionDefinition("PoolIdentityImpersonation", DisableParallelization = true)]
    public sealed class PoolIdentityImpersonationCollection
    {
    }

    /// <summary>
    /// Regression coverage for #4717 against a real SQL Server. Invalid-credential probes need
    /// only integrated security; alternate-account tests read credentials from environment
    /// variables.
    /// </summary>
    [Collection("PoolIdentityImpersonation")]
    [Trait("Set", "3")]
    public class PoolIdentityImpersonationTest
    {
        public static bool IsConfigured =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_USER"))
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_DOMAIN"))
            && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_PASSWORD"))
            && DataTestUtility.AreConnStringsSetup();

        /// <summary>
        /// Needs only an integrated-security connection string: invalid network-only credentials
        /// cannot open a new physical connection, so any successful open proves pool sharing.
        /// </summary>
        public static bool SupportsInvalidCredentialProbe =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && DataTestUtility.AreConnStringsSetup()
            && DataTestUtility.IsIntegratedSecuritySetup();

        /// <summary>
        /// A network-only caller with unusable credentials must not receive idle or replenished
        /// connections authenticated as the process account.
        /// </summary>
        [ConditionalTheory(nameof(SupportsInvalidCredentialProbe))]
        [InlineData(false, false, 0)]
        [InlineData(false, true, 0)]
        [InlineData(true, false, 0)]
        [InlineData(true, true, 0)]
        [InlineData(false, false, 3)]
        [InlineData(false, true, 3)]
        [InlineData(true, false, 3)]
        [InlineData(true, true, 3)]
        public async Task InvalidNetOnlyCredentials_DoNotReuseProcessConnections(bool usePoolV2, bool async, int minPoolSize)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            using SafeAccessTokenHandle token = CreateInvalidToken();
            string connectionString = BuildConnectionString(minPoolSize: minPoolSize);
            await SkipUnlessInvalidCredentialsRejected(token, connectionString, async);
            try
            {
                string processLogin = await GetLogin(connectionString, async);
                if (minPoolSize > 0)
                {
                    // Give background replenishment time to fill the process pool.
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                await Assert.ThrowsAsync<SqlException>(
                    () => WindowsIdentity.RunImpersonated(token, () => GetLogin(connectionString, async)));
                Assert.Equal(processLogin, await GetLogin(connectionString, async));
            }
            finally
            {
                using var poolConnection = new SqlConnection(connectionString);
                SqlConnection.ClearPool(poolConnection);
            }
        }

        /// <summary>
        /// A failed network-only open must not place the process account's pool in its error
        /// blocking period.
        /// </summary>
        [ConditionalTheory(nameof(SupportsInvalidCredentialProbe))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task InvalidNetOnlyCredentials_FailureDoesNotBlockProcess(bool usePoolV2, bool async)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            using SafeAccessTokenHandle token = CreateInvalidToken();
            string connectionString = BuildConnectionString();
            await SkipUnlessInvalidCredentialsRejected(token, connectionString, async);
            try
            {
                await Assert.ThrowsAsync<SqlException>(
                    () => WindowsIdentity.RunImpersonated(token, () => GetLogin(connectionString, async)));
                Assert.False(string.IsNullOrEmpty(await GetLogin(connectionString, async)));
            }
            finally
            {
                using var poolConnection = new SqlConnection(connectionString);
                SqlConnection.ClearPool(poolConnection);
            }
        }

        /// <summary>
        /// Connections returned under alternate outbound credentials must not be reused by the
        /// process account after impersonation ends, for either pool and either Open API.
        /// </summary>
        [ConditionalTheory(nameof(IsConfigured))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task NetOnlyImpersonation_RevertsLogin(bool usePoolV2, bool async)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            using SafeAccessTokenHandle token = CreateToken();
            string connectionString = BuildConnectionString();
            string original = await GetLogin(connectionString, async);
            string impersonated = await WindowsIdentity.RunImpersonated(token, () => GetLogin(connectionString, async));
            Assert.NotEqual(original, impersonated);
            Assert.Equal(original, await GetLogin(connectionString, async));
        }

        /// <summary>
        /// Returning an expired connection outside impersonation must not replenish the owning
        /// pool with process-account connections or cache a process-account login failure.
        /// </summary>
        [ConditionalTheory(nameof(IsConfigured))]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task NetOnlyImpersonation_Replenishment_PreservesLogin(bool usePoolV2, bool async)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            using SafeAccessTokenHandle token = CreateToken();
            string connectionString = BuildConnectionString(loadBalanceTimeout: 1, minPoolSize: 3);
            var unpooled = new SqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                MinPoolSize = 0
            };
            string expected = await WindowsIdentity.RunImpersonated(token, () => GetLogin(unpooled.ConnectionString, async));
            var opened = new List<SqlConnection>();
            try
            {
                SqlConnection expiring = await WindowsIdentity.RunImpersonated(token, () => Open(connectionString, async));
                opened.Add(expiring);
                for (int i = 0; i < 2; i++)
                {
                    opened.Add(await WindowsIdentity.RunImpersonated(token, () => Open(connectionString, async)));
                }

                // Exceed the one-second physical connection lifetime, then trigger replenishment
                // under the process context. Both pools now have a minimum to replenish.
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                expiring.Dispose();

                // Hold every checkout so a correctly authenticated idle connection cannot hide a
                // wrongly replenished one. MaxPoolSize bounds this probe at eight new checkouts.
                for (int i = 0; i < 8; i++)
                {
                    SqlConnection connection = await WindowsIdentity.RunImpersonated(token, () => Open(connectionString, async));
                    opened.Add(connection);
                    Assert.Equal(expected, await SelectLogin(connection, async));
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }
            }
            finally
            {
                using var poolConnection = new SqlConnection(connectionString);
                SqlConnection.ClearPool(poolConnection);
                foreach (SqlConnection connection in opened)
                {
                    connection.Dispose();
                }
            }
        }

        /// <summary>Creates the configured network-only token without persisting credentials.</summary>
        private static SafeAccessTokenHandle CreateToken() =>
            WindowsImpersonationHelper.LogonNetOnly(
                Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_USER"),
                Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_DOMAIN"),
                Environment.GetEnvironmentVariable("SQLCLIENT_NETONLY_PASSWORD"));

        /// <summary>
        /// Creates a network-only token with placeholder credentials; NEW_CREDENTIALS logons do
        /// not validate them until a new outbound authentication is attempted.
        /// </summary>
        private static SafeAccessTokenHandle CreateInvalidToken() =>
            WindowsImpersonationHelper.LogonNetOnly("<user>", "EXAMPLE", "<pwd>");

        /// <summary>
        /// The invalid-credential probes rely on the server rejecting the placeholder outbound
        /// credentials. Some environments (for example, loopback authentication that falls back
        /// to the local logon session) accept them, so a successful open would not prove pool
        /// sharing. Verify the precondition with an unpooled connection and skip otherwise.
        /// </summary>
        private static async Task SkipUnlessInvalidCredentialsRejected(
            SafeAccessTokenHandle token, string connectionString, bool async)
        {
            var unpooled = new SqlConnectionStringBuilder(connectionString)
            {
                Pooling = false,
                MinPoolSize = 0
            };
            try
            {
                await WindowsIdentity.RunImpersonated(token, () => GetLogin(unpooled.ConnectionString, async));
            }
            catch (SqlException)
            {
                return;
            }

            throw new Microsoft.DotNet.XUnitExtensions.SkipTestException(
                "The server accepted placeholder network-only credentials, so pool sharing cannot be detected.");
        }

        /// <summary>Creates a unique pool group using the configured server and integrated security.</summary>
        private static string BuildConnectionString(int loadBalanceTimeout = 0, int minPoolSize = 0)
        {
            var builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                IntegratedSecurity = true,
                Pooling = true,
                ApplicationName = $"PoolIdentityImpersonation-{Guid.NewGuid():N}",
                LoadBalanceTimeout = loadBalanceTimeout,
                MinPoolSize = minPoolSize,
                MaxPoolSize = 10,
                ConnectTimeout = 15
            };
            builder.Remove("User ID");
            builder.Remove("Password");
            builder.Remove("Authentication");
            return builder.ConnectionString;
        }

        /// <summary>Opens and disposes a connection after reading its authenticated SQL login.</summary>
        private static async Task<string> GetLogin(string connectionString, bool async)
        {
            using SqlConnection connection = await Open(connectionString, async);
            return await SelectLogin(connection, async);
        }

        /// <summary>Opens a connection with the selected API, disposing it on failure.</summary>
        private static async Task<SqlConnection> Open(string connectionString, bool async)
        {
            var connection = new SqlConnection(connectionString);
            try
            {
                if (async)
                {
                    await connection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>Reads the login that authenticated the physical connection.</summary>
        private static async Task<string> SelectLogin(SqlConnection connection, bool async)
        {
            using var command = new SqlCommand("SELECT ORIGINAL_LOGIN()", connection);
            return (string)(async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
        }
    }
}
