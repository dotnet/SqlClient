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
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Data.SqlClient.TestUtilities;
using Microsoft.Identity.Client;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DataTestUtility
    {
        public static readonly string NPConnectionString = null;
        public static readonly string TCPConnectionString = null;
        public static readonly string TCPConnectionStringHGSVBS = null;
        public static readonly string TCPConnectionStringNoneVBS = null;
        public static readonly string TCPConnectionStringAASSGX = null;
        public static readonly string AADAuthorityURL = null;
        public static readonly string AADPasswordConnectionString = null;
        public static readonly string AADServicePrincipalId = null;
        public static readonly string AADServicePrincipalSecret = null;
        public static readonly string AKVOriginalUrl = null;
        public static readonly string AKVTenantId = null;
        public static readonly string LocalDbAppName = null;
        public static readonly string LocalDbSharedInstanceName = null;
        public static List<string> AEConnStrings = new List<string>();
        public static List<string> AEConnStringsSetup = new List<string>();
        public static bool EnclaveEnabled { get; private set; } = false;
        public static readonly bool TracingEnabled = false;
        public static readonly bool SupportsIntegratedSecurity = false;
        public static readonly bool UseManagedSNIOnWindows = false;

        public static Uri AKVBaseUri = null;
        public static readonly string PowerShellPath = null;
        public static string FileStreamDirectory = null;

        public static readonly string DNSCachingConnString = null;
        public static readonly string DNSCachingServerCR = null;  // this is for the control ring
        public static readonly string DNSCachingServerTR = null;  // this is for the tenant ring
        public static readonly bool IsDNSCachingSupportedCR = false;  // this is for the control ring
        public static readonly bool IsDNSCachingSupportedTR = false;  // this is for the tenant ring
        public static readonly string UserManagedIdentityClientId = null;
        public static readonly string AliasName = null;


        public static readonly string EnclaveAzureDatabaseConnString = null;
        public static bool ManagedIdentitySupported = true;
        public static string AADAccessToken = null;
        public static bool SupportsSystemAssignedManagedIdentity = false;
        public static string AADSystemIdentityAccessToken = null;
        public static string AADUserIdentityAccessToken = null;
        public const string ApplicationClientId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";
        public const string UdtTestDbName = "UdtTestDb";
        public const string EventSourcePrefix = "Microsoft.Data.SqlClient";
        public const string MDSEventSourceName = "Microsoft.Data.SqlClient.EventSource";
        public const string AKVEventSourceName = "Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.EventSource";
        private const string ManagedNetworkingAppContextSwitch = "Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows";

        private static Dictionary<string, bool> AvailableDatabases;
        private static BaseEventListener TraceListener;

        public static readonly bool IsManagedInstance = false;

        //Kerberos variables
        public static readonly string KerberosDomainUser = null;
        internal static readonly string KerberosDomainPassword = null;

        // SQL server Version
        private static string s_sQLServerVersion = string.Empty;
        private static bool s_isTDS8Supported;

        //SQL Server EngineEdition
        private static string s_sqlServerEngineEdition;

        // Azure Synapse EngineEditionId == 6
        // More could be read at https://learn.microsoft.com/en-us/sql/t-sql/functions/serverproperty-transact-sql?view=sql-server-ver16#propertyname
        public static bool IsAzureSynapse
        {
            get
            {
                if (!string.IsNullOrEmpty(TCPConnectionString))
                {
                    s_sqlServerEngineEdition ??= GetSqlServerProperty(TCPConnectionString, "EngineEdition");
                }
                _ = int.TryParse(s_sqlServerEngineEdition, out int engineEditon);
                return engineEditon == 6;
            }
        }

        public static bool TcpConnectionStringDoesNotUseAadAuth
        {
            get
            {
                SqlConnectionStringBuilder builder = new(TCPConnectionString);
                return builder.Authentication == SqlAuthenticationMethod.SqlPassword || builder.Authentication == SqlAuthenticationMethod.NotSpecified;
            }
        }

        public static string SQLServerVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(TCPConnectionString))
                {
                    s_sQLServerVersion ??= GetSqlServerProperty(TCPConnectionString, "ProductMajorVersion");
                }
                return s_sQLServerVersion;
            }
        }

        // Is TDS8 supported
        public static bool IsTDS8Supported
        {
            get
            {
                if (!string.IsNullOrEmpty(TCPConnectionString))
                {
                    s_isTDS8Supported = GetSQLServerStatusOnTDS8(TCPConnectionString);
                }
                return s_isTDS8Supported;
            }
        }

        static DataTestUtility()
        {
            Config c = Config.Load();
            NPConnectionString = c.NPConnectionString;
            TCPConnectionString = c.TCPConnectionString;
            TCPConnectionStringHGSVBS = c.TCPConnectionStringHGSVBS;
            TCPConnectionStringNoneVBS = c.TCPConnectionStringNoneVBS;
            TCPConnectionStringAASSGX = c.TCPConnectionStringAASSGX;
            AADAuthorityURL = c.AADAuthorityURL;
            AADPasswordConnectionString = c.AADPasswordConnectionString;
            AADServicePrincipalId = c.AADServicePrincipalId;
            AADServicePrincipalSecret = c.AADServicePrincipalSecret;
            LocalDbAppName = c.LocalDbAppName;
            LocalDbSharedInstanceName = c.LocalDbSharedInstanceName;
            SupportsIntegratedSecurity = c.SupportsIntegratedSecurity;
            FileStreamDirectory = c.FileStreamDirectory;
            EnclaveEnabled = c.EnclaveEnabled;
            TracingEnabled = c.TracingEnabled;
            UseManagedSNIOnWindows = c.UseManagedSNIOnWindows;
            DNSCachingConnString = c.DNSCachingConnString;
            DNSCachingServerCR = c.DNSCachingServerCR;
            DNSCachingServerTR = c.DNSCachingServerTR;
            IsDNSCachingSupportedCR = c.IsDNSCachingSupportedCR;
            IsDNSCachingSupportedTR = c.IsDNSCachingSupportedTR;
            EnclaveAzureDatabaseConnString = c.EnclaveAzureDatabaseConnString;
            UserManagedIdentityClientId = c.UserManagedIdentityClientId;
            PowerShellPath = c.PowerShellPath;
            KerberosDomainPassword = c.KerberosDomainPassword;
            KerberosDomainUser = c.KerberosDomainUser;
            ManagedIdentitySupported = c.ManagedIdentitySupported;
            IsManagedInstance = c.IsManagedInstance;
            AliasName = c.AliasName;

#if NETFRAMEWORK
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
#endif

            if (TracingEnabled)
            {
                TraceListener = new BaseEventListener();
            }

            if (UseManagedSNIOnWindows)
            {
                AppContext.SetSwitch(ManagedNetworkingAppContextSwitch, true);
                Console.WriteLine($"App Context switch {ManagedNetworkingAppContextSwitch} enabled on {Environment.OSVersion}");
            }

            AKVOriginalUrl = c.AzureKeyVaultURL;
            if (!string.IsNullOrEmpty(AKVOriginalUrl) && Uri.TryCreate(AKVOriginalUrl, UriKind.Absolute, out AKVBaseUri))
            {
                AKVBaseUri = new Uri(AKVBaseUri, "/");
            }

            AKVTenantId = c.AzureKeyVaultTenantId;

            if (EnclaveEnabled)
            {
                if (!string.IsNullOrEmpty(TCPConnectionStringHGSVBS))
                {
                    AEConnStrings.Add(TCPConnectionStringHGSVBS);
                    AEConnStringsSetup.Add(TCPConnectionStringHGSVBS);
                }

                if (!string.IsNullOrEmpty(TCPConnectionStringNoneVBS))
                {
                    AEConnStrings.Add(TCPConnectionStringNoneVBS);
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

        public static IEnumerable<string> ConnectionStrings => GetConnectionStrings(withEnclave: true);

        public static IEnumerable<string> GetConnectionStrings(bool withEnclave)
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
            if (withEnclave && EnclaveEnabled)
            {
                foreach (var connStr in AEConnStrings)
                {
                    yield return connStr;
                }
            }
        }

        private static string GenerateAccessToken(string authorityURL, string aADAuthUserID, string aADAuthPassword)
        {
            return AcquireTokenAsync(authorityURL, aADAuthUserID, aADAuthPassword).GetAwaiter().GetResult();
        }

        private static Task<string> AcquireTokenAsync(string authorityURL, string userID, string password) => Task.Run(() =>
        {
            // The below properties are set specific to test configurations.
            string scope = "https://database.windows.net//.default";
            string applicationName = "Microsoft Data SqlClient Manual Tests";
            string clientVersion = "1.0.0.0";
            string adoClientId = "2fd908ad-0664-4344-b9be-cd3e8b574c38";

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

            securePassword.MakeReadOnly();
            result = app.AcquireTokenByUsernamePassword(scopes, userID, password).ExecuteAsync().Result;

            return result.AccessToken;
        });

        public static bool IsKerberosTest => !string.IsNullOrEmpty(KerberosDomainUser) && !string.IsNullOrEmpty(KerberosDomainPassword);

        public static string GetSqlServerProperty(string connectionString, string propertyName)
        {
            string propertyValue = string.Empty;
            using SqlConnection conn = new(connectionString);
            conn.Open();
            SqlCommand command = conn.CreateCommand();
            command.CommandText = $"SELECT SERVERProperty('{propertyName}')";
            SqlDataReader reader = command.ExecuteReader();
            if (reader.Read())
            {
                switch (propertyName)
                {
                    case "EngineEdition":
                        propertyValue = reader.GetInt32(0).ToString();
                        break;
                    case "ProductMajorVersion":
                        propertyValue = reader.GetString(0);
                        break;
                }
            }
            return propertyValue;
        }

        public static bool GetSQLServerStatusOnTDS8(string connectionString)
        {
            bool isTDS8Supported = false;
            SqlConnectionStringBuilder builder = new(connectionString)
            {
                [nameof(SqlConnectionStringBuilder.Encrypt)] = SqlConnectionEncryptOption.Strict
            };
            try
            {
                SqlConnection conn = new(builder.ConnectionString);
                conn.Open();
                isTDS8Supported = true;
            }
            catch (SqlException)
            {

            }
            return isTDS8Supported;
        }

        public static bool IsNotX86Architecture => RuntimeInformation.ProcessArchitecture != Architecture.X86;

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

        public static bool IsAdmin
        {
            get
            {
#if !NETFRAMEWORK
                System.Diagnostics.Debug.Assert(OperatingSystem.IsWindows());
#endif
                return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
            }
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

        public static bool IsDNSCachingSetup() => !string.IsNullOrEmpty(DNSCachingConnString);

        // Synapse: Always Encrypted is not supported with Azure Synapse.
        //          Ref: https://feedback.azure.com/forums/307516-azure-synapse-analytics/suggestions/17858869-support-always-encrypted-in-sql-data-warehouse
        public static bool IsEnclaveAzureDatabaseSetup()
        {
            return EnclaveEnabled && !string.IsNullOrEmpty(EnclaveAzureDatabaseConnString) && IsNotAzureSynapse();
        }

        public static bool IsNotAzureSynapse() => !IsAzureSynapse;

        // Synapse: UDT Test Database not compatible with Azure Synapse.
        public static bool IsUdtTestDatabasePresent() => IsDatabasePresent(UdtTestDbName) && IsNotAzureSynapse();

        public static bool AreConnStringsSetup()
        {
            return !string.IsNullOrEmpty(NPConnectionString) && !string.IsNullOrEmpty(TCPConnectionString);
        }

        public static bool IsSQL2022() => string.Equals("16", SQLServerVersion.Trim());

        public static bool IsSQL2019() => string.Equals("15", SQLServerVersion.Trim());

        public static bool IsSQL2016() => string.Equals("14", s_sQLServerVersion.Trim());

        public static bool IsSQLAliasSetup()
        {
            return !string.IsNullOrEmpty(AliasName);
        }
        public static bool IsTCPConnStringSetup()
        {
            return !string.IsNullOrEmpty(TCPConnectionString);
        }

        // Synapse: Always Encrypted is not supported with Azure Synapse.
        //          Ref: https://feedback.azure.com/forums/307516-azure-synapse-analytics/suggestions/17858869-support-always-encrypted-in-sql-data-warehouse
        public static bool AreConnStringSetupForAE()
        {
            return AEConnStrings.Count > 0 && IsNotAzureSynapse();
        }

        public static bool IsSGXEnclaveConnStringSetup() => !string.IsNullOrEmpty(TCPConnectionStringAASSGX);

        public static bool IsAADPasswordConnStrSetup()
        {
            return !string.IsNullOrEmpty(AADPasswordConnectionString);
        }

        public static bool IsAADServicePrincipalSetup()
        {
            return !string.IsNullOrEmpty(AADServicePrincipalId) && !string.IsNullOrEmpty(AADServicePrincipalSecret);
        }

        public static bool IsAADAuthorityURLSetup()
        {
            return !string.IsNullOrEmpty(AADAuthorityURL);
        }

        public static bool IsNotAzureServer()
        {
            return !AreConnStringsSetup() || !Utils.IsAzureSqlServer(new SqlConnectionStringBuilder(TCPConnectionString).DataSource);
        }

        public static bool IsAzureServer()
        {
            return AreConnStringsSetup() && Utils.IsAzureSqlServer(new SqlConnectionStringBuilder(TCPConnectionString).DataSource);
        }

        public static bool IsNotNamedInstance()
        {
            return !AreConnStringsSetup() || !new SqlConnectionStringBuilder(TCPConnectionString).DataSource.Contains(@"\");
        }

        public static bool IsLocalHost()
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            return ParseDataSource(builder.DataSource, out string hostname, out _, out _) && string.Equals("localhost", hostname, StringComparison.OrdinalIgnoreCase);
        }

        // Synapse: Always Encrypted is not supported with Azure Synapse.
        //          Ref: https://feedback.azure.com/forums/307516-azure-synapse-analytics/suggestions/17858869-support-always-encrypted-in-sql-data-warehouse
        public static bool IsAKVSetupAvailable()
        {
            return AKVBaseUri != null && !string.IsNullOrEmpty(UserManagedIdentityClientId) && !string.IsNullOrEmpty(AKVTenantId) && IsNotAzureSynapse();
        }

        private static readonly DefaultAzureCredential s_defaultCredential = new(new DefaultAzureCredentialOptions { ManagedIdentityClientId = UserManagedIdentityClientId });

        public static TokenCredential GetTokenCredential()
        {
            return s_defaultCredential;
        }

        public static bool IsTargetReadyForAeWithKeyStore()
        {
            return DataTestUtility.AreConnStringSetupForAE()
#if !NETFRAMEWORK
                // AE tests on Windows will use the Cert Store. On non-Windows, they require AKV.
                && (OperatingSystem.IsWindows() || DataTestUtility.IsAKVSetupAvailable())
#endif
                ;
        }

        public static bool IsSupportingDistributedTransactions()
        {
#if NET8_0_OR_GREATER
            return OperatingSystem.IsWindows() && System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture != System.Runtime.InteropServices.Architecture.X86 && IsNotAzureServer();
#elif NETFRAMEWORK
            return IsNotAzureServer();
#else
            return false;
#endif
        }

        public static bool IsUsingManagedSNI() => UseManagedSNIOnWindows;

        public static bool IsNotUsingManagedSNIOnWindows() => !UseManagedSNIOnWindows;

        public static bool IsUsingNativeSNI() =>
#if !NETFRAMEWORK
            IsNotUsingManagedSNIOnWindows();
#else 
            true;
#endif
        // Synapse: UTF8 collations are not supported with Azure Synapse.
        //          Ref: https://feedback.azure.com/forums/307516-azure-synapse-analytics/suggestions/40103791-utf-8-collations-should-be-supported-in-azure-syna
        public static bool IsUTF8Supported()
        {
            bool retval = false;
            if (AreConnStringsSetup() && IsNotAzureSynapse())
            {
                using (SqlConnection connection = new SqlConnection(TCPConnectionString))
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

        public static bool DoesHostAddressContainBothIPv4AndIPv6()
        {
            if (!IsDNSCachingSetup())
            {
                return false;
            }
            using (var connection = new SqlConnection(DNSCachingConnString))
            {
                List<IPAddress> ipAddresses = Dns.GetHostAddresses(connection.DataSource).ToList();
                return ipAddresses.Exists(ip => ip.AddressFamily == AddressFamily.InterNetwork) &&
                    ipAddresses.Exists(ip => ip.AddressFamily == AddressFamily.InterNetworkV6);
            }
        }

        // Generate a new GUID and return the characters from its 1st and 4th
        // parts, as shown here:
        //
        //   7ff01cb8-88c7-11f0-b433-00155d7e531e
        //   ^^^^^^^^           ^^^^
        //
        // These 12 characters are concatenated together without any
        // separators.  These 2 parts typically comprise a timestamp and clock
        // sequence, most likely to be unique for tests that generate names in
        // quick succession.
        private static string GetGuidParts()
        {
            var guid = Guid.NewGuid().ToString();
            // GOTCHA: The slice operator is inclusive of the start index and
            // exclusive of the end index!
            return guid.Substring(0, 8) + guid.Substring(19, 4);
        }

        /// <summary>
        /// Generate a short unique database object name, whose maximum length
        /// is 30 characters, with the format:
        ///
        ///   <Prefix>_<GuidParts>
        ///
        /// The Prefix will be truncated to satisfy the overall maximum length.
        ///
        /// The GUID parts will be the characters from the 1st and 4th blocks
        /// from a traditional string representation, as shown here:
        ///
        ///   7ff01cb8-88c7-11f0-b433-00155d7e531e
        ///   ^^^^^^^^           ^^^^
        ///
        /// These 2 parts typically comprise a timestamp and clock sequence,
        /// most likely to be unique for tests that generate names in quick
        /// succession.  The 12 characters are concatenated together without any
        /// separators.
        /// </summary>
        /// 
        /// <param name="prefix">
        /// The prefix to use when generating the unique name, truncated to at
        /// most 18 characters when withBracket is false, and 16 characters when
        /// withBracket is true.
        ///
        /// This should not contain any characters that cannot be used in
        /// database object names.
        /// </param>
        /// 
        /// <param name="withBracket">
        /// When true, the entire generated name will be enclosed in square
        /// brackets, for example:
        /// 
        ///   [MyPrefix_7ff01cb811f0]
        /// </param>
        /// 
        /// <returns>
        /// A unique database object name, no more than 30 characters long.
        /// </returns>
        public static string GetShortName(string prefix, bool withBracket = true)
        {
            StringBuilder name = new(30);

            if (withBracket)
            {
                name.Append('[');
            }

            int maxPrefixLength = withBracket ? 16 : 18;
            if (prefix.Length > maxPrefixLength)
            {
                prefix = prefix.Substring(0, maxPrefixLength);
            }

            name.Append(prefix);
            name.Append('_');
            name.Append(GetGuidParts());

            if (withBracket)
            {
                name.Append(']');
            }

            System.Console.WriteLine($"Generated short name \"{name.ToString()}\"");

            return name.ToString();
        }

        /// <summary>
        /// Generate a long unique database object name, whose maximum length is
        /// 96 characters, with the format:
        /// 
        ///   <Prefix>_<GuidParts>_<UserName>_<MachineName>
        ///
        /// The Prefix will be truncated to satisfy the overall maximum length.
        ///
        /// The GUID Parts will be the characters from the 1st and 4th blocks
        /// from a traditional string representation, as shown here:
        ///
        ///   7ff01cb8-88c7-11f0-b433-00155d7e531e
        ///   ^^^^^^^^           ^^^^
        ///
        /// These 2 parts typically comprise a timestamp and clock sequence,
        /// most likely to be unique for tests that generate names in quick
        /// succession.  The 12 characters are concatenated together without any
        /// separators.
        ///
        /// The UserName and MachineName are obtained from the Environment,
        /// and will be truncated to satisfy the maximum overall length.
        /// </summary>
        /// 
        /// <param name="prefix">
        /// The prefix to use when generating the unique name, truncated to at
        /// most 32 characters.
        ///
        /// This should not contain any characters that cannot be used in
        /// database object names.
        /// </param>
        /// 
        /// <param name="withBracket">
        /// When true, the entire generated name will be enclosed in square
        /// brackets, for example:
        /// 
        ///   [MyPrefix_7ff01cb811f0_test_user_ci_agent_machine_name]
        /// </param>
        /// 
        /// <returns>
        /// A unique database object name, no more than 96 characters long.
        /// </returns>
        public static string GetLongName(string prefix, bool withBracket = true)
        {
            StringBuilder name = new(96);

            if (withBracket)
            {
                name.Append('[');
            }

            if (prefix.Length > 32)
            {
                prefix = prefix.Substring(0, 32);
            }

            name.Append(prefix);
            name.Append('_');
            name.Append(GetGuidParts());
            name.Append('_');

            var suffix =
              Environment.UserName + '_' +
              Environment.MachineName;

            int maxSuffixLength = 96 - name.Length;
            if (withBracket)
            {
                --maxSuffixLength;
            }
            if (suffix.Length > maxSuffixLength)
            {
                suffix = suffix.Substring(0, maxSuffixLength);
            }

            name.Append(suffix);

            if (withBracket)
            {
                name.Append(']');
            }

            System.Console.WriteLine($"Generated long name \"{name.ToString()}\"");

            return name.ToString();
        }

        public static void CreateTable(SqlConnection sqlConnection, string tableName, string createBody)
        {
            DropTable(sqlConnection, tableName);
            string tableCreate = "CREATE TABLE " + tableName + createBody;
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = tableCreate;
                command.ExecuteNonQuery();
            }
        }

        public static void CreateSP(SqlConnection sqlConnection, string spName, string spBody)
        {
            DropStoredProcedure(sqlConnection, spName);
            string spCreate = "CREATE PROCEDURE " + spName + spBody;
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = spCreate;
                command.ExecuteNonQuery();
            }
        }

        public static void DropTable(SqlConnection sqlConnection, string tableName)
        {
            ResurrectConnection(sqlConnection);
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) \n DROP TABLE {0}", tableName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static void DropUserDefinedType(SqlConnection sqlConnection, string typeName)
        {
            ResurrectConnection(sqlConnection);
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (TYPE_ID('{0}') IS NOT NULL) \n DROP TYPE {0}", typeName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public static void DropStoredProcedure(SqlConnection sqlConnection, string spName)
        {
            ResurrectConnection(sqlConnection);
            using (SqlCommand cmd = new SqlCommand(string.Format("IF (OBJECT_ID('{0}') IS NOT NULL) \n DROP PROCEDURE {0}", spName), sqlConnection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        private static void ResurrectConnection(SqlConnection sqlConnection, int counter = 2)
        {
            if (sqlConnection.State == ConnectionState.Closed)
            {
                sqlConnection.Open();
            }
            while (counter-- > 0 && sqlConnection.State == ConnectionState.Connecting)
            {
                Thread.Sleep(80);
            }
        }

        /// <summary>
        /// Drops specified database on provided connection.
        /// </summary>
        /// <param name="sqlConnection">Open connection to be used.</param>
        /// <param name="dbName">Database name without brackets.</param>
        public static void DropDatabase(SqlConnection sqlConnection, string dbName)
        {
            ResurrectConnection(sqlConnection);
            using SqlCommand cmd = new(string.Format("IF (EXISTS(SELECT 1 FROM sys.databases WHERE name = '{0}')) \nBEGIN \n ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE \n DROP DATABASE [{0}] \nEND", dbName), sqlConnection);
            cmd.ExecuteNonQuery();
        }

        public static bool IsLocalDBInstalled() => !string.IsNullOrEmpty(LocalDbAppName?.Trim()) && IsIntegratedSecuritySetup();
        public static bool IsLocalDbSharedInstanceSetup() => !string.IsNullOrEmpty(LocalDbSharedInstanceName?.Trim()) && IsIntegratedSecuritySetup();
        public static bool IsIntegratedSecuritySetup() => SupportsIntegratedSecurity;

        public static string GetAccessToken()
        {
            if (AADAccessToken == null && IsAADPasswordConnStrSetup() && IsAADAuthorityURLSetup())
            {
                string username = RetrieveValueFromConnStr(AADPasswordConnectionString, new string[] { "User ID", "UID" });
                string password = RetrieveValueFromConnStr(AADPasswordConnectionString, new string[] { "Password", "PWD" });
                AADAccessToken = GenerateAccessToken(AADAuthorityURL, username, password);
            }
            // Creates a new Object Reference of Access Token - See GitHub Issue 438
            return AADAccessToken != null ? new string(AADAccessToken.ToCharArray()) : null;
        }

        public static string GetSystemIdentityAccessToken()
        {
            if (ManagedIdentitySupported && SupportsSystemAssignedManagedIdentity && AADSystemIdentityAccessToken == null && IsAADPasswordConnStrSetup())
            {
                AADSystemIdentityAccessToken = AADUtility.GetManagedIdentityToken().GetAwaiter().GetResult();
                if (AADSystemIdentityAccessToken == null)
                {
                    ManagedIdentitySupported = false;
                }
            }
            return AADSystemIdentityAccessToken != null ? new string(AADSystemIdentityAccessToken.ToCharArray()) : null;
        }

        public static string GetUserIdentityAccessToken()
        {
            if (ManagedIdentitySupported && AADUserIdentityAccessToken == null && IsAADPasswordConnStrSetup())
            {
                // Pass User Assigned Managed Identity Client Id here.
                AADUserIdentityAccessToken = AADUtility.GetManagedIdentityToken(UserManagedIdentityClientId).GetAwaiter().GetResult();
                if (AADUserIdentityAccessToken == null)
                {
                    ManagedIdentitySupported = false;
                }
            }
            return AADUserIdentityAccessToken != null ? new string(AADUserIdentityAccessToken.ToCharArray()) : null;
        }

        public static bool IsAccessTokenSetup() => !string.IsNullOrEmpty(GetAccessToken());

        public static bool IsFileStreamSetup() => !string.IsNullOrEmpty(FileStreamDirectory) && IsNotAzureServer() && IsNotAzureSynapse();

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
                Assert.Fail("ERROR: Did not get expected exception");
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

        public static string FetchKeyInConnStr(string connStr, string[] keys)
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

        public static bool ParseDataSource(string dataSource, out string hostname, out int port, out string instanceName)
        {
            hostname = string.Empty;
            port = -1;
            instanceName = string.Empty;

            // Remove leading and trailing spaces
            dataSource = dataSource.Trim();

            if (dataSource.Contains(":"))
            {
                dataSource = dataSource.Substring(dataSource.IndexOf(":", StringComparison.Ordinal) + 1);
            }

            if (dataSource.Contains(","))
            {
                if (!Int32.TryParse(dataSource.Substring(dataSource.LastIndexOf(",", StringComparison.Ordinal) + 1), out port))
                {
                    return false;
                }
                // IndexOf is zero-based, no need to subtract one
                dataSource = dataSource.Substring(0, dataSource.IndexOf(",", StringComparison.Ordinal));
            }

            if (dataSource.Contains("\\"))
            {
                instanceName = dataSource.Substring(dataSource.LastIndexOf("\\", StringComparison.Ordinal) + 1);
                dataSource = dataSource.Substring(0, dataSource.LastIndexOf("\\", StringComparison.Ordinal));
            }

            hostname = dataSource;

            return hostname.Length > 0 && hostname.IndexOfAny(new char[] { '\\', ':', ',' }) == -1;
        }

        public class AKVEventListener : BaseEventListener
        {
            public override string Name => AKVEventSourceName;
        }

        public class MDSEventListener : BaseEventListener
        {
            private static Type s_activityCorrelatorType = Type.GetType("Microsoft.Data.Common.ActivityCorrelator, Microsoft.Data.SqlClient");
            private static PropertyInfo s_currentActivityProperty = s_activityCorrelatorType.GetProperty("Current", BindingFlags.NonPublic | BindingFlags.Static);

            public readonly HashSet<string> ActivityIDs = new();

            public override string Name => MDSEventSourceName;

            protected override void OnMatchingEventWritten(EventWrittenEventArgs eventData)
            {
                object currentActivity = s_currentActivityProperty.GetValue(null);
                string activityId = GetActivityId(currentActivity);

                ActivityIDs.Add(activityId);
            }

            private static string GetActivityId(object currentActivity)
            {
                Type activityIdType = currentActivity.GetType();
                FieldInfo idField = activityIdType.GetField("Id", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo sequenceField = activityIdType.GetField("Sequence", BindingFlags.NonPublic | BindingFlags.Instance);

                Guid id = (Guid)idField.GetValue(currentActivity);
                uint sequence = (uint)sequenceField.GetValue(currentActivity);

                // This activity ID format matches the one used in the XEvents trace
                return string.Format("{0}-{1}", id, sequence).ToUpper();
            }
        }

        public class BaseEventListener : EventListener
        {
            public readonly List<int> IDs = new();
            public readonly List<EventWrittenEventArgs> EventData = new();

            public virtual string Name => EventSourcePrefix;

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name.StartsWith(Name, StringComparison.Ordinal))
                {
                    // Collect all traces for better code coverage
                    EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
                }
                else
                {
                    DisableEvents(eventSource);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (Name == eventData.EventSource.Name)
                {
                    IDs.Add(eventData.EventId);
                    EventData.Add(eventData);
                    OnMatchingEventWritten(eventData);
                }
            }

            protected virtual void OnMatchingEventWritten(EventWrittenEventArgs eventData)
            {
            }
        }

        public readonly ref struct XEventScope // : IDisposable
        {
            private const int MaxXEventsLatencyS = 5;

            private readonly SqlConnection _connection;
            private readonly bool _useDatabaseSession;

            public string SessionName { get; }

            public XEventScope(SqlConnection connection, string eventSpecification, string targetSpecification)
            {
                SessionName = GenerateRandomCharacters("Session");
                _connection = connection;
                // SQL Azure only supports database-scoped XEvent sessions
                _useDatabaseSession = GetSqlServerProperty(connection.ConnectionString, "EngineEdition") == "5";

                SetupXEvent(eventSpecification, targetSpecification);
            }

            public void Dispose()
                => DropXEvent();

            public System.Xml.XmlDocument GetEvents()
            {
                string xEventQuery = _useDatabaseSession
                    ? $@"SELECT xet.target_data
                        FROM sys.dm_xe_database_session_targets AS xet
                        INNER JOIN sys.dm_xe_database_sessions AS xe
                           ON (xe.address = xet.event_session_address)
                        WHERE xe.name = '{SessionName}'"
                    : $@"SELECT xet.target_data
                        FROM sys.dm_xe_session_targets AS xet
                        INNER JOIN sys.dm_xe_sessions AS xe
                           ON (xe.address = xet.event_session_address)
                        WHERE xe.name = '{SessionName}'";

                using (SqlCommand command = new SqlCommand(xEventQuery, _connection))
                {
                    Thread.Sleep(MaxXEventsLatencyS * 1000);

                    if (_connection.State == ConnectionState.Closed)
                    {
                        _connection.Open();
                    }

                    string targetData = command.ExecuteScalar() as string;
                    System.Xml.XmlDocument xmlDocument = new System.Xml.XmlDocument();

                    xmlDocument.LoadXml(targetData);
                    return xmlDocument;
                }
            }

            private void SetupXEvent(string eventSpecification, string targetSpecification)
            {
                string sessionLocation = _useDatabaseSession ? "DATABASE" : "SERVER";
                string xEventCreateAndStartCommandText = $@"CREATE EVENT SESSION [{SessionName}] ON {sessionLocation}
                        {eventSpecification}
                        {targetSpecification}
                        WITH (
                            MAX_MEMORY=16 MB,
                            EVENT_RETENTION_MODE=ALLOW_SINGLE_EVENT_LOSS,
                            MAX_DISPATCH_LATENCY={MaxXEventsLatencyS} SECONDS,
                            MAX_EVENT_SIZE=0 KB,
                            MEMORY_PARTITION_MODE=NONE,
                            TRACK_CAUSALITY=ON,
                            STARTUP_STATE=OFF)
                            
                        ALTER EVENT SESSION [{SessionName}] ON {sessionLocation} STATE = START ";

                using (SqlCommand createXEventSession = new SqlCommand(xEventCreateAndStartCommandText, _connection))
                {
                    if (_connection.State == ConnectionState.Closed)
                    {
                        _connection.Open();
                    }

                    createXEventSession.ExecuteNonQuery();
                }
            }

            private void DropXEvent()
            {
                string dropXEventSessionCommand = _useDatabaseSession
                    ? $"IF EXISTS (select * from sys.dm_xe_database_sessions where name ='{SessionName}')" +
                        $" DROP EVENT SESSION [{SessionName}] ON DATABASE"
                    : $"IF EXISTS (select * from sys.dm_xe_sessions where name ='{SessionName}')" +
                        $" DROP EVENT SESSION [{SessionName}] ON SERVER";

                using (SqlCommand command = new SqlCommand(dropXEventSessionCommand, _connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Resolves the machine's fully qualified domain name if it is applicable.
        /// </summary>
        /// <returns>Returns FQDN if the client was domain joined otherwise the machine name.</returns>
        public static string GetMachineFQDN(string hostname)
        {
            IPGlobalProperties machineInfo = IPGlobalProperties.GetIPGlobalProperties();
            StringBuilder fqdn = new();
            if (hostname.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                hostname.Equals(machineInfo.HostName, StringComparison.OrdinalIgnoreCase))
            {
                fqdn.Append(machineInfo.HostName);
                if (!string.IsNullOrEmpty(machineInfo.DomainName))
                {
                    fqdn.Append(".");
                    fqdn.Append(machineInfo.DomainName);
                }
            }
            else
            {
                IPHostEntry host = Dns.GetHostEntry(hostname);
                fqdn.Append(host.HostName);
            }
            return fqdn.ToString();
        }
    }
}
