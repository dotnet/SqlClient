// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AADConnectionsTest
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
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithIntegratedSecurityTrue()
        {
            string connStr = DataTestUtility.AADPasswordConnectionString + "Integrated Security=True;";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication' with 'Integrated Security'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithWrongPassword()
        {
            string[] credKeys = { "Password", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) + "Password=TestPassword;";

            AggregateException e = Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "ID3242: The security token could not be authenticated or authorized.";
            Assert.Contains(expectedMessage, e.InnerException.InnerException.InnerException.Message);
        }


        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void GetAccessTokenByPasswordTest()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
            {
                connection.Open();
            }
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void testADPasswordAuthentication()
        {
            // Connect to Azure DB with password and retrieve user name.
            try
            {
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
            catch (SqlException e)
            {
                throw e;
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
        public static void EmptyPasswordInConnStrAADPassword()
        {
            // connection fails with expected error message.
            string[] pwdKey = { "Password", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, pwdKey) + "Password=;";
            Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void EmptyCredInConnStrAADPassword()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) + "User ID=; Password=;";
            Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));
        }

        [PlatformSpecific(TestPlatforms.AnyUnix)]
        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void EmptyCredInConnStrAADPasswordAnyUnix()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) + "User ID=; Password=;";
            Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithInvalidUser()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "UID" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) + "User ID=testdotnet@microsoft.com";
            Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void NoCredentialsActiveDirectoryPassword()
        {
            // test Passes with correct connection string.
            ConnectAndDisconnect(DataTestUtility.AADPasswordConnectionString);

            // connection fails with expected error message.
            string[] credKeys = { "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Either Credential or both 'User ID' and 'Password' (or 'UID' and 'PWD') connection string keywords must be specified, if 'Authentication=Active Directory Password'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsAADServicePrincipalSetup))]
        public static void NoCredentialsActiveDirectoryServicePrincipal()
        {
            // test Passes with correct connection string.
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) +
                $"Authentication=Active Directory Service Principal; User ID={DataTestUtility.AADServicePrincipalId}; PWD={DataTestUtility.AADServicePrincipalSecret};";
            ConnectAndDisconnect(connStr);

            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                "Authentication=Active Directory Service Principal;";
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Either Credential or both 'User ID' and 'Password' (or 'UID' and 'PWD') connection string keywords must be specified, if 'Authentication=Active Directory Service Principal'.";
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

        [InlineData("2445343 2343253")]
        [InlineData("2445343$#^@@%2343253")]
        [ConditionalTheory(nameof(IsAADConnStringsSetup))]
        public static void ActiveDirectoryManagedIdentityWithInvalidUserIdMustFail(string userId)
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                $"Authentication=Active Directory Managed Identity; User Id={userId}";

            AggregateException e = Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Response: {\"error\":\"invalid_request\",\"error_description\":\"Identity not found\"}";
            Assert.Contains(expectedMessage, e.GetBaseException().Message);
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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsIntegratedSecuritySetup), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ADInteractiveUsingSSPI()
        {
            // test Passes with correct connection string.
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys) +
                $"Authentication=Active Directory Integrated;";
            ConnectAndDisconnect(connStr);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ConnectionSpeed()
        {
            //Ensure server endpoints are warm
            using (var connectionDrill = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
            {
                connectionDrill.Open();
            }
            SqlConnection.ClearAllPools();

            using (var connectionDrill = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
            {
                Stopwatch firstConnectionTime = new Stopwatch();
                firstConnectionTime.Start();
                connectionDrill.Open();
                firstConnectionTime.Stop();
                using (var connectionDrill2 = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
                {
                    Stopwatch secondConnectionTime = new Stopwatch();
                    secondConnectionTime.Start();
                    connectionDrill2.Open();
                    secondConnectionTime.Stop();
                    // Subsequent AAD connections within a short timeframe should use an auth token cached from the connection pool
                    // Second connection speed in tests was typically 10-15% of the first connection time. Using 30% since speeds may vary.
                    Assert.True(secondConnectionTime.ElapsedMilliseconds / firstConnectionTime.ElapsedMilliseconds < .30, $"Second AAD connection too slow ({secondConnectionTime.ElapsedMilliseconds}ms)! (More than 30% of the first ({firstConnectionTime.ElapsedMilliseconds}ms).)");
                }
            }
        }

        #region Managed Identity Authentication tests

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void SystemAssigned_ManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) +
                $"Authentication=Active Directory Managed Identity;";
            ConnectAndDisconnect(connStr);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void UserAssigned_ManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) +
                $"Authentication=Active Directory Managed Identity; User Id={DataTestUtility.UserManagedIdentityObjectId};";
            ConnectAndDisconnect(connStr);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
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

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure))]
        public static void Azure_SystemManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys)
                + $"Authentication=Active Directory Managed Identity;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure))]
        public static void Azure_UserManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys)
                + $"Authentication=Active Directory Managed Identity; User Id={DataTestUtility.UserManagedIdentityObjectId}";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                Assert.True(conn.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure))]
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

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure))]
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
