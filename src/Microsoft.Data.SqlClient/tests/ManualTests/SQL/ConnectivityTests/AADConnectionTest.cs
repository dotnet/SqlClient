// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security;
using System.Threading.Tasks;
using Azure.Core;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AADConnectionTest
    {
        private static void ConnectAndDisconnect(string connectionString, SqlCredential credential = null)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                if (credential != null)
                {
                    conn.Credential = credential;
                }
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }

        private static bool AreConnStringsSetup() => DataTestUtility.AreConnStringsSetup();
        private static bool IsAzure() => !DataTestUtility.IsNotAzureServer();
        private static bool IsAccessTokenSetup() => DataTestUtility.IsAccessTokenSetup();
        private static bool IsAADConnStringsSetup() => DataTestUtility.IsAADPasswordConnStrSetup();
        private static bool IsManagedIdentitySetup() => DataTestUtility.ManagedIdentitySupported;
        private static bool SupportsSystemAssignedManagedIdentity() => DataTestUtility.SupportsSystemAssignedManagedIdentity;


        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenTest()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD", "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                connection.AccessToken = DataTestUtility.GetAccessToken();
                connection.Open();

                Assert.True(connection.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void InvalidAccessTokenTest()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD", "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                connection.AccessToken = DataTestUtility.GetAccessToken() + "abc";
                SqlException e = Assert.Throws<SqlException>(() => connection.Open());

                string expectedMessage = "Login failed for user";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenWithAuthType()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
                    connection.AccessToken = DataTestUtility.GetAccessToken());

                string expectedMessage = "Cannot set the AccessToken property if 'Authentication' has been specified in the connection string.";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenWithCred()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
                connection.AccessToken = DataTestUtility.GetAccessToken());

                string expectedMessage = "Cannot set the AccessToken property if 'UserID', 'UID', 'Password', or 'PWD' has been specified in connection string.";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenTestWithEmptyToken()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD", "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                connection.AccessToken = "";
                SqlException e = Assert.Throws<SqlException>(() => connection.Open());

                string expectedMessage = "A connection was successfully established with the server, but then an error occurred during the login process.";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenTestWithIntegratedSecurityTrue()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "UID", "PWD", "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) + "Integrated Security=True;";

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => connection.AccessToken = "");

                string expectedMessage = "Cannot set the AccessToken property if the 'Integrated Security' connection string keyword has been set to 'true' or 'SSPI'.";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void InvalidAuthTypeTest()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) + "Authentication=Active Directory Pass;";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Invalid value for key 'authentication'.";
            Assert.Contains(expectedMessage, e.Message, StringComparison.OrdinalIgnoreCase);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithIntegratedSecurityTrue()
        {
            string connStr = DataTestUtility.AADPasswordConnectionString + "Integrated Security=True;";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication' with 'Integrated Security'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void GetAccessTokenByPasswordTest()
        {
            #pragma warning disable 0618 // Type or member is obsolete
            SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword);
            #pragma warning restore 0618 // Type or member is obsolete

            try
            {
                #pragma warning disable 0618 // Type or member is obsolete
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, new UsernamePasswordProvider(DataTestUtility.ApplicationClientId));
                #pragma warning restore 0618 // Type or member is obsolete

                using (SqlConnection connection = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
                {
                    connection.Open();
                    Assert.True(connection.State == System.Data.ConnectionState.Open);
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

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void TestCustomProviderAuthentication()
        {
            #pragma warning disable 0618 // Type or member is obsolete
            SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword);
            #pragma warning restore 0618 // Type or member is obsolete

            try
            {
                #pragma warning disable 0618 // Type or member is obsolete
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, new UsernamePasswordProvider(DataTestUtility.ApplicationClientId));
                #pragma warning restore 0618 // Type or member is obsolete
                // Connect to Azure DB with password and retrieve user name using custom authentication provider
                using (SqlConnection conn = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
                {
                    conn.Open();
                    using (SqlCommand sqlCommand = new SqlCommand
                    (
                        cmdText: $"SELECT SUSER_SNAME();",
                        connection: conn,
                        transaction: null
                    ))
                    {
                        string customerId = (string)sqlCommand.ExecuteScalar();
                        string expected = DataTestUtility.RetrieveValueFromConnStr(DataTestUtility.AADPasswordConnectionString, new string[] { "User ID", "UID" });
                        Assert.Equal(expected, customerId);
                    }
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

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryPasswordWithNoAuthType()
        {
            // connection fails with expected error message.
            string[] AuthKey = { "Authentication" };
            string connStrWithNoAuthType = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, AuthKey);
            Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStrWithNoAuthType));
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void IntegratedAuthWithCred()
        {
            // connection fails with expected error message.
            string[] AuthKey = { "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, AuthKey) + "Authentication=Active Directory Integrated;";
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string[] expectedMessage = { "Cannot use 'Authentication=Active Directory Integrated' with 'User ID', 'UID', 'Password' or 'PWD' connection string keywords.", //netfx
                "Cannot use 'Authentication=Active Directory Integrated' with 'Password' or 'PWD' connection string keywords." }; //netcore
            Assert.Contains(e.Message, expectedMessage);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void MFAAuthWithPassword()
        {
            // connection fails with expected error message.
            string[] AuthKey = { "Authentication" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, AuthKey) + "Authentication=Active Directory Interactive;";
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Interactive' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryDeviceCodeFlowWithUserIdMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithUID = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Device Code Flow; UID=someuser;";
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithUID));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Device Code Flow' with 'User ID', 'UID', 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryDeviceCodeFlowWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Device Code Flow;";

            SecureString str = new SecureString();
            foreach (char c in "hello")
            {
                str.AppendChar(c);
            }
            str.MakeReadOnly();
            SqlCredential credential = new SqlCredential("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Device Code Flow' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryManagedIdentityWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Managed Identity;";

            SecureString str = new SecureString();
            foreach (char c in "hello")
            {
                str.AppendChar(c);
            }
            str.MakeReadOnly();
            SqlCredential credential = new SqlCredential("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Managed Identity' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryWorkloadIdentityWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Workload Identity;";

            SecureString str = new SecureString();
            foreach (char c in "hello")
            {
                str.AppendChar(c);
            }
            str.MakeReadOnly();
            SqlCredential credential = new SqlCredential("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Workload Identity' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void ActiveDirectoryManagedIdentityWithPasswordMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Managed Identity; Password=anything";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Managed Identity' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryMSIWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory MSI;";

            SecureString str = new SecureString();
            foreach (char c in "hello")
            {
                str.AppendChar(c);
            }
            str.MakeReadOnly();
            SqlCredential credential = new SqlCredential("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory MSI' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryMSIWithPasswordMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=ActiveDirectoryMSI; Password=anything";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Cannot use 'Authentication=Active Directory MSI' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryDefaultWithCredentialsMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Default;";

            SecureString str = new SecureString();
            foreach (char c in "hello")
            {
                str.AppendChar(c);
            }
            str.MakeReadOnly();
            SqlCredential credential = new SqlCredential("someuser", str);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred, credential));

            string expectedMessage = "Cannot set the Credential property if 'Authentication=Active Directory Default' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryDefaultWithPasswordMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=ActiveDirectoryDefault; Password=anything";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Default' with 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryDefaultWithAccessTokenCallbackMustFail()
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=ActiveDirectoryDefault";
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
            {
                using (SqlConnection conn = new SqlConnection(connStrWithNoCred))
                {
                    conn.AccessTokenCallback = (ctx, token) =>
                        Task.FromResult(new SqlAuthenticationToken("my token", DateTimeOffset.MaxValue));
                    conn.Open();

                    Assert.NotEqual(System.Data.ConnectionState.Open, conn.State);
                }
            });

            string expectedMessage = "Cannot set the AccessTokenCallback property if 'Authentication=Active Directory Default' has been specified in the connection string.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AccessTokenCallbackMustOpenPassAndChangePropertyFail()
        {
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);
            var cred = DataTestUtility.GetTokenCredential();
            const string defaultScopeSuffix = "/.default";
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.AccessTokenCallback = (ctx, cancellationToken) =>
                {
                    string scope = ctx.Resource.EndsWith(defaultScopeSuffix) ? ctx.Resource : ctx.Resource + defaultScopeSuffix;
                    AccessToken token = cred.GetToken(new TokenRequestContext(new[] { scope }), cancellationToken);
                    return Task.FromResult(new SqlAuthenticationToken(token.Token, token.ExpiresOn));
                };
                conn.Open();
                Assert.Equal(System.Data.ConnectionState.Open, conn.State);

                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() => conn.AccessTokenCallback = null);
                string expectedMessage = "Not allowed to change the 'AccessTokenCallback' property. The connection's current state is open.";
                Assert.Contains(expectedMessage, ex.Message);
            }
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AccessTokenCallbackReceivesUsernameAndPassword()
        {
            var userId = "someuser";
            var pwd = "somepassword";
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                 $"User ID={userId}; Password={pwd}";
            var cred = DataTestUtility.GetTokenCredential();
            const string defaultScopeSuffix = "/.default";
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.AccessTokenCallback = (parms, cancellationToken) =>
                {
                    Assert.Equal(userId, parms.UserId);
                    Assert.Equal(pwd, parms.Password);
                    string scope = parms.Resource.EndsWith(defaultScopeSuffix) ? parms.Resource : parms.Resource + defaultScopeSuffix;
                    AccessToken token = cred.GetToken(new TokenRequestContext(new[] { scope }), cancellationToken);
                    return Task.FromResult(new SqlAuthenticationToken(token.Token, token.ExpiresOn));
                };
                conn.Open();
            }
        }

        // Test passes locally everytime, but in pieplines fails randomly with uncertainity.
        // e.g. Second AAD connection too slow (802ms)! (More than 30% of the first (576ms).)
        [ActiveIssue("16058")]
        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ConnectionSpeed()
        {
            #pragma warning disable 0618 // Type or member is obsolete
            SqlAuthenticationProvider original = SqlAuthenticationProvider.GetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword);
            #pragma warning restore 0618 // Type or member is obsolete

            try
            {
                #pragma warning disable 0618 // Type or member is obsolete
                SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, new UsernamePasswordProvider(DataTestUtility.ApplicationClientId));
                #pragma warning restore 0618 // Type or member is obsolete

                var connString = DataTestUtility.AADPasswordConnectionString;

                //Ensure server endpoints are warm
                using (var connectionDrill = new SqlConnection(connString))
                {
                    connectionDrill.Open();
                }

                SqlConnection.ClearAllPools();

                Stopwatch firstConnectionTime = new Stopwatch();
                Stopwatch secondConnectionTime = new Stopwatch();

                using (var connectionDrill = new SqlConnection(connString))
                {
                    firstConnectionTime.Start();
                    connectionDrill.Open();
                    firstConnectionTime.Stop();
                    using (var connectionDrill2 = new SqlConnection(connString))
                    {
                        secondConnectionTime.Start();
                        connectionDrill2.Open();
                        secondConnectionTime.Stop();
                    }
                }

                // Subsequent AAD connections within a short timeframe should use an auth token cached from the connection pool
                // Second connection speed in tests was typically 10-15% of the first connection time. Using 30% since speeds may vary.
                Assert.True(((double)secondConnectionTime.ElapsedMilliseconds / firstConnectionTime.ElapsedMilliseconds) < 0.30, $"Second AAD connection too slow ({secondConnectionTime.ElapsedMilliseconds}ms)! (More than 30% of the first ({firstConnectionTime.ElapsedMilliseconds}ms).)");
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

        #region Managed Identity Authentication tests

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
        public static void AccessToken_SystemManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys);
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.AccessToken = DataTestUtility.GetSystemIdentityAccessToken();
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void AccessToken_UserManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys);
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.AccessToken = DataTestUtility.GetUserIdentityAccessToken();
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure), nameof(IsAccessTokenSetup), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
        public static void Azure_AccessToken_SystemManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys);
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.AccessToken = DataTestUtility.GetSystemIdentityAccessToken();
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure), nameof(IsAccessTokenSetup), nameof(IsManagedIdentitySetup))]
        public static void Azure_AccessToken_UserManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys);
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.AccessToken = DataTestUtility.GetUserIdentityAccessToken();
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }
        #endregion
    }
}
