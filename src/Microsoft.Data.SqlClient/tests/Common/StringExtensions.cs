// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Security;
using static Microsoft.Data.SqlClient.Tests.Common.CommonUtils;

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
            => EnsureSeparator(connectionString) + "UID=" + (user ?? "DummyUser") + ';';

        /// <summary>
        /// Adds user provided or a dummy password to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddPasswordToConnString(this string connectionString, string? password = null)
        {
            string? pwd = password;
            if(string.IsNullOrEmpty(pwd))
            {
                using SecureString secureString = CommonUtils.GenerateRandomSecureString(20);
                pwd = new NetworkCredential(string.Empty, secureString).Password;
            }
            return EnsureSeparator(connectionString) + "PWD=" + pwd + ';';
        }

        /// <summary>
        /// Adds integrated security to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddIntegratedSecurityToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Integrated Security=True;";

        /// <summary>
        /// Adds AAD password authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADPasswordAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryPassword;";

        /// <summary>
        /// Adds AAD integrated authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADIntegratedAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryIntegrated;";

        /// <summary>
        /// Adds AAD interactive authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADInteractiveAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryInteractive;";

        /// <summary>
        /// Adds AAD device code flow authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADDeviceCodeFlowAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryDeviceCodeFlow;";

        /// <summary>
        /// Adds AAD service principal authentication to the connection string to test the connection string parsing logic
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string AddServicePrincipalAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryServicePrincipal;";

        /// <summary>
        /// Adds AAD workload identity authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADWorkloadIdentityAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryWorkloadIdentity;";

        /// <summary>
        /// Adds AAD MSI authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADMSIAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryMSI;";

        /// <summary>
        /// Adds AAD default authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddAADDefaultAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryDefault;";

        /// <summary>
        /// Adds an invalid AAD authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddInvalidAADAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryInvalid;";

        /// <summary>
        /// Adds managed identity authentication to the connection string to test the connection string parsing logic
        /// </summary>
        public static string AddManagedIdentityAuthenticationToConnString(this string connectionString)
            => EnsureSeparator(connectionString) + "Authentication=ActiveDirectoryManagedIdentity;";

        /// <summary>
        /// Removes the authentication and credential properties from the connection string. The properties removed are: "Authentication", "User ID", "Password", "UID", and "PWD".
        /// This is useful for testing scenarios where you want to ensure that the connection string does not
        /// </summary>
        /// <param name="connectionString"></param>
        /// <returns></returns>
        public static string RemoveAuthAndCredsProperties(this string connectionString)
        {
            string[] removeKeys = ["Authentication", "User ID", "Password", "UID", "PWD", "Trusted_Connection", "Integrated Security"];
            return RemoveKeysInConnStr(connectionString, removeKeys);
        }

        /// <summary>
        /// Removes the specified keys from the connection string. The keys are case-insensitive and will be removed if they match the start of a key in the connection string.
        /// </summary>
        /// <param name="connStr">The connection string to modify.</param>
        /// <param name="keysToRemove">The keys to remove from the connection string.</param>
        /// <returns>The modified connection string with the specified keys removed.</returns>
        public static string RemoveKeysInConnStr(this string connStr, string[] keysToRemove)
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
    }
}
