// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
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
    /// The server's error for a float32 value which cannot be narrowed to float16.
    /// </summary>
    private const int Float32ToFloat16OutOfRangeError = 42284;

    private readonly string _connectionString = DataTestUtility.VectorFloat16ConnectionString;
    private readonly SqlConnection _managementConnection;
    private readonly Table _float16Table;
    private readonly Table _float32Table;
    private bool _disposed;

    public VectorFloat16BehaviourTests()
    {
        _managementConnection = new SqlConnection(_connectionString);
        _managementConnection.Open();

        _float16Table = new Table(_managementConnection, "VectorF16BehaviourTable",
            $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector(3, float16) NULL)");
        _float32Table = new Table(_managementConnection, "VectorF32BehaviourTable",
            $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector(3, float32) NULL)");
    }

    public static bool IsSupported => DataTestUtility.IsSqlVectorFloat16Supported;

    #region Column metadata

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

    [ConditionalFact(nameof(IsSupported))]
    public void ReportsUnsupportedElementTypes()
    {
        Insert(_float16Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlDataReader reader = Select(_float16Table);
        Assert.True(reader.Read());

        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<double>(0));
        Assert.Throws<NotSupportedException>(() => reader.GetSqlVector<int>(0));
    }

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

    [ConditionalFact(nameof(IsSupported))]
    public void RejectsValuesOutsideTheFloat16Range()
    {
        // The value is sent as float32 and narrowed by the server, which reports the
        // overflow rather than silently saturating.
        SqlException exception = Assert.Throws<SqlException>(() =>
            Insert(_float16Table, new SqlVector<float>(new float[] { 70000f, 1f, 2f })));

        Assert.Equal(Float32ToFloat16OutOfRangeError, exception.Number);
    }

    #endregion

    #region Bulk copy across base types

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

    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("float32", "float16")]
    #if NET
    // On .NET Framework a float16 column reads as text, so this pairing takes the textual
    // path and the value is converted rather than rejected.
    [InlineData("float16", "float32")]
    #endif
    public void BulkCopyRejectsColumnsOfDifferentBaseTypes(string sourceBaseType, string destinationBaseType)
    {
        // A payload read from a vector column keeps its own base type, and the INSERT BULK
        // declaration states the destination's, so the server reports the mismatch. The
        // driver does not silently rewrite the payload: a caller which wants the conversion
        // reads the source column as text, which the server converts.
        Table source = sourceBaseType == "float16" ? _float16Table : _float32Table;
        Table destination = destinationBaseType == "float16" ? _float16Table : _float32Table;

        Insert(source, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlConnection sourceConnection = new(_connectionString);
        sourceConnection.Open();
        using SqlCommand selectCommand = new($"SELECT {ColumnName} FROM {source.Name}", sourceConnection);
        using SqlDataReader sourceReader = selectCommand.ExecuteReader();

        using SqlConnection destinationConnection = new(_connectionString);
        destinationConnection.Open();

        using SqlBulkCopy bulkCopy = new(destinationConnection) { DestinationTableName = destination.Name };
        bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);

        Assert.Throws<SqlException>(() => bulkCopy.WriteToServer(sourceReader));
    }

    #if !NET
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesFloat16ToFloat32ThroughTheTextualRepresentation()
    {
        // On .NET Framework a float16 column reads as a JSON string, so a copy into a
        // float32 column takes the textual path and the value is converted rather than
        // rejected. This is the counterpart of the .NET case above.
        Insert(_float16Table, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlConnection sourceConnection = new(_connectionString);
        sourceConnection.Open();
        using SqlCommand selectCommand = new($"SELECT {ColumnName} FROM {_float16Table.Name}", sourceConnection);
        using SqlDataReader sourceReader = selectCommand.ExecuteReader();

        using SqlConnection destinationConnection = new(_connectionString);
        destinationConnection.Open();

        using (SqlBulkCopy bulkCopy = new(destinationConnection) { DestinationTableName = _float32Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(sourceReader);
        }

        using SqlCommand verifyCommand =
            new($"SELECT TOP 1 {ColumnName} FROM {_float32Table.Name} ORDER BY Id DESC", destinationConnection);
        using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

        Assert.True(verifyReader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], verifyReader.GetSqlVector<float>(0).Memory.ToArray());
    }
    #endif

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

    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceIntoFloat16Column()
    {
        // A float16 column reads back as a JSON string where System.Half is unavailable, so
        // this is the ordinary table to table path on those frameworks.
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

    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopiesJsonStringSourceFromANonSqlClientReader()
    {
        // The source column type has to be read through IDataReader rather than through the
        // SqlDataReader field, which is null unless the reader is a SqlDataReader. A reader
        // from another provider still reports a string column, so the value is parsed into
        // the destination's base type as it is for any other textual source.
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

        // A textual source is parsed into the destination's base type by the client, so the
        // client reports the overflow itself rather than letting the saturated infinity
        // reach the server, which would reject it as a malformed vector instead. Bulk copy
        // wraps the failure to name the column and row.
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => bulkCopy.WriteToServer(table));

        OverflowException overflow = Assert.IsType<OverflowException>(exception.InnerException);
        Assert.Contains("float16", overflow.Message);
        Assert.Contains("70000", overflow.Message);
    }

    #endregion

    #region Helpers

    private void Insert(Table table, object value)
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new($"INSERT INTO {table.Name} ({ColumnName}) VALUES ({ParameterName})", connection);
        command.Parameters.Add(new SqlParameter(ParameterName, SqlDbTypeExtensions.Vector) { Value = value });

        Assert.Equal(1, command.ExecuteNonQuery());
    }

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

        _float16Table.Dispose();
        _float32Table.Dispose();
        _managementConnection.Dispose();
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}
