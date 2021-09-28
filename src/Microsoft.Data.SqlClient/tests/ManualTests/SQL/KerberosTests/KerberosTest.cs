// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class KerberosTests
    {
        [PlatformSpecific(TestPlatforms.AnyUnix)]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsKerberosTest))]
        [ClassData(typeof(ConnectionStringsProvider))]
        public void IsKerBerosSetupTest(string connection)
        {
            try
            {
                Task t = Task.Run(() => KerberosTicketManagemnt.Init(DataTestUtility.KerberosDomainUser)).ContinueWith((i) =>
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
            catch (Exception ex)
            {
                Assert.True(false, ex.Message);
            }
        }

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
}
