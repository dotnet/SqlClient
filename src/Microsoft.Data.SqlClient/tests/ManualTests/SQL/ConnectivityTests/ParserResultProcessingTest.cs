// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests;

/// <summary>
/// Verifies that parser prerequisite validation preserves valid login, nullable rows,
/// result skipping, and subsequent commands against SQL Server.
/// </summary>
[Trait("Set", "2")]
public class ParserResultProcessingTest
{
    /// <summary>
    /// Consumes null and non-null values, skips unread rows, and reuses the connection with
    /// synchronous/asynchronous APIs and both buffered and sequential access.
    /// </summary>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ValidResults_PreserveConnection(bool useAsync, bool sequential)
    {
        using SqlConnection connection = DataTestUtility.CreateConnection(
            DataTestUtility.TCPConnectionString);
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

        // Sorting puts NULL first and leaves a deterministic unread row before the second result.
        using SqlCommand command = new(
            "SELECT CAST(NULL AS int) AS value UNION ALL SELECT 1 UNION ALL SELECT 2 " +
            "ORDER BY value; " +
            "SELECT 42 AS value;", connection);
        CommandBehavior behavior = sequential
            ? CommandBehavior.SequentialAccess
            : CommandBehavior.Default;
        using (SqlDataReader reader = useAsync
            ? await command.ExecuteReaderAsync(behavior)
            : command.ExecuteReader(behavior))
        {
            Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.True(reader.IsDBNull(0));
            Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.Equal(1, reader.GetInt32(0));
            // Skip the unread value 2 and verify that the next result uses its own metadata.
            Assert.True(useAsync ? await reader.NextResultAsync() : reader.NextResult());
            Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.Equal(42, reader.GetInt32(0));
            Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
            Assert.False(useAsync ? await reader.NextResultAsync() : reader.NextResult());
        }

        // A subsequent command proves valid result processing did not invalidate the connection.
        command.CommandText = "SELECT 7";
        object result = useAsync
            ? await command.ExecuteScalarAsync()
            : command.ExecuteScalar();
        Assert.Equal(7, result);
        Assert.Equal(ConnectionState.Open, connection.State);
    }
}
