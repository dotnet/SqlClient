using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class KerberosTests
    {
        public enum AuthenticationScheme
        {
            KERBEROS
        }

        readonly SqlConnectionStringBuilder _builder = new()
        {
            DataSource = "ADO-WS2019-KERBEROS-TEST.galaxy.ad",
            IntegratedSecurity = true
        };

        [Fact]
        public void FailsToConnectWithNoTicketIssued()
        {
            using var conn = new SqlConnection(_builder.ConnectionString);
            Assert.Throws<SqlException>(() => conn.Open());
        }

        [Fact]
        public void IsKerBerosSetupTest()
        {
            Task t = Task.Run(() => KerberosTicketManagemnt.Init()).ContinueWith((i) =>
            {
                using var conn = new SqlConnection(_builder.ConnectionString);
                try
                {
                    conn.Open();
                    using var command = new SqlCommand("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Assert.Equal(AuthenticationScheme.KERBEROS.ToString(), reader.GetString(0));
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.Message);
                    Assert.False(true);
                }

            });
        }

        [Fact]
        public void ExpiredTicketTest()
        {
            Task t = Task.Run(() => KerberosTicketManagemnt.Destroy()).ContinueWith((i) =>
            {
                using var conn = new SqlConnection(_builder.ConnectionString);
                Assert.Throws<SqlException>(() => conn.Open());
            });
        }
    }
}
