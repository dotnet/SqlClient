// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Data.SqlClient.TestUtilities;

namespace Microsoft.Data.SqlClient.Performance.Tests
{
    public static class PerfTestUtility
    {
        public static readonly string NPConnectionString = null;
        public static readonly string TCPConnectionString = null;
        public static readonly string TCPConnectionStringHGSVBS = null;
        public static readonly string TCPConnectionStringAASVBS = null;
        public static readonly string TCPConnectionStringAASSGX = null;
        public static List<string> AEConnStrings = new List<string>();
        public static List<string> AEConnStringsSetup = new List<string>();
        public static readonly bool EnclaveEnabled = false;
        public static readonly bool UseManagedSNIOnWindows = false;

        private const string ManagedNetworkingAppContextSwitch = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";

        static PerfTestUtility()
        {
            Config c = Config.Load();
            NPConnectionString = c.NPConnectionString;
            TCPConnectionString = c.TCPConnectionString;
            TCPConnectionStringHGSVBS = c.TCPConnectionStringHGSVBS;
            TCPConnectionStringAASVBS = c.TCPConnectionStringAASVBS;
            TCPConnectionStringAASSGX = c.TCPConnectionStringAASSGX;
            UseManagedSNIOnWindows = c.UseManagedSNIOnWindows;
            EnclaveEnabled = c.EnclaveEnabled;

            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;

            if (UseManagedSNIOnWindows)
            {
                AppContext.SetSwitch(ManagedNetworkingAppContextSwitch, true);
                Console.WriteLine($"App Context switch {ManagedNetworkingAppContextSwitch} enabled on {Environment.OSVersion}");
            }

            if (EnclaveEnabled)
            {
                if (!string.IsNullOrEmpty(TCPConnectionStringHGSVBS))
                {
                    AEConnStrings.Add(TCPConnectionStringHGSVBS);
                    AEConnStringsSetup.Add(TCPConnectionStringHGSVBS);
                }

                if (!string.IsNullOrEmpty(TCPConnectionStringAASVBS))
                {
                    AEConnStrings.Add(TCPConnectionStringAASVBS);
                }

                if (!string.IsNullOrEmpty(TCPConnectionStringAASSGX))
                {
                    AEConnStrings.Add(TCPConnectionStringAASSGX);
                    AEConnStringsSetup.Add(TCPConnectionStringAASSGX);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(TCPConnectionString))
                {
                    AEConnStrings.Add(TCPConnectionString);
                    AEConnStringsSetup.Add(TCPConnectionString);
                }
            }
        }

        public static IEnumerable<string> ConnectionStrings
        {
            get
            {
                if (!string.IsNullOrEmpty(TCPConnectionString))
                {
                    yield return TCPConnectionString;
                }
                // Named Pipes are not supported on Unix platform and for Azure DB
                if (Environment.OSVersion.Platform != PlatformID.Unix && IsNotAzureServer() && !string.IsNullOrEmpty(NPConnectionString))
                {
                    yield return NPConnectionString;
                }
                if (EnclaveEnabled)
                {
                    foreach (var connStr in AEConnStrings)
                    {
                        yield return connStr;
                    }
                }
            }
        }

        public static bool AreConnStringsSetup()
        {
            return !string.IsNullOrEmpty(NPConnectionString) && !string.IsNullOrEmpty(TCPConnectionString);
        }

        public static bool IsNotAzureServer()
        {
            return !AreConnStringsSetup() || !Utils.IsAzureSqlServer(new SqlConnectionStringBuilder((TCPConnectionString)).DataSource);
        }

        public static bool IsNotUsingManagedSNIOnWindows() => !UseManagedSNIOnWindows;


        /// <summary>
        /// Generate a unique name to use in Sql Server; 
        /// some providers does not support names (Oracle supports up to 30).
        /// </summary>
        /// <param name="prefix">The name length will be no more then (16 + prefix.Length + escapeLeft.Length + escapeRight.Length).</param>
        /// <param name="withBracket">Name without brackets.</param>
        /// <returns>Unique name by considering the Sql Server naming rules.</returns>
        public static string GetUniqueName(string prefix, bool withBracket = true)
        {
            string escapeLeft = withBracket ? "[" : string.Empty;
            string escapeRight = withBracket ? "]" : string.Empty;
            string uniqueName = string.Format("{0}{1}_{2}_{3}{4}",
                escapeLeft,
                prefix,
                DateTime.Now.Ticks.ToString("X", CultureInfo.InvariantCulture), // up to 8 characters
                Guid.NewGuid().ToString().Substring(0, 6), // take the first 6 characters only
                escapeRight);
            return uniqueName;
        }

        /// <summary>
        /// Uses environment values `UserName` and `MachineName` in addition to the specified `prefix` and current date
        /// to generate a unique name to use in Sql Server; 
        /// SQL Server supports long names (up to 128 characters), add extra info for troubleshooting.
        /// </summary>
        /// <param name="prefix">Add the prefix to the generate string.</param>
        /// <param name="withBracket">Database name must be pass with brackets by default.</param>
        /// <returns>Unique name by considering the Sql Server naming rules.</returns>
        public static string GetUniqueNameForSqlServer(string prefix, bool withBracket = true)
        {
            string extendedPrefix = string.Format(
                "{0}_{1}_{2}@{3}",
                prefix,
                Environment.UserName,
                Environment.MachineName,
                DateTime.Now.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture));
            string name = GetUniqueName(extendedPrefix, withBracket);
            if (name.Length > 128)
            {
                throw new ArgumentOutOfRangeException("the name is too long - SQL Server names are limited to 128");
            }
            return name;
        }

        public static void DropTable(SqlConnection sqlConnection, string tableName)
        {
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) \n DROP TABLE {0}", tableName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Drops specified database on provided connection.
        /// </summary>
        /// <param name="sqlConnection">Open connection to be used.</param>
        /// <param name="dbName">Database name without brackets.</param>
        public static void DropDatabase(SqlConnection sqlConnection, string dbName)
        {
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (EXISTS(SELECT 1 FROM sys.databases WHERE name = '{0}')) \nBEGIN \n ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE \n DROP DATABASE [{0}] \nEND", dbName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}
