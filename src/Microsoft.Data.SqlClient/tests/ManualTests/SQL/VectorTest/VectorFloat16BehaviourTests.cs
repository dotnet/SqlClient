// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
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

    /// <summary>
    /// Whether the server converts between vector base types. Some builds block it and report
    /// error 42238, in which case a value can only be written to a column whose base type
    /// matches its own.
    /// </summary>
    public static bool ServerConvertsBaseTypes => DataTestUtility.IsSqlVectorBaseTypeConversionSupported;

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
        // A vector of either base type can be written to a column of either base type, where
        // the server converts between them. Whether it does is server dependent, which is
        // what ServerConvertsBaseTypes guards.
        yield return ["float16", new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f })];
        yield return ["float32", new SqlVector<float>(new float[] { 1.5f, 2.5f, 3.5f })];
        #if NET
        yield return ["float16", new SqlVector<Half>(new Half[] { (Half)1.5f, (Half)2.5f, (Half)3.5f })];
        yield return ["float32", new SqlVector<Half>(new Half[] { (Half)1.5f, (Half)2.5f, (Half)3.5f })];
        #endif
    }

    /// <summary>
    /// Verifies that a vector parameter can be written to a column of either base type, with
    /// the server performing the conversion. Skipped where the server blocks conversion
    /// between base types, in which case a JSON string is the only portable way to write a
    /// column whose base type differs from the value's.
    /// </summary>
    [ConditionalTheory(nameof(IsSupported), nameof(ServerConvertsBaseTypes))]
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
    /// Verifies that a float32 value which cannot be represented in float16 is reported
    /// rather than silently saturated. The value is sent as float32 and narrowed by the
    /// server, so this depends on the server converting between base types.
    /// </summary>
    [ConditionalFact(nameof(IsSupported), nameof(ServerConvertsBaseTypes))]
    public void RejectsValuesOutsideTheFloat16Range()
    {
        // The value is sent as float32 and narrowed by the server, which reports the
        // overflow rather than silently saturating.
        SqlException exception = Assert.Throws<SqlException>(() =>
            Insert(_float16Table, new SqlVector<float>(new float[] { 70000f, 1f, 2f })));

        Assert.Contains(exception.Number, s_float16OutOfRangeErrors);
    }

    #endregion

    #region Feature extension negotiation

    /// <summary>
    /// Verifies that writing a native float16 parameter on a connection which did not
    /// negotiate the base type is rejected by the driver, naming the keyword which enables
    /// it. Without this the server either stores a value the same connection would read back
    /// as a JSON string, or reports the protocol stream as malformed, neither of which tells
    /// the caller what to change. The JDBC and ODBC drivers reject it on the client too.
    /// </summary>
    /// <param name="setting">The level to request, or null to leave the keyword unset.</param>
    #if NET
    [ConditionalTheory(nameof(IsSupported))]
    // The default is v1, so an application which says nothing is covered too.
    [InlineData(null)]
    [InlineData(SqlVectorTypeSupport.V1)]
    [InlineData(SqlVectorTypeSupport.Off)]
    public void RejectsNativeFloat16ParameterBelowTheNegotiatedVersion(SqlVectorTypeSupport? setting)
    {
        using SqlConnection connection = OpenAt(setting);

        using SqlCommand command =
            new($"INSERT INTO {_float16Table.Name} ({ColumnName}) VALUES ({ParameterName})", connection);
        command.Parameters.AddWithValue(
            ParameterName, new SqlVector<Half>(new[] { (Half)1.5f, (Half)2.5f, (Half)3.5f }));

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());

        Assert.Contains("float16", exception.Message);
        Assert.Contains("Vector Type Support", exception.Message);
        Assert.Contains("V2", exception.Message);
    }

    /// <summary>
    /// Verifies that a bulk copy into a float16 column still works on a connection which did
    /// not negotiate float16. Such a connection is told the column is a <c>varchar(max)</c>,
    /// so the value travels as text and the server converts it — the column is never a vector
    /// from the client's point of view, and the base type check does not arise. This is what
    /// makes the default level usable for loading float16 data.
    /// </summary>
    /// <param name="setting">The level to request, or null to leave the keyword unset.</param>
    [ConditionalTheory(nameof(IsSupported))]
    [InlineData(null)]
    [InlineData(SqlVectorTypeSupport.V1)]
    [InlineData(SqlVectorTypeSupport.Off)]
    public void BulkCopiesIntoFloat16AsTextBelowTheNegotiatedVersion(SqlVectorTypeSupport? setting)
    {
        DataTable source = new();
        source.Columns.Add(ColumnName, typeof(string));
        source.Rows.Add("[1.5,2.5,3.5]");

        using SqlConnection connection = OpenAt(setting);

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = _float16Table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(source);
        }

        // Read back over a v2 connection, which sees the column as a vector.
        using SqlDataReader reader = Select(_float16Table);
        Assert.True(reader.Read());
        Assert.Equal([1.5f, 2.5f, 3.5f], reader.GetSqlVector<float>(0).Memory.ToArray());
    }

    /// <summary>
    /// Opens a connection requesting a given level of vector type support.
    /// </summary>
    /// <param name="setting">The level to request, or null to leave the keyword unset.</param>
    /// <returns>An open connection.</returns>
    private static SqlConnection OpenAt(SqlVectorTypeSupport? setting)
    {
        SqlConnectionStringBuilder builder = new(DataTestUtility.TCPConnectionString);

        if (setting.HasValue)
        {
            builder.VectorTypeSupport = setting.Value;
        }

        SqlConnection connection = new(builder.ConnectionString);
        connection.Open();
        return connection;
    }
    #endif

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

    /// <summary>
    /// Verifies that a JSON string can be bulk copied into a float16 column declaring more
    /// dimensions than can be sent as float32. The value is parsed into single precision
    /// before being rewritten to the destination's base type, and that intermediate must not
    /// be held to the float32 element limit: 2000 elements exceed what float32 can send, but
    /// are well within float16's 3996.
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
    /// parsed into the destination's base type. The declared type reports nothing useful
    /// here, so the value itself has to be examined; otherwise the float32 payload produced
    /// by coercion would reach a float16 column at the wrong width and the server would
    /// reject the copy.
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
    /// Verifies that the element count limit is still enforced for a float32 destination,
    /// which accepts half as many elements as a float16 one. The JSON intermediate is always
    /// float32 and is built without the limit so that a wide float16 column can be loaded, so
    /// the limit has to be applied against the destination's base type instead. Without it an
    /// oversized payload would reach the server and come back as a column length error.
    /// </summary>
    [ConditionalFact(nameof(IsSupported))]
    public void BulkCopyRejectsAJsonSourceWiderThanTheFloat32Limit()
    {
        // 1998 is the most a float32 vector column can declare; the source supplies more.
        using Table wideFloat32Table = new(_managementConnection, "VectorF32WideTable",
            $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector(1998, float32) NULL)");

        DataTable source = new();
        source.Columns.Add(ColumnName, typeof(string));
        source.Rows.Add(JsonSerializer.Serialize(new float[2000]));

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = wideFloat32Table.Name };
        bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);

        // Bulk copy wraps the failure to name the column and row.
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(() => bulkCopy.WriteToServer(source));

        Assert.IsType<ArgumentOutOfRangeException>(exception.InnerException);
    }

    /// <summary>
    /// Verifies that a JSON value near a binary16 tie is stored identically whether it is
    /// bulk copied or inserted through a literal. A bulk copy parses the text to float32 and
    /// narrows it on the client, so the value is rounded twice; an INSERT leaves the text to
    /// the server. If the server rounded once, the same JSON would give different stored
    /// values for the two paths.
    /// </summary>
    /// <param name="literal">A JSON array holding one value at or near a binary16 tie.</param>
    [ConditionalTheory(nameof(IsSupported))]
    // Exactly the tie between 1.0 and 1.0009765625, which ties-to-even resolves downwards.
    [InlineData("[1.00048828125]")]
    // Just above that tie in decimal, but the nearest float32 is the tie itself.
    [InlineData("[1.00048828125000001]")]
    // The tie between 1.0009765625 and 1.001953125, which resolves upwards.
    [InlineData("[1.00146484375]")]
    // Half of the smallest subnormal, and just above it.
    [InlineData("[2.98023223876953125e-8]")]
    [InlineData("[3.0e-8]")]
    public void BulkCopyRoundsTheSameWayAsTheServer(string literal)
    {
        using Table table = new(_managementConnection, "VectorF16RoundingTable",
            $"(Id INT PRIMARY KEY IDENTITY, {ColumnName} vector(1, float16) NULL)");

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        // The server parses the literal itself.
        using (SqlCommand insert =
            new($"INSERT INTO {table.Name} ({ColumnName}) VALUES ('{literal}')", connection))
        {
            insert.ExecuteNonQuery();
        }

        float[] viaServer = ReadLast(connection, table);

        // The client parses the same text and narrows it before sending.
        DataTable source = new();
        source.Columns.Add(ColumnName, typeof(string));
        source.Rows.Add(literal);

        using (SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = table.Name })
        {
            bulkCopy.ColumnMappings.Add(ColumnName, ColumnName);
            bulkCopy.WriteToServer(source);
        }

        float[] viaClient = ReadLast(connection, table);

        Assert.Equal(viaServer, viaClient);
    }

    /// <summary>
    /// Reads the most recently inserted vector from a table as single precision values.
    /// </summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="table">The table to read from.</param>
    /// <returns>The elements of the last row's vector.</returns>
    private static float[] ReadLast(SqlConnection connection, Table table)
    {
        using SqlCommand command =
            new($"SELECT TOP 1 {ColumnName} FROM {table.Name} ORDER BY Id DESC", connection);
        using SqlDataReader reader = command.ExecuteReader();

        Assert.True(reader.Read());
        return reader.GetSqlVector<float>(0).Memory.ToArray();
    }

    /// <summary>
    /// Verifies that a JSON string can be used as a vector parameter, which is how a
    /// <see cref="System.Data.Common.DbDataAdapter"/> update of a float16 column arrives on
    /// .NET Framework: the column has no <c>System.Half</c> to be surfaced as, so it is read
    /// as a string, and <see cref="SqlCommandBuilder"/> pairs that string with
    /// <c>SqlDbType.Vector</c> taken from the column's provider type.
    /// </summary>
    /// <remarks>
    /// Concurrency matches on the key alone because a vector column cannot appear in a WHERE
    /// clause, which is a server restriction rather than anything to do with the base type.
    /// <para>
    /// .NET Framework only. On .NET the column is surfaced as <c>SqlVector&lt;Half&gt;</c>,
    /// which <see cref="DataTable"/> stores through <c>SqlUdtStorage</c> and then refuses to
    /// assign; that is a limitation of <see cref="DataTable"/> which applies equally to
    /// <c>SqlVector&lt;float&gt;</c> and so predates this base type.
    /// </para>
    /// </remarks>
    #if !NET
    [ConditionalFact(nameof(IsSupported))]
    public void UpdatesAFloat16ColumnThroughADataAdapter()
    {
        using Table table = new(_managementConnection, "VectorF16AdapterTable",
            $"(Id INT PRIMARY KEY, {ColumnName} vector(3, float16) NULL)");

        using SqlConnection connection = new(_connectionString);
        connection.Open();

        using (SqlCommand seed =
            new($"INSERT INTO {table.Name} VALUES (1, '[1.5,2.5,3.5]')", connection))
        {
            seed.ExecuteNonQuery();
        }

        using SqlDataAdapter adapter = new($"SELECT Id, {ColumnName} FROM {table.Name}", connection);
        using SqlCommandBuilder builder = new(adapter)
        {
            ConflictOption = ConflictOption.OverwriteChanges
        };

        DataTable rows = new();
        adapter.Fill(rows);

        // No System.Half, so the column is surfaced as a JSON string and SqlCommandBuilder
        // pairs that string with SqlDbType.Vector. This is the case being guarded.
        rows.Rows[0][ColumnName] = "[4.5,5.5,6.5]";

        adapter.Update(rows);

        Assert.Equal([4.5f, 5.5f, 6.5f], ReadLast(connection, table));
    }
    #endif

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
