// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlConnectionStringBuilderTest
    {

        [Theory]
        [InlineData("Data Source= randomserver.sys.local")]
        [InlineData("Data Source= randomserver.sys.local; uid = a; pwd = b")]
        [InlineData("Workstation ID = myworkstation")]
        [InlineData("WSID = myworkstation")]
        [InlineData("Application Name = .Net Tests")]
        [InlineData("Pooling = false")]
        public void ConnectionStringTests(string connectionString)
        {
            ExecuteConnectionStringTests(connectionString);
        }

        [Theory]
        [InlineData("PoolBlockingPeriod = Auto")]
        [InlineData("PoolBlockingperiod = NeverBlock")]
        public void ConnectionStringTestsNetCoreApp(string connectionString)
        {
            ExecuteConnectionStringTests(connectionString);
        }

        [Theory]
        [InlineData("Authentication = Active Directory Password ")]
        [InlineData("Authentication = Active Directory Integrated ")]
        [InlineData("Authentication = ActiveDirectoryPassword ")]
        [InlineData("Authentication = ActiveDirectoryIntegrated ")]
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

        private void ExecuteConnectionStringTests(string connectionString)
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
        }

    }
}