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
/// Verifies row-time vector JSON fallback without changing native transfers or reader order.
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
    /// Renders typed vector reader values as JSON while preserving nulls and neighboring columns.
    /// Text fallback does not require a server with native vector support.
    /// </summary>
    /// <param name="useHalf">Whether the source uses the float16 base type.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled for ordinary text columns.</param>
    /// <param name="sequential">Whether to use an ordinal-checking IDataReader.</param>
    /// <param name="destination">The SQL text type receiving the JSON.</param>
    /// <returns>A task that completes after the copied rows have been checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    [MemberData(nameof(TextCases))]
    public Task ReaderVectorsToText(
        bool useHalf, bool useAsync, bool streaming, bool sequential, string destination) =>
        VerifyReaderCopy(useHalf, useAsync, streaming, sequential, destination,
            SqlVectorTypeSupport.V2);

    /// <summary>
    /// Sends vector JSON to a native JSON column rather than passing a boxed vector to its writer.
    /// </summary>
    /// <param name="useHalf">Whether the source uses the float16 base type.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="sequential">Whether to use an ordinal-checking IDataReader.</param>
    /// <returns>A task that completes after the JSON values have been checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsJsonSupported))]
    [MemberData(nameof(ReaderModes))]
    public Task ReaderVectorsToJson(
        bool useHalf, bool useAsync, bool streaming, bool sequential) =>
        VerifyReaderCopy(useHalf, useAsync, streaming, sequential, "json",
            SqlVectorTypeSupport.V2);

    /// <summary>
    /// Handles float16 destinations exposed as text under V1/Off, but preserves signed zero
    /// through the native V2 transfer path.
    /// </summary>
    /// <param name="useHalf">Whether the source uses the float16 base type.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <param name="streaming">Whether streaming is enabled.</param>
    /// <param name="sequential">Whether to use an ordinal-checking IDataReader.</param>
    /// <param name="version">The requested vector feature version.</param>
    /// <returns>A task that completes after metadata and stored values have been checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsSqlVectorFloat16Supported))]
    [MemberData(nameof(Float16Cases))]
    public Task ReaderVectorsToFloat16(
        bool useHalf, bool useAsync, bool streaming, bool sequential,
        SqlVectorTypeSupport version) =>
        VerifyReaderCopy(useHalf, useAsync, streaming, sequential, "vector(3, float16)", version);

    /// <summary>
    /// Exercises a payload large enough to require varchar(max)/nvarchar(max) handling.
    /// </summary>
    /// <param name="useHalf">Whether to use float16 elements.</param>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <returns>A task that completes after the large JSON value has been checked.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    [InlineData(false, false)]
    [InlineData(false, true)]
    #if NET
    [InlineData(true, false)]
    [InlineData(true, true)]
    #endif
    public Task LargeReaderVectorToText(bool useHalf, bool useAsync)
    {
        float[] values = new float[useHalf ? 3996 : 1998];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 65504f;
        }
        return VerifyReaderCopy(useHalf, useAsync, true, false, "nvarchar(max)",
            SqlVectorTypeSupport.V2, values);
    }

    /// <summary>
    /// Recomputes reader access methods when cached text metadata is reused for native vectors
    /// and ordinary streaming strings.
    /// </summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <returns>A task that completes after each source representation has been verified.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CachedTextMetadataSupportsDifferentReaderTypes(bool useAsync)
    {
        using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
        connection.Open();
        using Table target = new(connection, "VectorReaderCache",
            "(Id int NOT NULL, VectorData nvarchar(max) NULL)");
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
    /// Applies the normal Unicode text length check to serialized vector values.
    /// </summary>
    /// <param name="useAsync">Whether to copy asynchronously.</param>
    /// <returns>A task that completes after the truncation error and empty target are verified.</returns>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTCPConnStringSetup))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReaderJsonHonorsTextLength(bool useAsync)
    {
        using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
        connection.Open();
        using Table target = new(connection, "VectorReaderLength", "(VectorData nvarchar(2) NULL)");
        using DataTable source = new();
        source.Columns.Add("VectorData", typeof(SqlVector<float>));
        source.Rows.Add(new SqlVector<float>(new[] { 65504f }));
        using DataTableReader reader = source.CreateDataReader();
        using SqlBulkCopy copy = new(DataTestUtility.TCPConnectionString)
        {
            DestinationTableName = target.Name
        };
        InvalidOperationException error = useAsync
            ? await Assert.ThrowsAsync<InvalidOperationException>(
                () => copy.WriteToServerAsync(reader))
            : Assert.Throws<InvalidOperationException>(() => copy.WriteToServer(reader));
        InvalidOperationException truncation =
            Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.Contains(target.Name, truncation.Message);
        using SqlCommand verify = new($"SELECT COUNT(*) FROM {target.Name}", connection);
        Assert.Equal(0, verify.ExecuteScalar());
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
    /// <param name="values">Optional values for large-payload coverage.</param>
    /// <returns>A task that completes after the selected case has been verified.</returns>
    private static Task VerifyReaderCopy(
        bool useHalf, bool useAsync, bool streaming, bool sequential, string destination,
        SqlVectorTypeSupport version, float[] values = null)
    {
        values ??= new[] { -0.0f, 65504f, 0.333251953125f };
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
    /// Copies DBNull, SQL-null, and native values with the vector between two other columns.
    /// No source values may be consumed before their normal ordinal position.
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
        source.Rows.Add(0, DBNull.Value, "tail0");
        source.Rows.Add(1, new SqlVector<T>(values), "tail1");
        source.Rows.Add(2, SqlVector<T>.CreateNull(values.Length), "tail2");
        source.Rows.Add(3, new SqlVector<T>(values), "tail3");

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
                if (destination.StartsWith("vector", StringComparison.Ordinal)
                    && version == SqlVectorTypeSupport.V2)
                {
                    float first = result.GetSqlVector<float>(1).Memory.Span[0];
                    Assert.Equal(int.MinValue, BitConverter.ToInt32(BitConverter.GetBytes(first), 0));
                }
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
        private readonly DataTableReader _inner;
        private int _lastOrdinal = -1;

        /// <summary>Wraps the source without reading its first row.</summary>
        /// <param name="inner">The source reader owned by the caller.</param>
        internal SequentialReader(DataTableReader inner) => _inner = inner;

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
}
