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
        [Fact]
        public void GetSqlServerSPNs_ProtocolNone_WithResolvedPort_UsesPortNotInstanceName()
        {
            DataSource dataSource = DataSource.ParseServerName(@"server\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.None, dataSource.ResolvedProtocol);
            Assert.Equal("instance", dataSource.InstanceName);
            Assert.Equal(-1, dataSource.Port);

            dataSource.ResolvedPort = 12345;

            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            Assert.Contains(":12345", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetSqlServerSPNs_ProtocolTcp_WithResolvedPort_UsesPort()
        {
            DataSource dataSource = DataSource.ParseServerName(@"tcp:server\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.TCP, dataSource.ResolvedProtocol);

            dataSource.ResolvedPort = 54321;

            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            Assert.Contains(":54321", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void GetSqlServerSPNs_ProtocolNp_WithInstanceName_UsesInstanceName()
        {
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs("server", "myinstance", DataSource.Protocol.NP);

            Assert.Contains(":myinstance", spn.Primary);
            Assert.Null(spn.Secondary);
        }

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
