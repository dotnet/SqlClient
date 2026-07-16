// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Integration tests for Kerberos authentication with protocol-specific SPN handling.
    ///
    /// These tests verify that the driver generates protocol-specific SPNs for Kerberos authentication
    /// across different protocol types (TCP, None, Named Pipes, Admin). They complement unit tests
    /// in SniProxyGetSqlServerSPNsTest by verifying end-to-end authentication behavior in a real
    /// Kerberos domain environment.
    ///
    /// See: https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/register-a-service-principal-name-for-kerberos-connections
    /// </summary>
    [Trait("Set", "3")]
    public class KerberosTests
    {
        /// <summary>
        /// Baseline Kerberos connectivity test verifying that the Kerberos authentication mechanism works
        /// with the configured connection strings.
        ///
        /// This test runs on Unix platforms with Kerberos credentials and verifies that connections
        /// authenticate using the KERBEROS auth_scheme (confirmed via sys.dm_exec_connections).
        /// </summary>
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public void IsKerBerosSetupTestAsync(string connectionStr)
        {
            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);
            using SqlConnection conn = new(connectionStr);

            conn.Open();
            using SqlCommand command = new("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read(), "Expected to receive one row data");
            Assert.Equal("KERBEROS", reader.GetString(0));
        }

        /// <summary>
        /// Tests Kerberos authentication with Protocol.None (default when no protocol prefix specified).
        ///
        /// This regression test for GitHub issue #3566 verifies that named instances accessed via
        /// Protocol.None (e.g. "Data Source=hostname\instancename") use the correct SPN format:
        /// - If SSRP resolves a port: MSSQLSvc/hostname:port (not instance name)
        /// - Kerberos should authenticate successfully with the correct SPN
        ///
        /// Environment: Requires a named instance running on a domain-joined server with an SSRP-resolvable port.
        /// </summary>
        [PlatformSpecific(TestPlatforms.Linux)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest), nameof(DataTestUtility.IsNamedInstanceSetup))]
        public void KerberosTest_ProtocolNone_NamedInstanceWithSsrpResolution()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                out string hostname, out int port, out string instanceName);

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // Build from the base connection string to preserve environment settings (Encrypt,
            // TrustServerCertificate, timeouts, etc.), overriding only DataSource and IntegratedSecurity.
            // SSRP resolution should occur and populate the port in the SPN.
            SqlConnectionStringBuilder protocolNoneBuilder = new(tcpConnStr)
            {
                DataSource = $"{hostname}\\{instanceName}",
                IntegratedSecurity = true
            };

            using SqlConnection conn = new(protocolNoneBuilder.ConnectionString);
            conn.Open(); // Connection should succeed with Kerberos using the SSRP-resolved port in SPN

            // Verify authentication occurred with KERBEROS auth_scheme
            using SqlCommand command = new("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read(), "Expected to receive one row data");
            Assert.Equal("KERBEROS", reader.GetString(0));
        }

        /// <summary>
        /// Tests Kerberos authentication with explicit Protocol.TCP prefix on a named instance.
        ///
        /// Verifies that "tcp:hostname\instancename" uses the TCP-like SPN format with port:
        /// MSSQLSvc/hostname:port (where port is from explicit connection string or SSRP resolution).
        ///
        /// Environment: Requires a named instance with an explicitly specified or SSRP-resolvable port.
        /// </summary>
        [PlatformSpecific(TestPlatforms.Linux)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest), nameof(DataTestUtility.IsNamedInstanceSetup))]
        public void KerberosTest_ProtocolTcp_NamedInstance()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                out string hostname, out int port, out string instanceName);

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // Build the tcp: data source. Include an explicit port only when the test connection string
            // already has one; otherwise leave SSRP to resolve the port for the named instance.
            // Do NOT fall back to port 1433: that disables SSRP and is unlikely to be correct for
            // named instances, and produces an invalid "host\,1433" when instanceName is empty.
            string newDataSource = port > 0
                ? $"tcp:{hostname}\\{instanceName},{port}"
                : $"tcp:{hostname}\\{instanceName}";

            // Preserve the base connection string settings (Encrypt, TrustServerCertificate, etc.)
            SqlConnectionStringBuilder builder = new(tcpConnStr)
            {
                DataSource = newDataSource,
                IntegratedSecurity = true
            };

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            using SqlCommand command = new("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read(), "Expected to receive one row data");
            Assert.Equal("KERBEROS", reader.GetString(0));
        }

        /// <summary>
        /// Tests Kerberos authentication with a custom ServerSPN override.
        ///
        /// Verifies that explicitly setting ServerSPN in the connection string bypasses auto-generation
        /// and uses the provided SPN for Kerberos authentication. This is critical for environments where:
        /// - Kerberos SPNs are registered with specific hostnames/ports
        /// - SQL Server is behind a proxy or alias
        /// - Multi-instance environments with non-standard naming
        ///
        /// Environment: Requires ability to specify a valid SPN for the target instance.
        /// </summary>
        [PlatformSpecific(TestPlatforms.Linux)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest), nameof(DataTestUtility.HasExplicitPortInTCPConnString))]
        public void KerberosTest_CustomServerSPN_BypassesAutoGeneration()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                out string hostname, out int port, out string instanceName);

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // Build the TCP-format SPN that matches what the driver would auto-generate.
            // TCP Kerberos connections use MSSQLSvc/fqdn:port regardless of instance name.
            string fqdn = DataTestUtility.GetMachineFQDN(hostname);
            string customSpn = $"MSSQLSvc/{fqdn}:{port}";

            SqlConnectionStringBuilder builder = new(tcpConnStr);
            builder.IntegratedSecurity = true;
            builder.ServerSPN = customSpn;

            using SqlConnection conn = new(builder.ConnectionString);
            conn.Open();

            using SqlCommand command = new("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read(), "Expected to receive one row data");
            Assert.Equal("KERBEROS", reader.GetString(0));
        }

        /// <summary>
        /// Tests Kerberos authentication with Protocol.Admin (Dedicated Administrator Connection).
        ///
        /// Verifies that DAC connections (prefix "admin:") to named instances use the TCP-like SPN format:
        /// MSSQLSvc/hostname:port (not instance name). DAC uses SSRP resolution similar to TCP.
        ///
        /// Environment: Requires DAC to be enabled on the target SQL Server instance and admin credentials.
        /// Note: May be skipped if DAC is not enabled or not accessible via Kerberos.
        /// </summary>
        [PlatformSpecific(TestPlatforms.Linux)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest), nameof(DataTestUtility.IsNamedInstanceSetup))]
        public void KerberosTest_ProtocolAdmin_DedicatedAdminConnection()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                out string hostname, out int port, out string instanceName);

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // DAC can be unavailable in shared test environments. Skip this specific
            // integration test when the target instance has remote admin connections disabled.
            SqlConnectionStringBuilder preflightBuilder = new(tcpConnStr)
            {
                DataSource = $"{hostname}\\{instanceName}",
                IntegratedSecurity = true
            };

            using (SqlConnection preflightConn = new(preflightBuilder.ConnectionString))
            {
                preflightConn.Open();
                using SqlCommand preflightCommand = new("SELECT CAST(value_in_use AS bit) FROM sys.configurations WHERE name = 'remote admin connections'", preflightConn);
                object preflightResult = preflightCommand.ExecuteScalar();
                if (preflightResult is not bool remoteAdminEnabled || !remoteAdminEnabled)
                {
                    return; // Skip test; DAC is not enabled in this environment
                }
            }

            // Build the admin: data source without appending the regular TCP port.
            // The DAC port is separate from the regular SQL Server port and must be
            // discovered via SSRP (GetDacPortByInstanceName). Appending the regular
            // TCP port would bypass DAC port resolution and connect to the wrong endpoint.
            string newDataSource = $"admin:{hostname}\\{instanceName}";

            SqlConnectionStringBuilder adminBuilder = new(tcpConnStr)
            {
                DataSource = newDataSource,
                IntegratedSecurity = true
            };

            // Note: this test requires DAC to be enabled on the target instance
            // (sp_configure 'remote admin connections', 1).
            using SqlConnection conn = new(adminBuilder.ConnectionString);
            conn.Open();

            using SqlCommand command = new("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read(), "Expected to receive one row data");
            Assert.Equal("KERBEROS", reader.GetString(0));
        }
    }

    /// <summary>
    /// Provides connection strings from DataTestUtility for theory-based Kerberos tests.
    ///
    /// Each connection string is tested in a separate Kerberos context to ensure protocol-specific
    /// SPN behavior works across all available SQL Server configurations in the test environment.
    /// </summary>
    public class ConnectionStringsProvider : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            foreach (var cnnString in DataTestUtility.ConnectionStrings)
            {
                yield return new object[] { cnnString };
            }
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
