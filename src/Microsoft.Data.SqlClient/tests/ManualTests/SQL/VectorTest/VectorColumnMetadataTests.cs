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

    public static bool IsFloat16Supported => DataTestUtility.IsSqlVectorFloat16Supported;

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

        DataRow? vectorRow = FindVectorType(connection);

        Assert.NotNull(vectorRow);
        Assert.Equal((int)SqlDbTypeExtensions.Vector, vectorRow!["ProviderDbType"]);
        Assert.Equal("vector({0})", vectorRow["CreateFormat"]);
    }

    [ConditionalFact(nameof(IsSupported))]
    public void SchemaCollectionOmitsVectorTypeWhenItIsNotNegotiated()
    {
        // The type is reported according to what the connection negotiated, not according
        // to the version the server reports: Azure SQL reports 12.00 whatever it supports.
        // A connection which opted out reads vector columns as varchar(max), so it has no
        // vector type to report.
        string connectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            VectorTypeSupport = SqlVectorTypeSupport.Off
        }.ConnectionString;

        using SqlConnection connection = new(connectionString);
        connection.Open();

        Assert.Null(FindVectorType(connection));
    }

    private static DataRow? FindVectorType(SqlConnection connection)
    {
        foreach (DataRow row in connection.GetSchema("DataTypes").Rows)
        {
            if (string.Equals(row["TypeName"]?.ToString(), "vector", StringComparison.OrdinalIgnoreCase))
            {
                return row;
            }
        }

        return null;
    }

    [ConditionalFact(nameof(IsFloat16Supported))]
    public void DrivesReadPathForACallerWhichDoesNotKnowTheSchema()
    {
        // This is the use case the properties exist for. GetFieldType is not enough on its
        // own: it reports string for a float16 column on .NET Framework, which is what a
        // varchar column reports too, and it cannot distinguish the two base types at all
        // for a caller which wants to read both through one representation.
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        connection.Open();

        using SqlCommand command = new(
            @"SELECT CAST('[1.5,2.5,3.5]' AS vector(3, float16)) AS v
              UNION ALL
              SELECT CAST('[1.5,2.5,3.5]' AS vector(3, float16))", connection);
        using SqlDataReader reader = command.ExecuteReader();

        DbColumn column = reader.GetColumnSchema()[0];
        string baseType = Assert.IsType<string>(column["VectorBaseType"]);
        int dimensions = Assert.IsType<int>(column["VectorDimensions"]);

        Assert.Equal("float16", baseType);
        Assert.Equal(3, dimensions);

        // The dimension count is known before any row is read, so a buffer can be sized once.
        float[] buffer = new float[dimensions];

        while (reader.Read())
        {
            Assert.Contains(baseType, new[] { "float16", "float32" });

            // Widening from float16 is exact, so single precision reads both base types
            // without loss, and is the only option where System.Half is unavailable.
            reader.GetSqlVector<float>(0).Memory.Span.CopyTo(buffer);

            Assert.Equal(new float[] { 1.5f, 2.5f, 3.5f }, buffer);
        }
    }
}
