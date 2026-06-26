// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Security;
using static Microsoft.Data.SqlClient.Tests.Common.TestRandomUtilities;

namespace Microsoft.Data.SqlClient.Tests.Common
{
    /// <summary>
    /// Extension methods for string class to add connection string keywords to connection string
    /// for testing connection string parsing logic
    /// </summary>
    public static class StringExtensions
    {
        /// <summary>
        /// Adds a user provided or a dummy user to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddUserToConnString(this string connectionString, string? user = null)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                UserID = user ?? "DummyUser"
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds a user provided or randomly generated password to the connection string for
        /// testing SQL Server Authentication (password auth). Not related to Entra ID Password auth,
        /// which has been removed. This is used to test SQL auth scenarios and connection string parsing.
        /// </summary>
        public static string AddPasswordToConnString(this string connectionString, string? password = null)
        {
            string? pwd = password;
            if (string.IsNullOrEmpty(pwd))
            {
                using SecureString secureString = GenerateRandomSecureString(20);
                pwd = new NetworkCredential(string.Empty, secureString).Password;
            }
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Password = pwd
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds integrated security to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddIntegratedSecurityToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                IntegratedSecurity = true
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD password authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADPasswordAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
#pragma warning disable CS0618 // ActiveDirectoryPassword is deprecated; used here only for testing legacy authentication scenarios
                Authentication = SqlAuthenticationMethod.ActiveDirectoryPassword
            };
#pragma warning restore CS0618
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD integrated authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADIntegratedAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryIntegrated
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD interactive authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADInteractiveAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryInteractive
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD device code flow authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADDeviceCodeFlowAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryDeviceCodeFlow
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD service principal authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddServicePrincipalAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD workload identity authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADWorkloadIdentityAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryWorkloadIdentity
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD MSI authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADMSIAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryMSI
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds AAD default authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADDefaultAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Adds an invalid AAD authentication value to the connection string to test parser error handling
        /// </summary>
        public static string AddInvalidAADAuthenticationToConnString(this string connectionString)
        {
            // NOTE: Intentionally uses raw string manipulation. SqlConnectionStringBuilder validates
            // the Authentication value against SqlAuthenticationMethod and would throw ArgumentException
            // for "ActiveDirectoryInvalid". This method exists to test parser behavior with invalid input.
            string separator = string.IsNullOrEmpty(connectionString) || connectionString.TrimEnd().EndsWith(";", StringComparison.Ordinal)
                ? string.Empty : ";";
            return connectionString + separator + "Authentication=ActiveDirectoryInvalid;";
        }

        /// <summary>
        /// Adds managed identity authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddManagedIdentityAuthenticationToConnString(this string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity
            };
            return builder.ConnectionString;
        }

        /// <summary>
        /// Removes the authentication and credential properties from the connection string. The properties removed are: "Authentication", "User ID", "Password", "UID", and "PWD".
        /// This is useful for testing scenarios where you want to ensure that the connection string does not contain any sensitive information related to authentication or credentials.
        /// </summary>
        public static string RemoveAuthAndCredsProperties(this string connectionString)
        {
            string[] removeKeys = ["Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security"];
            return RemoveKeysInConnStr(connectionString, removeKeys);
        }

        /// <summary>
        /// Removes the specified keys from the connection string using SqlConnectionStringBuilder.
        /// </summary>
        /// <param name="connStr">The connection string to modify.</param>
        /// <param name="keysToRemove">The keys to remove from the connection string.</param>
        /// <returns>The modified connection string with the specified keys removed.</returns>
        public static string RemoveKeysInConnStr(this string connStr, string[] keysToRemove)
        {
            if (connStr == null || keysToRemove == null)
            {
                return connStr ?? string.Empty;
            }

            var builder = new SqlConnectionStringBuilder(connStr);
            foreach (var key in keysToRemove)
            {
                builder.Remove(key);
            }

            return builder.ConnectionString;
        }
    }
}
