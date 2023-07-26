// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class ConnectionBehaviorTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ConnectionBehaviorClose()
        {
            using (SqlConnection sqlConnection = new SqlConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MaxPoolSize = 1 }).ConnectionString))
            {
                using (SqlCommand command = new SqlCommand("SELECT '1'", sqlConnection))
                {
                    sqlConnection.Open();
                    using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection))
                    {
                        while (reader.Read())
                        {
                            string result = reader[0].ToString();
                        }
                    }

                    Assert.Equal(ConnectionState.Closed, sqlConnection.State);
                }
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ConnectionBehaviorCloseAsync()
        {
            using (SqlConnection sqlConnection = new SqlConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MaxPoolSize = 1 }).ConnectionString))
            {
                Task<bool> result = VerifyConnectionBehaviorCloseAsync(sqlConnection);
                bool value = result.Result;
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void CheckIsConnectionAlivewithOpen()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                Assert.True(!sqlConnection.IsConnectionAlive());
                sqlConnection.Open();
                Assert.True(sqlConnection.IsConnectionAlive());
                sqlConnection.Close();
                Assert.True(!sqlConnection.IsConnectionAlive());
            }
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.NPConnectionString))
            {
                Assert.True(!sqlConnection.IsConnectionAlive());
                sqlConnection.Open();
                Assert.True(sqlConnection.IsConnectionAlive());
                sqlConnection.Close();
                Assert.True(!sqlConnection.IsConnectionAlive());
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task CheckIsConnectionAlivewithOpenAsync()
        {
            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                Assert.True(!sqlConnection.IsConnectionAlive());
                await sqlConnection.OpenAsync();
                Assert.True(sqlConnection.IsConnectionAlive());
                sqlConnection.Close();
                Assert.True(!sqlConnection.IsConnectionAlive());
            }

            using (SqlConnection sqlConnection = new SqlConnection(DataTestUtility.NPConnectionString))
            {
                Assert.True(!sqlConnection.IsConnectionAlive());
                await sqlConnection.OpenAsync();
                Assert.True(sqlConnection.IsConnectionAlive());
                sqlConnection.Close();
                Assert.True(!sqlConnection.IsConnectionAlive());
            }
        }

        private async Task<bool> VerifyConnectionBehaviorCloseAsync(SqlConnection sqlConnection)
        {
            using (SqlCommand command = new SqlCommand("SELECT '1'", sqlConnection))
            {
                await sqlConnection.OpenAsync();
                using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection))
                {
                    while (reader.Read())
                    {
                        string result = reader[0].ToString();
                    }
                }

                Assert.Equal(ConnectionState.Closed, sqlConnection.State);
            }

            return true;
        }
    }
}
