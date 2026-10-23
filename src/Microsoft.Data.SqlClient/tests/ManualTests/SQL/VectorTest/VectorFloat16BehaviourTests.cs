// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Microsoft.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

/// <summary>
/// Tests for behaviour which is specific to the float16 vector base type, or which
/// concerns the interaction between the two base types, and so has no equivalent in the
/// shared <see cref="NativeVectorTestsBase{TElement, TTestData}"/> suite.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorFloat16BehaviourTests : IDisposable
{
    private const string ColumnName = "VectorData";
    private const string ParameterName = "@VectorData";

    /// <summary>
    /// The server's errors for a value which cannot be narrowed to float16. Both describe
    /// the same overflow, and which one is raised depends on the server build: SQL Server
    /// 2025 reports 42284 for a vector parameter where Azure SQL reports 42241.
    /// </summary>
    private static readonly int[] s_float16OutOfRangeErrors = [42284, 42241];

    private readonly string _connectionString = DataTestUtility.VectorFloat16ConnectionString;
    private readonly SqlConnection _managementConnection;
    private readonly Table _float16Table;
    private readonly Table _float32Table;
    private bool _disposed;

    /// <summary>
    /// Creates isolated columns of both base types for each behavior test.
    /// </summary>
    public VectorFloat16BehaviourTests()
    {
        _managementConnection = new SqlConnection(_connectionString);

        // NOTE: If this constructor throws, xUnit never calls Dispose, so any object already
        //   created (each of which has a GUID-based name) would be left in the database
        //   permanently. This mirrors NativeVectorTestsBase.
        try
        {
            _managementConnection.Open();

            _float16Table = new Table(_managementConnection, "VectorF16BehaviourTable",
                $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector(3, float16) NULL)");
            _float32Table = new Table(_managementConnection, "VectorF32BehaviourTable",
                $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector(3, float32) NULL)");
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public static bool IsSupported => DataTestUtility.IsSqlVectorFloat16Supported;

    #region Column metadata

    /// <summary>
    /// Reports float16 metadata without requiring a row to be read.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void ColumnSchemaReportsFloat16BaseType()
    {
        // Metadata for vector columns in general is covered by VectorColumnMetadataTests;
        // this checks only that the float16 base type is reported by its own name, and that
        // its dimension count accounts for the smaller element size.
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = new("SELECT CAST(NULL AS vector(3, float16)) AS v", connection);
        using SqlDataReader reader = command.ExecuteReader();

        DbColumn column = reader.GetColumnSchema()[0];

        Assert.Equal("float16", column["VectorBaseType"]);
        Assert.Equal(3, column["VectorDimensions"]);
    }

    #endregion

    #region Reading

    /// <summary>
    /// Widens binary16 elements exactly through the strongly typed Framework read path.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void ReadsFloat16ColumnAsWidenedSingles()
    {
        // Requesting single precision from a float16 column widens the elements, which is
        // exact. This is the only strongly typed read available where System.Half is not.
        Insert(_float16Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlDataReader reader = Select(_float16Table);
        Assert.True(reader.Read());

        SqlVector<float> vector = reader.GetSqlVector<float>(0);

        Assert.Equal(3, vector.Length);
        Assert.Equal([1.5f, 2.5f, 3.5f], vector.Memory.ToArray());
    }

    /// <summary>
    /// Renders the actual element values rather than shorter float16 round-trip spellings.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void RendersValuesExactlyRatherThanShortestRoundTrip()
    {
        // The largest finite binary16 value renders as 65500 if the elements are formatted
        // as System.Half, because that is the shortest string which round trips to the same
        // Half. Widening to single precision first renders the value itself.
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command = new("SELECT CAST('[65504,1,2]' AS vector(3, float16))", connection);
        using SqlDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal("[65504,1,2]", reader.GetString(0));
        Assert.Equal("[65504,1,2]", reader.GetFieldValue<string>(0));
    }

    /// <summary>
    /// Rejects generic element types which have no supported vector representation.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void ReportsUnsupportedElementTypes()
    {
        Insert(_float16Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlDataReader reader = Select(_float16Table);
        Assert.True(reader.Read());

        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<double>(0));
        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<int>(0));
    }

    /// <summary>
    /// Returns a SQL type from provider-specific accessors on both framework families.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void ReportsProviderSpecificValueAsASqlType()
    {
        // Every provider specific value is a type from System.Data.SqlTypes, including the
        // JSON rendering a float16 column falls back to where System.Half is unavailable.
        Insert(_float16Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlDataReader reader = Select(_float16Table);
        Assert.True(reader.Read());

        #if NET
        Assert.IsType<SqlVector<Half>>(reader.GetProviderSpecificValue(0));
        Assert.Equal(typeof(SqlVector<Half>), reader.GetProviderSpecificFieldType(0));
        #else
        SqlString value = Assert.IsType<SqlString>(reader.GetProviderSpecificValue(0));
        Assert.Equal("[1.5,2.5,3.5]", value.Value);
        Assert.Equal(typeof(SqlString), reader.GetProviderSpecificFieldType(0));

        // GetValue is the CLR path, so it keeps returning a plain string.
        Assert.IsType<string>(reader.GetValue(0));
        Assert.Equal(typeof(string), reader.GetFieldType(0));
        #endif
    }

    /// <summary>
    /// Rejects narrowing reads consistently for null and populated rows.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void ReportsNarrowingReadsConsistentlyForNullAndNonNullRows()
    {
        // The base type pairing is a property of the column, so a null row has to be
        // rejected the same way a populated one is. Reading a float32 column as a vector of
        // a narrower element type is not supported in either case.
        Insert(_float32Table, DBNull.Value);
        Insert(_float32Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new($"SELECT {ColumnName} FROM {_float32Table.Name} ORDER BY Id DESC", connection);
        using SqlDataReader reader = command.ExecuteReader();

        // The populated row is read first, then the null one, so that a failure identifies
        // which of the two diverged.
        Assert.True(reader.Read());
        Assert.False(reader.IsDBNull(0));
        AssertNarrowingReadIsRejected(reader);

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
        AssertNarrowingReadIsRejected(reader);
    }

    /// <summary>
    /// Preserves the same narrowing-read contract during asynchronous reads.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public async Task ReportsNarrowingReadsConsistentlyForNullAndNonNullRowsAsync()
    {
        Insert(_float32Table, DBNull.Value);
        Insert(_float32Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlConnection connection = new(_connectionString);
        await connection.OpenAsync();

        using SqlCommand command =
            new($"SELECT {ColumnName} FROM {_float32Table.Name} ORDER BY Id DESC", connection);
        using SqlDataReader reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.False(await reader.IsDBNullAsync(0));
        await AssertNarrowingReadIsRejectedAsync(reader);

        Assert.True(await reader.ReadAsync());
        Assert.True(await reader.IsDBNullAsync(0));
        await AssertNarrowingReadIsRejectedAsync(reader);
    }

    /// <summary>
    /// Checks unsupported typed reads at the reader's current row.
    /// </summary>
    private static void AssertNarrowingReadIsRejected(SqlDataReader reader)
    {
        #if NET
        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<Half>(0));
        Assert.Throws<NotSupportedException>(() => reader.GetFieldValue<SqlVector<Half>>(0));
        #endif

        // double is never a vector base type, so it stands in for the narrowing case on
        // frameworks without System.Half.
        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<double>(0));
    }

    /// <summary>
    /// Checks unsupported asynchronous typed reads at the reader's current row.
    /// </summary>
    private static async Task AssertNarrowingReadIsRejectedAsync(SqlDataReader reader)
    {
        #if NET
        await Assert.ThrowsAsync<NotSupportedException>(
            async () => await reader.GetFieldValueAsync<SqlVector<Half>>(0));
        #else
        await Task.CompletedTask;
        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<double>(0));
        #endif
    }

    #endregion

    #region Writing across base types

    /// <summary>
    /// Supplies each supported native source type for both destination base types.
    /// </summary>
    public static IEnumerable<object[]> CrossBaseTypeParameters()
    {
        // A vector of either base type can be written to a column of either base type: the
        // conversion is performed by the server, which knows the destination's base type.
        yield return ["float16", new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f })];
        yield return ["float32", new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f })];
        #if NET
        yield return ["float16", new SqlVector<Half>(new Half[] { (Half)1.5f, (Half)2.5f, (Half)3.5f })];
        yield return ["float32", new SqlVector<Half>(new Half[] { (Half)1.5f, (Half)2.5f, (Half)3.5f })];
        #endif
    }

    /// <summary>
    /// Leaves parameter base-type conversion to SQL Server.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [MemberData(nameof(CrossBaseTypeParameters), DisableDiscoveryEnumeration = true)]
    public void WritesVectorParameterToColumnOfEitherBaseType(string columnBaseType, object value)
    {
        Table table = columnBaseType == "float16" ? _float16Table : _float32Table;

        Insert(table, value);

        using SqlDataReader reader = Select(table);
        Assert.True(reader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], reader.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Surfaces the server's error when a float32 parameter cannot be narrowed to float16.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void RejectsValuesOutsideTheFloat16Range()
    {
        // The value is sent as float32 and narrowed by the server, which reports the
        // overflow rather than silently saturating.
        SqlException exception = Assert.Throws<SqlException>(() =>
            Insert(_float16Table, new SqlVector<float>(new float[] { 70000f, 1f, 2f })));

        Assert.Contains(exception.Number, s_float16OutOfRangeErrors);
    }

    #endregion

    #region Bulk copy across base types

    /// <summary>
    /// Copies native payloads between matching base types without an intermediate string.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("float16")]
    [InlineData("float32")]
    public void BulkCopiesBetweenColumnsOfTheSameBaseType(string baseType)
    {
        // A payload read from a vector column is transferred to a column of the same base
        // type as-is, with no conversion and no intermediate representation.
        Table table = baseType == "float16" ? _float16Table : _float32Table;

        Insert(table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlConnection sourceConnection = new(_connectionString);
        sourceConnection.Open();
        using SqlCommand selectCommand = new($"SELECT {ColumnName} FROM {table.Name}", sourceConnection);
        using SqlDataReader sourceReader = selectCommand.ExecuteReader();

        using SqlConnection destinationConnection = new(_connectionString);
        destinationConnection.Open();

        using (SqlBulkCopy bulkCopy = new(destinationConnection) { DestinationTableName = table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(sourceReader);
        }

        using SqlCommand verifyCommand =
            new($"SELECT TOP 1 {ColumnName} FROM {table.Name} ORDER BY Id DESC", destinationConnection);
        using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

        Assert.True(verifyReader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], verifyReader.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Preserves the incoming vector type so SQL Server converts between column base types.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("float32", "float16", false)]
    [InlineData("float32", "float16", true)]
    [InlineData("float16", "float32", false)]
    [InlineData("float16", "float32", true)]
    public async Task BulkCopiesColumnsOfDifferentBaseTypes(
        string sourceBaseType, string destinationBaseType, bool useAsync)
    {
        Table source = sourceBaseType == "float16" ? _float16Table : _float32Table;
        Table destination = destinationBaseType == "float16" ? _float16Table : _float32Table;

        Insert(source, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));
        Insert(source, DBNull.Value);

        using SqlConnection sourceConnection = new(_connectionString);
        sourceConnection.Open();
        using SqlCommand selectCommand =
            new($"SELECT {ColumnName} FROM {source.Name} ORDER BY Id", sourceConnection);
        using SqlDataReader sourceReader = selectCommand.ExecuteReader();

        using SqlConnection destinationConnection = new(_connectionString);
        destinationConnection.Open();

        using SqlBulkCopy bulkCopy = new(destinationConnection) { DestinationTableName = destination.Name };
        bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);

        if (useAsync)
        {
            await bulkCopy.WriteToServerAsync(sourceReader);
        }
        else
        {
            bulkCopy.WriteToServer(sourceReader);
        }

        using SqlCommand verifyCommand =
            new($"SELECT {ColumnName} FROM {destination.Name} ORDER BY Id", destinationConnection);
        using SqlDataReader verifyReader = verifyCommand.ExecuteReader();
        Assert.True(verifyReader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], verifyReader.GetSqlVector<float>(0).Memory.ToArray());
        Assert.True(verifyReader.Read());
        Assert.True(verifyReader.IsDBNull(0));
        Assert.False(verifyReader.Read());
    }

    /// <summary>
    /// Preserves interleaved SQL nulls and native values in the bulk stream.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("float16")]
    [InlineData("float32")]
    public void BulkCopyPreservesNullsBetweenColumnsOfTheSameBaseType(string baseType)
    {
        Table table = baseType == "float16" ? _float16Table : _float32Table;

        // Interleaved, so that a row's nullness cannot be satisfied by position alone.
        Insert(table, DBNull.Value);
        Insert(table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));
        Insert(table, DBNull.Value);
        Insert(table, DBNull.Value);

        using SqlConnection sourceConnection = new(_connectionString);
        sourceConnection.Open();
        using SqlCommand selectCommand =
            new($"SELECT {ColumnName} FROM {table.Name} ORDER BY Id", sourceConnection);
        using SqlDataReader sourceReader = selectCommand.ExecuteReader();

        using SqlConnection destinationConnection = new(_connectionString);
        destinationConnection.Open();

        using (SqlBulkCopy bulkCopy = new(destinationConnection) { DestinationTableName = table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(sourceReader);
        }

        using SqlCommand verifyCommand =
            new($"SELECT TOP 4 {ColumnName} FROM {table.Name} ORDER BY Id DESC", destinationConnection);
        using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

        // Read back in descending order, so the expected pattern is the reverse of the
        // order the rows were inserted in.
        foreach (bool expectedNull in new[] { true, true, false, true })
        {
            Assert.True(verifyReader.Read());
            Assert.Equal(expectedNull, verifyReader.IsDBNull(0));

            if (expectedNull)
            {
                Assert.Equal(DBNull.Value, verifyReader.GetValue(0));
            }
            else
            {
                Assert.Equal([1.5f, 2.5f, 3.5f], verifyReader.GetSqlVector<float>(0).Memory.ToArray());
            }
        }
    }

    /// <summary>
    /// Sends JSON text to a float16 destination for server-side conversion.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceIntoFloat16Column()
    {
        // The JSON string reaches SQL Server as text, without client-side narrowing.
        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(string));
        table.Rows.Add("[1.5,2.5,3.5]");

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(table);
        }

        using SqlDataReader reader = Select(_float16Table);
        Assert.True(reader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], reader.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Supports textual vector input from any IDataReader, including a leading null.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceFromANonSqlClientReader()
    {
        // A generic reader does not provide SqlDataReader's native source metadata.
        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(string));
        table.Rows.Add(DBNull.Value);
        table.Rows.Add("[1.5,2.5,3.5]");

        using IDataReader reader = table.CreateDataReader();

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(reader);
        }

        using SqlDataReader verify = Select(_float16Table);
        Assert.True(verify.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], verify.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Preserves the shipped JSON-to-float32 coercion when float32 is negotiated.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceIntoFloat32ColumnAtV1()
    {
        // Control: at v1 a float32 column IS presented as a vector, so the declaration says
        // vector(N) and the string is coerced to a float32 payload by the client.
        string v1 = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
        {
            VectorTypeSupport = SqlVectorTypeSupport.V1
        }.ConnectionString;

        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(string));
        table.Rows.Add("[1.5,2.5,3.5]");

        using SqlConnection connection = new(v1);
        connection.Open();

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float32Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(table);
        }

        using SqlCommand command =
            new($"SELECT TOP 1 CAST({ColumnName} AS varchar(100)) FROM {_float32Table.Name} ORDER BY Id DESC", connection);
        Assert.Contains("1.5", (string)command.ExecuteScalar());
    }

    /// <summary>
    /// Uses textual bulk copy when the server describes float16 as varchar under v1.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceIntoFloat16ColumnAtV1()
    {
        // At v1 the server presents a float16 column as varchar(max), so the ordinary text
        // path applies and the server performs the conversion.
        string v1 = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
        {
            VectorTypeSupport = SqlVectorTypeSupport.V1
        }.ConnectionString;

        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(string));
        table.Rows.Add("[1.5,2.5,3.5]");

        using SqlConnection connection = new(v1);
        connection.Open();

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(table);
        }

        using SqlCommand command =
            new($"SELECT TOP 1 CAST({ColumnName} AS varchar(100)) FROM {_float16Table.Name} ORDER BY Id DESC", connection);
        Assert.Contains("1.5", (string)command.ExecuteScalar());
    }

    /// <summary>
    /// Verifies that a JSON string can be bulk copied into a float16 column declaring more
    /// dimensions than can be sent as float32. Sending it as text avoids a float32
    /// intermediate and leaves parsing and rounding to SQL Server.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceIntoAWideFloat16Column()
    {
        const int Dimensions = 2000;

        using Table wideTable = new(_managementConnection, "VectorF16WideTable",
            $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector({Dimensions}, float16) NULL)");

        float[] values = new float[Dimensions];
        for (int i = 0; i < values.Length; i++)
        {
            // Eighths are exactly representable in binary16 at this magnitude.
            values[i] = (i % 8) * 0.125f;
        }

        DataTable source = new();
        source.Columns.Add(ColumnName, typeof(string));
        source.Rows.Add(JsonSerializer.Serialize(values));

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = wideTable.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(source);
        }

        using SqlCommand command =
            new($"SELECT TOP 1 {ColumnName} FROM {wideTable.Name} ORDER BY Id DESC", connection);
        using SqlDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());

        // Reading it back also exercises the widening path at a width beyond the float32
        // element limit.
        Assert.Equal(values, reader.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Verifies that a JSON string in a column declared as <see cref="object"/> is still
    /// sent as text. The declared CLR type does not identify it as a string.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceFromAnObjectTypedColumn()
    {
        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(object));
        table.Rows.Add("[1.5,2.5,3.5]");

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(table);
        }

        using SqlCommand command =
            new($"SELECT TOP 1 {ColumnName} FROM {_float16Table.Name} ORDER BY Id DESC", connection);
        using SqlDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], reader.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Leaves out-of-range JSON values to SQL Server instead of saturating on the client.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopyRejectsValuesOutsideTheFloat16Range()
    {
        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(string));
        table.Rows.Add("[70000,1,2]");

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name };
        bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);

        SqlException exception = Assert.Throws<SqlException>(() => bulkCopy.WriteToServer(table));
        Assert.Contains(exception.Number, s_float16OutOfRangeErrors);
    }

    /// <summary>
    /// Preserves source vector metadata when SQL Server converts the bulk stream to text,
    /// without comparing the vector's absent collation to the destination's collation.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("varchar(max)", false)]
    [InlineData("varchar(max)", true)]
    [InlineData("nvarchar(max)", false)]
    [InlineData("nvarchar(max)", true)]
    [InlineData("json", false)]
    [InlineData("json", true)]
    public async Task BulkCopiesNativeFloat16ToText(string destinationType, bool useAsync)
    {
        using Table target = new(_managementConnection, "VectorTextTarget",
            $"(Id int IDENTITY, {ColumnName} {destinationType} NULL)");
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        using SqlCommand command = new(
            $"SELECT CAST('[1.5,2.5,3.5]' AS vector(3,float16)) AS {ColumnName} " +
            "UNION ALL SELECT CAST(NULL AS vector(3,float16))", connection);
        using SqlDataReader source = command.ExecuteReader(CommandBehavior.SequentialAccess);
        using SqlBulkCopy copy = new(_connectionString) { DestinationTableName = target.Name };
        copy.ColumnMappings.Add(ColumnName, ColumnName);
        copy.EnableStreaming = true;
        if (useAsync)
        {
            await copy.WriteToServerAsync(source);
        }
        else
        {
            copy.WriteToServer(source);
        }

        using SqlCommand verify = new(
            $"SELECT CAST({ColumnName} AS varchar(max)) FROM {target.Name} ORDER BY Id",
            _managementConnection);
        using SqlDataReader reader = verify.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f],
            Assert.IsType<float[]>(JsonSerializer.Deserialize<float[]>(reader.GetString(0))));
        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(0));
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Rebuilds incoming metadata for each source while retaining cached destination schema.
    /// Leading nulls must not prevent selecting the typed column's native base type.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkCopyCachedMetadataAcceptsDifferentSourceRepresentations(bool useAsync)
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        using SqlBulkCopy copy = new(connection, SqlBulkCopyOptions.CacheMetadata, null)
        {
            DestinationTableName = _float16Table.Name,
            BatchSize = 1
        };
        copy.ColumnMappings.Add("source", ColumnName);
        using DataTable text = new();
        text.Columns.Add("source", typeof(object));
        text.Rows.Add(DBNull.Value);
        text.Rows.Add("[1.5,2.5,3.5]");
        using DataTable vectors = new();
        vectors.Columns.Add("source", typeof(SqlVector<float>));
        vectors.Rows.Add(DBNull.Value);
        vectors.Rows.Add(new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));
        foreach (DataTable table in new[] { text, vectors, text })
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
        using SqlCommand verify = new(
            $"SELECT COUNT(*), COUNT({ColumnName}) FROM {_float16Table.Name}", connection);
        using SqlDataReader reader = verify.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(6, reader.GetInt32(0));
        Assert.Equal(3, reader.GetInt32(1));
    }

    /// <summary>
    /// Preserves a v2 reader's native float16 payload when the destination connection
    /// reports the same SQL column as varchar under v1.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkCopiesNativeFloat16ToV1Destination(bool useAsync)
    {
        using SqlConnection source = new(_connectionString);
        source.Open();
        using SqlCommand select = new(
            $"SELECT CAST('[1.5,2.5,3.5]' AS vector(3,float16)) AS {ColumnName}", source);
        using SqlDataReader reader = select.ExecuteReader();
        string destination = new SqlConnectionStringBuilder(_connectionString)
        {
            VectorTypeSupport = SqlVectorTypeSupport.V1
        }.ConnectionString;
        using SqlBulkCopy copy = new(destination) { DestinationTableName = _float16Table.Name };
        copy.ColumnMappings.Add(ColumnName, ColumnName);
        if (useAsync)
        {
            await copy.WriteToServerAsync(reader);
        }
        else
        {
            copy.WriteToServer(reader);
        }
        using SqlDataReader verify = Select(_float16Table);
        Assert.True(verify.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], verify.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Compares identical JSON input through bulk copy and ordinary parameters so parsing
    /// and rounding remain server-owned, including values close to binary16 midpoints.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false)]
    [InlineData(true)]
    public async Task JsonBulkCopyMatchesServerRounding(bool useAsync)
    {
        string[] inputs =
        [
            "[1.00048828125000001,-1.00048828125000001,0.3333333333333333]",
            "[0.000000029802322387695313,65504,-65504]"
        ];
        using SqlConnection connection = new(_connectionString);
        connection.Open();
        using SqlBulkCopy copy = new(connection) { DestinationTableName = _float16Table.Name };
        copy.ColumnMappings.Add(ColumnName, ColumnName);
        foreach (string json in inputs)
        {
            using DataTable table = new();
            table.Columns.Add(ColumnName, typeof(string));
            table.Rows.Add(json);
            if (useAsync)
            {
                await copy.WriteToServerAsync(table);
            }
            else
            {
                copy.WriteToServer(table);
            }
            using SqlCommand verify = new(
                $"SELECT TOP 1 {ColumnName}, CAST(@json AS vector(3,float16)) " +
                $"FROM {_float16Table.Name} ORDER BY Id DESC", connection);
            verify.Parameters.Add("@json", SqlDbType.VarChar, -1).Value = json;
            using SqlDataReader reader = verify.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(reader.GetSqlBinary(1).Value, reader.GetSqlBinary(0).Value);
        }
    }

    /// <summary>
    /// Keeps the established client-side float32 JSON size check without restricting
    /// float16 JSON text, which is parsed by the server.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void JsonBulkCopyRejectsOversizedFloat32Payload()
    {
        using DataTable source = new();
        source.Columns.Add(ColumnName, typeof(string));
        source.Rows.Add(JsonSerializer.Serialize(new float[1999]));
        using SqlBulkCopy copy = new(_connectionString) { DestinationTableName = _float32Table.Name };
        copy.ColumnMappings.Add(ColumnName, ColumnName);
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => copy.WriteToServer(source));
        Assert.IsType<ArgumentOutOfRangeException>(error.GetBaseException());
    }

    #if NETFRAMEWORK
    /// <summary>
    /// Binds Framework's string representation as varchar(max), including wide values and
    /// null updates, instead of casting a JSON value to the native vector interface.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(3)]
    [InlineData(3000)]
    public void CommandBuilderUpdatesFloat16AsText(int dimensions)
    {
        using Table target = new(_managementConnection, "VectorAdapterTarget",
            $"(Id int PRIMARY KEY, {ColumnName} vector({dimensions},float16) NULL)");
        using (SqlCommand seed = new($"INSERT INTO {target.Name} VALUES(1,NULL)", _managementConnection))
        {
            seed.ExecuteNonQuery();
        }
        using SqlDataAdapter adapter = new($"SELECT Id,{ColumnName} FROM {target.Name}", _managementConnection);
        using SqlCommandBuilder builder = new(adapter) { ConflictOption = ConflictOption.OverwriteChanges };
        using DataTable data = new();
        adapter.Fill(data);
        adapter.UpdateCommand = builder.GetUpdateCommand();
        SqlParameter parameter = Assert.Single(adapter.UpdateCommand.Parameters.Cast<SqlParameter>(),
            p => p.SourceColumn == ColumnName);
        Assert.Equal(SqlDbType.VarChar, parameter.SqlDbType);
        Assert.Equal(-1, parameter.Size);
        data.Rows[0][ColumnName] = JsonSerializer.Serialize(new float[dimensions]);
        Assert.Equal(1, adapter.Update(data));
        using (SqlCommand verify = new($"SELECT {ColumnName} FROM {target.Name}", _managementConnection))
        using (SqlDataReader reader = verify.ExecuteReader())
        {
            Assert.True(reader.Read());
            Assert.Equal(dimensions, reader.GetSqlVector<float>(0).Length);
        }
        data.Rows[0][ColumnName] = DBNull.Value;
        Assert.Equal(1, adapter.Update(data));
    }
    #endif

    /// <summary>
    /// Verifies both output accessors honor the client's float32 or text binding to a
    /// float16 procedure parameter, rather than imposing a Framework-only return type.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task OutputParameterPreservesClientRepresentation(bool text, bool isNull, bool useAsync)
    {
        using StoredProcedure procedure = new(_managementConnection, "VectorOutputProcedure",
            "@value vector(3,float16) OUTPUT AS SET @value=" +
            (isNull ? "NULL" : "'[1.5,2.5,3.5]'"));
        using SqlCommand command = new(procedure.Name, _managementConnection)
        {
            CommandType = CommandType.StoredProcedure
        };
        SqlParameter parameter = command.Parameters.Add("@value",
            text ? SqlDbType.VarChar : SqlDbTypeExtensions.Vector);
        parameter.Direction = ParameterDirection.Output;
        if (text)
        {
            parameter.Size = -1;
        }
        else
        {
            parameter.Value = SqlVector<float>.CreateNull(3);
        }
        if (useAsync)
        {
            await command.ExecuteNonQueryAsync();
        }
        else
        {
            command.ExecuteNonQuery();
        }
        if (text)
        {
            Assert.Equal(isNull, Assert.IsType<SqlString>(parameter.SqlValue).IsNull);
            if (isNull)
            {
                Assert.Equal(DBNull.Value, parameter.Value);
            }
            else
            {
                Assert.Equal([1.5f, 2.5f, 3.5f],
                    Assert.IsType<float[]>(JsonSerializer.Deserialize<float[]>(
                        Assert.IsType<string>(parameter.Value))));
            }
        }
        else
        {
            SqlVector<float> value = Assert.IsType<SqlVector<float>>(parameter.Value);
            SqlVector<float> sqlValue = Assert.IsType<SqlVector<float>>(parameter.SqlValue);
            Assert.Equal(isNull, value.IsNull);
            Assert.Equal(isNull, sqlValue.IsNull);
            Assert.Equal(3, value.Length);
            Assert.Equal(value.Memory.ToArray(), sqlValue.Memory.ToArray());
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Inserts one native parameter value or SQL null into the requested test table.
    /// </summary>
    private void Insert(Table table, object value)
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new($"INSERT INTO {table.Name} ({ColumnName}) VALUES ({ParameterName})", connection);
        command.Parameters.Add(new SqlParameter(ParameterName, SqlDbTypeExtensions.Vector) { Value = value });

        Assert.Equal(1, command.ExecuteNonQuery());
    }

    /// <summary>
    /// Reads the newest vector and transfers connection ownership to the returned reader.
    /// </summary>
    private SqlDataReader Select(Table table)
    {
        SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new($"SELECT TOP 1 {ColumnName} FROM {table.Name} ORDER BY Id DESC", connection);

        return command.ExecuteReader(CommandBehavior.CloseConnection);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Reachable from the constructor with the fields still unset, so each drop is
        // null-tolerant and best-effort: failing to drop one object must not leak the others.
        DisposeSafely(_float16Table);
        DisposeSafely(_float32Table);
        _managementConnection?.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    private static void DisposeSafely(IDisposable? disposable)
    {
        try
        {
            disposable?.Dispose();
        }
        catch
        {
            // Best-effort cleanup; the object is named with a GUID so a leak is not a
            // correctness problem for other tests.
        }
    }

    #endregion
}
