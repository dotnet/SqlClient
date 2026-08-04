// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.LargeDataReadRunner;

/// <summary>
/// Benchmarks for sync vs async reading of large VARBINARY(MAX) values.
/// Reproduces issues #593 and #1562.
/// </summary>
public abstract class LargeDataReadRunnerBase : BaseRunner
{
    protected SqlConnection _connection;
    protected Tests.Common.Fixtures.DatabaseObjects.Table _table;

    public abstract IEnumerable<CommandBehavior> ExecutedCommandBehaviors { get; }

    /// <summary>
    /// Size of the data to read in bytes.
    /// </summary>
    [Params(1_048_576, 5_242_880, 10_485_760, 20_971_520)]
    public int DataSizeBytes { get; set; }

    /// <summary>
    /// Size of the client-side read buffer used to drain the VARBINARY(MAX) column.
    /// Kept small (8 KB) and large (1 MB) to observe whether buffer size relative to
    /// the payload materially changes throughput.
    /// </summary>
    [Params(8_192, 1_048_576)]
    public int ReadBufferBytes { get; set; }

    /// <summary>
    /// CommandBehavior to use when executing the reader.
    /// SequentialAccess is expected to be faster for large payloads. Default is included
    /// to facilitate comparison with Always Encrypted (which doesn't support SequentialAccess.)
    /// </summary>
    [ParamsSource(nameof(ExecutedCommandBehaviors))]
    public CommandBehavior CommandBehavior { get; set; }

    protected abstract SqlConnection OpenConnection();

    protected abstract Tests.Common.Fixtures.DatabaseObjects.Table CreateTable();

    protected virtual void OnCleanup() { }

    [GlobalSetup]
    public void Setup()
    {
        _connection = OpenConnection();

        _table = CreateTable();

        // Cannot generate the payload server-side (and avoid a multi-megabyte byte[] allocation
        // on the client) because Always Encrypted values must be generated client-side.
        using SqlCommand insertCmd = new($"INSERT INTO {_table.Name} (Data) VALUES (@data)", _connection);
        insertCmd.Parameters.Add("@data", SqlDbType.VarBinary, -1).Value = new byte[DataSizeBytes];
        insertCmd.ExecuteNonQuery();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        using (_connection)
        {
            try
            {
                _table.Dispose();
            }
            finally
            {
                OnCleanup();
            }
        }

        SqlConnection.ClearAllPools();
    }

    [Benchmark]
    public void ReadLargeDataSync_GetFieldValue()
    {
        using SqlCommand cmd = new($"SELECT Data FROM {_table.Name}", _connection);
        using SqlDataReader reader = cmd.ExecuteReader(CommandBehavior);

        while (reader.Read())
        {
            _ = reader.GetFieldValue<SqlBinary>(0);
        }
    }

    [Benchmark]
    public async Task ReadLargeDataAsync_GetFieldValue()
    {
        await using SqlCommand cmd = new($"SELECT Data FROM {_table.Name}", _connection);
        await using SqlDataReader reader = await cmd.ExecuteReaderAsync(CommandBehavior);

        while (await reader.ReadAsync())
        {
            _ = await reader.GetFieldValueAsync<SqlBinary>(0);
        }
    }
}
