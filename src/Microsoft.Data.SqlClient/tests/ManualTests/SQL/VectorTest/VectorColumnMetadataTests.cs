// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.VectorTest;

#nullable enable

/// <summary>
/// Tests vector column metadata and result representations. Float32 cases run against any
/// server with vector support; float16 cases require support for that base type.
/// </summary>
[Trait("Set", "3")]
public sealed class VectorColumnMetadataTests
{
    private readonly string _connectionString = DataTestUtility.TCPConnectionString;

    public static bool IsSupported => DataTestUtility.IsSqlVectorSupported;

    public static bool IsFloat16Supported => DataTestUtility.IsSqlVectorFloat16Supported;

    /// <summary>
    /// Uses native float16 metadata by default while preserving explicit V1 and Off text
    /// results, including the framework-specific CLR types and synchronous/asynchronous paths.
    /// </summary>
    /// <param name="setting">The requested version, or null to omit the keyword.</param>
    /// <param name="expectNative">Whether native vector metadata is expected.</param>
    /// <param name="useAsync">Whether to open, execute, and read asynchronously.</param>
    /// <returns>A task that completes after metadata and returned values have been verified.</returns>
    [ConditionalTheory(nameof(IsFloat16Supported))]
    [InlineData(null, true, false)]
    [InlineData(null, true, true)]
    [InlineData(SqlVectorTypeSupport.V1, false, false)]
    [InlineData(SqlVectorTypeSupport.V1, false, true)]
    [InlineData(SqlVectorTypeSupport.V2, true, false)]
    [InlineData(SqlVectorTypeSupport.V2, true, true)]
    [InlineData(SqlVectorTypeSupport.Off, false, false)]
    [InlineData(SqlVectorTypeSupport.Off, false, true)]
    public async Task DefaultVectorSupportAndTextOptOuts(
        SqlVectorTypeSupport? setting, bool expectNative, bool useAsync)
    {
        SqlConnectionStringBuilder builder = new(_connectionString);
        builder.Remove("Vector Type Support");
        if (setting.HasValue)
        {
            builder.VectorTypeSupport = setting.Value;
        }

        using SqlConnection connection = new(builder.ConnectionString);
        if (useAsync)
        {
            await connection.OpenAsync();
        }
        else
        {
            connection.Open();
        }

        using SqlCommand command = new(
            "SELECT CAST('[1.5,2.5,3.5]' AS vector(3, float16)) AS v", connection);
        using SqlDataReader reader = useAsync
            ? await command.ExecuteReaderAsync()
            : command.ExecuteReader();
        DbColumn column = reader.GetColumnSchema()[0];
        Assert.Equal(expectNative ? "float16" : null, column["VectorBaseType"]);
        Assert.Equal(expectNative ? (int?)3 : null, column["VectorDimensions"]);

        Type expectedType = typeof(string);
        #if NET
        if (expectNative)
        {
            expectedType = typeof(Microsoft.Data.SqlTypes.SqlVector<Half>);
        }
        #endif
        Assert.Equal(expectedType, reader.GetFieldType(0));
        Assert.True(useAsync ? await reader.ReadAsync() : reader.Read());
        Assert.IsType(expectedType, reader.GetValue(0));
        Assert.Equal(
            new[] { 1.5f, 2.5f, 3.5f },
            JsonSerializer.Deserialize<float[]>(reader.GetString(0)));
        Assert.False(useAsync ? await reader.ReadAsync() : reader.Read());
    }

    /// <summary>
    /// Verifies that a vector column reports its base type and dimension count under their
    /// own property names. Both are encoded indirectly on the wire: the base type as the
    /// numeric scale and the dimension count derived from the column size, so this is the
    /// contract that spares callers from knowing that encoding.
    /// </summary>
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

    /// <summary>
    /// Verifies that the vector properties read as null for columns which are not vectors,
    /// so that a caller can probe any column without having to guard the call.
    /// </summary>
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

    /// <summary>
    /// Verifies that adding the vector properties leaves the standard column schema
    /// properties intact, and that an unrecognised property name still returns null rather
    /// than throwing. This guards against the new properties disturbing the existing
    /// indexer contract.
    /// </summary>
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

    /// <summary>
    /// Verifies that a connection which negotiated vector support reports the type in the
    /// DataTypes schema collection, with the provider type and create format a caller would
    /// use to declare a column.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void SchemaCollectionIncludesVectorType()
    {
        string connectionString = new SqlConnectionStringBuilder(_connectionString)
        {
            VectorTypeSupport = SqlVectorTypeSupport.V1
        }.ConnectionString;
        using SqlConnection connection = new(connectionString);
        connection.Open();

        DataRow? vectorRow = FindVectorType(connection);

        Assert.NotNull(vectorRow);
        Assert.Equal((int)SqlDbTypeExtensions.Vector, vectorRow!["ProviderDbType"]);
        Assert.Equal("vector({0})", vectorRow["CreateFormat"]);
        Assert.Equal("dimensions", vectorRow["CreateParameters"]);
    }

    /// <summary>
    /// Exposes the base-type creation argument at V2 and verifies both resulting declarations.
    /// </summary>
    /// <param name="baseType">The requested base type.</param>
    [ConditionalTheory(nameof(IsFloat16Supported))]
    [InlineData("float32")]
    [InlineData("float16")]
    public void V2CreationFormatIncludesBaseType(string baseType)
    {
        using SqlConnection connection = new(DataTestUtility.VectorFloat16ConnectionString);
        connection.Open();
        DataRow? vector = FindVectorType(connection);
        Assert.NotNull(vector);
        Assert.Equal("vector({0}, {1})", vector["CreateFormat"]);
        Assert.Equal("dimensions,base_type", vector["CreateParameters"]);
        string declaration = string.Format((string)vector["CreateFormat"], 3, baseType);
        using Table table = new(connection, "VectorCreationFormat", $"(v {declaration} NULL)");
        using SqlCommand command = new($"SELECT v FROM {table.Name}", connection);
        using SqlDataReader reader = command.ExecuteReader();
        DbColumn column = reader.GetColumnSchema()[0];
        Assert.Equal(baseType, column["VectorBaseType"]);
        Assert.Equal(3, column["VectorDimensions"]);
    }

    /// <summary>
    /// Verifies that a connection which opted out of vector support does not report the
    /// type, because it reads vector columns as <c>varchar(max)</c> and so has no vector
    /// type to offer. Paired with the test above, this pins the collection to what the
    /// connection negotiated rather than to the version the server reports, which is what
    /// makes it correct on Azure SQL.
    /// </summary>
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

    /// <summary>
    /// Finds the vector row in a connection's DataTypes schema collection.
    /// </summary>
    /// <param name="connection">An open connection whose schema is read.</param>
    /// <returns>The vector row, or <see langword="null"/> when the type is not reported.</returns>
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

    /// <summary>
    /// Verifies the use case the properties exist for: choosing a read path without knowing
    /// the schema in advance. <c>GetFieldType</c> is not enough on its own, because it
    /// reports string for a float16 column on .NET Framework just as it does for a varchar
    /// one, and cannot distinguish the two base types at all for a caller which wants to
    /// read both through a single representation.
    /// </summary>
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
        int rowsRead = 0;

        while (reader.Read())
        {
            // Widening from float16 is exact, so single precision reads both base types
            // without loss, and is the only option where System.Half is unavailable.
            reader.GetSqlVector<float>(0).Memory.Span.CopyTo(buffer);

            Assert.Equal(new float[] { 1.5f, 2.5f, 3.5f }, buffer);
            rowsRead++;
        }

        Assert.Equal(2, rowsRead);
    }
}
