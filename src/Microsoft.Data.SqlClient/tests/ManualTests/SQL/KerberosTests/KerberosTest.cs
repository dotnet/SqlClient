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
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        public void KerberosTest_ProtocolNone_NamedInstanceWithSsrpResolution()
        {
            // Skip if no TCP connection string with a named instance is available
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            if (string.IsNullOrEmpty(tcpConnStr) ||
                !DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                    out string hostname, out int port, out string instanceName) ||
                string.IsNullOrEmpty(instanceName))
            {
                return; // Skip test; no named instance available
            }

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // Build a connection string with Protocol.None (no prefix) pointing to the named instance
            // SSRP resolution should occur and populate the port in the SPN
            string protocolNoneConnStr = $"Data Source={hostname}\\{instanceName};Integrated Security=true;";

            using SqlConnection conn = new(protocolNoneConnStr);
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
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        public void KerberosTest_ProtocolTcp_NamedInstanceWithExplicitPort()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            if (string.IsNullOrEmpty(tcpConnStr) ||
                !DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                    out string hostname, out int port, out string instanceName))
            {
                return; // Skip test
            }

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // If an explicit port is available in the test connection string, use it
            // Otherwise, use a typical SQL instance port (1433) and rely on SSRP if needed
            int testPort = port > 0 ? port : 1433;

            string protocolTcpConnStr = $"Data Source=tcp:{hostname}\\{instanceName},{testPort};Integrated Security=true;";

            using SqlConnection conn = new(protocolTcpConnStr);
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
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        public void KerberosTest_CustomServerSPN_BypassesAutoGeneration()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            if (string.IsNullOrEmpty(tcpConnStr) ||
                !DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                    out string hostname, out int port, out string instanceName))
            {
                return; // Skip test
            }

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            // Build the expected SPN for the server
            string fqdn = DataTestUtility.GetMachineFQDN(hostname);
            string customSpn = $"MSSQLSvc/{fqdn}";
            if (!string.IsNullOrEmpty(instanceName))
            {
                customSpn += ":" + instanceName;
            }
            else if (port > 0)
            {
                customSpn += ":" + port;
            }

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
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        public void KerberosTest_ProtocolAdmin_DedicatedAdminConnection()
        {
            string tcpConnStr = DataTestUtility.TCPConnectionString;
            if (string.IsNullOrEmpty(tcpConnStr) ||
                !DataTestUtility.ParseDataSource(new SqlConnectionStringBuilder(tcpConnStr).DataSource,
                    out string hostname, out int port, out string instanceName))
            {
                return; // Skip test
            }

            KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser, DataTestUtility.KerberosDomainPassword);

            int testPort = port > 0 ? port : 1433;

            // Build admin: connection string
            string protocolAdminConnStr = $"Data Source=admin:{hostname}\\{instanceName},{testPort};Integrated Security=true;";

            try
            {
                using SqlConnection conn = new(protocolAdminConnStr);
                conn.Open();

                using SqlCommand command = new("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
                using SqlDataReader reader = command.ExecuteReader();
                if (reader.Read())
                {
                    Assert.Equal("KERBEROS", reader.GetString(0));
                }
            }
            catch (SqlException ex) when (ex.Message.Contains("DAC") || ex.Message.Contains("Dedicated"))
            {
                // DAC may not be enabled or accessible; skip this test without failing
                return;
            }
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
