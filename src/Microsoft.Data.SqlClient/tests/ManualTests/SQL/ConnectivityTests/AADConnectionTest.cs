// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Identity.Client;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class AADConnectionsTest
    {
        class CustomSqlAuthenticationProvider : SqlAuthenticationProvider
        {
            string _appClientId;

            internal CustomSqlAuthenticationProvider(string appClientId)
            {
                _appClientId = appClientId;
            }

            public override async Task<SqlAuthenticationToken> AcquireTokenAsync(SqlAuthenticationParameters parameters)
            {
                string s_defaultScopeSuffix = "/.default";
                string scope = parameters.Resource.EndsWith(s_defaultScopeSuffix, StringComparison.Ordinal) ? parameters.Resource : parameters.Resource + s_defaultScopeSuffix;

                _ = parameters.ServerName;
                _ = parameters.DatabaseName;
                _ = parameters.ConnectionId;

                var cts = new CancellationTokenSource();
                cts.CancelAfter(parameters.ConnectionTimeout * 1000);

                string[] scopes = new string[] { scope };
                SecureString password = new SecureString();

#pragma warning disable CS0618 // Type or member is obsolete
                AuthenticationResult result = await PublicClientApplicationBuilder.Create(_appClientId)
                .WithAuthority(parameters.Authority)
                .Build().AcquireTokenByUsernamePassword(scopes, parameters.UserId, parameters.Password)
                    .WithCorrelationId(parameters.ConnectionId)
                    .ExecuteAsync(cancellationToken: cts.Token);
#pragma warning restore CS0618 // Type or member is obsolete

                return new SqlAuthenticationToken(result.AccessToken, result.ExpiresOn);
            }

            public override bool IsSupported(SqlAuthenticationMethod authenticationMethod)
            {
                #pragma warning disable 0618 // Type or member is obsolete
                return authenticationMethod.Equals(SqlAuthenticationMethod.ActiveDirectoryPassword);
                #pragma warning restore 0618 // Type or member is obsolete
            }
        }

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

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void KustoDatabaseTest()
        {
            // This is a sample Kusto database that can be connected by any AD account.
            using SqlConnection connection = new SqlConnection($"Data Source=help.kusto.windows.net; Authentication=Active Directory Default;Trust Server Certificate=True;User ID = {DataTestUtility.UserManagedIdentityClientId};");
            connection.Open();
            Assert.True(connection.State == System.Data.ConnectionState.Open);
        }

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

        [ConditionalFact(nameof(IsAccessTokenSetup), nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithWrongPassword()
        {
            string[] credKeys = { "Password", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) + "Password=TestPassword;";

            Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

            // We cannot verify error message with certainity as driver may cache token from other tests for current user
            // and error message may change accordingly.
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void GetAccessTokenByPasswordTest()
        {
            // Clear token cache for code coverage.
            ActiveDirectoryAuthenticationProvider.ClearUserTokenCache();
            using (SqlConnection connection = new SqlConnection(DataTestUtility.AADPasswordConnectionString))
            {
                connection.Open();
                Assert.True(connection.State == System.Data.ConnectionState.Open);
            }
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void TestADPasswordAuthentication()
        {
            // Connect to Azure DB with password and retrieve user name.
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

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void TestCustomProviderAuthentication()
        {
            #pragma warning disable 0618 // Type or member is obsolete
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, new CustomSqlAuthenticationProvider(DataTestUtility.ApplicationClientId));
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
            // Reset to driver internal provider.
            #pragma warning disable 0618 // Type or member is obsolete
            SqlAuthenticationProvider.SetProvider(SqlAuthenticationMethod.ActiveDirectoryPassword, new ActiveDirectoryAuthenticationProvider(DataTestUtility.ApplicationClientId));
            #pragma warning restore 0618 // Type or member is obsolete
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
            SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

            string user = DataTestUtility.FetchKeyInConnStr(DataTestUtility.AADPasswordConnectionString, new string[] { "User Id", "UID" });
            string expectedMessage = string.Format("Failed to authenticate the user {0} in Active Directory (Authentication=ActiveDirectoryPassword).", user);
            Assert.Contains(expectedMessage, e.Message);
        }

        [PlatformSpecific(TestPlatforms.Windows)]
        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void EmptyCredInConnStrAADPassword()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) + "User ID=; Password=;";
            SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "Failed to authenticate the user  in Active Directory (Authentication=ActiveDirectoryPassword).";
            Assert.Contains(expectedMessage, e.Message);
        }

        [PlatformSpecific(TestPlatforms.AnyUnix)]
        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void EmptyCredInConnStrAADPasswordAnyUnix()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) + "User ID=; Password=;";
            SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = "MSAL cannot determine the username (UPN) of the currently logged in user.For Integrated Windows Authentication and Username/Password flows, please use .WithUsername() before calling ExecuteAsync().";
            Assert.Contains(expectedMessage, e.Message);
        }

        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void AADPasswordWithInvalidUser()
        {
            // connection fails with expected error message.
            string[] removeKeys = { "User ID", "UID" };
            string user = "testdotnet@domain.com";
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) + $"User ID={user}";
            SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

            string expectedMessage = string.Format("Failed to authenticate the user {0} in Active Directory (Authentication=ActiveDirectoryPassword).", user);
            Assert.Contains(expectedMessage, e.Message);
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

        [InlineData("2445343 2343253")]
        [InlineData("2445343$#^@@%2343253")]
        [ConditionalTheory(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void ActiveDirectoryManagedIdentityWithInvalidUserIdMustFail(string userId)
        {
            // connection fails with expected error message.
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStrWithNoCred = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                $"Authentication=Active Directory Managed Identity; User Id={userId}";

            SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStrWithNoCred));

            string expectedMessage = "[Managed Identity] Authentication unavailable";
            Assert.Contains(expectedMessage, e.GetBaseException().Message, StringComparison.OrdinalIgnoreCase);
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

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup))]
        public static void ActiveDirectoryDefaultMustPass()
        {
            string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, credKeys) +
                $"Authentication=ActiveDirectoryDefault;User ID={DataTestUtility.UserManagedIdentityClientId};";

            // Connection should be established using Managed Identity by default.
            ConnectAndDisconnect(connStr);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsIntegratedSecuritySetup), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ADIntegratedUsingSSPI()
        {
            // test Passes with correct connection string.
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys) +
                $"Authentication=Active Directory Integrated;";
            ConnectAndDisconnect(connStr);
        }

        // Test passes locally everytime, but in pieplines fails randomly with uncertainity.
        // e.g. Second AAD connection too slow (802ms)! (More than 30% of the first (576ms).)
        [ActiveIssue("16058")]
        [ConditionalFact(nameof(IsAADConnStringsSetup))]
        public static void ConnectionSpeed()
        {
            var connString = DataTestUtility.AADPasswordConnectionString;

            //Ensure server endpoints are warm
            using (var connectionDrill = new SqlConnection(connString))
            {
                connectionDrill.Open();
            }

            SqlConnection.ClearAllPools();
            ActiveDirectoryAuthenticationProvider.ClearUserTokenCache();

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

        #region Managed Identity Authentication tests

        [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
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
                $"Authentication=Active Directory Managed Identity; User Id={DataTestUtility.UserManagedIdentityClientId};";
            ConnectAndDisconnect(connStr);
        }

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

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
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

        [ConditionalFact(nameof(AreConnStringsSetup), nameof(IsAzure), nameof(IsManagedIdentitySetup))]
        public static void Azure_UserManagedIdentityTest()
        {
            string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
            string connectionString = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.TCPConnectionString, removeKeys)
                + $"Authentication=Active Directory Managed Identity; User Id={DataTestUtility.UserManagedIdentityClientId}";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
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
