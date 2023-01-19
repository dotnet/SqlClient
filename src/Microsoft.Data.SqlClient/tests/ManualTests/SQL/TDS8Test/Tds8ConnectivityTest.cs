// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class Tds8ConnectivityTest
    {
        // NOTE: Please run SqlClient\tools\scripts\makeSelfSignedCert.ps1 to set up the self-signed certificate for tests.

        #region Environment setup variables and helper methods
        // These environment variables are populated from a powershell script or bash script after the certificate is generated.
        private const string ENV_CERT_FRIENDLYNAME = "TDS8_Test_Certificate_FriendlyName";
        private const string ENV_CERT_MISMATCH_FRIENDLYNAME = "TDS8_Test_Certificate_Mismatch_FriendlyName";
        private const string ENV_VALID_CERT_PATH = "TDS8_Test_Certificate_On_FileSystem";
        private const string ENV_VALID_MISMATCH_CERT_PATH = "TDS8_Test_MismatchCertificate_On_FileSystem";
        private const string ENV_INVALID_CERT_PATH = "TDS8_Test_InvalidCertificate_On_FileSystem";
        private const string ENV_EXTERNAL_IP = "TDS8_EXTERNAL_IP";
        private const string ENV_SQL_SERVER_VERSION = "TDS8_Test_SqlServerVersion";

        // Note these names comes from the makeSelfSignCert.ps1
        private readonly string _validCertificateFriendlyName = "TDS8SqlClientCert";
        private readonly string _validMismatchCertificateFriendlyName = "TDS8SqlClientCertMismatch";

        // The following variables are populated from the environment variables above.

        private const string CertificateFileName = "sqlservercert.cer";
        private const string MismatchCertificateFileName = "mismatchsqlservercert.cer";
        private const string InvalidCertificateFormatFileName = "sqlservercert.pfx";

        private static string s_validCertificatePath = "";
        private static string s_validMismatchCertificatePath = "";
        private static string s_invalidFormatCertificatePath = "";
        private static string s_invalidDNECertificatePath = "";

        private static string s_hostName = null;
        private static string GetHostName()
        {
            if (s_hostName != null)
            {
                return s_hostName;
            }
            s_hostName = System.Net.Dns.GetHostEntry(Environment.MachineName).HostName;
            return s_hostName;
        }

        private static string s_externalIp = null;
        private static string GetExternalIp()
        {
            if (s_externalIp == null)
            {
                s_externalIp = Environment.GetEnvironmentVariable(ENV_EXTERNAL_IP);
                if (s_externalIp == null)
                {
                    using HttpClient client = new();

                    HttpResponseMessage response = client.GetAsync("https://ifconfig.me/ip").Result;

                    if (response.IsSuccessStatusCode)
                    {
                        string body = response.Content.ReadAsStringAsync().Result;

                        if (!string.IsNullOrEmpty(body))
                        {
                            s_externalIp = body;
                            return s_externalIp;
                        }
                    }

                    throw new NullReferenceException("Unable to retrieve the external ip address from the environment variable.");
                }
            }
            return s_externalIp;
        }

        private static int? s_serverMajorVersion = null;
        private static int? GetSqlServerMajorVersion()
        {
            if (s_serverMajorVersion == null)
            {
                string version = Environment.GetEnvironmentVariable(ENV_SQL_SERVER_VERSION);
                if (version != null)
                {
                    if (int.TryParse(version, out int majorVersionNumber))
                    {
                        // NOTE: version 15 is 2019, and 16 is 2022
                        s_serverMajorVersion = majorVersionNumber;
                    }
                }

                // Note the fallback to retrieve the SQL Server major version from the query is a bit of overhead.
                if (s_serverMajorVersion == null)
                {
                    string versionQuery = "select SERVERPROPERTY('ProductMajorVersion')";

                    // this connection string should already be verfied from the conditional theory.
                    using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
                    connection.Open();

                    using SqlCommand command = new(versionQuery, connection);
                    string majorVersion = command.ExecuteScalar().ToString();

                    if(!string.IsNullOrEmpty(majorVersion))
                    {
                        if (int.TryParse(majorVersion, out int majorVersionNumber))
                        {
                            s_serverMajorVersion = majorVersionNumber;

                            return s_serverMajorVersion;
                        }
                    }

                    throw new NullReferenceException("Unable to retrieve the sql server version from the environment variable.");
                }
            }
            return s_serverMajorVersion;
        }

        // Helper enum and convert methods for the server name
        private static string TcpDataSourceHostName => string.Format("tcp:{0}", GetHostName());
        private const string TcpDataSourceLocalhost = "tcp:localhost";
        private const string TcpDataSourceLoopbackAddress = "tcp:127.0.0.1";

        public enum DataSourceType
        {
            Hostname, Localhost, LoopbackAddress, NamedPipe
        }

        public static string GetDataSourceName(DataSourceType type)
        {
            return type switch
            {
                DataSourceType.Hostname => TcpDataSourceHostName,
                DataSourceType.Localhost => TcpDataSourceLocalhost,
                DataSourceType.LoopbackAddress => TcpDataSourceLoopbackAddress,
                DataSourceType.NamedPipe => ".",
                _ => throw new InvalidEnumArgumentException("The value passed in is not supported."),
            };
        }

        public enum CertificatePathType
        {
            Valid, Mismatch, Invalid_DNE, Invalid_Format
        }

        /// <summary>
        /// Converts the CertificatePathType enum to the path to the certificate
        /// </summary>
        /// <param name="type">Certificate Path Type</param>
        /// <returns>path to the specified certificate path type</returns>
        /// <exception cref="InvalidEnumArgumentException">When a valid does not exist in the enum</exception>
        public static string GetPathFromCertificateType(CertificatePathType type)
        {
            return type switch
            {
                CertificatePathType.Valid => s_validCertificatePath,
                CertificatePathType.Mismatch => s_validMismatchCertificatePath,
                CertificatePathType.Invalid_DNE => s_invalidDNECertificatePath,
                CertificatePathType.Invalid_Format => s_invalidFormatCertificatePath,
                _ => throw new InvalidEnumArgumentException("The value passed in is not supported."),
            };
        }

        /// <summary>
        /// Retrieve the certificate with the matching subject name from the trust store
        /// </summary>
        /// <param name="subject">The subject name</param>
        /// <param name="checkFromTrustStore">Whether or not to retrieve the certificate from the Personal or Trusted Root Certification Authorities</param>
        /// <returns>The certificate if exists; otherwise null</returns>
        private static X509Certificate2 GetCertificateFromStore(string subject, bool checkFromTrustStore = false)
        {
            // Get the certificate store for the current user.
            X509Store store = checkFromTrustStore ? new X509Store(StoreName.Root, StoreLocation.LocalMachine) : new X509Store(StoreLocation.LocalMachine);

            if (store == null)
            {
                throw new ArgumentNullException(nameof(store), "Unable to load the certificate store");
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentNullException(nameof(subject), "The subject cannot be empty");
            }

            // Append CN= to the subject if it is missing.
            string subjectName = "";
            if (!subject.StartsWith("CN="))
            {
                 subjectName = $"CN={subject}";
            }

            try
            {
                store.Open(OpenFlags.ReadOnly);

                X509Certificate2Collection certCollection = store.Certificates;
                X509Certificate2Collection currentCerts = certCollection.Find(X509FindType.FindByTimeValid, DateTime.Now, false);
                X509Certificate2Collection signingCert = currentCerts.Find(X509FindType.FindBySubjectDistinguishedName, subjectName, false);
                if (signingCert.Count == 0)
                {
                    return null;
                }
                return signingCert[0];
            }
            finally
            {
                store.Close();
            }
        }

        #endregion // Environment setup variables and helper methods

        #region Flags for conditional fact/theory

        // Wrapper conditions from the DataTestUtility.
        private static bool AreConnectionStringsSetup => DataTestUtility.AreConnStringsSetup();

        private static bool IsNotAzureServer => DataTestUtility.IsNotAzureServer();

        private static bool IsNotAzureSynapse => DataTestUtility.IsNotAzureSynapse();

        #endregion // Flags for conditional fact/theory

        #region Flags for detecting if a certificate is installed
        // The following causes an exception where it doesn't detect any of the InlineData as it thinks all the InlineData does not provide any parameters.
        // Hence, the following flags/conditionals are
        public static bool TrustedRootWithMismatchHostNameSelfSignedCertifcateInstalled()
        {
            X509Certificate2 cert = GetCertificateFromStore(GetExternalIp(), true);
            if (cert == null)
            {
                return false;
            }
            return !cert.SubjectName.Name.Contains(GetHostName());
        }

        private static bool TrustedRootSelfSignedCertifcateInstalled() => GetCertificateFromStore(GetHostName(), true) != null;

        private static bool SelfSignedCertificateInstalled() => GetCertificateFromStore(GetHostName()) != null;

        #endregion // Flags for detecting if a certificate is installed

        public Tds8ConnectivityTest()
        {
            // Get tools script directory
            string solutionDir = "";
            string rootName = "SqlClient";
            int solutionDirIndex = Environment.CurrentDirectory.IndexOf(rootName);
            if (solutionDirIndex != -1)
            {
                solutionDir = Environment.CurrentDirectory.Substring(0, solutionDirIndex + rootName.Length);
            }
            string scriptsDir = Path.Combine(solutionDir, "tools", "scripts");

            // Populate the variables with the environment variable values
            _validCertificateFriendlyName = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_CERT_FRIENDLYNAME)) ?
                _validCertificateFriendlyName : Environment.GetEnvironmentVariable(ENV_CERT_FRIENDLYNAME);
            _validMismatchCertificateFriendlyName = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_CERT_MISMATCH_FRIENDLYNAME)) ?
                _validMismatchCertificateFriendlyName : Environment.GetEnvironmentVariable(ENV_CERT_MISMATCH_FRIENDLYNAME);
            s_invalidFormatCertificatePath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_INVALID_CERT_PATH)) ?
                Path.Combine(scriptsDir, InvalidCertificateFormatFileName) : Environment.GetEnvironmentVariable(ENV_INVALID_CERT_PATH);
            s_invalidDNECertificatePath = Path.Combine(Environment.CurrentDirectory, "DOES_NOT_EXIST.cer");
            s_validCertificatePath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_VALID_CERT_PATH)) ?
                Path.Combine(scriptsDir, CertificateFileName): Environment.GetEnvironmentVariable(ENV_VALID_CERT_PATH);
            s_validMismatchCertificatePath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_VALID_MISMATCH_CERT_PATH)) ?
                Path.Combine(scriptsDir,  MismatchCertificateFileName) : Environment.GetEnvironmentVariable(ENV_VALID_MISMATCH_CERT_PATH);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.LoopbackAddress, true)]
        [InlineData(DataSourceType.LoopbackAddress, false)]
        [InlineData(DataSourceType.NamedPipe, true)]
        [InlineData(DataSourceType.NamedPipe, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public static void ShouldConnectWithMatchingHNICWithTrustedRootCertificateAndDoesNotMatchHostname(DataSourceType dataSourceType, bool strict)
        {
            // NOTE: the server must have the mismatch hostname certificate installed in the trusted root authorties
            // i.e. subject = CA="something.else.com" and hostname is computer.

            Assert.True(TrustedRootWithMismatchHostNameSelfSignedCertifcateInstalled(), "The mismatch self-sign certificate is not installed in the trusted root and test skipped.");

            // The certificate subject name is the IP Address in this mismatch certificate and the local machine hostname will be set in the HNIC.
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                HostNameInCertificate = GetHostName()
            };

            if (strict && GetSqlServerMajorVersion() < 16)
            {
                // Connecting in Strict mode with HNIC is only available in SQL Server 2022; it's expected to fail lower SQL Server versions.
                Assert.Throws<SqlException>(() => Connect(builder.ConnectionString));
            } 
            else
            {
                Connect(builder.ConnectionString);
            }
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.LoopbackAddress, true)]
        [InlineData(DataSourceType.LoopbackAddress, false)]
        [InlineData(DataSourceType.NamedPipe, true)]
        [InlineData(DataSourceType.NamedPipe, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public static void ShouldConnectIgnoreHNICWithCertificateInTrustedRootCertificate(DataSourceType dataSourceType, bool strict)
        {
            // NOTE: the server must have the self-signed certificate installed in the trusted root.
            Assert.True(TrustedRootSelfSignedCertifcateInstalled(), "The self sign certificate is not installed in trusted root and test skipped.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                HostNameInCertificate = "IGNORED.TEST.COM",
            };

            Connect(builder.ConnectionString);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.LoopbackAddress, true)]
        [InlineData(DataSourceType.LoopbackAddress, false)]
        [InlineData(DataSourceType.NamedPipe, true)]
        [InlineData(DataSourceType.NamedPipe, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public void ShouldConnectIgnoreServerCertificateWithCertificateInRootTrustedCertificate(DataSourceType dataSourceType, bool strict)
        {
            // NOTE: the server must have the self-signed certificate installed.
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            string pathToMissingCert = GetPathFromCertificateType(CertificatePathType.Invalid_DNE);
            Assert.False(File.Exists(pathToMissingCert), $"The path to certificate [{pathToMissingCert}] should not exist.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = pathToMissingCert,
            };

            Connect(builder.ConnectionString);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public void ShouldFailWithLocalhostAndMissingServerCertificate(DataSourceType dataSourceType, bool strict)
        {
            // NOTE: the server must have the self-signed certificate installed.
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            string pathToMissingCert = GetPathFromCertificateType(CertificatePathType.Invalid_DNE);
            Assert.False(File.Exists(pathToMissingCert), $"The path to certificate [{pathToMissingCert}] should not exist.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = pathToMissingCert
            };

            Assert.Throws<SqlException>(() => Connect(builder.ConnectionString));
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public void ShouldFailWithLocalhostAndInvalidServerCertificateFormat(DataSourceType dataSourceType, bool strict)
        {
            // NOTE: the server must have the self-signed certificate installed.
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            string pathToInvalidFormatCertificate = GetPathFromCertificateType(CertificatePathType.Invalid_Format);
            Assert.True(File.Exists(pathToInvalidFormatCertificate), $"The certificate [{InvalidCertificateFormatFileName}] must exist for the test.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = pathToInvalidFormatCertificate
            };

            Assert.Throws<SqlException>(() => Connect(builder.ConnectionString));
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost)]
        [InlineData(DataSourceType.Hostname)]
        public void ShouldConnectIgnoreHNICAndConnectWithEncryptOpional(DataSourceType dataSourceType)
        {
            // NOTE: the server must have the self-signed certificate installed.
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = SqlConnectionEncryptOption.Optional,
                HostNameInCertificate = "IGNORED.TEST.COM",
            };

            Connect(builder.ConnectionString);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost)]
        [InlineData(DataSourceType.Hostname)]
        public void ShouldConnectIgnoreServerCertificateWithEncryptOptional(DataSourceType dataSourceType)
        {
            // NOTE: the server must have the self-signed certificate installed.
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = SqlConnectionEncryptOption.Optional,
                ServerCertificate = GetPathFromCertificateType(CertificatePathType.Invalid_DNE),
            };

            Connect(builder.ConnectionString);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost)]
        [InlineData(DataSourceType.Hostname)]
        public void ShouldConnectIgnoreServerCertificateWithTrustServerCertificateEncryptOptional(DataSourceType dataSourceType)
        {
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = true,
                ServerCertificate = GetPathFromCertificateType(CertificatePathType.Invalid_DNE),
            };

            Connect(builder.ConnectionString);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public void ShouldConnectWithMatchingServerCertificate(DataSourceType dataSourceType, bool strict)
        {
            Assert.True(SelfSignedCertificateInstalled(), "The self sign certificate is not installed and test skipped.");

            string mismatchValidCertificatePath = GetPathFromCertificateType(CertificatePathType.Mismatch);
            Assert.True(File.Exists(mismatchValidCertificatePath), "The validate certificate does not exist.");

            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = mismatchValidCertificatePath
            };

            if (strict && GetSqlServerMajorVersion() < 16)
            {
                // Connecting in Strict mode with Server Cerficiate is only available in SQL Server 2022; it's expected to fail lower SQL Server versions.
                Assert.Throws<SqlException>(() => Connect(builder.ConnectionString));
            }
            else
            {
                Connect(builder.ConnectionString);
            }

        }

        /// <summary>
        /// Makes a connection using the specified connection string and queries the version.
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        private static void Connect(string connectionString)
        {
            using SqlConnection connection = new(connectionString);
            connection.Open();

            // The reason why we also make a query is because it validates that decryption is working from SNI.
            // If it fails decryption, it would throw an exception.
            using SqlCommand command = new("select @@VERSION", connection);

            string version = command.ExecuteScalar().ToString();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }
    }
}
