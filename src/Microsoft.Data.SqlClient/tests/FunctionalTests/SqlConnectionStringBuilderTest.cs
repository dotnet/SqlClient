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
        [InlineData("Asynchronous Processing = True")]
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
