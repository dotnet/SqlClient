// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Verifies connection lifecycle behavior against a configured SQL Server.
    /// </summary>
    [Trait("Set", "2")]
    public class ConnectionBehaviorTest
    {
        /// <summary>
        /// Opens TCP connections synchronously and asynchronously without an explicit MSF keyword.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MultiSubnetFailover_Default_OpenSucceeds(bool useAsync)
        {
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);
            builder.Remove("MultiSubnetFailover");
            builder.Pooling = false;
            Assert.True(builder.MultiSubnetFailover);

            using SqlConnection connection = new(builder.ConnectionString);
            if (useAsync)
            {
                await connection.OpenAsync();
            }
            else
            {
                connection.Open();
            }

            Assert.Equal(ConnectionState.Open, connection.State);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ConnectionBehaviorClose()
        {
            using (SqlConnection sqlConnection = DataTestUtility.CreateConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MaxPoolSize = 1 }).ConnectionString))
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
            using (SqlConnection sqlConnection = DataTestUtility.CreateConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MaxPoolSize = 1 }).ConnectionString))
            {
                Task<bool> result = VerifyConnectionBehaviorCloseAsync(sqlConnection);
                bool value = result.Result;
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
