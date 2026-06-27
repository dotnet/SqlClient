// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Regression tests for SPN (Service Principal Name) selection logic in <see cref="SniProxy"/>.
    ///
    /// These tests verify that the driver generates protocol-specific SPNs for Kerberos authentication:
    /// - TCP-like protocols (TCP, None, Admin) use MSSQLSvc/hostname:port
    /// - Named Pipes uses MSSQLSvc/hostname:instancename
    /// - Custom ServerSPN overrides are always respected
    ///
    /// This addresses GitHub issue #3566: named instances connecting without a protocol prefix
    /// (Protocol.None) should use the SSRP-resolved port in the SPN, not the instance name.
    ///
    /// See: https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/register-a-service-principal-name-for-kerberos-connections
    /// </summary>
    public class SniProxyGetSqlServerSPNsTest
    {
        /// <summary>
        /// Verifies that Protocol.None (default when no prefix specified, e.g. "server\instance")
        /// uses the SSRP-resolved port in the SPN, not the instance name.
        ///
        /// This is a regression test for GitHub issue #3566. On Linux with SSRP, a named instance
        /// connection string like "Data Source=server\instance" requires the resolved TCP port
        /// from SSRP to be used in the SPN for Kerberos authentication to succeed.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolNone_WithResolvedPort_UsesPortNotInstanceName()
        {
            // Arrange: parse "localhost\instance" which sets Protocol.None and IsSsrpRequired.
            // Using "localhost" instead of an arbitrary hostname avoids real DNS lookups
            // that would make the test flaky in environments with restricted DNS resolution.
            DataSource dataSource = DataSource.ParseServerName(@"localhost\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.None, dataSource.ResolvedProtocol);
            Assert.Equal("instance", dataSource.InstanceName);
            Assert.Equal(-1, dataSource.Port); // No explicit port in connection string

            // Simulate SSRP resolution setting the port (as CreateTcpHandle would do)
            dataSource.ResolvedPort = 12345;

            // Act: generate SPN for this named instance with resolved port
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Assert: SPN should contain the resolved port, NOT the instance name
            Assert.Contains(":12345", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that Protocol.TCP (explicit "tcp:" prefix) uses the resolved port in the SPN.
        ///
        /// This was the original fix for GitHub issue #2187 and ensures TCP protocol behavior
        /// is consistent across all platforms.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolTcp_WithResolvedPort_UsesPort()
        {
            // Arrange: parse "tcp:localhost\instance" which sets Protocol.TCP.
            // Using "localhost" avoids real DNS lookups that would make the test flaky.
            DataSource dataSource = DataSource.ParseServerName(@"tcp:localhost\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.TCP, dataSource.ResolvedProtocol);

            // Simulate SSRP resolution setting the port
            dataSource.ResolvedPort = 54321;

            // Act: generate SPN for this TCP named instance
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Assert: SPN should use the resolved port
            Assert.Contains(":54321", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies that Protocol.NP (Named Pipes) uses the instance name in the SPN,
        /// not a port number.
        ///
        /// Named Pipes protocol requires instance-name-based SPNs per SQL Server guidelines.
        /// This test ensures NP behavior is preserved when the general protocol logic is updated.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolNp_WithInstanceName_UsesInstanceName()
        {
            // Arrange & Act: test the lower-level overload directly with NP protocol.
            // Named Pipes data sources go through a different parsing path that doesn't
            // populate InstanceName in the same way, so we call the helper directly.
            // Using "localhost" avoids real DNS lookups that would make the test flaky.
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs("localhost", "myinstance", DataSource.Protocol.NP);

            // Assert: SPN should use the instance name, not a port
            Assert.Contains(":myinstance", spn.Primary);
            Assert.Null(spn.Secondary); // NP does not generate a secondary SPN
        }

        /// <summary>
        /// Verifies that explicit ServerSPN overrides (via connection string) are used as-is,
        /// bypassing all auto-generation logic.
        ///
        /// This is critical for Kerberos environments where custom SPNs may be required
        /// (e.g., non-standard ports, aliased hostnames, or specific service accounts).
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_CustomSpnProvided_UsesCustomSpn()
        {
            // Arrange: parse a named instance, but provide a custom SPN override.
            // Using "localhost" avoids real DNS lookups that would make the test flaky.
            DataSource dataSource = DataSource.ParseServerName(@"localhost\instance");
            Assert.NotNull(dataSource);
            dataSource.ResolvedPort = 12345;

            string customSpn = "MSSQLSvc/myserver.domain.com:1433";

            // Act: generate SPN with explicit override
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: customSpn);

            // Assert: custom SPN is used exactly as provided, without modification
            Assert.Equal(customSpn, spn.Primary);
            Assert.Null(spn.Secondary);
        }

        /// <summary>
        /// Verifies that Protocol.Admin (Dedicated Administrator Connection, DAC)
        /// uses the resolved port in the SPN, not the instance name.
        ///
        /// DAC also uses SSRP resolution and should follow the same protocol-based logic
        /// as Protocol.TCP and Protocol.None for SPN generation.
        /// </summary>
        [Fact]
        public void GetSqlServerSPNs_ProtocolAdmin_WithResolvedPort_UsesPort()
        {
            // Arrange: parse "admin:localhost\instance" which sets Protocol.Admin.
            // Using "localhost" avoids real DNS lookups that would make the test flaky.
            DataSource dataSource = DataSource.ParseServerName(@"admin:localhost\instance");
            Assert.NotNull(dataSource);
            Assert.Equal(DataSource.Protocol.Admin, dataSource.ResolvedProtocol);

            // Simulate SSRP resolution setting the port
            dataSource.ResolvedPort = 11111;

            // Act: generate SPN for this DAC connection to a named instance
            ResolvedServerSpn spn = SniProxy.GetSqlServerSPNs(dataSource, serverSPN: string.Empty);

            // Assert: SPN should use the resolved port, not the instance name
            Assert.Contains(":11111", spn.Primary);
            Assert.DoesNotContain("instance", spn.Primary, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}

#endif
