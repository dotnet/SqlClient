// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.Common.SystemDataInternals;
using Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "3")]
    public class AADFedAuthTokenRefreshTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public AADFedAuthTokenRefreshTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAADPasswordConnStrSetup))]
        public void FedAuthTokenRefreshTest()
        {
            #pragma warning disable 0618 // Type or member is obsolete
            SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword);
            #pragma warning restore 0618 // Type or member is obsolete

            try
            {
                #pragma warning disable 0618 // Type or member is obsolete
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, new UsernamePasswordProvider(DataTestUtility.ApplicationClientId));
                #pragma warning restore 0618 // Type or member is obsolete

                string connectionString = DataTestUtility.AADPasswordConnectionString;

                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();

                string oldTokenHash = "";
                DateTime? oldExpiryDateTime = FedAuthTokenHelper.SetTokenExpiryDateTime(connection, minutesToExpire: 1, out oldTokenHash);
                Assert.True(oldExpiryDateTime != null, "Failed to make token expiry to expire in one minute.");

                // Convert and display the old expiry into local time which should be in 1 minute from now
                DateTime oldLocalExpiryTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)oldExpiryDateTime, TimeZoneInfo.Local);
                LogInfo($"Token: {oldTokenHash}   Old Expiry: {oldLocalExpiryTime}");
                TimeSpan timeDiff = oldLocalExpiryTime - DateTime.Now;
                Assert.InRange(timeDiff.TotalSeconds, 0, 60);

                // Check if connection is still alive to continue further testing
                string result = "";
                SqlCommand cmd = connection.CreateCommand();
                cmd.CommandText = "select @@version";
                result = $"{cmd.ExecuteScalar()}";
                Assert.True(result != string.Empty, "The connection's command must return a value");

                // The new connection will use the same FedAuthToken but will refresh it first as it will expire in 1 minute.
                using (SqlConnection connection2 = new SqlConnection(connectionString))
                {
                    connection2.Open();

                    // Check if connection is alive
                    cmd = connection2.CreateCommand();
                    cmd.CommandText = "select 1";
                    result = $"{cmd.ExecuteScalar()}";
                    Assert.True(result != string.Empty, "The connection's command must return a value after a token refresh.");

                    string newTokenHash = "";
                    DateTime? newExpiryDateTime = FedAuthTokenHelper.GetTokenExpiryDateTime(connection2, out newTokenHash);
                    DateTime newLocalExpiryTime = TimeZoneInfo.ConvertTimeFromUtc((DateTime)newExpiryDateTime, TimeZoneInfo.Local);
                    LogInfo($"Token: {newTokenHash}   New Expiry: {newLocalExpiryTime}");

                    Assert.True(oldTokenHash == newTokenHash, "The token's hash before and after token refresh must be identical.");
                    Assert.True(newLocalExpiryTime > oldLocalExpiryTime, "The refreshed token must have a new or later expiry time.");
                }
            }
            finally
            {
                if (original is not null)
                {
                    // Reset to driver internal provider.
                    #pragma warning disable 0618 // Type or member is obsolete
                    SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, original);
                    #pragma warning restore 0618 // Type or member is obsolete
                }
            }
        }

        /// <summary>
        /// Verifies both pools replace connections with expired or nearly expired tokens and
        /// invoke the callback when the cached token also needs refreshing, for Open and OpenAsync.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsAADPasswordConnStrSetup))]
        [InlineData(false, false, -1)]
        [InlineData(false, true, -1)]
        [InlineData(true, false, -1)]
        [InlineData(true, true, -1)]
        [InlineData(false, false, 5)]
        [InlineData(false, true, 5)]
        [InlineData(true, false, 5)]
        [InlineData(true, true, 5)]
        public async Task AccessTokenCallback_PooledConnectionIsReplacedOnExpiry(bool usePoolV2, bool async, int expiresInSeconds)
        {
            using var poolVersion = new ConnectionPoolVersionScope(usePoolV2);
            string[] credentialKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            var builder = new SqlConnectionStringBuilder(
                DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credentialKeys))
            {
                Pooling = true,
                MinPoolSize = 0,
                MaxPoolSize = 1,
                ConnectTimeout = 30,
                Enlist = false
            };
            var credential = DataTestUtility.GetTokenCredential();
            SqlAuthenticationToken callbackToken = null;
            int callbackInvocations = 0;
            using var connection = new SqlConnection(builder.ConnectionString)
            {
                AccessTokenCallback = async (parameters, cancellationToken) =>
                {
                    Interlocked.Increment(ref callbackInvocations);
                    const string suffix = "/.default";
                    string scope = parameters.Resource.EndsWith(suffix) ? parameters.Resource : parameters.Resource + suffix;
                    AccessToken token = await credential.GetTokenAsync(new TokenRequestContext(new[] { scope }), cancellationToken);
                    callbackToken = new SqlAuthenticationToken(token.Token, token.ExpiresOn);
                    return callbackToken;
                }
            };

            Task OpenConnection()
            {
                if (async)
                {
                    return connection.OpenAsync();
                }
                connection.Open();
                return Task.CompletedTask;
            }

            // The empty pool requires a physical login, which invokes the callback and caches its token.
            await OpenConnection();
            object original = connection.GetInternalConnection();
            Assert.NotNull(callbackToken);
            int callbackCountAfterLogin = callbackInvocations;
            Assert.True(callbackCountAfterLogin > 0);
            // Close returns the physical connection to the pool; reopening reuses it without authentication.
            connection.Close();
            await OpenConnection();
            Assert.Same(original, connection.GetInternalConnection());
            Assert.Equal(callbackCountAfterLogin, callbackInvocations);

            // The connection's expiry controls eviction; the cached token's expiry controls callback refresh.
            // Age both without changing the real token or waiting. Five seconds is within the 30-second
            // checkout buffer; minus one second covers an already expired token.
            DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
            object cachedContext = FedAuthTokenHelper.GetAuthenticationContextValue(connection);
            FieldInfo cacheExpiryField = cachedContext.GetType().GetField("_expirationTime", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(cacheExpiryField);
            cacheExpiryField.SetValue(cachedContext, expiry.UtcDateTime);
            FieldInfo tokenField = original.GetType().GetField("_fedAuthToken", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(tokenField);
            object expiringToken = Activator.CreateInstance(tokenField.FieldType,
                BindingFlags.Instance | BindingFlags.NonPublic, binder: null,
                args: new object[] { new SqlAuthenticationToken(callbackToken.AccessToken, expiry) },
                culture: null);
            tokenField.SetValue(original, expiringToken);
            // Expiry is checked on checkout, not return, so Close does not invoke the callback.
            connection.Close();
            Assert.Equal(callbackCountAfterLogin, callbackInvocations);

            // Checkout discards the expired physical connection rather than reauthenticating it.
            // Its replacement needs a login, and the expiring cache entry forces another callback.
            // The credential may still return the same valid token; callback invocation is what matters.
            await OpenConnection();
            Assert.NotSame(original, connection.GetInternalConnection());
            Assert.True(callbackInvocations > callbackCountAfterLogin);
            object replacement = connection.GetInternalConnection();
            int callbackCountAfterRefresh = callbackInvocations;
            // The replacement now has a valid token, so another reopen reuses it without another callback.
            connection.Close();
            await OpenConnection();
            Assert.Same(replacement, connection.GetInternalConnection());
            Assert.Equal(callbackCountAfterRefresh, callbackInvocations);
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            Assert.Equal(1, async ? await command.ExecuteScalarAsync() : command.ExecuteScalar());
        }

        [Conditional("DEBUG")]
        private void LogInfo(string message)
        {
            _testOutputHelper.WriteLine(message);
        }
    }
}
