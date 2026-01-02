// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class ConnectionBehaviorTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ConnectionBehaviorClose()
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString) { MaxPoolSize = 1 };
            using SqlConnection sqlConnection = new SqlConnection(builder.ConnectionString);
            sqlConnection.Open();

            using SqlCommand command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT 1";

            // Act
            using (SqlDataReader reader = command.ExecuteReader(CommandBehavior.CloseConnection))
            {
                reader.FlushResultSet();
            }

            // Assert
            Assert.Equal(ConnectionState.Closed, sqlConnection.State);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task ConnectionBehaviorCloseAsync()
        {
            // Arrange
            SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString) { MaxPoolSize = 1 };
            using SqlConnection sqlConnection = new SqlConnection(builder.ConnectionString);
            await sqlConnection.OpenAsync();

            using SqlCommand command = sqlConnection.CreateCommand();
            command.CommandText = "SELECT 1";

            // Act
            using (SqlDataReader reader = await command.ExecuteReaderAsync(CommandBehavior.CloseConnection))
            {
                await reader.FlushResultSetAsync();
            }

            // Assert
            Assert.Equal(ConnectionState.Closed, sqlConnection.State);
        }
    }
}
