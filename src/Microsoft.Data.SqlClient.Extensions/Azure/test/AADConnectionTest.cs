// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// These tests were migrated from MDS ManualTests AADConnectionTest.cs.
public class AADConnectionTest
{
    private static void ConnectAndDisconnect(
        string connectionString, SqlCredential? credential = null)
    {
        using SqlConnection conn = new(connectionString);

        if (credential is not null)
        {
            conn.Credential = credential;
        }

        conn.Open();

        Assert.True(conn.State == System.Data.ConnectionState.Open);
    }

    private static void ConnectAndDisconnect(
        SqlConnectionStringBuilder builder, SqlCredential? credential = null)
    {
        ConnectAndDisconnect(builder.ToString(), credential);
    }

    [ConditionalFact(typeof(Config), nameof(Config.HasUsernamePassword))]
    public static void KustoDatabaseTest()
    {
        // This is a sample Kusto database that can be connected by any AD
        // account.
        using SqlConnection connection = new(
            "Data Source=help.kusto.windows.net;" +
            "Authentication=Active Directory Password;" +
            "Trust Server Certificate=True;" +
            $"User ID={Config.Username};" +
            $"Password={Config.Password};");
        
        connection.Open();
        
        Assert.True(connection.State == System.Data.ConnectionState.Open);
    }


    /*


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

            [ConditionalFact(nameof(IsAADConnStringsSetup), nameof(IsManagedIdentitySetup), nameof(SupportsSystemAssignedManagedIdentity))]
            public static void SystemAssigned_ManagedIdentityTest()
            {
                string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
                string connStr = DataTestUtility.RemoveKeysInConnStr(DataTestUtility.AADPasswordConnectionString, removeKeys) +
                    $"Authentication=Active Directory Managed Identity;";
                ConnectAndDisconnect(connStr);
            }

    */

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasServer),
        nameof(Config.HasDatabase),
        nameof(Config.HasManagedIdentity))]
    public static void UserAssigned_ManagedIdentityTest()
    {
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = Config.Server,
            InitialCatalog = Config.Database,
            TrustServerCertificate = true,
            Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity,
            UserID = Config.ManagedIdentity
        };

        ConnectAndDisconnect(builder);
    }

/*

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


*/

}
