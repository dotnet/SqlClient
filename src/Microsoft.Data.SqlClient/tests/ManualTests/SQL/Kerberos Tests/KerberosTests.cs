using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class KerberosTests
    {
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        [InlineData("Data Source=ADO-WS2019-KERBEROS-TEST; Integrated Security=true;")]
        public void IsKerBerosSetupTest(string connection)
        {
            Task t = Task.Run(() => KerberosTicketManagemnt.Init(DataTestUtility.DomainProviderName)).ContinueWith((i) =>
            {
                using var conn = new SqlConnection(connection);
                try
                {
                    conn.Open();
                    using var command = new SqlCommand("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Assert.Equal("KERBEROS", reader.GetString(0));
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.Message);
                    Assert.False(true);
                }
            });
        }

        [PlatformSpecific(TestPlatforms.AnyUnix)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        [InlineData("Data Source=ADO-WS2019-KERBEROS-TEST; Integrated Security=true;")]
        public void ExpiredTicketTest(string connection)
        {
            Task t = Task.Run(() => KerberosTicketManagemnt.Destroy()).ContinueWith((i) =>
            {
                using var conn = new SqlConnection(connection);
                Assert.Throws<SqlException>(() => conn.Open());
            });

            //makes sure that kerberos is initiated for other tests to pass
            KerberosTicketManagemnt.Init(DataTestUtility.DomainProviderName);
            Task t1 = Task.Run(() => KerberosTicketManagemnt.Init(DataTestUtility.DomainProviderName)).ContinueWith((i) =>
            {
                using var conn = new SqlConnection(connection);
                try
                {
                    conn.Open();
                    using var command = new SqlCommand("SELECT auth_scheme from sys.dm_exec_connections where session_id = @@spid", conn);
                    using SqlDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        Assert.Equal("KERBEROS", reader.GetString(0));
                    }
                }
                catch (SqlException ex)
                {
                    Console.WriteLine(ex.Message);
                    Assert.False(true);
                }
            });
        }
    }
}

