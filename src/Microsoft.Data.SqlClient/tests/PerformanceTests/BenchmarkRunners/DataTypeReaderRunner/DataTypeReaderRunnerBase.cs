// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

public abstract class DataTypeReaderRunnerBase : BaseRunner
{
    protected SqlConnection _connection;
    protected Table _table;

    protected IEnumerable<DataType> AvailableTypes =>
        s_datatypes.Others
            .Concat(s_datatypes.Numerics)
            .Concat(s_datatypes.Decimals)
            .Concat(s_datatypes.DateTimes)
            .Concat(s_datatypes.Characters)
            .Concat(s_datatypes.Binary)
            .Concat(s_datatypes.MaxTypes);

    protected abstract RunnerJob Configuration { get; }

    public abstract IEnumerable<DataType> ExecutedTypes { get; }

    [ParamsSource(nameof(ExecutedTypes))]
    public DataType Type { get; set; }

    protected abstract SqlConnection OpenConnection();

    protected abstract Table CreateTable();

    protected virtual void OnCleanup() { }

    [GlobalSetup]
    public void Setup()
    {
        long rowCount = Configuration.RowCount;

        _connection = OpenConnection();

        _table = CreateTable()
            .InsertBulkRows(rowCount, _connection);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        using (_connection)
        {
            _table.DropTable(_connection);

            OnCleanup();
        }

        SqlConnection.ClearAllPools();
    }

    [IterationCleanup]
    public void ResetConnection()
    {
        SqlConnection.ClearAllPools();
    }

    [Benchmark]
    public async Task ReadAsync()
    {
        await using SqlCommand sqlCommand = new($"SELECT * FROM {_table.Name}", _connection);
        await using SqlDataReader reader = await sqlCommand.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        { }
    }

    [Benchmark]
    public void Read()
    {
        using SqlCommand sqlCommand = new($"SELECT * FROM {_table.Name}", _connection);
        using SqlDataReader reader = sqlCommand.ExecuteReader();

        while (reader.Read())
        { }
    }
}
