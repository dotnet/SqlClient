// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.LargeDataReadRunner;

/// <summary>
/// Measures large-value consumption without timing connections, commands, or payload generation.
/// Reproduces issues #593 and #1562.
/// </summary>
public abstract class LargeDataReadRunnerBase : CommandRunnerBase
{
    protected Tests.Common.Fixtures.DatabaseObjects.Table _table;
    protected SqlCommand ReadCommand { get; private set; }
    private long _bytesRead;
    private long _rowsRead;
    private bool _hasResults;

    public abstract IEnumerable<CommandBehavior> ExecutedCommandBehaviors { get; }
    protected abstract CommandRunnerJob Configuration { get; }
    protected override CommandRunnerJob Settings => Configuration;

    /// <summary>The column representation; text sizes are UTF-16 byte counts.</summary>
    public enum DataKind { Binary, Text }

    public virtual IEnumerable<DataKind> ExecutedKinds => [DataKind.Binary];

    [ParamsSource(nameof(ExecutedKinds))]
    public DataKind Kind { get; set; }

    public IEnumerable<int> ExecutedSizes => Configuration.PayloadSizesBytes;

    /// <summary>
    /// Size of the data to read in bytes.
    /// </summary>
    [ParamsSource(nameof(ExecutedSizes))]
    public int DataSizeBytes { get; set; }

    /// <summary>
    /// CommandBehavior to use when executing the reader.
    /// SequentialAccess is expected to be faster for large payloads. Default is included
    /// to facilitate comparison with Always Encrypted (which doesn't support SequentialAccess.)
    /// </summary>
    [ParamsSource(nameof(ExecutedCommandBehaviors))]
    public CommandBehavior CommandBehavior { get; set; }

    protected abstract Tests.Common.Fixtures.DatabaseObjects.Table CreateTable();

    protected virtual void OnCleanup() { }

    /// <summary>Persists a correctly sized payload and prepares the reusable read resources.</summary>
    protected override void SetupCommands()
    {
        _hasResults = false;
        _table = CreateTable();
        PopulatePayload();
        ReadCommand = CreateCommand($"SELECT Data FROM {_table.Name}");
        SetupBuffers();
        ValidateFixture();
    }

    /// <summary>Generates encrypted payloads client-side; plaintext overrides with server-side generation.</summary>
    protected virtual void PopulatePayload()
    {
        SqlCommand insertCmd = CreateCommand($"INSERT INTO {_table.Name} (Data) VALUES (@data)");
        insertCmd.Parameters.Add("@data", SqlDbType.VarBinary, -1).Value = new byte[DataSizeBytes];
        insertCmd.ExecuteNonQuery();
        insertCmd.Parameters.Clear();
    }

    protected virtual void SetupBuffers() { }

    /// <summary>Checks decrypted size for encrypted fixtures, outside timing.</summary>
    protected virtual void ValidateFixture()
    {
        ReadLargeDataSync_GetFieldValue();
        ValidateConsumption();
    }

    /// <summary>Checks measured consumption and releases the table before its encryption keys.</summary>
    protected override void CleanupFixtures()
    {
        try
        {
            if (_hasResults)
            {
                ValidateConsumption();
            }
        }
        finally
        {
            try
            {
                _table?.Dispose();
                _table = null;
            }
            finally
            {
                OnCleanup();
                ReadCommand = null;
            }
        }
    }

    /// <summary>Records lengths for untimed cleanup validation, not per-chunk assertions.</summary>
    /// <param name="rows">Observed row count.</param>
    /// <param name="bytes">Observed payload length in bytes.</param>
    /// <returns>The observed byte count.</returns>
    protected long RecordConsumption(long rows, long bytes)
    {
        _rowsRead = rows;
        _hasResults = true;
        return _bytesRead = bytes;
    }

    /// <summary>Detects truncated reads and unintended fixture sizes outside the benchmark body.</summary>
    private void ValidateConsumption()
    {
        if (_rowsRead != 1 || _bytesRead != DataSizeBytes)
        {
            throw new InvalidOperationException($"Expected one {DataSizeBytes}-byte value, read {_rowsRead} rows and {_bytesRead} bytes.");
        }
    }

    /// <summary>Materializes the full binary or text value, intentionally measuring its allocation.</summary>
    [Benchmark]
    public long ReadLargeDataSync_GetFieldValue()
    {
        using SqlDataReader reader = ReadCommand.ExecuteReader(CommandBehavior);
        long rows = 0;
        long bytes = 0;
        while (reader.Read())
        {
            bytes += Kind == DataKind.Binary
                ? reader.GetFieldValue<SqlBinary>(0).Length
                : (long)reader.GetFieldValue<string>(0).Length * sizeof(char);
            rows++;
        }
        return RecordConsumption(rows, bytes);
    }

    /// <summary>Materializes the full value through the actual async field-access API.</summary>
    [Benchmark]
    public async Task<long> ReadLargeDataAsync_GetFieldValue()
    {
        await using SqlDataReader reader = await ReadCommand.ExecuteReaderAsync(CommandBehavior);
        long rows = 0;
        long bytes = 0;
        while (await reader.ReadAsync())
        {
            bytes += Kind == DataKind.Binary
                ? (await reader.GetFieldValueAsync<SqlBinary>(0)).Length
                : (long)(await reader.GetFieldValueAsync<string>(0)).Length * sizeof(char);
            rows++;
        }
        return RecordConsumption(rows, bytes);
    }
}
