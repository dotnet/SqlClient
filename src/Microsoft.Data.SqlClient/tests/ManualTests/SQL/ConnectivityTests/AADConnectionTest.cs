// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;
using System;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AADConnectionsTest
    {
        private static void ConnectAndDisconnect(string connectionString, SqlCredential credential = null)
        {
            SqlConnection conn = new SqlConnection(connectionString);
            if (credential != null)
            {
                conn.Credential = credential;
            }
            conn.Open();
            conn.Close();
        }

        private static string RemoveKeysInConnStr(string connStr, string[] keysToRemove)
        {
            // tokenize connection string and remove input keys.
            string res = "";
            string[] keys = connStr.Split(';');
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key.Trim()))
                {
                    bool removeKey = false;
                    foreach (var keyToRemove in keysToRemove)
                    {
                        if (key.Trim().ToLower().StartsWith(keyToRemove.Trim().ToLower()))
                        {
                            removeKey = true;
                            break;
                        }
                    }
                    if (!removeKey)
                    {
                        res += key + ";";
                    }
                }
            }
            return res;
        }

        private static string RetrieveValueFromConnStr(string connStr, string keyword)
        {
            // tokenize connection string and retrieve value for a specific key.
            string res = "";
            string[] keys = connStr.Split(';');
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key.Trim()))
                {
                    if (key.Trim().ToLower().StartsWith(keyword.Trim().ToLower()))
                    {
                        res = key.Substring(key.IndexOf('=') + 1).Trim();
                        break;
                    }
                }
            }
            return res;
        }

        private static bool IsAccessTokenSetup() => DataTestUtility.IsAccessTokenSetup();
        private static bool IsAADConnStringsSetup() => DataTestUtility.IsAADPasswordConnStrSetup();

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenTest()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.TcpConnStr))
            {
                connection.AccessToken = DataTestUtility.getAccessToken();
                connection.Open();
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void InvalidAccessTokenTest()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "Authentication" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                connection.AccessToken = DataTestUtility.getAccessToken() + "abc";
                SqlException e = Assert.Throws<SqlException>(() => connection.Open());

                string expectedMessage = "Login failed for user";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenWithAuthType()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password"};
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys);

            using (SqlConnection connection = new SqlConnection(connStr))
            {
                InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => 
                    connection.AccessToken = DataTestUtility.getAccessToken());

                string expectedMessage = "Cannot set the AccessToken property if 'Authentication' has been specified in the connection string.";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenWithCred()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "Authentication" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys);
            
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
                connection.AccessToken = DataTestUtility.getAccessToken());

                string expectedMessage = "Cannot set the AccessToken property if 'UserID', 'UID', 'Password', or 'PWD' has been specified in connection string.";
                Assert.Contains(expectedMessage, e.Message);
            }
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AccessTokenTestWithEmptyToken()
        {
            // Remove cred info and add invalid token
            string[] credKeys = { "User ID", "Password", "Authentication" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys);
            
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
            string[] credKeys = { "User ID", "Password", "Authentication" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys) + "Integrated Security=True;";

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
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys) + "Authentication=Active Directory Pass;";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Invalid value for key 'authentication'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithIntegratedSecurityTrue()
        {
             string connStr = DataTestUtility.AADPasswordConnStr + "Integrated Security=True;";

            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication' with 'Integrated Security'.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithWrongPassword()
        {
            string[] credKeys = { "Password" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys) + "Password=TestPassword;";

            AggregateException e = Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "ID3242: The security token could not be authenticated or authorized.";
            Assert.Contains(expectedMessage, e.InnerException.InnerException.InnerException.Message);
        }


        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void GetAccessTokenByPasswordTest()
        {
            using (SqlConnection connection = new SqlConnection(DataTestUtility.AADPasswordConnStr))
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
                using (SqlConnection conn = new SqlConnection(DataTestUtility.AADPasswordConnStr))
                {
                    conn.Open();
                    SqlCommand sqlCommand = new SqlCommand
                    (
                        cmdText: $"SELECT SUSER_SNAME();",
                        connection: conn,
                        transaction: null
                    );
                    string customerId = (string)sqlCommand.ExecuteScalar();
                    string expected = RetrieveValueFromConnStr(DataTestUtility.AADPasswordConnStr, "User ID");
                    Assert.Equal(expected, customerId);
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
            string connStrWithNoAuthType = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, AuthKey);
            SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStrWithNoAuthType));

            string expectedMessage = "Cannot open server \"microsoft.com\" requested by the login.  The login failed.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        public static void IntegratedAuthWithCred()
        {
            // connection fails with expected error message.
            string[] AuthKey = { "Authentication" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, AuthKey) + "Authentication=Active Directory Integrated;";
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Integrated' with 'User ID', 'UID', 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        public static void MFAAuthWithCred()
        {
            // connection fails with expected error message.
            string[] AuthKey = { "Authentication" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, AuthKey) + "Authentication=Active Directory Interactive;";
            ArgumentException e = Assert.Throws<ArgumentException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Cannot use 'Authentication=Active Directory Interactive' with 'User ID', 'UID', 'Password' or 'PWD' connection string keywords.";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void EmptyPasswordInConnStrAADPassword()
        {
            // connection fails with expected error message.
            string[] pwdKey = { "Password" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, pwdKey) + "Password=;";
            AggregateException e = Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "ID3242: The security token could not be authenticated or authorized.";
            Assert.Contains(expectedMessage, e.InnerException.InnerException.InnerException.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        [ActiveIssue(9417)]
        public static void EmptyCredInConnStrAADPassword()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "Password" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, removeKeys) + "User ID=; Password=;";
            AggregateException e = Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Unsupported User Type 'Unknown'";
            Assert.Contains(expectedMessage, e.InnerException.InnerException.InnerException.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithInvalidUser()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID" };
            string connStr = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, removeKeys) + "User ID=testdotnet@microsoft.com";
            AggregateException e = Assert.Throws<AggregateException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "ID3242: The security token could not be authenticated or authorized.";
            Assert.Contains(expectedMessage, e.InnerException.InnerException.InnerException.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void NoCredentialsActiveDirectoryPassword()
        {
            // test Passes with correct connection string.
            ConnectAndDisconnect(DataTestUtility.AADPasswordConnStr);

            // connection fails with expected error message.
            string[] credKeys = { "User ID", "Password" };
            string connStrWithNoCred = RemoveKeysInConnStr(DataTestUtility.AADPasswordConnStr, credKeys);
            InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "Either Credential or both 'User ID' and 'Password' (or 'UID' and 'PWD') connection string keywords must be specified, if 'Authentication=Active Directory Password'.";
            Assert.Contains(expectedMessage, e.Message);
        }
    }
}
