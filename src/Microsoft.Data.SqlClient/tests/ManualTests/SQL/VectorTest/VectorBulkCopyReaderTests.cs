// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

/// <summary>
/// Verifies native vector reader transfers and the boundary with unsupported text destinations.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorBulkCopyReaderTests
{
    /// <summary>
    /// Covers supported base types, sync/async, streaming, and both reader interfaces.
    /// </summary>
    /// <returns>The reader execution modes supported by this runtime.</returns>
    public static IEnumerable<object[]> ReaderModes()
    {
        foreach (bool useAsync in new[] { false, true })
        {
            foreach (bool streaming in new[] { false, true })
            {
                foreach (bool sequential in new[] { false, true })
                {
                    yield return new object[] { false, useAsync, streaming, sequential };
                    #if NET
                    yield return new object[] { true, useAsync, streaming, sequential };
                    #endif
                }
            }
        }
    }

    /// <summary>
    /// Exercises both ANSI and Unicode text destination metadata.
    /// </summary>
    /// <returns>The text types and reader execution modes.</returns>
    public static IEnumerable<object[]> TextCases()
    {
        foreach (string destination in new[] { "varchar(max)", "nvarchar(max)" })
        {
            foreach (object[] mode in ReaderModes())
            {
                yield return new[] { mode[0], mode[1], mode[2], mode[3], destination };
            }
        }
    }

    /// <summary>
    /// Covers text projections under V1/Off and the unchanged native path under V2.
    /// </summary>
    /// <returns>The requested feature version and reader execution modes.</returns>
    public static IEnumerable<object[]> Float16Cases()
    {
        foreach (SqlVectorTypeSupport version in new[]
        {
            SqlVectorTypeSupport.V1, SqlVectorTypeSupport.Off, SqlVectorTypeSupport.V2
        })
        {
            foreach (object[] mode in ReaderModes())
            {
                yield return new[] { mode[0], mode[1], mode[2], mode[3], version };
            }
        }
    }

    /// <summary>
    /// Rejects typed vector values for text destinations without implicitly rendering JSON.
    /// </summary>
    /// <param name="useHalf">Whether the source uses the float16 base type.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled for ordinary text columns.</param>
    /// <param name="sequential">Whether to use an ordinal-checking IDataReader.</param>
    /// <param name="destination">The SQL text type which cannot receive native vectors.</param>
    /// <returns>A task that completes after the rejection and empty target are checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    [MemberData(nameof(TextCases))]
    public Task ReaderVectorsToTextAreRejected(
        bool useHalf, bool useAsync, bool streaming, bool sequential, string destination) =>
        VerifyReaderCopy(useHalf, useAsync, streaming, sequential, destination,
            SqlVectorTypeSupport.V2);

    /// <summary>
    /// Rejects native vector values for a JSON destination without changing its metadata.
    /// </summary>
    /// <param name="useHalf">Whether the source uses the float16 base type.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="sequential">Whether to use an ordinal-checking IDataReader.</param>
    /// <returns>A task that completes after the rejection and empty target are checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsJsonSupported))]
    [MemberData(nameof(ReaderModes))]
    public Task ReaderVectorsToJsonAreRejected(
        bool useHalf, bool useAsync, bool streaming, bool sequential) =>
        VerifyReaderCopy(useHalf, useAsync, streaming, sequential, "json",
            SqlVectorTypeSupport.V2);

    /// <summary>
    /// Rejects native input when V1/Off exposes the destination as text, while preserving
    /// nulls and signed zero through native V2 transfers.
    /// </summary>
    /// <param name="useHalf">Whether the source uses the float16 base type.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="sequential">Whether to use an ordinal-checking IDataReader.</param>
    /// <param name="version">The requested vector feature version.</param>
    /// <returns>A task that completes after metadata and stored values have been checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility),
        nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
    [MemberData(nameof(Float16Cases))]
    public Task ReaderVectorsToFloat16(
        bool useHalf, bool useAsync, bool streaming, bool sequential,
        SqlVectorTypeSupport version) =>
        VerifyReaderCopy(useHalf, useAsync, streaming, sequential, "vector(3, float16)", version);

    /// <summary>
    /// Recomputes native source metadata when a cached vector destination receives different
    /// base types and ordinary JSON strings, including leading nulls.
    /// </summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <returns>A task that completes after each source representation has been verified.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedVectorMetadataSupportsDifferentReaderTypes(bool useAsync)
    {
        using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
        connection.Open();
        using Table target = new(connection, "VectorReaderCache",
            "(Id int NOT NULL, VectorData vector(1, float16) NULL)");
        using SqlBulkCopy copy = new(
            DataTestUtility.TCPConnectionString, SqlBulkCopyOptions.CacheMetadata)
        {
            DestinationTableName = target.Name,
            EnableStreaming = true
        };
        List<object> values = new()
        {
            new SqlVector<float>(new[] { 65504f }),
            "[65504]"
        };
        #if NET
        values.Add(new SqlVector<Half>(new[] { (Half)65504f }));
        #endif
        values.Add(new SqlVector<float>(new[] { 65504f }));

        foreach (object value in values)
        {
            using DataTable source = new();
            source.Columns.Add("Id", typeof(int));
            source.Columns.Add("VectorData", value.GetType());
            source.Rows.Add(0, DBNull.Value);
            source.Rows.Add(1, value);
            using (DataTableReader reader = source.CreateDataReader())
            {
                if (useAsync)
                {
                    await copy.WriteToServerAsync(reader);
                }
                else
                {
                    copy.WriteToServer(reader);
                }
            }
            using SqlCommand verify = new(
                $"SELECT VectorData FROM {target.Name} ORDER BY Id", connection);
            using (SqlDataReader result = verify.ExecuteReader())
            {
                Assert.True(result.Read());
                Assert.True(result.IsDBNull(0));
                Assert.True(result.Read());
                Assert.Equal(new[] { 65504f },
                    JsonSerializer.Deserialize<float[]>(result.GetString(0)));
                Assert.False(result.Read());
            }
            target.Truncate();
        }
    }

    /// <summary>
    /// Combines reader interfaces, missing metadata, streaming, and sync/async execution.
    /// </summary>
    /// <returns>The supported reader execution combinations.</returns>
    public static IEnumerable<object[]> MissingMetadataModes()
    {
        foreach (bool useAsync in new[] { false, true })
        foreach (bool streaming in new[] { false, true })
        foreach (bool plainReader in new[] { false, true })
        foreach (bool notSupported in new[] { false, true })
        {
            yield return new object[] { useAsync, streaming, plainReader, notSupported };
        }
    }

    /// <summary>
    /// Covers bounded and streaming text destinations without vector metadata discovery.
    /// </summary>
    /// <returns>Text destinations and reader execution combinations.</returns>
    public static IEnumerable<object[]> MissingTextMetadataCases()
    {
        foreach (string destination in new[] { "nvarchar(50)", "varchar(max)", "nvarchar(max)" })
        foreach (object[] mode in MissingMetadataModes())
        {
            yield return new[] { mode[0], mode[1], mode[2], mode[3], destination };
        }
    }

    /// <summary>
    /// Covers both legacy JSON and native float32 inputs without reader type information.
    /// </summary>
    /// <returns>Input representations and reader execution combinations.</returns>
    public static IEnumerable<object[]> MissingVectorMetadataCases()
    {
        foreach (bool nativeValue in new[] { false, true })
        foreach (object[] mode in MissingMetadataModes())
        {
            yield return new[] { mode[0], mode[1], mode[2], mode[3], nativeValue };
        }
    }

    /// <summary>Ordinary text copies do not acquire new reader metadata requirements.</summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="plainReader">Whether the runtime source implements only IDataReader.</param>
    /// <param name="notSupported">Whether metadata reports an unsupported operation.</param>
    /// <param name="destination">The destination SQL text type.</param>
    /// <returns>A task that completes after text and metadata-call counts are verified.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    [MemberData(nameof(MissingTextMetadataCases))]
    public Task TextReaderDoesNotRequireSchema(
        bool useAsync, bool streaming, bool plainReader, bool notSupported, string destination) =>
        VerifyMissingMetadata(
            useAsync, streaming, plainReader, notSupported, destination, "ordinary");

    /// <summary>Ordinary JSON copies retain their existing reader contract.</summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="plainReader">Whether the runtime source implements only IDataReader.</param>
    /// <param name="notSupported">Whether metadata reports an unsupported operation.</param>
    /// <returns>A task that completes after JSON and metadata-call counts are verified.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsJsonSupported))]
    [MemberData(nameof(MissingMetadataModes))]
    public Task JsonReaderDoesNotRequireSchema(
        bool useAsync, bool streaming, bool plainReader, bool notSupported) =>
        VerifyMissingMetadata(useAsync, streaming, plainReader, notSupported, "json", "[1,2,3]");

    /// <summary>
    /// Missing optional type information preserves existing float32 reader inputs.
    /// </summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="plainReader">Whether the runtime source implements only IDataReader.</param>
    /// <param name="notSupported">Whether metadata reports an unsupported operation.</param>
    /// <param name="nativeValue">Whether the source supplies a vector instead of JSON text.</param>
    /// <returns>A task that completes after the stored vector has been verified.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsSqlVectorSupported))]
    [MemberData(nameof(MissingVectorMetadataCases))]
    public Task VectorReaderSupportsMissingSchema(
        bool useAsync, bool streaming, bool plainReader, bool notSupported, bool nativeValue) =>
        VerifyMissingMetadata(useAsync, streaming, plainReader, notSupported, "vector(3)",
            nativeValue ? (object)new SqlVector<float>(new[] { 1f, 2f, 3f }) : "[1,2,3]");

    /// <summary>
    /// Unexpected metadata-provider errors propagate rather than becoming missing metadata.
    /// </summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="plainReader">Whether the runtime source implements only IDataReader.</param>
    /// <returns>A task that completes after the original exception has been observed.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsSqlVectorSupported))]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReaderMetadataFailureIsNotSwallowed(bool useAsync, bool plainReader)
    {
        using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
        connection.Open();
        using Table target = new(connection, "VectorReaderMetadataError", "(v vector(3) NULL)");
        using DataTable source = new();
        source.Columns.Add("v", typeof(string));
        source.Rows.Add("[1,2,3]");
        InvalidOperationException expected = new("Metadata provider failed.");
        using MetadataReader metadataReader = new(source.CreateDataReader(), expected);
        using IDataReader reader = plainReader
            ? new SequentialReader(metadataReader) : metadataReader;
        using SqlBulkCopy copy = new(connection) { DestinationTableName = target.Name };
        Exception error = useAsync
            ? await Record.ExceptionAsync(() => copy.WriteToServerAsync(reader))
            : Record.Exception(() => copy.WriteToServer(reader));
        Assert.NotNull(error);
        Assert.Same(expected, error.GetBaseException());
    }

    /// <summary>
    /// Exercises bulk copy without schema-table access or metadata-time value reads.
    /// </summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="plainReader">Whether the runtime source implements only IDataReader.</param>
    /// <param name="notSupported">The optional metadata failure to simulate.</param>
    /// <param name="destination">The destination SQL type.</param>
    /// <param name="value">The input value.</param>
    /// <returns>A task that completes after values and metadata-call counts are checked.</returns>
    private static async Task VerifyMissingMetadata(
        bool useAsync, bool streaming, bool plainReader, bool notSupported,
        string destination, object value)
    {
        using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
        connection.Open();
        using Table target = new(
            connection, "VectorReaderMissingMetadata", $"(v {destination} NULL)");
        using DataTable source = new();
        source.Columns.Add("v", value.GetType());
        source.Rows.Add(value);
        Exception metadataError = notSupported
            ? new NotSupportedException("GetFieldType")
            : new NotImplementedException("GetFieldType");
        using MetadataReader metadataReader = new(source.CreateDataReader(), metadataError);
        using IDataReader reader = plainReader
            ? new SequentialReader(metadataReader) : metadataReader;
        using SqlBulkCopy copy = new(connection)
        {
            DestinationTableName = target.Name,
            EnableStreaming = streaming
        };
        if (useAsync)
        {
            await copy.WriteToServerAsync(reader);
        }
        else
        {
            copy.WriteToServer(reader);
        }
        bool vectorDestination = destination.StartsWith("vector", StringComparison.Ordinal);
        if (vectorDestination)
        {
            Assert.True(metadataReader.FieldTypeCalls > 0);
        }
        else
        {
            Assert.Equal(0, metadataReader.FieldTypeCalls);
        }
        Assert.Equal(0, metadataReader.SchemaTableCalls);
        using SqlCommand verify = new(
            $"SELECT CAST(v AS nvarchar(max)) FROM {target.Name}", connection);
        using SqlDataReader result = verify.ExecuteReader();
        Assert.True(result.Read());
        if (vectorDestination || destination == "json")
        {
            Assert.Equal(new[] { 1f, 2f, 3f },
                JsonSerializer.Deserialize<float[]>(result.GetString(0)));
        }
        else
        {
            Assert.Equal(value, result.GetString(0));
        }
        Assert.False(result.Read());
    }

    /// <summary>
    /// Selects the source's native CLR type while keeping expected mathematical values explicit.
    /// </summary>
    /// <param name="useHalf">Whether to create Half source values.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="sequential">Whether to use the checked IDataReader wrapper.</param>
    /// <param name="destination">The destination SQL type.</param>
    /// <param name="version">The destination connection's vector setting.</param>
    /// <returns>A task that completes after native copying or text rejection is verified.</returns>
    private static Task VerifyReaderCopy(
        bool useHalf, bool useAsync, bool streaming, bool sequential, string destination,
        SqlVectorTypeSupport version)
    {
        float[] values = { -0.0f, 65504f, 0.333251953125f };
        #if NET
        if (useHalf)
        {
            return VerifyReaderCopy(
                Array.ConvertAll(values, value => (Half)value), values,
                useAsync, streaming, sequential, destination, version);
        }
        #endif
        return VerifyReaderCopy(values, values, useAsync, streaming, sequential, destination, version);
    }

    /// <summary>
    /// Copies native vectors and nulls without reading values out of order, or rejects
    /// non-null vectors when the destination is exposed as text.
    /// </summary>
    /// <typeparam name="T">The source vector element type.</typeparam>
    /// <param name="values">The native vector values.</param>
    /// <param name="expected">The same values widened exactly to float32.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="sequential">Whether to use the checked IDataReader wrapper.</param>
    /// <param name="destination">The destination SQL type.</param>
    /// <param name="version">The destination connection's vector setting.</param>
    /// <returns>A task that completes after every row and null has been verified.</returns>
    private static async Task VerifyReaderCopy<T>(
        T[] values, float[] expected, bool useAsync, bool streaming, bool sequential,
        string destination, SqlVectorTypeSupport version) where T : unmanaged
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        connection.Open();
        using Table target = new(connection, "VectorReaderText",
            $"(Id int NOT NULL, VectorData {destination} NULL, Tail nvarchar(max) NULL)");
        using DataTable source = new();
        source.Columns.Add("Id", typeof(int));
        source.Columns.Add("VectorData", typeof(SqlVector<T>));
        source.Columns.Add("Tail", typeof(string));
        bool nativeDestination = destination.StartsWith("vector", StringComparison.Ordinal)
            && version == SqlVectorTypeSupport.V2;
        if (nativeDestination)
        {
            source.Rows.Add(0, DBNull.Value, "tail0");
            source.Rows.Add(1, new SqlVector<T>(values), "tail1");
            source.Rows.Add(2, SqlVector<T>.CreateNull(values.Length), "tail2");
            source.Rows.Add(3, new SqlVector<T>(values), "tail3");
        }
        else
        {
            source.Rows.Add(0, new SqlVector<T>(values), "tail0");
        }

        string copyConnectionString = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
        {
            VectorTypeSupport = version
        }.ConnectionString;
        if (destination.StartsWith("vector", StringComparison.Ordinal))
        {
            using SqlConnection metadataConnection = new(copyConnectionString);
            metadataConnection.Open();
            using SqlCommand metadataCommand = new($"SELECT VectorData FROM {target.Name}",
                metadataConnection);
            using SqlDataReader metadataReader = metadataCommand.ExecuteReader();
            Assert.Equal(version == SqlVectorTypeSupport.V2 ? "float16" : null,
                metadataReader.GetColumnSchema()[0]["VectorBaseType"]);
        }

        using SqlBulkCopy copy = new(copyConnectionString)
        {
            DestinationTableName = target.Name,
            EnableStreaming = streaming
        };
        using DataTableReader reader = source.CreateDataReader();
        async Task WriteRows()
        {
            if (sequential)
            {
                using IDataReader checkedReader = new SequentialReader(reader);
                if (useAsync)
                {
                    await copy.WriteToServerAsync(checkedReader);
                }
                else
                {
                    copy.WriteToServer(checkedReader);
                }
            }
            else if (useAsync)
            {
                await copy.WriteToServerAsync(reader);
            }
            else
            {
                copy.WriteToServer(reader);
            }
        }
        if (!nativeDestination)
        {
            Exception error = await Record.ExceptionAsync(WriteRows);
            Assert.NotNull(error);
            Assert.IsType<InvalidCastException>(error.GetBaseException());
            using SqlCommand count = new($"SELECT COUNT(*) FROM {target.Name}", connection);
            Assert.Equal(0, count.ExecuteScalar());
            return;
        }
        await WriteRows();

        using SqlCommand verify = new($"SELECT * FROM {target.Name} ORDER BY Id", connection);
        using SqlDataReader result = verify.ExecuteReader();
        for (int i = 0; i < source.Rows.Count; i++)
        {
            Assert.True(result.Read());
            Assert.Equal(i, result.GetInt32(0));
            Assert.Equal("tail" + i, result.GetString(2));
            if (i is 0 or 2)
            {
                Assert.True(result.IsDBNull(1));
            }
            else
            {
                Assert.Equal(expected, JsonSerializer.Deserialize<float[]>(result.GetString(1)));
                float first = result.GetSqlVector<float>(1).Memory.Span[0];
                Assert.Equal(int.MinValue, BitConverter.ToInt32(BitConverter.GetBytes(first), 0));
            }
        }
        Assert.False(result.Read());
    }

    /// <summary>
    /// Models an IDataReader-only source that rejects value access in decreasing ordinal order.
    /// Schema access never consumes values, and each Read resets the ordinal boundary.
    /// </summary>
    private sealed class SequentialReader : IDataReader
    {
        private readonly DbDataReader _inner;
        private int _lastOrdinal = -1;

        /// <summary>Wraps the source without reading its first row.</summary>
        /// <param name="inner">The source reader owned by the caller.</param>
        internal SequentialReader(DbDataReader inner) => _inner = inner;

        /// <summary>Checks monotonic value access within the current row.</summary>
        /// <param name="ordinal">The column about to be read.</param>
        private void CheckOrdinal(int ordinal)
        {
            Assert.True(ordinal >= _lastOrdinal,
                $"Column {ordinal} was read after {_lastOrdinal}.");
            _lastOrdinal = ordinal;
        }

        /// <inheritdoc/>
        public object GetValue(int i)
        {
            CheckOrdinal(i);
            return _inner.GetValue(i);
        }

        /// <inheritdoc/>
        public bool IsDBNull(int i)
        {
            CheckOrdinal(i);
            return _inner.IsDBNull(i);
        }

        /// <inheritdoc/>
        public bool Read()
        {
            _lastOrdinal = -1;
            return _inner.Read();
        }

        /// <inheritdoc/>
        public object this[int i] => GetValue(i);
        /// <inheritdoc/>
        public object this[string name] => GetValue(GetOrdinal(name));
        /// <inheritdoc/>
        public int FieldCount => _inner.FieldCount;
        /// <inheritdoc/>
        public int Depth => _inner.Depth;
        /// <inheritdoc/>
        public bool IsClosed => _inner.IsClosed;
        /// <inheritdoc/>
        public int RecordsAffected => _inner.RecordsAffected;
        /// <inheritdoc/>
        public void Close() => _inner.Close();
        /// <inheritdoc/>
        public void Dispose() => _inner.Dispose();
        /// <inheritdoc/>
        public bool NextResult() => _inner.NextResult();
        /// <inheritdoc/>
        public DataTable GetSchemaTable() => _inner.GetSchemaTable();
        /// <inheritdoc/>
        public string GetName(int i) => _inner.GetName(i);
        /// <inheritdoc/>
        public string GetDataTypeName(int i) => _inner.GetDataTypeName(i);
        /// <inheritdoc/>
        public Type GetFieldType(int i) => _inner.GetFieldType(i);
        /// <inheritdoc/>
        public int GetOrdinal(string name) => _inner.GetOrdinal(name);
        /// <inheritdoc/>
        public bool GetBoolean(int i) => (bool)GetValue(i);
        /// <inheritdoc/>
        public byte GetByte(int i) => (byte)GetValue(i);
        /// <inheritdoc/>
        public char GetChar(int i) => (char)GetValue(i);
        /// <inheritdoc/>
        public Guid GetGuid(int i) => (Guid)GetValue(i);
        /// <inheritdoc/>
        public short GetInt16(int i) => (short)GetValue(i);
        /// <inheritdoc/>
        public int GetInt32(int i) => (int)GetValue(i);
        /// <inheritdoc/>
        public long GetInt64(int i) => (long)GetValue(i);
        /// <inheritdoc/>
        public float GetFloat(int i) => (float)GetValue(i);
        /// <inheritdoc/>
        public double GetDouble(int i) => (double)GetValue(i);
        /// <inheritdoc/>
        public string GetString(int i) => (string)GetValue(i);
        /// <inheritdoc/>
        public decimal GetDecimal(int i) => (decimal)GetValue(i);
        /// <inheritdoc/>
        public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
        /// <inheritdoc/>
        public IDataReader GetData(int i) => _inner.GetData(i);
        /// <inheritdoc/>
        public long GetBytes(int i, long offset, byte[] buffer, int bufferOffset, int length) =>
            _inner.GetBytes(i, offset, buffer, bufferOffset, length);
        /// <inheritdoc/>
        public long GetChars(int i, long offset, char[] buffer, int bufferOffset, int length) =>
            _inner.GetChars(i, offset, buffer, bufferOffset, length);
        /// <inheritdoc/>
        public int GetValues(object[] values)
        {
            int count = Math.Min(values.Length, FieldCount);
            for (int i = 0; i < count; i++)
            {
                values[i] = GetValue(i);
            }
            return count;
        }
    }

    /// <summary>Provides value access while rejecting optional type/schema discovery.</summary>
    private sealed class MetadataReader : DbDataReader
    {
        private readonly DataTableReader _inner;
        private readonly Exception _metadataError;

        /// <summary>
        /// Wraps a source and preserves the exact metadata exception for propagation checks.
        /// </summary>
        /// <param name="inner">The owned source reader.</param>
        /// <param name="metadataError">The exception thrown by GetFieldType.</param>
        internal MetadataReader(DataTableReader inner, Exception metadataError)
        {
            _inner = inner;
            _metadataError = metadataError;
        }

        internal int FieldTypeCalls { get; private set; }
        internal int SchemaTableCalls { get; private set; }

        /// <inheritdoc/>
        public override Type GetFieldType(int ordinal)
        {
            FieldTypeCalls++;
            throw _metadataError;
        }
        /// <inheritdoc/>
        public override DataTable GetSchemaTable()
        {
            SchemaTableCalls++;
            throw new NotImplementedException("GetSchemaTable");
        }
        /// <inheritdoc/>
        public override bool IsDBNull(int ordinal)
        {
            object value = GetValue(ordinal);
            return value is DBNull
                || value is System.Data.SqlTypes.INullable sqlValue && sqlValue.IsNull;
        }
        /// <inheritdoc/>
        public override object GetValue(int ordinal) => _inner.GetValue(ordinal);
        /// <inheritdoc/>
        public override bool Read() => _inner.Read();
        /// <inheritdoc/>
        public override bool NextResult() => _inner.NextResult();
        /// <inheritdoc/>
        public override object this[int ordinal] => GetValue(ordinal);
        /// <inheritdoc/>
        public override object this[string name] => GetValue(GetOrdinal(name));
        /// <inheritdoc/>
        public override int FieldCount => _inner.FieldCount;
        /// <inheritdoc/>
        public override int Depth => _inner.Depth;
        /// <inheritdoc/>
        public override bool IsClosed => _inner.IsClosed;
        /// <inheritdoc/>
        public override bool HasRows => _inner.HasRows;
        /// <inheritdoc/>
        public override int RecordsAffected => _inner.RecordsAffected;
        /// <inheritdoc/>
        public override void Close() => _inner.Close();
        /// <inheritdoc/>
        public override string GetName(int ordinal) => _inner.GetName(ordinal);
        /// <inheritdoc/>
        public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);
        /// <inheritdoc/>
        public override int GetOrdinal(string name) => _inner.GetOrdinal(name);
        /// <inheritdoc/>
        public override bool GetBoolean(int ordinal) => _inner.GetBoolean(ordinal);
        /// <inheritdoc/>
        public override byte GetByte(int ordinal) => _inner.GetByte(ordinal);
        /// <inheritdoc/>
        public override char GetChar(int ordinal) => _inner.GetChar(ordinal);
        /// <inheritdoc/>
        public override Guid GetGuid(int ordinal) => _inner.GetGuid(ordinal);
        /// <inheritdoc/>
        public override short GetInt16(int ordinal) => _inner.GetInt16(ordinal);
        /// <inheritdoc/>
        public override int GetInt32(int ordinal) => _inner.GetInt32(ordinal);
        /// <inheritdoc/>
        public override long GetInt64(int ordinal) => _inner.GetInt64(ordinal);
        /// <inheritdoc/>
        public override float GetFloat(int ordinal) => _inner.GetFloat(ordinal);
        /// <inheritdoc/>
        public override double GetDouble(int ordinal) => _inner.GetDouble(ordinal);
        /// <inheritdoc/>
        public override string GetString(int ordinal) => _inner.GetString(ordinal);
        /// <inheritdoc/>
        public override decimal GetDecimal(int ordinal) => _inner.GetDecimal(ordinal);
        /// <inheritdoc/>
        public override DateTime GetDateTime(int ordinal) => _inner.GetDateTime(ordinal);
        /// <inheritdoc/>
        public override long GetBytes(
            int ordinal, long offset, byte[] buffer, int start, int length) =>
            _inner.GetBytes(ordinal, offset, buffer, start, length);
        /// <inheritdoc/>
        public override long GetChars(
            int ordinal, long offset, char[] buffer, int start, int length) =>
            _inner.GetChars(ordinal, offset, buffer, start, length);
        /// <inheritdoc/>
        public override int GetValues(object[] values) => _inner.GetValues(values);
        /// <inheritdoc/>
        public override System.Collections.IEnumerator GetEnumerator() => _inner.GetEnumerator();
    }
}
