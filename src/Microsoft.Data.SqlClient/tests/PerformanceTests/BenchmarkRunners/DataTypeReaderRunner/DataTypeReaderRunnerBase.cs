// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace Microsoft.Data.SqlClient.PerformanceTests.BenchmarkRunners.DataTypeReaderRunner;

/// <summary>Compares row draining and value access without timing connection or fixture setup.</summary>
public abstract class DataTypeReaderRunnerBase : CommandRunnerBase
{
    private static readonly Dictionary<Type, Action<SqlDataReader, int>> Getters = new()
    {
        {typeof(bool), (r, i) => _ = r.GetBoolean(i)},
        {typeof(byte), (r, i) => _ = r.GetByte(i)},
        {typeof(short), (r, i) => _ = r.GetInt16(i)},
        {typeof(int), (r, i) => _ = r.GetInt32(i)},
        {typeof(long), (r, i) => _ = r.GetInt64(i)},
        {typeof(float), (r, i) => _ = r.GetFloat(i)},
        {typeof(double), (r, i) => _ = r.GetDouble(i)},
        {typeof(decimal), (r, i) => _ = r.GetDecimal(i)},
        {typeof(DateTime), (r, i) => _ = r.GetDateTime(i)},
        {typeof(DateTimeOffset), (r, i) => _ = r.GetDateTimeOffset(i)},
        {typeof(TimeSpan), (r, i) => _ = r.GetTimeSpan(i)},
        {typeof(Guid), (r, i) => _ = r.GetGuid(i)},
        {typeof(string), (r, i) => _ = r.GetString(i)},
        {typeof(byte[]), (r, i) => _ = r.GetFieldValue<byte[]>(i)},
    };

    protected Table _table;
    private SqlCommand _command;
    private Action<SqlDataReader, int>[] _getters;
    private object[] _values;
    private long _rowsRead;
    private long? _valuesRead;
    private long _expectedValues;

    public abstract IEnumerable<DataType> ExecutedTypes { get; }

    public virtual IEnumerable<ReaderCase> ExecutedCases => ExecutedTypes.Select(type => new ReaderCase(type));

    [ParamsSource(nameof(ExecutedCases))]
    public ReaderCase Case { get; set; }

    public IEnumerable<long> ExecutedRowCounts => [Configuration.RowCount];

    [ParamsSource(nameof(ExecutedRowCounts))]
    public long RowCount { get; set; }

    protected DataType Type => Case.Type;

    public virtual IEnumerable<CommandBehavior> ExecutedCommandBehaviors => [CommandBehavior.Default];

    [ParamsSource(nameof(ExecutedCommandBehaviors))]
    public CommandBehavior CommandBehavior { get; set; }

    protected IEnumerable<DataType> AvailableTypes =>
        s_datatypes.Others
            .Concat(s_datatypes.Numerics)
            .Concat(s_datatypes.Decimals)
            .Concat(s_datatypes.DateTimes)
            .Concat(s_datatypes.Characters)
            .Concat(s_datatypes.Binary)
            .Concat(s_datatypes.MaxTypes);

    protected abstract CommandRunnerJob Configuration { get; }
    protected override CommandRunnerJob Settings => Configuration;

    protected abstract Table CreateTable();

    protected virtual void OnCleanup() { }

    /// <summary>Populates the selected shape and resolves accessors from actual CLR metadata.</summary>
    protected override void SetupCommands()
    {
        _setupReady = false;
        long rowCount = RowCount;
        if (rowCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Configuration.RowCount), "Reader workloads require at least one row.");
        }
        string tableName;
        if (Type == null)
        {
            tableName = MixedRowFixture.Create(CreateCommand, rowCount, Case.ColumnGroups);
        }
        else
        {
            _table = CreateTable();
            _table.CreateTable(Connection, Settings.CommandTimeoutSeconds);
            _table.InsertBulkRows(rowCount, Connection, Settings.CommandTimeoutSeconds);
            tableName = _table.Name;
        }
        _command = CreateCommand($"SELECT * FROM {tableName}");
        using (SqlDataReader reader = _command.ExecuteReader(CommandBehavior.SchemaOnly))
        {
            _values = new object[reader.FieldCount];
            _getters = Enumerable.Range(0, reader.FieldCount)
                .Select(i => CreateGetter(reader.GetFieldType(i))).ToArray();
        }
        ReadTyped();
        ReadTypedAsync().GetAwaiter().GetResult();
        ValidateConsumption();
    }

    /// <summary>Checks observed consumption outside timing and releases fixtures even on failure.</summary>
    protected override void CleanupFixtures()
    {
        try
        {
            if (_command != null && _setupReady)
            {
                ValidateConsumption();
            }
        }
        finally
        {
            try
            {
                if (_table != null)
                {
                    _table.DropTable(Connection, Settings.CommandTimeoutSeconds);
                    _table = null;
                }
            }
            finally
            {
                OnCleanup();
                _command = null;
            }
        }
    }

    private bool _setupReady;

    /// <summary>Checks rows and accessor counts after the workload has finished.</summary>
    private void ValidateConsumption()
    {
        if (_rowsRead != RowCount || (_valuesRead.HasValue && _valuesRead.Value != _expectedValues))
        {
            throw new InvalidOperationException("Reader did not consume the expected rows and values.");
        }
        _setupReady = true;
    }

    /// <summary>Drains rows asynchronously as a baseline without materializing all fields.</summary>
    [Benchmark]
    public async Task<long> ReadAsync()
    {
        await using SqlDataReader reader = await _command.ExecuteReaderAsync(CommandBehavior);
        long rows = 0;
        while (await reader.ReadAsync())
        {
            _ = await reader.IsDBNullAsync(0);
            rows++;
        }
        _valuesRead = null;
        return _rowsRead = rows;
    }

    /// <summary>Drains rows synchronously as a baseline without materializing all fields.</summary>
    [Benchmark]
    public long Read()
    {
        using SqlDataReader reader = _command.ExecuteReader(CommandBehavior);
        long rows = 0;
        while (reader.Read())
        {
            _ = reader.IsDBNull(0);
            rows++;
        }
        _valuesRead = null;
        return _rowsRead = rows;
    }

    /// <summary>Materializes every non-NULL field with the matching unboxed typed accessor.</summary>
    [Benchmark]
    public long ReadTyped()
    {
        using SqlDataReader reader = _command.ExecuteReader(CommandBehavior);
        long rows = 0;
        long values = 0;
        while (reader.Read())
        {
            values += ReadTypedRow(reader);
            rows++;
        }
        RecordTypedValues(values);
        return _rowsRead = rows;
    }

    /// <summary>Advances asynchronously, then uses typed getters on the current row in ordinal order.</summary>
    [Benchmark]
    public async Task<long> ReadTypedAsync()
    {
        await using SqlDataReader reader = await _command.ExecuteReaderAsync(CommandBehavior);
        long rows = 0;
        long values = 0;
        while (await reader.ReadAsync())
        {
            values += ReadTypedRow(reader);
            rows++;
        }
        RecordTypedValues(values);
        return _rowsRead = rows;
    }

    /// <summary>Reads all fields into a reusable array, measuring boxing but not array construction.</summary>
    [Benchmark]
    public long ReadValues()
    {
        using SqlDataReader reader = _command.ExecuteReader(CommandBehavior);
        long rows = 0;
        long values = 0;
        while (reader.Read())
        {
            values += reader.GetValues(_values);
            rows++;
        }
        _valuesRead = values;
        _expectedValues = RowCount * _values.Length;
        return _rowsRead = rows;
    }

    /// <summary>Advances asynchronously and materializes each row through GetValues.</summary>
    [Benchmark]
    public async Task<long> ReadValuesAsync()
    {
        await using SqlDataReader reader = await _command.ExecuteReaderAsync(CommandBehavior);
        long rows = 0;
        long values = 0;
        while (await reader.ReadAsync())
        {
            values += reader.GetValues(_values);
            rows++;
        }
        _valuesRead = values;
        _expectedValues = RowCount * _values.Length;
        return _rowsRead = rows;
    }

    /// <summary>Reads fields in ascending order to respect SequentialAccess.</summary>
    /// <param name="reader">Reader positioned on a row.</param>
    /// <returns>The number of non-NULL values accessed.</returns>
    private int ReadTypedRow(SqlDataReader reader)
    {
        int values = 0;
        for (int i = 0; i < _getters.Length; i++)
        {
            if (!reader.IsDBNull(i))
            {
                _getters[i](reader, i);
                values++;
            }
        }
        return values;
    }

    /// <summary>Records NULL-aware expected counts for untimed validation.</summary>
    /// <param name="values">Observed non-NULL field count.</param>
    private void RecordTypedValues(long values)
    {
        _valuesRead = values;
        long nonNullRows = Type == null ? RowCount - RowCount / 4 : RowCount;
        _expectedValues = nonNullRows * _getters.Length;
    }

    /// <summary>Resolves a typed accessor once so metadata lookup and boxing are not timed.</summary>
    /// <param name="type">The actual CLR field type, including SQL float precision effects.</param>
    /// <returns>An accessor that consumes its field.</returns>
    private static Action<SqlDataReader, int> CreateGetter(System.Type type)
    {
        return Getters.TryGetValue(type, out var getter )
            ? getter
            : throw new NotSupportedException();
    }
}
