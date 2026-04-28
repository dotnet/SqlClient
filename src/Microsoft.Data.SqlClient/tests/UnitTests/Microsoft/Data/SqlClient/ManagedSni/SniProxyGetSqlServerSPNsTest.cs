// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    public class SniProxyGetSqlServerSPNsTest
    {
        /// <summary>
        /// Verifies that when connecting to a named instance without a protocol prefix
        /// (Protocol.None), the SPN uses the resolved port number from SSRP rather than
        /// the instance name. This is a regression test for GitHub issue #3566.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolNone_WithResolvedPort_UsesPortNotInstanceName()
        {
            // Arrange: parse "server\instance" which sets Protocol.None and IsSsrpRequired
            DataSource dataSource = DataSource.ParseServerName(@"server\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.None, dataSource.ResolvedProtocol);
            Assert.Equal("instance", dataSource.InstanceName);
            Assert.Equal(-1, dataSource.Port);

            // Simulate SSRP resolution setting the port (as CreateTcpHandle would do)
            dataSource.ResolvedPort = 12345;

            // Act
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Assert: SPN should contain the resolved port, NOT the instance name
            Assert.Contains(":12345", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that when connecting with an explicit tcp: prefix (Protocol.TCP),
        /// the SPN uses the resolved port number. This was the original fix for #2187.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolTcp_WithResolvedPort_UsesPort()
        {
            // Arrange: parse "tcp:server\instance" which sets Protocol.TCP
            DataSource dataSource = DataSource.ParseServerName(@"tcp:server\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.TCP, dataSource.ResolvedProtocol);

            dataSource.ResolvedPort = 54321;

            // Act
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Assert
            Assert.Contains(":54321", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that when connecting with Named Pipes protocol, the SPN uses
        /// the instance name rather than a port number.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolNp_WithInstanceName_UsesInstanceName()
        {
            // Named Pipes data sources go through a different parsing path
            // (InferNamedPipesInformation) that doesn't populate InstanceName,
            // so we test the lower-level overload directly.
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs("server", "myinstance", DataSource.Protocol.NP);

            Assert.Contains(":myinstance", spn.Primary);
            Assert.Null(spn.Secondary);
        }

        /// <summary>
        /// Verifies that when a custom ServerSPN is provided in the connection string,
        /// it is used as-is regardless of protocol or instance name.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_CustomSpnProvided_UsesCustomSpn()
        {
            DataSource dataSource = DataSource.ParseServerName(@"server\instance");
            Assert.NotNull(dataSource);
            dataSource.ResolvedPort = 12345;

            string customSpn = "MSSQLSvc/myserver.domain.com:1433";
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: customSpn);

            Assert.Equal(customSpn, spn.Primary);
            Assert.Null(spn.Secondary);
        }

        /// <summary>
        /// Verifies that when connecting with admin: prefix (DAC), the SPN uses
        /// the resolved port number (DAC also resolves via SSRP).
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolAdmin_WithResolvedPort_UsesPort()
        {
            DataSource dataSource = DataSource.ParseServerName(@"admin:server\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.Admin, dataSource.ResolvedProtocol);

            dataSource.ResolvedPort = 11111;

            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            Assert.Contains(":11111", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

#endif
