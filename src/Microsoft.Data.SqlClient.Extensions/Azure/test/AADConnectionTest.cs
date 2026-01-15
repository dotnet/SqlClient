// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.RegularExpressions;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// These tests were migrated from MDS ManualTests AADConnectionTest.cs.
public class AADConnectionTest
{
    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnAdoPool),
        nameof(Config.HasUserManagedIdentityClientId))]
    public static void KustoDatabaseTest()
    {
        // This is a sample Kusto database that can be connected by any AD account.
        using SqlConnection connection = new SqlConnection($"Data Source=help.kusto.windows.net; Authentication=Active Directory Default;Trust Server Certificate=True;User ID = {Config.UserManagedIdentityClientId};");
        connection.Open();
        Assert.True(connection.State == System.Data.ConnectionState.Open);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString))]
    public static void AADPasswordWithWrongPassword()
    {
        string[] credKeys = { "Password", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, credKeys) + "Password=TestPassword;";

        Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

        // We cannot verify error message with certainty as driver may cache token from other tests for current user
        // and error message may change accordingly.
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString))]
    public static void TestADPasswordAuthentication()
    {
        // Connect to Azure DB with password and retrieve user name.
        using (SqlConnection conn = new SqlConnection(Config.PasswordConnectionString))
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
                string expected = RetrieveValueFromConnStr(Config.PasswordConnectionString, new string[] { "User ID", "UID" });
                Assert.Equal(expected, customerId);
            }
        }
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString))]
    public static void EmptyPasswordInConnStrAADPassword()
    {
        // connection fails with expected error message.
        string[] pwdKey = { "Password", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, pwdKey) + "Password=;";
        SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

        string? user = FetchKeyInConnStr(Config.PasswordConnectionString, new string[] { "User Id", "UID" });
        string expectedMessage = string.Format("Failed to authenticate the user {0} in Active Directory (Authentication=ActiveDirectoryPassword).", user);
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnWindows),
        nameof(Config.HasPasswordConnectionString))]
    public static void EmptyCredInConnStrAADPassword()
    {
        // connection fails with expected error message.
        string[] removeKeys = { "User ID", "Password", "UID", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, removeKeys) + "User ID=; Password=;";
        SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

        string expectedMessage = "Failed to authenticate the user  in Active Directory (Authentication=ActiveDirectoryPassword).";
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnUnix),
        nameof(Config.HasPasswordConnectionString))]
    public static void EmptyCredInConnStrAADPasswordAnyUnix()
    {
        // connection fails with expected error message.
        string[] removeKeys = { "User ID", "Password", "UID", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, removeKeys) + "User ID=; Password=;";
        SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

        string expectedMessage = "MSAL cannot determine the username (UPN) of the currently logged in user.For Integrated Windows Authentication and Username/Password flows, please use .WithUsername() before calling ExecuteAsync().";
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString))]
    public static void AADPasswordWithInvalidUser()
    {
        // connection fails with expected error message.
        string[] removeKeys = { "User ID", "UID" };
        string user = "testdotnet@domain.com";
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, removeKeys) + $"User ID={user}";
        SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStr));

        string expectedMessage = string.Format("Failed to authenticate the user {0} in Active Directory (Authentication=ActiveDirectoryPassword).", user);
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString))]
    public static void NoCredentialsActiveDirectoryPassword()
    {
        // test Passes with correct connection string.
        ConnectAndDisconnect(Config.PasswordConnectionString);

        // connection fails with expected error message.
        string[] credKeys = { "User ID", "Password", "UID", "PWD" };
        string connStrWithNoCred = RemoveKeysInConnStr(Config.PasswordConnectionString, credKeys);
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred));

        string expectedMessage = "Either Credential or both 'User ID' and 'Password' (or 'UID' and 'PWD') connection string keywords must be specified, if 'Authentication=Active Directory Password'.";
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString),
        nameof(Config.HasServicePrincipal))]
    public static void NoCredentialsActiveDirectoryServicePrincipal()
    {
        // test Passes with correct connection string.
        string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, removeKeys) +
        $"Authentication=Active Directory Service Principal; User ID={Config.ServicePrincipalId}; PWD={Config.ServicePrincipalSecret};";
        ConnectAndDisconnect(connStr);

        // connection fails with expected error message.
        string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
        string connStrWithNoCred = RemoveKeysInConnStr(Config.PasswordConnectionString, credKeys) +
        "Authentication=Active Directory Service Principal;";
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => ConnectAndDisconnect(connStrWithNoCred));

        string expectedMessage = "Either Credential or both 'User ID' and 'Password' (or 'UID' and 'PWD') connection string keywords must be specified, if 'Authentication=Active Directory Service Principal'.";
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalTheory(
        typeof(Config),
        nameof(Config.HasPasswordConnectionString),
        nameof(Config.HasUserManagedIdentityClientId))]
    [InlineData("2445343 2343253")]
    [InlineData("2445343$#^@@%2343253")]
    public static void ActiveDirectoryManagedIdentityWithInvalidUserIdMustFail(string userId)
    {
        // connection fails with expected error message.
        string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
        string connStrWithNoCred = RemoveKeysInConnStr(Config.PasswordConnectionString, credKeys) +
        $"Authentication=Active Directory Managed Identity; User Id={userId}";

        SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStrWithNoCred));

        Regex expected = new(
            @"(\[Managed Identity\]|ManagedIdentityCredential) Authentication unavailable",
            RegexOptions.IgnoreCase);

        Assert.Matches(expected, e.GetBaseException().Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnAdoPool),
        nameof(Config.HasPasswordConnectionString),
        nameof(Config.HasUserManagedIdentityClientId))]
    public static void ActiveDirectoryDefaultMustPass()
    {
        string[] credKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, credKeys) +
        $"Authentication=ActiveDirectoryDefault;User ID={Config.UserManagedIdentityClientId};";

        // Connection should be established using Managed Identity by default.
        ConnectAndDisconnect(connStr);
    }

    // This test works on main in the existing jobs (like Win22_Sql22), but
    // fails in the Azure project tests on a similar agent/image:
    //
    //   Failed Microsoft.Data.SqlClient.Extensions.Azure.Test.AADConnectionTest.ADIntegratedUsingSSPI [59 ms]
    //   Error Message:
    //     Microsoft.Data.SqlClient.SqlException : Failed to authenticate the user NT Authority\Anonymous Logon in Active Directory (Authentication=ActiveDirectoryIntegrated).
    //   Error code 0xget_user_name_failed
    //   Failed to acquire access token for ActiveDirectoryIntegrated: Failed to get user name.
    //
    // ActiveIssue tests can be filtered out of test runs on the dotnet CLI
    // using the filter "category != failing".
    //
    [ActiveIssue("https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/40107")]
    [ConditionalFact(
        typeof(Config),
        nameof(Config.SupportsIntegratedSecurity),
        nameof(Config.HasTcpConnectionString))]
    public static void ADIntegratedUsingSSPI()
    {
        // test Passes with correct connection string.
        string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
        string connStr = RemoveKeysInConnStr(Config.TcpConnectionString, removeKeys) +
        $"Authentication=Active Directory Integrated;";
        ConnectAndDisconnect(connStr);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.SupportsManagedIdentity),
        nameof(Config.SupportsSystemAssignedManagedIdentity),
        nameof(Config.HasPasswordConnectionString))]
    public static void SystemAssigned_ManagedIdentityTest()
    {
        string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, removeKeys) +
        $"Authentication=Active Directory Managed Identity;";
        ConnectAndDisconnect(connStr);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnAdoPool),
        nameof(Config.HasPasswordConnectionString),
        nameof(Config.HasUserManagedIdentityClientId))]
    public static void UserAssigned_ManagedIdentityTest()
    {
        string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD" };
        string connStr = RemoveKeysInConnStr(Config.PasswordConnectionString, removeKeys) +
        $"Authentication=Active Directory Managed Identity; User Id={Config.UserManagedIdentityClientId};";
        ConnectAndDisconnect(connStr);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.SupportsManagedIdentity),
        nameof(Config.SupportsSystemAssignedManagedIdentity),
        nameof(Config.HasTcpConnectionString),
        nameof(Config.IsAzureSqlServer))]
    public static void Azure_SystemManagedIdentityTest()
    {
        string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
        string connectionString = RemoveKeysInConnStr(Config.TcpConnectionString, removeKeys)
        + $"Authentication=Active Directory Managed Identity;";

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();

            Assert.True(conn.State == System.Data.ConnectionState.Open);
        }
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.SupportsManagedIdentity),
        nameof(Config.SupportsSystemAssignedManagedIdentity),
        nameof(Config.HasTcpConnectionString),
        nameof(Config.HasUserManagedIdentityClientId),
        nameof(Config.IsAzureSqlServer))]
    public static void Azure_UserManagedIdentityTest()
    {
        string[] removeKeys = { "Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security" };
        string connectionString = RemoveKeysInConnStr(Config.TcpConnectionString, removeKeys)
            + $"Authentication=Active Directory Managed Identity; User Id={Config.UserManagedIdentityClientId}";

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            conn.Open();

            Assert.True(conn.State == System.Data.ConnectionState.Open);
        }
    }

    #region Helpers from AADConnectionTest.cs

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

    #endregion

    #region Helpers from ManualTests DataTestUtility.cs

    public static string RemoveKeysInConnStr(string connStr, string[] keysToRemove)
    {
        // tokenize connection string and remove input keys.
        string res = "";
        if (connStr != null && keysToRemove != null)
        {
            string[] keys = connStr.Split(';');
            foreach (var key in keys)
            {
                if (!string.IsNullOrEmpty(key.Trim()))
                {
                    bool removeKey = false;
                    foreach (var keyToRemove in keysToRemove)
                    {
                        if (key.Trim().ToLower().StartsWith(keyToRemove.Trim().ToLower(), StringComparison.Ordinal))
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
        }
        return res;
    }

    public static string? FetchKeyInConnStr(string connStr, string[] keys)
    {
        // tokenize connection string and find matching key
        if (connStr != null && keys != null)
        {
            string[] connProps = connStr.Split(';');
            foreach (string cp in connProps)
            {
                if (!string.IsNullOrEmpty(cp.Trim()))
                {
                    foreach (var key in keys)
                    {
                        if (cp.Trim().ToLower().StartsWith(key.Trim().ToLower(), StringComparison.Ordinal))
                        {
                            return cp.Substring(cp.IndexOf('=') + 1);
                        }
                    }
                }
            }
        }
        return null;
    }

    public static string RetrieveValueFromConnStr(string connStr, string[] keywords)
    {
        // tokenize connection string and retrieve value for a specific key.
        string res = "";
        if (connStr != null && keywords != null)
        {
            string[] keys = connStr.Split(';');
            foreach (var key in keys)
            {
                foreach (var keyword in keywords)
                {
                    if (!string.IsNullOrEmpty(key.Trim()))
                    {
                        if (key.Trim().ToLower().StartsWith(keyword.Trim().ToLower(), StringComparison.Ordinal))
                        {
                            res = key.Substring(key.IndexOf('=') + 1).Trim();
                            break;
                        }
                    }
                }
            }
        }
        return res;
    }

    #endregion
}
