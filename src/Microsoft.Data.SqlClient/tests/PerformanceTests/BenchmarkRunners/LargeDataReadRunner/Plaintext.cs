// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.LargeDataReadRunner;

public class Plaintext : LargeDataReadRunnerBase
{
    public override IEnumerable<CommandBehavior> ExecutedCommandBehaviors =>
        [CommandBehavior.Default, CommandBehavior.SequentialAccess];

    protected override SqlConnection OpenConnection()
    {
        SqlConnection conn = new(s_config.ConnectionString);

        conn.Open();
        return conn;
    }

    protected override Tests.Common.Fixtures.DatabaseObjects.Table CreateTable() =>
        new(_connection, nameof(Plaintext), "(Id INT IDENTITY PRIMARY KEY, Data VARBINARY(MAX))");

    [Benchmark]
    public void ReadLargeDataSync_GetBytes()
    {
        using SqlCommand cmd = new($"SELECT Data FROM {_table.Name}", _connection);
        using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior);
        byte[] buffer = new byte[ReadBufferBytes];

        while (reader.Read())
        {
            long offset = 0;
            long bytesRead;
            do
            {
                bytesRead = reader.GetBytes(0, offset, buffer, 0, buffer.Length);
                offset += bytesRead;
            } while (bytesRead > 0);
        }
    }

    [Benchmark]
    public async Task ReadLargeDataAsync_GetStream()
    {
        await using SqlCommand cmd = new($"SELECT Data FROM {_table.Name}", _connection);
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior);
        byte[] buffer = new byte[ReadBufferBytes];

        while (await reader.ReadAsync())
        {
            await using Stream stream = reader.GetStream(0);
            int bytesRead;

            do
            {
                bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            } while (bytesRead > 0);
        }
    }
}
