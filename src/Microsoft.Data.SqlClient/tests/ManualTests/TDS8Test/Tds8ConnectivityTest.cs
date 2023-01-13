using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.TDS8
{
    public class Tds8ConnectivityTest
    {
        #region Environment setup variables and helper methods

        // These environment variables are populated from a powershell or bash script.after the certificates are generated
        private const string ENV_CERT_FRIENDLYNAME = "TDS8_Test_Certificate_FriendlyName";
        private const string ENV_CERT_MISMATCH_FRIENDLYNAME = "TDS8_Test_Certificate_Mismatch_FriendlyName";
        private const string ENV_VALID_CERT_PATH = "TDS8_Test_Certificate_On_FileSystem";
        private const string ENV_VALID_MISMATCH_CERT_PATH = "TDS8_Test_MismatchCertificate_On_FileSystem";
        private const string ENV_INVALID_CERT_PATH = "TDS8_Test_InvalidCertificate_On_FileSystem";
        private const string ENV_EXTERNAL_IP = "TDS8_EXTERNAL_IP";
        private const string ENV_SQL_SERVER_VERSION = "TDS8_Test_SqlServerVersion";

        // Note these names comes from the makeSelfSignCert.ps1
        private readonly string ValidCertificateFriendlyName = "TDS8SqlClientCert";
        private readonly string ValidMismatchCertificateFriendlyName = "TDS8SqlClientCertMismatch";

        // The following variables are populated from the environment variables above.
        private static string ValidCertificatePath = "c:/cert/sqlservercert.cer";
        private static string ValidMismatchCertificatePath = "c:/cert/mismatchsqlservercert.cer";
        private static string InvalidFormatCertificatePath = "c:/cert/sqlservercert.pfx";
        private static string InvalidDNECertificatePath = "c:/cert/does_not_exist.cer";

        private static string s_hostName = null;
        private static string GetHostName()
        {
            if (s_hostName == null)
            {
                s_hostName = System.Net.Dns.GetHostEntry(Environment.MachineName).HostName;
            }
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
                    throw new NullReferenceException("Unable to retrieve the external ip address from the environment variable.");
                }
            }
            return s_externalIp;
        }

        private static string s_serverVersion = null;
        private static string GetServerVersion()
        {
            if (s_serverVersion == null)
            {
                s_serverVersion = Environment.GetEnvironmentVariable(ENV_SQL_SERVER_VERSION);
                if (s_serverVersion == null)
                {
                    throw new NullReferenceException("Unable to retrieve the sql server version from the environment variable.");
                }
            }
            return s_serverVersion;
        }

        // Helper enum and convert methods for the server name
        private static string TcpDataSourceHostName => string.Format("tcp:{0}", GetHostName());
        private static string TcpDataSourceLocalhost => "tcp:localhost";
        private static string TcpDataSourceLoopbackAddress => "tcp:127.0.0.1";

        public enum DataSourceType
        {
            Hostname, Localhost, LoopbackAddress, NamedPipe
        }

        public static string GetDataSourceName(DataSourceType type)
        {
            switch (type)
            {
                case DataSourceType.Hostname:
                    return TcpDataSourceHostName;
                case DataSourceType.Localhost:
                    return TcpDataSourceLocalhost;
                case DataSourceType.LoopbackAddress:
                    return TcpDataSourceLoopbackAddress;
                case DataSourceType.NamedPipe:
                    return ".";
                default:
                    throw new InvalidEnumArgumentException("The value passed in is not supported.");
            }
        }

        public enum CertificatePathType
        {
            Valid, Invalid_DNE, Invalid_Format
        }

        /// <summary>
        /// Converts the CertificatePathType enum to the path to the certificate
        /// </summary>
        /// <param name="type">Certificate Path Type</param>
        /// <returns>path to the specified certificate path type</returns>
        /// <exception cref="InvalidEnumArgumentException">When a valid does not exist in the enum</exception>
        public static string GetPathFromCertificateType(CertificatePathType type)
        {
            switch (type)
            {
                case CertificatePathType.Valid:
                    return ValidCertificatePath;
                case CertificatePathType.Invalid_DNE:
                    return InvalidDNECertificatePath;
                case CertificatePathType.Invalid_Format:
                    return InvalidFormatCertificatePath;
                default:
                    throw new InvalidEnumArgumentException("The value passed in is not supported.");
            }
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
            X509Store store = checkFromTrustStore ? new X509Store(StoreName.Root, StoreLocation.LocalMachine) : new X509Store(StoreLocation.CurrentUser);

            if (store == null)
            {
                throw new ArgumentNullException("Unable to load the certificate store");
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentNullException("The subject cannot be empty");
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

        // TODO: needs a way to check what version is local server running without using a sql query if possible
        // TODO: needs to call sqlcmd or modify the registry to ensure SqlServer is running with the self-signed certificate set.

        public Tds8ConnectivityTest()
        {
            // Populate the variables with the environment variable values
            ValidCertificateFriendlyName = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_CERT_FRIENDLYNAME)) ? ValidCertificateFriendlyName : Environment.GetEnvironmentVariable(ENV_CERT_FRIENDLYNAME);
            ValidMismatchCertificateFriendlyName = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_CERT_MISMATCH_FRIENDLYNAME)) ? ValidMismatchCertificateFriendlyName : Environment.GetEnvironmentVariable(ENV_CERT_MISMATCH_FRIENDLYNAME);
            InvalidFormatCertificatePath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_INVALID_CERT_PATH)) ? InvalidFormatCertificatePath : Environment.GetEnvironmentVariable(ENV_INVALID_CERT_PATH);
            InvalidDNECertificatePath = Path.Combine(Environment.CurrentDirectory, "DOES_NOT_EXIST.cer");
            ValidCertificatePath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_VALID_CERT_PATH)) ? ValidCertificatePath : Environment.GetEnvironmentVariable(ENV_VALID_CERT_PATH);
            ValidMismatchCertificatePath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable(ENV_VALID_MISMATCH_CERT_PATH)) ? ValidMismatchCertificatePath : Environment.GetEnvironmentVariable(ENV_VALID_MISMATCH_CERT_PATH);
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

            if (!TrustedRootWithMismatchHostNameSelfSignedCertifcateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            // The certificate subject name is the IP Address in this mismatch certificate and the local machine hostname will be set in the HNIC.
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString)
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                HostNameInCertificate = GetHostName()
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
        public static void ShouldConnectIgnoreHNICWithCertificateInTrustedRootCertificate(DataSourceType dataSourceType, bool strict)
        {
            // NOTE: the server must have the self-signed certificate installed in the trusted root.
            if (!TrustedRootSelfSignedCertifcateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                HostNameInCertificate = "IGNORED.TEST.COM",
            };
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
            if (!SelfSignedCertificateInstalled())
            {
                Assert.True(true, "The self sign certificate is not installed and test skipped.");
                return;
            }

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = GetPathFromCertificateType(CertificatePathType.Invalid_DNE),
            };
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, CertificatePathType.Invalid_DNE, true)]
        [InlineData(DataSourceType.Localhost, CertificatePathType.Invalid_DNE, false)]
        [InlineData(DataSourceType.Hostname, CertificatePathType.Invalid_DNE, true)]
        [InlineData(DataSourceType.Hostname, CertificatePathType.Invalid_DNE, false)]
        [InlineData(DataSourceType.Localhost, CertificatePathType.Invalid_Format, true)]
        [InlineData(DataSourceType.Localhost, CertificatePathType.Invalid_Format, false)]
        [InlineData(DataSourceType.Hostname, CertificatePathType.Invalid_Format, true)]
        [InlineData(DataSourceType.Hostname, CertificatePathType.Invalid_Format, false)]
        public void ShouldFailWithLocalhostAndInvalidServerCertificate(DataSourceType dataSourceType, CertificatePathType certPathType, bool strict)
        {
            // NOTE: the server must have the self-signed certificate installed.
            if (!SelfSignedCertificateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = GetPathFromCertificateType(certPathType)
            };

            SqlException ex = Assert.Throws<SqlException>(() => Connect(builder.ConnectionString));
            Assert.NotNull(ex);
        }

        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost)]
        [InlineData(DataSourceType.Hostname)]
        public void ShouldConnectIgnoreHNICAndConnectWithEncryptOpional(DataSourceType dataSourceType)
        {
            // NOTE: the server must have the self-signed certificate installed.
            if (!SelfSignedCertificateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            SqlConnectionStringBuilder builder = new()
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
            if (!SelfSignedCertificateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            SqlConnectionStringBuilder builder = new()
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
            if (!SelfSignedCertificateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = SqlConnectionEncryptOption.Mandatory,
                TrustServerCertificate = true,
                ServerCertificate = GetPathFromCertificateType(CertificatePathType.Invalid_DNE),
            };

            Connect(builder.ConnectionString);
        }

        // Ideally, we would have a remote server to connect to and one that is a SQL Server 2022 agent and one that is not.
        [ConditionalTheory(nameof(IsNotAzureServer), nameof(IsNotAzureSynapse), nameof(AreConnectionStringsSetup))]
        [InlineData(DataSourceType.Localhost, true)]
        [InlineData(DataSourceType.Localhost, false)]
        [InlineData(DataSourceType.Hostname, true)]
        [InlineData(DataSourceType.Hostname, false)]
        public static void ShouldConnectWithMatchingServerCertificate(DataSourceType dataSourceType, bool strict)
        {
            if (!SelfSignedCertificateInstalled())
            {
                Assert.True(false, "The self sign certificate is not installed and test skipped.");
                return;
            }

            Assert.True(File.Exists(ValidCertificatePath));

            SqlConnectionStringBuilder builder = new()
            {
                DataSource = GetDataSourceName(dataSourceType),
                Encrypt = strict ? SqlConnectionEncryptOption.Strict : SqlConnectionEncryptOption.Mandatory,
                ServerCertificate = ValidCertificatePath
            };

            Connect(builder.ConnectionString);
        }

        /// <summary>
        /// Makes a connection using the specified connection string and queries the version.
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        private static void Connect(string connectionString)
        {
            using SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();

            // The reason why we also make a query is because it validates that decryption is working from SNI.
            // If it fails decryption, it would throw an exception.
            using SqlCommand command = new SqlCommand("select @@VERSION", connection);

            string version = command.ExecuteScalar().ToString();

            Assert.NotNull(version);
            Assert.NotEmpty(version);
        }
    }
}
