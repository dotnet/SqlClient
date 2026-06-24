// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "2")]
    public class AADConnectionTest
    {
        private static void ConnectAndDisconnect(string connectionString, SqlCredential credential = null)
        {
            using SqlConnection conn = new(connectionString);
            if (credential != null)
            {
                conn.Credential = credential;
            }
            conn.Open();

            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }

        private static bool AreConnStringsSetup() => DataTestUtility.AreConnStringsSetup();
        private static bool IsAzure() => !DataTestUtility.IsNotAzureServer();
        private static bool IsAccessTokenSetup() => DataTestUtility.IsAccessTokenAsyncSetup().GetAwaiter().GetResult();
        private static bool IsAzureSqlConnStringSetup() => DataTestUtility.IsAzureConnStringSetup();
        private static bool IsManagedIdentitySetup() => DataTestUtility.IsUserManagedIdentitySupported;
        private static bool SupportsSystemAssignedManagedIdentity() => DataTestUtility.IsSystemManagedIdentitySupported;


        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static async Task AccessTokenTest()
        {
            using SqlConnection connection = new(DataTestUtility.AzureSqlConnectionString);
            connection.AccessToken = await DataTestUtility.GetAccessTokenAsync();
            await connection.OpenAsync();

            Assert.Equal(System.Data.ConnectionState.Open, connection.State);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static async Task InvalidAccessTokenTest()
        {
            using SqlConnection connection = new(DataTestUtility.AzureSqlConnectionString);
            connection.AccessToken = await DataTestUtility.GetAccessTokenAsync() + "abc";
            SqlException e = Assert.Throws<SqlException>(() => connection.Open());

            string expectedMessage = "Login failed for user";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static async Task AccessTokenWithAuthType()
        {
            using SqlConnection connection = new(DataTestUtility.AzureSqlConnectionString
                .AddManagedIdentityAuthenticationToConnString());
            InvalidOperationException e = await Assert.ThrowsAsync<InvalidOperationException>
            (async () =>
                connection.AccessToken = await DataTestUtility.GetAccessTokenAsync()
            );

            string expectedMessage = "Cannot set the AccessToken property if 'Authentication' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static async Task AccessTokenWithCred()
        {
            string connString = DataTestUtility.AzureSqlConnectionString
                .AddUserToConnString()
                .AddPasswordToConnString();

            using SqlConnection connection = new(connString);
            InvalidOperationException e = await Assert.ThrowsAsync<InvalidOperationException>
            (async () =>
            connection.AccessToken = await DataTestUtility.GetAccessTokenAsync()
            );

            string expectedMessage = "Cannot set the AccessToken property if 'UserID', 'UID', 'Password', or 'PWD' has been specified in connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static void AccessTokenTestWithEmptyToken()
        {
            string connStr = DataTestUtility.AzureSqlConnectionString;

            using SqlConnection connection = new(connStr);
            connection.AccessToken = "";
            SqlException e = Assert.Throws<SqlException>(() => connection.Open());

            string expectedMessage = "A connection was successfully established with the server, but then an error occurred during the login process.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static void AccessTokenTestWithIntegratedSecurityTrue()
        {
            string connStr = DataTestUtility.AzureSqlConnectionString
                .AddIntegratedSecurityToConnString();

            using SqlConnection connection = new(connStr);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => connection.AccessToken = "");

            string expectedMessage = "Cannot set the AccessToken property if the 'Integrated Security' connection string keyword has been set to 'true' or 'SSPI'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAzureSqlConnStringSetup))]
        public static void InvalidAuthTypeTest()
        {
            string connStr = DataTestUtility.AzureSqlConnectionString
                .AddInvalidAADAuthenticationToConnString();

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Invalid value for key 'authentication'.";
            Assert.Contains(expectedMessage, e.Message, StringComparison.OrdinalIgnoreCase);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void AADPasswordWithIntegratedSecurityTrue()
        {
            string connStr = DataTestUtility.AzureSqlConnectionString
                .AddAADPasswordAuthenticationToConnString()
                .AddUserToConnString()
                .AddPasswordToConnString()
                .AddIntegratedSecurityToConnString();

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication' with 'Integrated Security'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void TestCustomProviderAuthentication()
        {
            SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity);

            try
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, new UserAssignedManagedIdentityProvider());

                string connStr = DataTestUtility.GetUserIdentityConnectionString();
                // Connect to Azure DB with managed identity and retrieve user name using custom authentication provider
                using SqlConnection conn = new(connStr);

                conn.Open();
                using SqlCommand sqlCommand = new(
                    cmdText: "SELECT SUSER_SNAME();",
                    connection: conn,
                    transaction: null);
                string customerId = (string)sqlCommand.ExecuteScalar();
                // SUSER_SNAME() may return "clientId@tenantId" for managed identity principals.
                string clientIdPart = customerId.Contains('@') ? customerId.Substring(0, customerId.IndexOf('@')) : customerId;
                Assert.Equal(DataTestUtility.UserManagedIdentityClientId, clientIdPart);
            }
            finally
            {
                if (original is not null)
                {
                    // Reset to driver internal provider.
                    SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, original);
                }
            }
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryPasswordWithNoAuthType()
        {
            string connStrWithNoAuthType = DataTestUtility.AzureSqlConnectionString
                .AddUserToConnString()
                .AddPasswordToConnString();
            Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStrWithNoAuthType));
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void AADIntegratedAuthWithCred()
        {
            string connStr = DataTestUtility.AzureSqlConnectionString
                .AddAADIntegratedAuthenticationToConnString()
                .AddUserToConnString()
                .AddPasswordToConnString();
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string[] expectedMessage = { "Cannot use 'Authentication=Active Directory Integrated' with 'User ID', 'UID', 'Password' or 'PWD' connection string keywords.", //netfx
                "Cannot use 'Authentication=Active Directory Integrated' with 'Password' or 'PWD' connection string keywords." }; //netcore
            Assert.Contains(e.Message, expectedMessage);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void MFAAuthWithPassword()
        {
            // connection fails with expected error message.
            string connStr = DataTestUtility.AzureSqlConnectionString
                .AddAADInteractiveAuthenticationToConnString()
                .AddUserToConnString()
                .AddPasswordToConnString();
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Interactive' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryDeviceCodeFlowWithUserIdMustFail()
        {
            // connection fails with expected error message.
            string connStrWithUID = DataTestUtility.AzureSqlConnectionString
                .AddAADDeviceCodeFlowAuthenticationToConnString()
                .AddUserToConnString("someuser");
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithUID));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Device Code Flow' with 'User ID', 'UID', 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryDeviceCodeFlowWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADDeviceCodeFlowAuthenticationToConnString();

            using SecureString str = CommonUtils.GenerateRandomSecureString(10);
            SqlCredential credential = new("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Device Code Flow' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryManagedIdentityWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddManagedIdentityAuthenticationToConnString();

            using SecureString str = CommonUtils.GenerateRandomSecureString(10);
            SqlCredential credential = new("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Managed Identity' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryWorkloadIdentityWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADWorkloadIdentityAuthenticationToConnString();

            using SecureString str = CommonUtils.GenerateRandomSecureString(10);
            SqlCredential credential = new("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Workload Identity' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup), nameof(IsManagedIdentitySetup))]
        public static void ActiveDirectoryManagedIdentityWithPasswordMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddManagedIdentityAuthenticationToConnString()
                .AddPasswordToConnString("anything");

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Managed Identity' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryMSIWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADMSIAuthenticationToConnString();

            using SecureString str = CommonUtils.GenerateRandomSecureString(10);
            SqlCredential credential = new("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>
            (() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory MSI' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryMSIWithPasswordMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADMSIAuthenticationToConnString()
                .AddPasswordToConnString();

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Cannot use 'Authentication=Active Directory MSI' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryDefaultWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADDefaultAuthenticationToConnString();

            using SecureString str = CommonUtils.GenerateRandomSecureString(10);

            SqlCredential credential = new("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>
            (() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Default' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryDefaultWithPasswordMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADDefaultAuthenticationToConnString()
                .AddPasswordToConnString("anything");

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Default' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ActiveDirectoryDefaultWithAccessTokenCallbackMustFail()
        {
            // connection fails with expected error message.
            string connStrWithNoCred = DataTestUtility.AzureSqlConnectionString
                .AddAADDefaultAuthenticationToConnString();

            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
            {
                using SqlConnection conn = new(connStrWithNoCred);
                conn.AccessTokenCallback = (ctx, token) =>
                    Task.FromResult(new SqlAuthenticationToken("my token", DateTimeOffset.MaxValue));
                conn.Open();

                Assert.NotEqual(System.Data.ConnectionState.Open, conn.State);
            });

            string expectedMessage = "Cannot set the AccessTokenCallback property if 'Authentication=Active Directory Default' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void AccessTokenCallbackMustOpenPassAndChangePropertyFail()
        {
            string connStr = DataTestUtility.AzureSqlConnectionString;

            TokenCredential cred = DataTestUtility.GetTokenCredential();
            const string defaultScopeSuffix = "/.default";

            using SqlConnection conn = new(connStr);
            conn.AccessTokenCallback = (ctx, cancellationToken) =>
            {
                string scope = ctx.Resource.EndsWith(defaultScopeSuffix) ? ctx.Resource : ctx.Resource + defaultScopeSuffix;
                AccessToken token = cred.GetToken(new TokenRequestContext([scope]), cancellationToken);
                return Task.FromResult(new SqlAuthenticationToken(token.Token, token.ExpiresOn));
            };
            conn.Open();
            Assert.Equal(System.Data.ConnectionState.Open, conn.State);

            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => conn.AccessTokenCallback = null);
            string expectedMessage = "Not allowed to change the 'AccessTokenCallback' property. The connection's current state is open.";
            Assert.Contains(expectedMessage, ex.Message);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void AccessTokenCallbackReceivesUsernameAndPassword()
        {
            var userId = "someuser";
            var pwd = "somepassword";
            string connStr = DataTestUtility.AzureSqlConnectionString
                .AddUserToConnString(userId)
                .AddPasswordToConnString(pwd);

            TokenCredential cred = DataTestUtility.GetTokenCredential();
            const string defaultScopeSuffix = "/.default";
            
            using SqlConnection conn = new(connStr);
            conn.AccessTokenCallback = (parms, cancellationToken) =>
            {
                Assert.Equal(userId, parms.UserId);
                Assert.Equal(pwd, parms.Password);
                string scope = parms.Resource.EndsWith(defaultScopeSuffix) ? parms.Resource : parms.Resource + defaultScopeSuffix;
                AccessToken token = cred.GetToken(new TokenRequestContext([scope]), cancellationToken);
                return Task.FromResult(new SqlAuthenticationToken(token.Token, token.ExpiresOn));
            };
            conn.Open();
        }

        // Test passes locally everytime, but in pipelines fails randomly with uncertainty.
        // e.g. Second Entra ID connection too slow (802ms)! (More than 30% of the first (576ms).)
        [ActiveIssue("16058")]
        [ConditionalFact(nameof(IsAzureSqlConnStringSetup))]
        public static void ConnectionSpeed()
        {
            SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity);

            try
            {
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, new UserAssignedManagedIdentityProvider());

                string connString = DataTestUtility.GetUserIdentityConnectionString();

                // Ensure server endpoints are warm
                using (SqlConnection connectionDrill = new(connString))
                {
                    connectionDrill.Open();
                }

                SqlConnection.ClearAllPools();

                Stopwatch firstConnectionTime = new();
                Stopwatch secondConnectionTime = new();

                using (SqlConnection connectionDrill = new(connString))
                {
                    firstConnectionTime.Start();
                    connectionDrill.Open();
                    firstConnectionTime.Stop();

                    using SqlConnection connectionDrill2 = new(connString);
                    secondConnectionTime.Start();
                    connectionDrill2.Open();
                    secondConnectionTime.Stop();
                }

                // Subsequent Entra ID connections within a short timeframe should use an auth token cached from the connection pool
                // Second connection speed in tests was typically 10-15% of the first connection time. Using 30% since speeds may vary.
                Assert.True(((double)secondConnectionTime.ElapsedMilliseconds / firstConnectionTime.ElapsedMilliseconds) < 0.30, $"Second Entra ID connection too slow ({secondConnectionTime.ElapsedMilliseconds}ms)! (More than 30% of the first ({firstConnectionTime.ElapsedMilliseconds}ms).)");
            }
            finally
            {
                if (original is not null)
                {
                    // Reset to driver internal provider.
                    SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryManagedIdentity, original);
                }
            }
        }

        #region Managed Identity Authentication tests

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
        public static async Task AccessToken_SystemManagedIdentityTest()
        {
            using SqlConnection conn = new(DataTestUtility.AzureSqlConnectionString);
            conn.AccessToken = await DataTestUtility.GetSystemIdentityAccessTokenAsync();
            conn.Open();

            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }

        [ConditionalFact(nameof(IsAzureSqlConnStringSetup), nameof(IsManagedIdentitySetup))]
        public static async Task AccessToken_UserManagedIdentityTest()
        {
            using SqlConnection conn = new(DataTestUtility.AzureSqlConnectionString);
            conn.AccessToken = await DataTestUtility.GetUserIdentityAccessTokenAsync();
            conn.Open();

            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure), nameof(IsAccessTokenSetup), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
        public static async Task Azure_AccessToken_SystemManagedIdentityTest()
        {
            string connectionString = DataTestUtility.TCPConnectionString
                .RemoveAuthAndCredsProperties();

            using SqlConnection conn = new(connectionString);
            conn.AccessToken = await DataTestUtility.GetSystemIdentityAccessTokenAsync();
            conn.Open();

            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure), nameof(IsAccessTokenSetup), nameof(IsManagedIdentitySetup))]
        public static async Task Azure_AccessToken_UserManagedIdentityTest()
        {
            string connectionString = DataTestUtility.TCPConnectionString
                .RemoveAuthAndCredsProperties();
            
            using SqlConnection conn = new(connectionString);
            conn.AccessToken = await DataTestUtility.GetUserIdentityAccessTokenAsync();
            conn.Open();

            Assert.Equal(System.Data.ConnectionState.Open, conn.State);
        }
        #endregion
    }
}
