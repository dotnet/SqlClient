using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class KerberosTests
    {
        readonly SqlConnectionStringBuilder _builder = new()
        {
            DataSource = "ADO-WS2019-KERBEROS-TEST.galaxy.ad",
            IntegratedSecurity = true
        };

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public void FailsToConnectWithNoTicketIssued(string cnn)
        {
            using var conn = new SqlConnection(cnn);
            Assert.Throws<SqlException>(() => conn.Open());
        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        [ClassData(typeof(ConnectionStringsProvider))]
        [ClassData(typeof(DomainProvider))]
        public void IsKerBerosSetupTest(string connection, string domain)
        {
            Task t = Task.Run(() => KerberosTicketManagemnt.Init(domain)).ContinueWith((i) =>
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

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        public void ExpiredTicketTest()
        {
            Task t = Task.Run(() => KerberosTicketManagemnt.Destroy()).ContinueWith((i) =>
            {
                using var conn = new SqlConnection(_builder.ConnectionString);
                Assert.Throws<SqlException>(() => conn.Open());
            });
        }

        public class ConnectionStringsProvider : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var cnnString in DataTestUtility.ConnectionStrings)
                {
                    yield return new object[] { cnnString, false };
                    yield return new object[] { cnnString, true };
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class DomainProvider : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                foreach (var provider in DataTestUtility.DomainProviderNames)
                {
                    yield return new object[] { provider };
                }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}

