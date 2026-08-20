// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Regression tests for Named Pipes data source parsing in <see cref="DataSource"/>.
    ///
    /// A UNC path host component may never contain a colon, so an IPv6 literal server name
    /// produces a malformed pipe path such as <c>\\::1\pipe\sql\query</c>. Handing that to the
    /// OS sends the SMB redirector into an SMB session setup that can fault LSASS on Windows
    /// and force a reboot. Such data sources must be rejected during parsing.
    ///
    /// See: https://github.com/dotnet/SqlClient/issues/4523
    /// </summary>
    public class DataSourceNamedPipesTests
    {
        [Theory]
        [InlineData(@"np:::1")]
        [InlineData(@"np:[::1]")]
        [InlineData(@"np:fe80::1")]
        [InlineData(@"np:2001:db8::1")]
        [InlineData(@"\\::1\pipe\sql\query")]
        [InlineData(@"np:\\::1\pipe\sql\query")]
        [InlineData(@"np:\\[::1]\pipe\MSSQL$MYINSTANCE\sql\query")]
        public void ParseServerName_NamedPipesWithIPv6Literal_IsRejected(string dataSource)
        {
            Assert.Null(DataSource.ParseServerName(dataSource));
        }

        [Theory]
        [InlineData(@"np:127.0.0.1", "127.0.0.1")]
        [InlineData(@"np:localhost", "localhost")]
        [InlineData(@"np:.", ".")]
        [InlineData(@"np:server\instance", "server")]
        [InlineData(@"\\127.0.0.1\pipe\sql\query", "127.0.0.1")]
        [InlineData(@"\\.\pipe\MSSQL$MYINSTANCE\sql\query", ".")]
        [InlineData(@"\\my-server\pipe\sql\query", "my-server")]
        public void ParseServerName_NamedPipesWithValidHost_IsAccepted(
            string dataSource, string expectedPipeHostName)
        {
            DataSource details = DataSource.ParseServerName(dataSource);

            Assert.NotNull(details);
            Assert.Equal(DataSource.Protocol.NP, details.ResolvedProtocol);
            Assert.Equal(expectedPipeHostName, details.PipeHostName);
            Assert.False(string.IsNullOrEmpty(details.PipeName));
        }

        [Theory]
        [InlineData(@"np:127.0.0.1", @"sql\query")]
        [InlineData(@"np:localhost", @"sql\query")]
        [InlineData(@"np:server\instance", @"MSSQL$instance\sql\query")]
        public void ParseServerName_NamedPipesWithoutUncPath_ComposesDefaultPipeName(
            string dataSource, string expectedPipeName)
        {
            DataSource details = DataSource.ParseServerName(dataSource);

            Assert.NotNull(details);
            Assert.Equal(expectedPipeName, details.PipeName);
        }

        /// <summary>
        /// Without an explicit protocol prefix, managed SNI defaults to TCP, so an IPv6 literal
        /// server name must continue to parse successfully and never reach the Named Pipes path.
        /// </summary>
        [Theory]
        [InlineData("::1")]
        [InlineData("[::1]")]
        [InlineData("fe80::1")]
        public void ParseServerName_IPv6LiteralWithoutProtocol_ResolvesToNonNamedPipes(string dataSource)
        {
            DataSource details = DataSource.ParseServerName(dataSource);

            Assert.NotNull(details);
            Assert.NotEqual(DataSource.Protocol.NP, details.ResolvedProtocol);
        }

        [Theory]
        [InlineData("::1", false)]
        [InlineData("[::1]", false)]
        [InlineData("", false)]
        [InlineData(".", true)]
        [InlineData("localhost", true)]
        [InlineData("127.0.0.1", true)]
        [InlineData("my-server.contoso.com", true)]
        public void IsValidPipeHostName_ReturnsExpected(string hostName, bool expected)
        {
            Assert.Equal(expected, DataSource.IsValidPipeHostName(hostName));
        }

        [Fact]
        public void IsValidPipeHostName_Null_ReturnsFalse()
        {
            Assert.False(DataSource.IsValidPipeHostName(null));
        }
    }
}

#endif
