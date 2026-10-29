// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

/// <summary>
/// Verifies source-format discovery and streaming safety for vector bulk copies.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorBulkCopySourceTests
{
    /// <summary>
    /// Indicates whether the server supports both vector base types used by these tests.
    /// </summary>
    public static bool IsSupported => DataTestUtility.IsSqlVectorFloat16Supported;

    /// <summary>
    /// Limits three-part destination tests to servers which support cross-database queries.
    /// </summary>
    public static bool IsCrossDatabaseSupported =>
        IsSupported && !DataTestUtility.IsAzureSqlConnectionString(DataTestUtility.TCPConnectionString);

    /// <summary>
    /// Covers both destinations, supported native sources, row-selection modes, and sync/async.
    /// </summary>
    /// <returns>The supported source/destination combinations.</returns>
    public static IEnumerable<object[]> ObjectSourceCases()
    {
        foreach (string destination in new[] { "float32", "float16" })
        {
            foreach (bool useAsync in new[] { false, true })
            {
                for (int sourceMode = 0; sourceMode < 4; sourceMode++)
                {
                    yield return new object[] { false, destination, useAsync, sourceMode };
                    #if NET
                    yield return new object[] { true, destination, useAsync, sourceMode };
                    #endif
                }
            }
        }
    }

    /// <summary>
    /// Finds the first non-null native value without losing rows or inspecting excluded rows.
    /// </summary>
    /// <param name="useHalf">Whether the source contains native float16 values.</param>
    /// <param name="destination">The destination column's base type.</param>
    /// <param name="useAsync">Whether to use asynchronous bulk copy.</param>
    /// <param name="sourceMode">Table, row subset, Added rows, or table with a deleted row.</param>
    /// <returns>A task that completes after all selected rows have been verified.</returns>
    [ConditionalTheory(nameof(IsSupported))]
    [MemberData(nameof(ObjectSourceCases))]
    public async Task ObjectSourceLeadingNulls(
        bool useHalf, string destination, bool useAsync, int sourceMode)
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        string columnType = destination == "float32" ? "vector(3)" : "vector(3, float16)";
        using Table target = new(connection, "VectorObjectSource",
            $"(Id int NOT NULL, VectorData {columnType} NULL)");
        using DataTable source = CreateSource();
        if (sourceMode != 0)
        {
            // This row is excluded from the copy and must not determine its dimensions.
            source.Rows.Add(99, new SqlVector<float>(new float[] { 1, 2, 3, 4, 5 }));
            source.AcceptChanges();
            if (sourceMode == 3)
            {
                source.Rows[0].Delete();
            }
        }

        float[] values = { -0.0f, 1.5f, -2.5f };
        DataRow[] rows =
        {
            source.Rows.Add(0, DBNull.Value),
            source.Rows.Add(1, DBNull.Value),
            source.Rows.Add(2, CreateVector(useHalf, values)),
            source.Rows.Add(3, DBNull.Value),
            source.Rows.Add(4, CreateVector(useHalf, values)),
        };
        using SqlBulkCopy copy = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            DestinationTableName = target.Name
        };
        await CopyRows(copy, source, rows, sourceMode, useAsync);

        using SqlCommand verify = new(
            $"SELECT Id, VectorData FROM {target.Name} ORDER BY Id", connection);
        using SqlDataReader reader = verify.ExecuteReader();
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.True(reader.Read());
            Assert.Equal(i, reader.GetInt32(0));
            if (i is 2 or 4)
            {
                float[] actual = reader.GetSqlVector<float>(1).Memory.ToArray();
                Assert.Equal(values.Length, actual.Length);
                for (int element = 0; element < values.Length; element++)
                {
                    Assert.Equal(
                        BitConverter.ToInt32(BitConverter.GetBytes(values[element]), 0),
                        BitConverter.ToInt32(BitConverter.GetBytes(actual[element]), 0));
                }
            }
            else
            {
                Assert.True(reader.IsDBNull(1));
            }
        }
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Copies all-null object columns without requiring a source vector value.
    /// </summary>
    /// <param name="destination">The destination base type.</param>
    /// <param name="useAsync">Whether to use asynchronous bulk copy.</param>
    /// <returns>A task that completes after the null rows have been counted.</returns>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("float32", false)]
    [InlineData("float32", true)]
    [InlineData("float16", false)]
    [InlineData("float16", true)]
    public async Task AllNullObjectSource(string destination, bool useAsync)
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        string columnType = destination == "float32" ? "vector(3)" : "vector(3, float16)";
        using Table target = new(connection, "VectorNullSource",
            $"(Id int NOT NULL, VectorData {columnType} NULL)");
        using DataTable source = CreateSource();
        source.Rows.Add(0, DBNull.Value);
        source.Rows.Add(1, SqlVector<float>.Null);
        using SqlBulkCopy copy = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            DestinationTableName = target.Name
        };
        await CopyRows(copy, source, Array.Empty<DataRow>(), 0, useAsync);

        using SqlCommand verify = new(
            $"SELECT COUNT(*) FROM {target.Name} WHERE VectorData IS NULL", connection);
        Assert.Equal(2, verify.ExecuteScalar());
    }

    /// <summary>
    /// Preserves the existing float32 coercion of JSON strings mixed with native float32 values.
    /// </summary>
    /// <param name="jsonFirst">Whether JSON is the first non-null value.</param>
    [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsSqlVectorSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyFloat32ObjectCoercion(bool jsonFirst)
    {
        using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
        using Table target = new(connection, "VectorLegacyObject",
            "(Id int NOT NULL, VectorData vector(3) NULL)");
        float[] values = { 1.5f, 2.5f, 3.5f };
        object json = JsonSerializer.Serialize(values);
        object native = new SqlVector<float>(values);
        using DataTable source = CreateSource();
        source.Rows.Add(0, DBNull.Value);
        source.Rows.Add(1, jsonFirst ? json : native);
        source.Rows.Add(2, jsonFirst ? native : json);
        using SqlBulkCopy copy = new(DataTestUtility.TCPConnectionString)
        {
            DestinationTableName = target.Name
        };
        copy.WriteToServer(source);

        using SqlCommand verify = new(
            $"SELECT VectorData FROM {target.Name} WHERE Id > 0 ORDER BY Id", connection);
        using SqlDataReader reader = verify.ExecuteReader();
        for (int i = 0; i < 2; i++)
        {
            Assert.True(reader.Read());
            Assert.Equal(values, reader.GetSqlVector<float>(0).Memory.ToArray());
        }
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Infers native vector dimensions after leading nulls when the destination is text.
    /// SQL Server, rather than a client-side normalization, produces the JSON representation.
    /// </summary>
    /// <param name="useHalf">Whether the source contains native float16 values.</param>
    /// <param name="objectColumn">Whether the column declares object instead of its vector type.</param>
    /// <param name="useAsync">Whether to use asynchronous bulk copy.</param>
    /// <returns>A task that completes after the null and server-produced JSON are verified.</returns>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    #if NET
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    #endif
    public async Task NativeSourceToTextAfterLeadingNull(
        bool useHalf, bool objectColumn, bool useAsync)
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        connection.Open();
        using Table target = new(connection, "VectorNativeToText",
            "(Id int NOT NULL, VectorData varchar(max) NULL)");
        float[] values = { 1.5f, 2.5f, -3.5f };
        object vector = CreateVector(useHalf, values);
        using DataTable source = new();
        source.Columns.Add("Id", typeof(int));
        source.Columns.Add("VectorData", objectColumn ? typeof(object) : vector.GetType());
        source.Rows.Add(0, DBNull.Value);
        source.Rows.Add(1, vector);
        using SqlBulkCopy copy = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            DestinationTableName = target.Name
        };
        await CopyRows(copy, source, Array.Empty<DataRow>(), 0, useAsync);

        using SqlCommand verify = new(
            $"SELECT VectorData FROM {target.Name} ORDER BY Id", connection);
        using SqlDataReader reader = verify.ExecuteReader();
        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
        Assert.True(reader.Read());
        Assert.Equal(values, JsonSerializer.Deserialize<float[]>(reader.GetString(0)));
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Keeps JSON in an object column as text for server-owned float16 parsing after leading nulls.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void ObjectJsonSourceKeepsServerRounding()
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        using Table target = new(connection, "VectorObjectJson",
            "(Id int NOT NULL, VectorData vector(1, float16) NULL)");
        using DataTable source = CreateSource();
        source.Rows.Add(0, DBNull.Value);
        source.Rows.Add(1, "[1.0004883408546447753906250001]");
        using SqlBulkCopy copy = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            DestinationTableName = target.Name
        };
        copy.WriteToServer(source);

        using SqlCommand verify = new(
            $"SELECT VectorData FROM {target.Name} WHERE Id = 1", connection);
        using SqlDataReader reader = verify.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(1.0f, reader.GetSqlVector<float>(0).Memory.Span[0]);
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Does not consume a later sql_variant JSON value before earlier sequential columns.
    /// </summary>
    /// <param name="useAsync">Whether to execute and copy asynchronously.</param>
    /// <returns>A task that completes after both copied columns have been verified.</returns>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SequentialVariantSource(bool useAsync)
    {
        using SqlConnection destination = new(DataTestUtility.VectorFloat16ConnectionString);
        using Table target = new(destination, "VectorSequentialSource",
            "(Id int NOT NULL, VectorData vector(3, float16) NULL)");
        using SqlConnection source = new(DataTestUtility.VectorFloat16ConnectionString);
        await source.OpenAsync();
        using SqlCommand command = new(
            "SELECT 7 AS Id, CAST('[1.5,2.5,3.5]' AS sql_variant) AS VectorData", source);
        using SqlDataReader reader = useAsync
            ? await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
            : command.ExecuteReader(CommandBehavior.SequentialAccess);
        using SqlBulkCopy copy = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            DestinationTableName = target.Name,
            EnableStreaming = true
        };
        if (useAsync)
        {
            await copy.WriteToServerAsync(reader);
        }
        else
        {
            copy.WriteToServer(reader);
        }

        using SqlCommand verify = new($"SELECT Id, VectorData FROM {target.Name}", destination);
        using SqlDataReader result = verify.ExecuteReader();
        Assert.True(result.Read());
        Assert.Equal(7, result.GetInt32(0));
        Assert.Equal(
            new[] { 1.5f, 2.5f, 3.5f },
            result.GetSqlVector<float>(1).Memory.ToArray());
        Assert.False(result.Read());
    }

    /// <summary>
    /// Pins the server's TDS collation descriptor for the explicit UTF-8 vector text transport.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void Utf8VectorTextCollationMatchesServer()
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        connection.Open();
        using SqlCommand command = new(
            "SELECT CAST('x' AS varchar(1)) COLLATE Latin1_General_100_BIN2_UTF8", connection);
        using SqlDataReader reader = command.ExecuteReader();
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object metadata = typeof(SqlDataReader).GetProperty("MetaData", Flags).GetValue(reader);
        object column = metadata.GetType().GetProperty("Item", Flags).GetValue(metadata, new object[] { 0 });
        object collation = column.GetType().GetField("collation", Flags).GetValue(column);
        uint info = (uint)collation.GetType().GetField("_info", Flags).GetValue(collation);
        byte sortId = (byte)collation.GetType().GetField("_sortId", Flags).GetValue(collation);

        Assert.Equal(0x26000409u, info);
        Assert.Equal((byte)0, sortId);
        Encoding encoding = (Encoding)column.GetType().GetProperty("Encoding", Flags).GetValue(column);
        Assert.Equal(65001, encoding.CodePage);
    }

    /// <summary>
    /// Re-encodes JSON from a different source collation while targeting another database.
    /// The wide value also exercises PLP streaming without float32 intermediate conversion.
    /// </summary>
    /// <param name="useAsync">Whether to execute and copy asynchronously.</param>
    /// <returns>A task that completes after the copied values and null have been verified.</returns>
    [ConditionalTheory(nameof(IsCrossDatabaseSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Utf8TextSourceAcrossDatabases(bool useAsync)
    {
        using SqlConnection destination = new(DataTestUtility.VectorFloat16ConnectionString);
        using Table target = Table.WithName(destination,
            $"[dbo].[VectorUtf8_{Guid.NewGuid():N}]",
            "(Id int NOT NULL, VectorData vector(2000, float16) NULL)");
        SqlConnectionStringBuilder sourceBuilder = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            InitialCatalog = string.Equals(destination.Database, "master",
                StringComparison.OrdinalIgnoreCase) ? "tempdb" : "master"
        };
        using SqlConnection source = new(sourceBuilder.ConnectionString);
        await source.OpenAsync();
        Assert.NotEqual(destination.Database, source.Database);
        using SqlCommand command = new(
            "SELECT Id, JsonValue COLLATE Japanese_CI_AS AS VectorData " +
            "FROM (VALUES (7, @json), (8, CAST(NULL AS varchar(max)))) AS s(Id, JsonValue) " +
            "ORDER BY Id", source);
        float[] values = new float[2000];
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 1.5f;
        }
        command.Parameters.Add("@json", SqlDbType.VarChar, -1).Value = JsonSerializer.Serialize(values);
        using SqlDataReader reader = useAsync
            ? await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess)
            : command.ExecuteReader(CommandBehavior.SequentialAccess);
        using SqlCommandBuilder names = new();
        using SqlBulkCopy copy = new(sourceBuilder.ConnectionString)
        {
            DestinationTableName = names.QuoteIdentifier(destination.Database) + "." + target.Name,
            EnableStreaming = true
        };
        if (useAsync)
        {
            await copy.WriteToServerAsync(reader);
        }
        else
        {
            copy.WriteToServer(reader);
        }

        using SqlCommand verify = new(
            $"SELECT Id, VectorData FROM {target.Name} ORDER BY Id", destination);
        using SqlDataReader result = verify.ExecuteReader();
        Assert.True(result.Read());
        Assert.Equal(7, result.GetInt32(0));
        Assert.Equal(values, result.GetSqlVector<float>(1).Memory.ToArray());
        Assert.True(result.Read());
        Assert.Equal(8, result.GetInt32(0));
        Assert.True(result.IsDBNull(1));
        Assert.False(result.Read());
    }

    #if NET
    /// <summary>
    /// Rediscovers each object source without overwriting cached destination vector metadata.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void CachedMetadataSupportsDifferentObjectSourceBaseTypes()
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        using Table target = new(connection, "VectorObjectCache",
            "(Id int NOT NULL, VectorData vector(3, float16) NULL)");
        using SqlBulkCopy copy = new(
            DataTestUtility.VectorFloat16ConnectionString, SqlBulkCopyOptions.CacheMetadata)
        {
            DestinationTableName = target.Name
        };
        float[] values = { 1.5f, 2.5f, 3.5f };
        foreach (bool useHalf in new[] { false, true, false })
        {
            using DataTable source = CreateSource();
            source.Rows.Add(0, DBNull.Value);
            source.Rows.Add(1, CreateVector(useHalf, values));
            copy.WriteToServer(source);

            using SqlCommand verify = new(
                $"SELECT VectorData FROM {target.Name} WHERE Id = 1", connection);
            using (SqlDataReader reader = verify.ExecuteReader())
            {
                Assert.True(reader.Read());
                Assert.Equal(values, reader.GetSqlVector<float>(0).Memory.ToArray());
                Assert.False(reader.Read());
            }
            target.Truncate();
        }
    }

    /// <summary>
    /// Rejects a later incompatible base type during copying, not by scanning all source values
    /// before the first batch. Earlier committed batches retain their usual semantics.
    /// </summary>
    /// <param name="halfFirst">Whether float16 establishes the incoming representation.</param>
    /// <param name="useAsync">Whether to use asynchronous bulk copy.</param>
    /// <returns>A task that completes after the conversion failure and prior batches are verified.</returns>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RejectsMixedNativeBaseTypesDuringCopy(bool halfFirst, bool useAsync)
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        using Table target = new(connection, "VectorMixedSource",
            "(Id int NOT NULL, VectorData vector(3, float16) NULL)");
        float[] values = { 1.5f, 2.5f, 3.5f };
        using DataTable source = CreateSource();
        source.Rows.Add(0, DBNull.Value);
        source.Rows.Add(1, CreateVector(halfFirst, values));
        source.Rows.Add(2, CreateVector(!halfFirst, values));
        using SqlBulkCopy copy = new(DataTestUtility.VectorFloat16ConnectionString)
        {
            DestinationTableName = target.Name,
            BatchSize = 1
        };
        InvalidOperationException error;
        if (useAsync)
        {
            error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => copy.WriteToServerAsync(source));
        }
        else
        {
            error = Assert.Throws<InvalidOperationException>(() => copy.WriteToServer(source));
        }
        Assert.IsType<ArgumentException>(error.InnerException);

        using SqlCommand verify = new(
            $"SELECT COUNT(*) FROM {target.Name} WHERE Id < 2", connection);
        Assert.Equal(2, verify.ExecuteScalar());
        verify.CommandText = $"SELECT COUNT(*) FROM {target.Name} WHERE Id = 2";
        Assert.Equal(0, verify.ExecuteScalar());
    }
    #endif

    /// <summary>
    /// Creates an in-memory source whose vector representation must be inferred from its values.
    /// </summary>
    /// <returns>An empty table with an identifier and object-valued vector column.</returns>
    private static DataTable CreateSource()
    {
        DataTable table = new();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("VectorData", typeof(object));
        return table;
    }

    /// <summary>
    /// Creates a native vector without changing its base type.
    /// </summary>
    /// <param name="useHalf">Whether to use the float16 base type.</param>
    /// <param name="values">Exactly representable sample values.</param>
    /// <returns>The boxed native vector.</returns>
    private static object CreateVector(bool useHalf, float[] values)
    {
        #if NET
        if (useHalf)
        {
            Half[] half = Array.ConvertAll(values, value => (Half)value);
            return new SqlVector<Half>(half);
        }
        #endif
        return new SqlVector<float>(values);
    }

    /// <summary>
    /// Exercises the public bulk-copy overload for the selected in-memory row source.
    /// </summary>
    /// <param name="copy">The configured bulk-copy operation.</param>
    /// <param name="table">The source table.</param>
    /// <param name="rows">The selected subset for the row-array overload.</param>
    /// <param name="sourceMode">The overload and row-state selection to exercise.</param>
    /// <param name="useAsync">Whether to use asynchronous bulk copy.</param>
    /// <returns>A task that completes after the rows have been copied.</returns>
    private static async Task CopyRows(
        SqlBulkCopy copy, DataTable table, DataRow[] rows, int sourceMode, bool useAsync)
    {
        if (sourceMode == 1)
        {
            if (useAsync)
            {
                await copy.WriteToServerAsync(rows);
            }
            else
            {
                copy.WriteToServer(rows);
            }
        }
        else if (sourceMode == 2)
        {
            if (useAsync)
            {
                await copy.WriteToServerAsync(table, DataRowState.Added);
            }
            else
            {
                copy.WriteToServer(table, DataRowState.Added);
            }
        }
        else
        {
            if (useAsync)
            {
                await copy.WriteToServerAsync(table);
            }
            else
            {
                copy.WriteToServer(table);
            }
        }
    }
}
