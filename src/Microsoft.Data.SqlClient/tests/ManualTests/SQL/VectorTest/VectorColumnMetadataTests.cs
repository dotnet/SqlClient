// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

/// <summary>
/// Tests for the metadata a vector column reports. This applies to every base type, so the
/// tests here use float32 and run against any server which supports vectors.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorColumnMetadataTests
{
    private readonly string _connectionString = DataTestUtility.TCPConnectionString;

    public static bool IsSupported => DataTestUtility.IsSqlVectorSupported;

    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(1998)]
    public void ReportsBaseTypeAndDimensions(int dimensions)
    {
        // A vector column carries its base type as the numeric scale, and its dimension count
        // has to be derived from the column size. Both are surfaced under their own names so
        // that callers do not have to know that encoding.
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new($"SELECT CAST(NULL AS vector({dimensions}, float32)) AS v", connection);
        using SqlDataReader reader = command.ExecuteReader();

        DbColumn column = reader.GetColumnSchema()[0];

        Assert.Equal("float32", column["VectorBaseType"]);
        Assert.Equal(dimensions, column["VectorDimensions"]);
    }

    [ConditionalFact(nameof(IsSupported))]
    public void ReportsNullForNonVectorColumns()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new("SELECT CAST('abc' AS varchar(10)) AS s, CAST(1 AS int) AS i", connection);
        using SqlDataReader reader = command.ExecuteReader();

        foreach (DbColumn column in reader.GetColumnSchema())
        {
            Assert.Null(column["VectorBaseType"]);
            Assert.Null(column["VectorDimensions"]);
        }
    }

    [ConditionalFact(nameof(IsSupported))]
    public void ReportsStandardPropertiesAlongsideVectorProperties()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlCommand command =
            new("SELECT CAST(NULL AS vector(3, float32)) AS v", connection);
        using SqlDataReader reader = command.ExecuteReader();

        DbColumn column = reader.GetColumnSchema()[0];

        Assert.Equal("v", column.ColumnName);
        Assert.Equal("vector", column.DataTypeName);

        // An unrecognised property name continues to return null rather than throwing.
        Assert.Null(column["NoSuchProperty"]);
    }

    [ConditionalFact(nameof(IsSupported))]
    public void SchemaCollectionIncludesVectorType()
    {
        using SqlConnection connection = new(_connectionString);
        connection.Open();

        DataTable dataTypes = connection.GetSchema("DataTypes");
        DataRow? vectorRow = null;

        foreach (DataRow row in dataTypes.Rows)
        {
            if (string.Equals(row["TypeName"]?.ToString(), "vector", StringComparison.OrdinalIgnoreCase))
            {
                vectorRow = row;
                break;
            }
        }

        Assert.NotNull(vectorRow);
        Assert.Equal((int)SqlDbTypeExtensions.Vector, vectorRow!["ProviderDbType"]);
        Assert.Equal("vector({0})", vectorRow["CreateFormat"]);
    }
}
