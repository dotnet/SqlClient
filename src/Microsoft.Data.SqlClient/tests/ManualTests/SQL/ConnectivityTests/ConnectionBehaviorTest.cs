// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
        /// Opens two physical connections without explicit MSF or mirroring partner keywords.
        /// Covers cached server-sent partners on configured TCP and Named Pipes targets.
        /// </summary>
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        [InlineData(false)]
        [InlineData(true)]
        public async Task MultiSubnetFailover_Default_OpenSucceeds(bool useAsync)
        {
            foreach (string connectionString in DataTestUtility.GetConnectionStrings(withEnclave: false))
            {
                SqlConnectionStringBuilder builder = new(connectionString);
                builder.Remove("MultiSubnetFailover");
                builder.Remove("Failover Partner");
                builder.Pooling = true;
                builder.ApplicationName = Guid.NewGuid().ToString();
                Assert.True(builder.MultiSubnetFailover);

                using SqlConnection first = new(builder.ConnectionString);
                using SqlConnection second = new(builder.ConnectionString);
                try
                {
                    if (useAsync)
                    {
                        await first.OpenAsync();
                        await second.OpenAsync();
                    }
                    else
                    {
                        first.Open();
                        second.Open();
                    }

                    Assert.Equal(ConnectionState.Open, first.State);
                    Assert.Equal(ConnectionState.Open, second.State);
                    Assert.NotEqual(first.ClientConnectionId, second.ClientConnectionId);
                }
                finally
                {
                    SqlConnection.ClearPool(first);
                }
            }
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
