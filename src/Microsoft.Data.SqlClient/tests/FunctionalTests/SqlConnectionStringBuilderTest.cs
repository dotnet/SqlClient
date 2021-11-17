// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public partial class SqlConnectionStringBuilderTest
    {

        [Theory]
        [InlineData("Application Name = .Net Tests")]
        [InlineData("Application Intent = ReadOnly")]
        [InlineData("ApplicationIntent = ReadOnly")]
        [InlineData("Attestation Protocol = HGS")]
        [InlineData("Authentication = Active Directory Password ")]
        [InlineData("Authentication = ActiveDirectoryPassword ")]
        [InlineData("Authentication = Active Directory Integrated ")]
        [InlineData("Authentication = ActiveDirectoryIntegrated ")]
        [InlineData("Authentication = Active Directory Interactive ")]
        [InlineData("Authentication = ActiveDirectoryInteractive ")]
        [InlineData("Authentication = Active Directory Device Code Flow ")]
        [InlineData("Authentication = ActiveDirectoryDeviceCodeFlow ")]
        [InlineData("Authentication = Active Directory Service Principal ")]
        [InlineData("Authentication = ActiveDirectoryServicePrincipal ")]
        [InlineData("Authentication = Active Directory Managed Identity ")]
        [InlineData("Authentication = ActiveDirectoryManagedIdentity ")]
        [InlineData("Authentication = Active Directory MSI ")]
        [InlineData("Authentication = ActiveDirectoryMSI ")]
        [InlineData("Authentication = Active Directory Default ")]
        [InlineData("Authentication = ActiveDirectoryDefault ")]
        [InlineData("Command Timeout = 5")]
        [InlineData("Command Timeout = 15")]
        [InlineData("Command Timeout = 0")]
        [InlineData("ConnectRetryCount = 5")]
        [InlineData("Connect Retry Count = 0")]
        [InlineData("ConnectRetryInterval = 10")]
        [InlineData("Connect Retry Interval = 20")]
        [InlineData("Connect Timeout = 30")]
        [InlineData("Connection Timeout = 5")]
        [InlineData("Connection Lifetime = 30")]
        [InlineData("Load Balance Timeout = 0")]
        [InlineData("Current Language = true")]
        [InlineData("Timeout = 0")]
        [InlineData("Column Encryption Setting = Enabled")]
        [InlineData("Data Source = randomserver.sys.local")]
        [InlineData("Address = np:localhost")]
        [InlineData("Network Address = (localdb)\\myInstance")]
        [InlineData("Server = randomserver.sys.local; uid = a; pwd = b")]
        [InlineData("Addr = randomserver.sys.local; User Id = a; Password = b")]
        [InlineData("Database = master")]
        [InlineData("Enclave Attestation Url = http://dymmyurl")]
        [InlineData("Encrypt = true")]
        [InlineData("Enlist = false")]
        [InlineData("Initial Catalog = Northwind; Failover Partner = randomserver.sys.local")]
        [InlineData("Initial Catalog = tempdb")]
        [InlineData("Integrated Security = true")]
        [InlineData("IPAddressPreference = IPv4First")]
        [InlineData("IPAddressPreference = IPv6First")]
        [InlineData("IPAddressPreference = UsePlatformDefault")]
        [InlineData("Trusted_Connection = false")]
        [InlineData("Max Pool Size = 50")]
        [InlineData("Min Pool Size = 20")]
        [InlineData("MultipleActiveResultSets = true")]
        [InlineData("Multiple Active Result Sets = false")]
        [InlineData("Multi Subnet Failover = true")]
        [InlineData("Packet Size = 12000")]
        [InlineData("Password = some@pass#!@123")]
        [InlineData("Persist Security Info = false")]
        [InlineData("PersistSecurityInfo = true")]
        [InlineData("PoolBlockingPeriod = Auto")]
        [InlineData("PoolBlockingperiod = NeverBlock")]
        [InlineData("Pooling = no")]
        [InlineData("Pooling = false")]
        [InlineData("Replication = true")]
        [InlineData("Transaction Binding = Explicit Unbind")]
        [InlineData("Trust Server Certificate = true")]
        [InlineData("TrustServerCertificate = false")]
        [InlineData("Type System Version = Latest")]
        [InlineData("User Instance = true")]
        [InlineData("Workstation ID = myworkstation")]
        [InlineData("WSID = myworkstation")]
        public void ConnectionStringTests(string connectionString)
        {
            ExecuteConnectionStringTests(connectionString);
        }

        [Theory]
        [InlineData("Connection Reset = false")]
        [InlineData("Context Connection = false")]
        [InlineData("Network Library = dbmssocn")]
        [InlineData("Network = dbnmpntw")]
        [InlineData("Net = dbmsrpcn")]
        [InlineData("TransparentNetworkIPResolution = false")]
        [InlineData("Transparent Network IP Resolution = true")]
        [SkipOnTargetFramework(~TargetFrameworkMonikers.NetFramework)]
        public void ConnectionStringTestsNetFx(string connectionString)
        {
            ExecuteConnectionStringTests(connectionString);
        }

        [Fact]
        public void SetInvalidApplicationIntent_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ApplicationIntent invalid = (ApplicationIntent)Enum.GetValues(typeof(ApplicationIntent)).Length + 1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.ApplicationIntent = invalid);
            Assert.Contains("ApplicationIntent", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidAttestationProtocol_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            SqlConnectionAttestationProtocol invalid = (SqlConnectionAttestationProtocol)Enum.GetValues(typeof(SqlConnectionAttestationProtocol)).Length + 1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.AttestationProtocol = invalid);
            Assert.Contains("SqlConnectionAttestationProtocol", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidAuthentication_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            SqlAuthenticationMethod invalid = (SqlAuthenticationMethod)Enum.GetValues(typeof(SqlAuthenticationMethod)).Length + 1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.Authentication = invalid);
            Assert.Contains("SqlAuthenticationMethod", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidColumnEncryptionSetting_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            SqlConnectionColumnEncryptionSetting invalid = (SqlConnectionColumnEncryptionSetting)Enum.GetValues(typeof(SqlConnectionColumnEncryptionSetting)).Length + 1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.ColumnEncryptionSetting = invalid);
            Assert.Contains("SqlConnectionColumnEncryptionSetting", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidConnectTimeout_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.ConnectTimeout = -1);
            Assert.Contains("connect timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidCommandTimeout_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.CommandTimeout = -1);
            Assert.Contains("command timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(256)]
        public void SetInvalidConnectRetryCount_Throws(int invalid)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.ConnectRetryCount = invalid);
            Assert.Contains("connect retry count", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(256)]
        public void SetInvalidConnectRetryInterval_Throws(int invalid)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.ConnectRetryInterval = invalid);
            Assert.Contains("connect retry interval", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidIPAddressPreference_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            SqlConnectionIPAddressPreference invalid = (SqlConnectionIPAddressPreference)Enum.GetValues(typeof(SqlConnectionIPAddressPreference)).Length + 1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.IPAddressPreference = invalid);
            Assert.Contains("SqlConnectionIPAddressPreference", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidPoolBlockingPeriod_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            PoolBlockingPeriod invalid = (PoolBlockingPeriod)Enum.GetValues(typeof(PoolBlockingPeriod)).Length + 1;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => builder.PoolBlockingPeriod = invalid);
            Assert.Contains("PoolBlockingPeriod", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidLoadBalanceTimeout_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.LoadBalanceTimeout = -1);
            Assert.Contains("load balance timeout", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidMaxPoolSize_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.MaxPoolSize = 0);
            Assert.Contains("max pool size", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SetInvalidMinPoolSize_Throws()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.MinPoolSize = -1);
            Assert.Contains("min pool size", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(511)]
        [InlineData(32769)]
        [InlineData(int.MaxValue)]
        public void SetInvalidPacketSize_Throws(int invalid)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();

            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder.PacketSize = invalid);
            Assert.Contains("packet size", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("AttachDBFilename","somefile.db")]
        public void SetKeyword(string keyword, string value)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder[keyword] = value;
            Assert.Equal(builder[keyword], value);
        }

        [Fact]
        public void SetNotSupportedKeyword_Throws()
        {
            // We may want to remove the unreachable code path for default in the GetIndex(keyword) method already throws UnsupportedKeyword
            // so default: throw UnsupportedKeyword(keyword) is never reached unless it's a supported keyword, but it's not handled in the switch case.

            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            ArgumentException ex = Assert.Throws<ArgumentException>(() => builder["NotSupported"] = "not important");
            Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UnexpectedKeywordRetrieval()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder("Data Source=localhost");
            Assert.Throws<ArgumentException>(() => builder["RandomKeyword"]);
        }

        [Theory]
        [InlineData(@"C:\test\attach.mdf", "AttachDbFilename=C:\\test\\attach.mdf")]
        [InlineData(@"C:\test\attach.mdf;", "AttachDbFilename=\"C:\\test\\attach.mdf;\"")]
        public void ConnectionString_AttachDbFileName_Plain(string value, string expected)
        {
            var builder = new SqlConnectionStringBuilder();
            builder.AttachDBFilename = value;
            Assert.Equal(expected, builder.ConnectionString);
        }

        [Theory]
        [PlatformSpecific(TestPlatforms.Windows)]
        [InlineData(@"|DataDirectory|\attach.mdf",
                    @"AttachDbFilename=|DataDirectory|\attach.mdf",
                    @"C:\test\")]
        [InlineData(@"|DataDirectory|\attach.mdf",
                    @"AttachDbFilename=|DataDirectory|\attach.mdf",
                    @"C:\test")]
        [InlineData(@"|DataDirectory|attach.mdf",
                    @"AttachDbFilename=|DataDirectory|attach.mdf",
                    @"C:\test")]
        [InlineData(@"|DataDirectory|attach.mdf",
                    @"AttachDbFilename=|DataDirectory|attach.mdf",
                    @"C:\test\")]
        [InlineData(@"  |DataDirectory|attach.mdf",
                    "AttachDbFilename=\"  |DataDirectory|attach.mdf\"",
                    @"C:\test\")]
        [InlineData(@"|DataDirectory|attach.mdf  ",
                    "AttachDbFilename=\"|DataDirectory|attach.mdf  \"",
                    @"C:\test\")]
        public void ConnectionStringBuilder_AttachDbFileName_DataDirectory(string value, string expected, string dataDirectory)
        {
            AppDomain.CurrentDomain.SetData("DataDirectory", dataDirectory);

            var builder = new SqlConnectionStringBuilder();
            builder.AttachDBFilename = value;
            Assert.Equal(expected, builder.ConnectionString);
        }

        internal void ExecuteConnectionStringTests(string connectionString)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString);
            string retrievedString = builder.ConnectionString;
            SqlConnectionStringBuilder builder2 = new SqlConnectionStringBuilder(retrievedString);

            Assert.Equal(builder, builder2);
            Assert.NotNull(builder.Values);
            Assert.True(builder.Values.Count > 0);
            foreach (string key in builder2.Keys)
            {
                Assert.True(builder.TryGetValue(key, out object valueBuilder1));
                Assert.True(builder2.TryGetValue(key, out object valueBuilder2));
                Assert.Equal(valueBuilder1, valueBuilder2);
                Assert.True(builder2.ContainsKey(key));
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                Assert.NotNull(connection);
            }
        }
    }
}
