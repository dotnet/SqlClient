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
    /// A UNC path host component may never contain a colon, so an IPv6 literal server name cannot
    /// be used directly. Passing one through anyway composes a malformed pipe path such as
    /// <c>\\::1\pipe\sql\query</c>, which sends the SMB redirector into an SMB session setup that
    /// can fault LSASS on Windows and force a reboot. Windows instead defines a transcription for
    /// this case (<c>2001:db8::1</c> becomes <c>2001-db8--1.ipv6-literal.net</c>), which the parser
    /// now applies so IPv6 Named Pipes connections keep working.
    ///
    /// See: https://github.com/dotnet/SqlClient/issues/4523
    /// and https://learn.microsoft.com/openspecs/windows_protocols/ms-dtyp/62e862f4-2a51-452e-8eeb-dc4ff5ee33cc
    /// </summary>
    public class DataSourceNamedPipesTests
    {
        /// <summary>
        /// Verifies that an IPv6 literal host is transcribed to its <c>.ipv6-literal.net</c> UNC
        /// form, covering both the <c>np:host</c> form and the <c>\\host\pipe\...</c> UNC form,
        /// with and without brackets and with a zone index.
        /// </summary>
        [Theory]
        [InlineData(@"np:::1", "--1.ipv6-literal.net")]
        [InlineData(@"np:[::1]", "--1.ipv6-literal.net")]
        [InlineData(@"np:2001:db8::1", "2001-db8--1.ipv6-literal.net")]
        [InlineData(@"np:fe80::1%3", "fe80--1s3.ipv6-literal.net")]
        [InlineData(@"\\::1\pipe\sql\query", "--1.ipv6-literal.net")]
        [InlineData(@"np:\\::1\pipe\sql\query", "--1.ipv6-literal.net")]
        [InlineData(@"np:\\[2001:db8::1]\pipe\MSSQL$MYINSTANCE\sql\query", "2001-db8--1.ipv6-literal.net")]
        public void ParseServerName_NamedPipesWithIPv6Literal_IsTranscribedToUncForm(
            string dataSource, string expectedPipeHostName)
        {
            DataSource details = DataSource.ParseServerName(dataSource);

            Assert.NotNull(details);
            Assert.Equal(DataSource.Protocol.NP, details.ResolvedProtocol);
            Assert.Equal(expectedPipeHostName, details.PipeHostName);
            // The pipe host name is what reaches the OS, so it must never carry a colon.
            Assert.DoesNotContain(":", details.PipeHostName);
        }

        /// <summary>
        /// Verifies that a colon-bearing host that is not a parseable IPv6 literal has no UNC form
        /// and is therefore rejected, rather than composing a malformed pipe path.
        /// </summary>
        [Theory]
        [InlineData(@"np:not:a:host")]
        [InlineData(@"np:2001:db8:::::1")]
        [InlineData(@"\\not:a:host\pipe\sql\query")]
        public void ParseServerName_NamedPipesWithUnparseableColonHost_IsRejected(string dataSource)
        {
            Assert.Null(DataSource.ParseServerName(dataSource));
        }

        /// <summary>
        /// Verifies that IPv6 transcription does not regress legitimate Named Pipes data sources:
        /// IPv4 literals, <c>localhost</c>, <c>.</c>, named instances, and explicit UNC pipe paths
        /// must still parse and yield an unchanged pipe host name.
        /// </summary>
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

        /// <summary>
        /// Verifies that a Named Pipes data source given without a UNC path still composes the
        /// default pipe name, including the <c>MSSQL$&lt;instance&gt;</c> prefix for named instances.
        /// These forms are asserted separately from the UNC forms because the UNC path builds its
        /// pipe name with <see cref="System.IO.Path.DirectorySeparatorChar"/>, which is platform dependent.
        /// </summary>
        [Theory]
        [InlineData(@"np:127.0.0.1", @"sql\query")]
        [InlineData(@"np:localhost", @"sql\query")]
        [InlineData(@"np:::1", @"sql\query")]
        [InlineData(@"np:server\instance", @"MSSQL$instance\sql\query")]
        public void ParseServerName_NamedPipesWithoutUncPath_ComposesDefaultPipeName(
            string dataSource, string expectedPipeName)
        {
            DataSource details = DataSource.ParseServerName(dataSource);

            Assert.NotNull(details);
            Assert.Equal(expectedPipeName, details.PipeName);
        }

        /// <summary>
        /// Verifies that an IPv6 literal keeps its original form in <see cref="DataSource.ServerName"/>,
        /// which feeds SPN creation, even though the pipe host name is transcribed.
        /// </summary>
        [Fact]
        public void ParseServerName_NamedPipesWithIPv6Literal_PreservesServerNameForSpn()
        {
            DataSource details = DataSource.ParseServerName(@"np:2001:db8::1");

            Assert.NotNull(details);
            Assert.Equal("2001:db8::1", details.ServerName);
            Assert.Equal("2001-db8--1.ipv6-literal.net", details.PipeHostName);
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

        /// <summary>
        /// Verifies <see cref="DataSource.GetUncCompatibleHostName"/> directly: colon-free host names
        /// pass through untouched, IPv6 literals are transcribed per MS-DTYP, and colon-bearing host
        /// names with no IPv6 interpretation return <see langword="null"/>.
        /// </summary>
        [Theory]
        [InlineData(".", ".")]
        [InlineData("localhost", "localhost")]
        [InlineData("127.0.0.1", "127.0.0.1")]
        [InlineData("my-server.contoso.com", "my-server.contoso.com")]
        [InlineData("--1.ipv6-literal.net", "--1.ipv6-literal.net")]
        [InlineData("::1", "--1.ipv6-literal.net")]
        [InlineData("[::1]", "--1.ipv6-literal.net")]
        [InlineData("2001:db8::1", "2001-db8--1.ipv6-literal.net")]
        [InlineData("fe80::1%3", "fe80--1s3.ipv6-literal.net")]
        public void GetUncCompatibleHostName_ReturnsExpected(string hostName, string expected)
        {
            Assert.Equal(expected, DataSource.GetUncCompatibleHostName(hostName));
        }

        /// <summary>
        /// Verifies <see cref="DataSource.GetUncCompatibleHostName"/> returns <see langword="null"/>
        /// for host names that contain a colon but have no IPv6 interpretation, and for empty input.
        /// </summary>
        [Theory]
        [InlineData("not:a:host")]
        [InlineData("2001:db8:::::1")]
        [InlineData("[:]")]
        [InlineData("")]
        public void GetUncCompatibleHostName_UnconvertibleHost_ReturnsNull(string hostName)
        {
            Assert.Null(DataSource.GetUncCompatibleHostName(hostName));
        }

        /// <summary>
        /// Verifies <see cref="DataSource.GetUncCompatibleHostName"/> returns <see langword="null"/>
        /// for a null host name. Covered separately from the theory above because xUnit disallows
        /// null theory data for a non-nullable string parameter.
        /// </summary>
        [Fact]
        public void GetUncCompatibleHostName_Null_ReturnsNull()
        {
            Assert.Null(DataSource.GetUncCompatibleHostName(null));
        }
    }
}

#endif
