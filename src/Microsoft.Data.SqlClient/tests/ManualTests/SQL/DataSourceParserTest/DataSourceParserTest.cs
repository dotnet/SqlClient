// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.DataSourceParserTest
{
    public class DataSourceParserTest
    {
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("localhost")]
        [InlineData("tcp:localhost")]
        [InlineData(" localhost ")]
        [InlineData(" tcp:localhost ")]
        [InlineData(" localhost")]
        [InlineData(" tcp:localhost")]
        [InlineData("localhost ")]
        [InlineData("tcp:localhost ")]
        public void ParseDataSourceWithoutInstanceNorPortTestShouldSucceed(string dataSource)
        {
            DataTestUtility.ParseDataSource(dataSource, out string hostname, out _, out _);
            Assert.Equal("localhost", hostname);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("localhost,1433")]
        [InlineData("tcp:localhost,1433")]
        [InlineData(" localhost,1433 ")]
        [InlineData(" tcp:localhost,1433 ")]
        [InlineData(" localhost,1433")]
        [InlineData(" tcp:localhost,1433")]
        [InlineData("localhost,1433 ")]
        [InlineData("tcp:localhost,1433 ")]
        public void ParseDataSourceWithoutInstanceButWithPortTestShouldSucceed(string dataSource)
        {
            DataTestUtility.ParseDataSource(dataSource, out string hostname, out int port, out _);
            Assert.Equal("localhost", hostname);
            Assert.Equal(1433, port);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("localhost\\MSSQLSERVER02")]
        [InlineData("tcp:localhost\\MSSQLSERVER02")]
        [InlineData(" localhost\\MSSQLSERVER02 ")]
        [InlineData(" tcp:localhost\\MSSQLSERVER02 ")]
        [InlineData(" localhost\\MSSQLSERVER02")]
        [InlineData(" tcp:localhost\\MSSQLSERVER02")]
        [InlineData("localhost\\MSSQLSERVER02 ")]
        [InlineData("tcp:localhost\\MSSQLSERVER02 ")]
        public void ParseDataSourceWithInstanceButWithoutPortTestShouldSucceed(string dataSource)
        {
            DataTestUtility.ParseDataSource(dataSource, out string hostname, out _, out string instanceName);
            Assert.Equal("localhost", hostname);
            Assert.Equal("MSSQLSERVER02", instanceName);
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        [InlineData("localhost\\MSSQLSERVER02,1433")]
        [InlineData("tcp:localhost\\MSSQLSERVER02,1433")]
        [InlineData(" localhost\\MSSQLSERVER02,1433 ")]
        [InlineData(" tcp:localhost\\MSSQLSERVER02,1433 ")]
        [InlineData(" localhost\\MSSQLSERVER02,1433")]
        [InlineData(" tcp:localhost\\MSSQLSERVER02,1433")]
        [InlineData("localhost\\MSSQLSERVER02,1433 ")]
        [InlineData("tcp:localhost\\MSSQLSERVER02,1433 ")]
        public void ParseDataSourceWithInstanceAndPortTestShouldSucceed(string dataSource)
        {
            DataTestUtility.ParseDataSource(dataSource, out string hostname, out int port, out string instanceName);
            Assert.Equal("localhost", hostname);
            Assert.Equal("MSSQLSERVER02", instanceName);
            Assert.Equal(1433, port);
        }
    }
}
