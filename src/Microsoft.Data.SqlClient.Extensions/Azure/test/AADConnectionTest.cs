// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39072):
// TODO(https://sqlclientdrivers.visualstudio.com/ADO.Net/_workitems/edit/39073):
// This file has intentionally not been tidied up or modernized.  Its content will be absorbed into
// new unit and/or integration tests in the future.

using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient.Tests.Common;

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
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.HasAzureSqlConnectionString),
        nameof(Config.HasServicePrincipal))]
    public static void NoCredentialsActiveDirectoryServicePrincipal()
    {
        // test Passes with correct connection string.
        string connString = Config.AzureSqlConnString
            .AddServicePrincipalAuthenticationToConnString()
            .AddUserToConnString(Config.ServicePrincipalId)
            .AddPasswordToConnString(Config.ServicePrincipalSecret);
        
        ConnectAndDisconnect(connString);

        // connection fails with expected error message.
        string connStrWithNoCred = Config.AzureSqlConnString
            .AddServicePrincipalAuthenticationToConnString();

        InvalidOperationException e = Assert.Throws<InvalidOperationException>
        (() => ConnectAndDisconnect(connStrWithNoCred));

        string expectedMessage = "Either Credential or both 'User ID' and 'Password' (or 'UID' and 'PWD') connection string keywords must be specified, if 'Authentication=Active Directory Service Principal'.";
        Assert.Contains(expectedMessage, e.Message);
    }

    [ConditionalTheory(
        typeof(Config),
        nameof(Config.HasAzureSqlConnectionString),
        nameof(Config.HasUserManagedIdentityClientId))]
    [InlineData("2445343 2343253")]
    [InlineData("2445343$#^@@%2343253")]
    public static void ActiveDirectoryManagedIdentityWithInvalidUserIdMustFail(string userId)
    {
        // connection fails with expected error message.
        string connStrWithNoCred = Config.AzureSqlConnString
            .AddManagedIdentityAuthenticationToConnString()
            .AddUserToConnString(userId);

        SqlException e = Assert.Throws<SqlException>(() => ConnectAndDisconnect(connStrWithNoCred));

        // The Azure.Identity surface message can vary across SDK versions and
        // platforms, so assert on the stable driver-emitted error that proves
        // the managed-identity auth path was taken and failed.
        Regex expected = new(
            @"Failed to authenticate the user.*Authentication=ActiveDirectoryManagedIdentity",
            RegexOptions.IgnoreCase);

        Assert.Matches(expected, e.GetBaseException().Message);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnAdoPool),
        nameof(Config.HasAzureSqlConnectionString),
        nameof(Config.HasUserManagedIdentityClientId))]
    public static void ActiveDirectoryDefaultMustPass()
    {
        string connStr = Config.AzureSqlConnString
            .AddAADDefaultAuthenticationToConnString()
            .AddUserToConnString(Config.UserManagedIdentityClientId);

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
        string connStr = Config.TcpConnectionString
            .RemoveAuthAndCredsProperties()
            .AddAADIntegratedAuthenticationToConnString();
        ConnectAndDisconnect(connStr);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.SupportsManagedIdentity),
        nameof(Config.SupportsSystemAssignedManagedIdentity),
        nameof(Config.HasAzureSqlConnectionString))]
    public static void SystemAssigned_ManagedIdentityTest()
    {
        string connStr = Config.AzureSqlConnString
            .AddManagedIdentityAuthenticationToConnString();

        ConnectAndDisconnect(connStr);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnAdoPool),
        nameof(Config.HasAzureSqlConnectionString),
        nameof(Config.HasUserManagedIdentityClientId))]
    public static void UserAssigned_ManagedIdentityTest()
    {
        string connStr = Config.AzureSqlConnString
            .AddManagedIdentityAuthenticationToConnString()
            .AddUserToConnString(Config.UserManagedIdentityClientId);

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
        string connectionString = Config.TcpConnectionString
            .RemoveAuthAndCredsProperties()
            .AddManagedIdentityAuthenticationToConnString();

        using SqlConnection conn = new(connectionString);
        conn.Open();

        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    [ConditionalFact(
        typeof(Config),
        nameof(Config.OnAdoPool),
        nameof(Config.SupportsManagedIdentity),
        nameof(Config.HasTcpConnectionString),
        nameof(Config.HasUserManagedIdentityClientId),
        nameof(Config.IsAzureSqlServer))]
    public static void Azure_UserManagedIdentityTest()
    {
        string connectionString = Config.TcpConnectionString
            .RemoveAuthAndCredsProperties()
            .AddManagedIdentityAuthenticationToConnString()
            .AddUserToConnString(Config.UserManagedIdentityClientId);

        using SqlConnection conn = new(connectionString);
        conn.Open();

        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    // The helpers below were copied verbatim from AADConnectionTest.cs and ManualTests
    // DataTestUtility.cs in MDS.  No attempt has been made to share them via a common project since
    // they will likely disappear when the tests above are modernized.

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

        Assert.Equal(System.Data.ConnectionState.Open, conn.State);
    }

    #endregion

    #region Helpers from ManualTests DataTestUtility.cs

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
