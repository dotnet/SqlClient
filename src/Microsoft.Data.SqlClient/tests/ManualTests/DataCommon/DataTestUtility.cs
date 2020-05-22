// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Client;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DataTestUtility
    {
        public static readonly string NPConnectionString = null;
        public static readonly string TCPConnectionString = null;
        public static readonly string TCPConnectionStringHGSVBS = null;
        public static readonly string TCPConnectionStringAASVBS = null;
        public static readonly string TCPConnectionStringAASSGX = null;
        public static readonly string AADAuthorityURL = null;
        public static readonly string AADPasswordConnectionString = null;
        public static readonly string AADAccessToken = null;
        public static readonly string AKVBaseUrl = null;
        public static readonly string AKVUrl = null;
        public static readonly string AKVClientId = null;
        public static readonly string AKVClientSecret = null;
        public static List<string> AEConnStrings = new List<string>();
        public static List<string> AEConnStringsSetup = new List<string>();
        public static readonly bool EnclaveEnabled = false;
        public static readonly bool TracingEnabled = false;
        public static readonly bool SupportsIntegratedSecurity = false;
        public static readonly bool SupportsLocalDb = false;
        public static readonly bool SupportsFileStream = false;
        public static readonly bool UseManagedSNIOnWindows = false;

        public const string UdtTestDbName = "UdtTestDb";
        public const string AKVKeyName = "TestSqlClientAzureKeyVaultProvider";
        private const string ManagedNetworkingAppContextSwitch = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";

        private static readonly string[] AzureSqlServerEndpoints = {".database.windows.net",
                                                                     ".database.cloudapi.de",
                                                                     ".database.usgovcloudapi.net",
                                                                     ".database.chinacloudapi.cn"};

        private static Dictionary<string, bool> AvailableDatabases;
        private static TraceEventListener TraceListener;

        private class Config
        {
            public string TCPConnectionString = null;
            public string NPConnectionString = null;
            public string TCPConnectionStringHGSVBS = null;
            public string TCPConnectionStringAASVBS = null;
            public string TCPConnectionStringAASSGX = null;
            public string AADAuthorityURL = null;
            public string AADPasswordConnectionString = null;
            public string AzureKeyVaultURL = null;
            public string AzureKeyVaultClientId = null;
            public string AzureKeyVaultClientSecret = null;
            public bool EnclaveEnabled = false;
            public bool TracingEnabled = false;
            public bool SupportsIntegratedSecurity = false;
            public bool SupportsLocalDb = false;
            public bool SupportsFileStream = false;
            public bool UseManagedSNIOnWindows = false;
        }

        static DataTestUtility()
        {
            using (StreamReader r = new StreamReader("config.json"))
            {
                string json = r.ReadToEnd();
                Config c = JsonConvert.DeserializeObject<Config>(json);

                NPConnectionString = c.NPConnectionString;
                TCPConnectionString = c.TCPConnectionString;
                TCPConnectionStringHGSVBS = c.TCPConnectionStringHGSVBS;
                TCPConnectionStringAASVBS = c.TCPConnectionStringAASVBS;
                TCPConnectionStringAASSGX = c.TCPConnectionStringAASSGX;
                AADAuthorityURL = c.AADAuthorityURL;
                AADPasswordConnectionString = c.AADPasswordConnectionString;
                SupportsLocalDb = c.SupportsLocalDb;
                SupportsIntegratedSecurity = c.SupportsIntegratedSecurity;
                SupportsFileStream = c.SupportsFileStream;
                EnclaveEnabled = c.EnclaveEnabled;
                TracingEnabled = c.TracingEnabled;
                UseManagedSNIOnWindows = c.UseManagedSNIOnWindows;

                if (TracingEnabled)
                {
                    TraceListener = new DataTestUtility.TraceEventListener();
                }

                if (UseManagedSNIOnWindows)
                {
                    AppContext.SetSwitch(ManagedNetworkingAppContextSwitch, true);
                    Console.WriteLine($"App Context switch {ManagedNetworkingAppContextSwitch} enabled on {Environment.OSVersion}");
                }

                if (IsAADPasswordConnStrSetup() && IsAADAuthorityURLSetup())
                {
                    string username = RetrieveValueFromConnStr(AADPasswordConnectionString, new string[] { "User ID", "UID" });
                    string password = RetrieveValueFromConnStr(AADPasswordConnectionString, new string[] { "Password", "PWD" });
                    AADAccessToken = GenerateAccessToken(AADAuthorityURL, username, password);
                }

                string url = c.AzureKeyVaultURL;
                Uri AKVBaseUri = null;
                if (!string.IsNullOrEmpty(url) && Uri.TryCreate(url, UriKind.Absolute, out AKVBaseUri))
                {
                    AKVBaseUri = new Uri(AKVBaseUri, "/");
                    AKVBaseUrl = AKVBaseUri.AbsoluteUri;
                    AKVUrl = (new Uri(AKVBaseUri, $"/keys/{AKVKeyName}")).AbsoluteUri;
                }

                AKVClientId = c.AzureKeyVaultClientId;
                AKVClientSecret = c.AzureKeyVaultClientSecret;
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

                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
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
        private static string GenerateAccessToken(string authorityURL, string aADAuthUserID, string aADAuthPassword)
        {
            return AcquireTokenAsync(authorityURL, aADAuthUserID, aADAuthPassword).Result;
        }

        private static Task<string> AcquireTokenAsync(string authorityURL, string userID, string password) => Task.Run(() =>
        {
            // The below properties are set specific to test configurations.
            string scope = "https://database.windows.net//.default";
            string applicationName = "Microsoft Data SqlClient Manual Tests";
            string clientVersion = "1.0.0.0";
            string adoClientId = "4d079b4c-cab7-4b7c-a115-8fd51b6f8239";

            IPublicClientApplication app = PublicClientApplicationBuilder.Create(adoClientId)
                .WithAuthority(authorityURL)
                .WithClientName(applicationName)
                .WithClientVersion(clientVersion)
                .Build();
            AuthenticationResult result;
            string[] scopes = new string[] { scope };

            // Note: CorrelationId, which existed in ADAL, can not be set in MSAL (yet?).
            // parameter.ConnectionId was passed as the CorrelationId in ADAL to aid support in troubleshooting.
            // If/When MSAL adds CorrelationId support, it should be passed from parameters here, too.

            SecureString securePassword = new SecureString();

            foreach (char c in password)
                securePassword.AppendChar(c);
            securePassword.MakeReadOnly();
            result = app.AcquireTokenByUsernamePassword(scopes, userID, securePassword).ExecuteAsync().Result;

            return result.AccessToken;
        });

        public static bool IsDatabasePresent(string name)
        {
            AvailableDatabases = AvailableDatabases ?? new Dictionary<string, bool>();
            bool present = false;
            if (AreConnStringsSetup() && !string.IsNullOrEmpty(name) && !AvailableDatabases.TryGetValue(name, out present))
            {
                var builder = new SqlConnectionStringBuilder(TCPConnectionString);
                builder.ConnectTimeout = 2;
                using (var connection = new SqlConnection(builder.ToString()))
                using (var command = new SqlCommand("SELECT COUNT(*) FROM sys.databases WHERE name=@name", connection))
                {
                    connection.Open();
                    command.Parameters.AddWithValue("name", name);
                    present = Convert.ToInt32(command.ExecuteScalar()) == 1;
                }
                AvailableDatabases[name] = present;
            }
            return present;
        }

        /// <summary>
        /// Checks if object SYS.SENSITIVITY_CLASSIFICATIONS exists in SQL Server
        /// </summary>
        /// <returns>True, if target SQL Server supports Data Classification</returns>
        public static bool IsSupportedDataClassification()
        {
            try
            {
                using (var connection = new SqlConnection(TCPConnectionString))
                using (var command = new SqlCommand("SELECT * FROM SYS.SENSITIVITY_CLASSIFICATIONS", connection))
                {
                    connection.Open();
                    command.ExecuteNonQuery();
                }
            }
            catch (SqlException e)
            {
                // Check for Error 208: Invalid Object Name
                if (e.Errors != null && e.Errors[0].Number == 208)
                {
                    return false;
                }
            }
            return true;
        }
        public static bool IsUdtTestDatabasePresent() => IsDatabasePresent(UdtTestDbName);

        public static bool AreConnStringsSetup()
        {
            return !string.IsNullOrEmpty(NPConnectionString) && !string.IsNullOrEmpty(TCPConnectionString);
        }

        public static bool AreConnStringSetupForAE()
        {
            return AEConnStrings.Count > 0;
        }

        public static bool IsAADPasswordConnStrSetup()
        {
            return !string.IsNullOrEmpty(AADPasswordConnectionString);
        }

        public static bool IsAADAuthorityURLSetup()
        {
            return !string.IsNullOrEmpty(AADAuthorityURL);
        }

        public static bool IsNotAzureServer()
        {
            return AreConnStringsSetup() ? !DataTestUtility.IsAzureSqlServer(new SqlConnectionStringBuilder((DataTestUtility.TCPConnectionString)).DataSource) : true;
        }

        public static bool IsAKVSetupAvailable()
        {
            return !string.IsNullOrEmpty(AKVUrl) && !string.IsNullOrEmpty(AKVClientId) && !string.IsNullOrEmpty(AKVClientSecret);
        }

        public static bool IsUsingManagedSNI() => UseManagedSNIOnWindows;

        public static bool IsUsingNativeSNI() => !IsUsingManagedSNI();

        public static bool IsUTF8Supported()
        {
            bool retval = false;
            if (AreConnStringsSetup())
            {
                using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
                using (SqlCommand command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandText = "SELECT CONNECTIONPROPERTY('SUPPORT_UTF8')";
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // CONNECTIONPROPERTY('SUPPORT_UTF8') returns NULL in SQLServer versions that don't support UTF-8.
                            retval = !reader.IsDBNull(0);
                        }
                    }
                }
            }
            return retval;
        }

        public static bool IsTCPConnectionStringPasswordIncluded()
        {
            return RetrieveValueFromConnStr(TCPConnectionString, new string[] { "Password", "PWD" }) != string.Empty;
        }

        // the name length will be no more then (16 + prefix.Length + escapeLeft.Length + escapeRight.Length)
        // some providers does not support names (Oracle supports up to 30)
        public static string GetUniqueName(string prefix)
        {
            string escapeLeft = "[";
            string escapeRight = "]";
            string uniqueName = string.Format("{0}{1}_{2}_{3}{4}",
                escapeLeft,
                prefix,
                DateTime.Now.Ticks.ToString("X", CultureInfo.InvariantCulture), // up to 8 characters
                Guid.NewGuid().ToString().Substring(0, 6), // take the first 6 characters only
                escapeRight);
            return uniqueName;
        }

        // SQL Server supports long names (up to 128 characters), add extra info for troubleshooting
        public static string GetUniqueNameForSqlServer(string prefix)
        {
            string extendedPrefix = string.Format(
                "{0}_{1}@{2}",
                prefix,
                Environment.UserName,
                Environment.MachineName,
                DateTime.Now.ToString("yyyy_MM_dd", CultureInfo.InvariantCulture));
            string name = GetUniqueName(extendedPrefix);
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

        public static void DropUserDefinedType(SqlConnection sqlConnection, string typeName)
        {
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (TYPE_ID('{0}') IS NOT NULL) \n DROP TYPE {0}", typeName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static void DropStoredProcedure(SqlConnection sqlConnection, string spName)
        {
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) \n DROP PROCEDURE {0}", spName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static bool IsLocalDBInstalled() => SupportsLocalDb;

        public static bool IsIntegratedSecuritySetup() => SupportsIntegratedSecurity;

        public static string GetAccessToken()
        {
            // Creates a new Object Reference of Access Token - See GitHub Issue 438
            return (null != AADAccessToken) ? new string(AADAccessToken.ToCharArray()) : null;
        }

        public static bool IsAccessTokenSetup() => !string.IsNullOrEmpty(GetAccessToken());

        public static bool IsFileStreamSetup() => SupportsFileStream;

        // This method assumes dataSource parameter is in TCP connection string format.
        public static bool IsAzureSqlServer(string dataSource)
        {
            int i = dataSource.LastIndexOf(',');
            if (i >= 0)
            {
                dataSource = dataSource.Substring(0, i);
            }

            i = dataSource.LastIndexOf('\\');
            if (i >= 0)
            {
                dataSource = dataSource.Substring(0, i);
            }

            // trim redundant whitespace
            dataSource = dataSource.Trim();

            // check if servername end with any azure endpoints
            for (i = 0; i < AzureSqlServerEndpoints.Length; i++)
            {
                if (dataSource.EndsWith(AzureSqlServerEndpoints[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool CheckException<TException>(Exception ex, string exceptionMessage, bool innerExceptionMustBeNull) where TException : Exception
        {
            return ((ex != null) && (ex is TException) &&
                ((string.IsNullOrEmpty(exceptionMessage)) || (ex.Message.Contains(exceptionMessage))) &&
                ((!innerExceptionMustBeNull) || (ex.InnerException == null)));
        }

        public static void AssertEqualsWithDescription(object expectedValue, object actualValue, string failMessage)
        {
            if (expectedValue == null || actualValue == null)
            {
                var msg = string.Format("{0}\nExpected: {1}\nActual: {2}", failMessage, expectedValue, actualValue);
                Assert.True(expectedValue == actualValue, msg);
            }
            else
            {
                var msg = string.Format("{0}\nExpected: {1} ({2})\nActual: {3} ({4})", failMessage, expectedValue, expectedValue.GetType(), actualValue, actualValue.GetType());
                Assert.True(expectedValue.Equals(actualValue), msg);
            }
        }

        public static TException AssertThrowsWrapper<TException>(Action actionThatFails, string exceptionMessage = null, bool innerExceptionMustBeNull = false, Func<TException, bool> customExceptionVerifier = null) where TException : Exception
        {
            TException ex = Assert.Throws<TException>(actionThatFails);
            if (exceptionMessage != null)
            {
                Assert.True(ex.Message.Contains(exceptionMessage),
                    string.Format("FAILED: Exception did not contain expected message.\nExpected: {0}\nActual: {1}", exceptionMessage, ex.Message));
            }

            if (innerExceptionMustBeNull)
            {
                Assert.True(ex.InnerException == null, "FAILED: Expected InnerException to be null.");
            }

            if (customExceptionVerifier != null)
            {
                Assert.True(customExceptionVerifier(ex), "FAILED: Custom exception verifier returned false for this exception.");
            }

            return ex;
        }

        public static TException AssertThrowsWrapper<TException, TInnerException>(Action actionThatFails, string exceptionMessage = null, string innerExceptionMessage = null, bool innerExceptionMustBeNull = false, Func<TException, bool> customExceptionVerifier = null) where TException : Exception
        {
            TException ex = AssertThrowsWrapper<TException>(actionThatFails, exceptionMessage, innerExceptionMustBeNull, customExceptionVerifier);

            if (innerExceptionMessage != null)
            {
                Assert.True(ex.InnerException != null, "FAILED: Cannot check innerExceptionMessage because InnerException is null.");
                Assert.True(ex.InnerException.Message.Contains(innerExceptionMessage),
                    string.Format("FAILED: Inner Exception did not contain expected message.\nExpected: {0}\nActual: {1}", innerExceptionMessage, ex.InnerException.Message));
            }

            return ex;
        }

        public static TException AssertThrowsWrapper<TException, TInnerException, TInnerInnerException>(Action actionThatFails, string exceptionMessage = null, string innerExceptionMessage = null, string innerInnerExceptionMessage = null, bool innerInnerInnerExceptionMustBeNull = false) where TException : Exception where TInnerException : Exception where TInnerInnerException : Exception
        {
            TException ex = AssertThrowsWrapper<TException, TInnerException>(actionThatFails, exceptionMessage, innerExceptionMessage);
            if (innerInnerInnerExceptionMustBeNull)
            {
                Assert.True(ex.InnerException != null, "FAILED: Cannot check innerInnerInnerExceptionMustBeNull since InnerException is null");
                Assert.True(ex.InnerException.InnerException == null, "FAILED: Expected InnerInnerException to be null.");
            }

            if (innerInnerExceptionMessage != null)
            {
                Assert.True(ex.InnerException != null, "FAILED: Cannot check innerInnerExceptionMessage since InnerException is null");
                Assert.True(ex.InnerException.InnerException != null, "FAILED: Cannot check innerInnerExceptionMessage since InnerInnerException is null");
                Assert.True(ex.InnerException.InnerException.Message.Contains(innerInnerExceptionMessage),
                    string.Format("FAILED: Inner Exception did not contain expected message.\nExpected: {0}\nActual: {1}", innerInnerExceptionMessage, ex.InnerException.InnerException.Message));
            }

            return ex;
        }

        public static TException ExpectFailure<TException>(Action actionThatFails, string[] exceptionMessages, bool innerExceptionMustBeNull = false, Func<TException, bool> customExceptionVerifier = null) where TException : Exception
        {
            try
            {
                actionThatFails();
                Assert.False(true, "ERROR: Did not get expected exception");
                return null;
            }
            catch (Exception ex)
            {
                foreach (string exceptionMessage in exceptionMessages)
                {
                    if ((CheckException<TException>(ex, exceptionMessage, innerExceptionMustBeNull)) && ((customExceptionVerifier == null) || (customExceptionVerifier(ex as TException))))
                    {
                        return (ex as TException);
                    }
                }
                throw;
            }
        }

        public static TException ExpectFailure<TException, TInnerException>(Action actionThatFails, string exceptionMessage = null, string innerExceptionMessage = null, bool innerInnerExceptionMustBeNull = false) where TException : Exception where TInnerException : Exception
        {
            try
            {
                actionThatFails();
                Assert.False(true, "ERROR: Did not get expected exception");
                return null;
            }
            catch (Exception ex)
            {
                if ((CheckException<TException>(ex, exceptionMessage, false)) && (CheckException<TInnerException>(ex.InnerException, innerExceptionMessage, innerInnerExceptionMustBeNull)))
                {
                    return (ex as TException);
                }
                else
                {
                    throw;
                }
            }
        }

        public static TException ExpectFailure<TException, TInnerException, TInnerInnerException>(Action actionThatFails, string exceptionMessage = null, string innerExceptionMessage = null, string innerInnerExceptionMessage = null, bool innerInnerInnerExceptionMustBeNull = false) where TException : Exception where TInnerException : Exception where TInnerInnerException : Exception
        {
            try
            {
                actionThatFails();
                Assert.False(true, "ERROR: Did not get expected exception");
                return null;
            }
            catch (Exception ex)
            {
                if ((CheckException<TException>(ex, exceptionMessage, false)) && (CheckException<TInnerException>(ex.InnerException, innerExceptionMessage, false)) && (CheckException<TInnerInnerException>(ex.InnerException.InnerException, innerInnerExceptionMessage, innerInnerInnerExceptionMustBeNull)))
                {
                    return (ex as TException);
                }
                else
                {
                    throw;
                }
            }
        }

        public static void ExpectAsyncFailure<TException>(Func<Task> actionThatFails, string exceptionMessage = null, bool innerExceptionMustBeNull = false) where TException : Exception
        {
            ExpectFailure<AggregateException, TException>(() => actionThatFails().Wait(), null, exceptionMessage, innerExceptionMustBeNull);
        }

        public static void ExpectAsyncFailure<TException, TInnerException>(Func<Task> actionThatFails, string exceptionMessage = null, string innerExceptionMessage = null, bool innerInnerExceptionMustBeNull = false) where TException : Exception where TInnerException : Exception
        {
            ExpectFailure<AggregateException, TException, TInnerException>(() => actionThatFails().Wait(), null, exceptionMessage, innerExceptionMessage, innerInnerExceptionMustBeNull);
        }

        public static string GenerateObjectName()
        {
            return string.Format("TEST_{0}{1}{2}", Environment.GetEnvironmentVariable("ComputerName"), Environment.TickCount, Guid.NewGuid()).Replace('-', '_');
        }

        // Returns randomly generated characters of length 11.
        public static string GenerateRandomCharacters(string prefix)
        {
            string path = Path.GetRandomFileName();
            path = path.Replace(".", ""); // Remove period.
            return prefix + path;
        }

        public static void RunNonQuery(string connectionString, string sql, int numberOfRetries = 0)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    using (SqlConnection connection = new SqlConnection(connectionString))
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        connection.Open();
                        command.CommandText = sql;
                        command.ExecuteNonQuery();
                        break;
                    }
                }
                catch (Exception)
                {
                    if (retries >= numberOfRetries)
                    {
                        throw;
                    }
                    retries++;
                    Thread.Sleep(1000);
                }
            }
        }

        public static DataTable RunQuery(string connectionString, string sql)
        {
            DataTable result = null;
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        result = new DataTable();
                        result.Load(reader);
                    }
                }
            }
            return result;
        }

        public static void DropFunction(SqlConnection sqlConnection, string funcName)
        {
            using (SqlCommand cmd = new SqlCommand(string.Format("IF EXISTS (SELECT * FROM sys.objects WHERE name = '{0}') \n DROP FUNCTION {0}", funcName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static string GetValueString(object paramValue)
        {
            if (paramValue.GetType() == typeof(DateTimeOffset))
            {
                return ((DateTimeOffset)paramValue).ToString("M/d/yyyy hh:mm:ss tt zzz");
            }
            else if (paramValue.GetType() == typeof(DateTime))
            {
                return ((DateTime)paramValue).ToString("M/d/yyyy hh:mm:ss tt");
            }
            else if (paramValue.GetType() == typeof(SqlDateTime))
            {
                return ((SqlDateTime)paramValue).Value.ToString("M/d/yyyy hh:mm:ss tt");
            }

            return paramValue.ToString();
        }

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
            }
            return res;
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
                            if (key.Trim().ToLower().StartsWith(keyword.Trim().ToLower()))
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

        public class TraceEventListener : EventListener
        {
            public List<int> IDs = new List<int>();

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource"))
                {
                    // Collect all traces for better code coverage
                    EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                IDs.Add(eventData.EventId);
            }
        }
    }
}
