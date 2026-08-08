// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
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

    private readonly string _connectionString = DataTestUtility.TCPConnectionString;
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
        Assert.Equal("[65504,1,2]", reader.GetValue(0).ToString());
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
        // The server reports the failure rather than silently saturating the value.
        SqlException exception = Assert.Throws<SqlException>(() =>
            Insert(_float16Table, new SqlVector<float>(new float[] { 70000f, 1f, 2f })));

        Assert.NotEmpty(exception.Message);
    }

    #endregion

    #region Bulk copy across base types

    [ConditionalTheory(nameof(IsSupported))]
    [InlineData("float16", "float32")]
    [InlineData("float32", "float16")]
    [InlineData("float16", "float16")]
    public void BulkCopiesBetweenColumnsOfEitherBaseType(string sourceBaseType, string destinationBaseType)
    {
        // Unlike a parameter, a bulk copy states the destination's base type in the
        // INSERT BULK statement, so the driver converts the payload rather than relying on
        // the server, which rejects a size mismatch instead of converting it.
        Table source = sourceBaseType == "float16" ? _float16Table : _float32Table;
        Table destination = destinationBaseType == "float16" ? _float16Table : _float32Table;

        Insert(source, new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f }));

        using SqlConnection sourceConnection = new(_connectionString);
        sourceConnection.Open();
        using SqlCommand selectCommand = new($"SELECT {ColumnName} FROM {source.Name}", sourceConnection);
        using SqlDataReader sourceReader = selectCommand.ExecuteReader();

        using SqlConnection destinationConnection = new(_connectionString);
        destinationConnection.Open();

        using (SqlBulkCopy bulkCopy = new(destinationConnection) { DestinationTableName = destination.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(sourceReader);
        }

        using SqlCommand verifyCommand =
            new($"SELECT TOP 1 {ColumnName} FROM {destination.Name} ORDER BY Id DESC", destinationConnection);
        using SqlDataReader verifyReader = verifyCommand.ExecuteReader();

        Assert.True(verifyReader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], verifyReader.GetSqlVector<float>(0).Memory.ToArray());
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
    public void BulkCopyRejectsValuesOutsideTheFloat16Range()
    {
        DataTable table = new();
        table.Columns.Add(ColumnName, typeof(SqlVector<float>));
        table.Rows.Add(new SqlVector<float>(new float[] { 70000f, 1f, 2f }));

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name };
        bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);

        Assert.Throws<SqlException>(() => bulkCopy.WriteToServer(table));
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
