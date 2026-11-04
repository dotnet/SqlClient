// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlTypes;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies bulk-copy metadata boundaries without a SQL Server.</summary>
/// <remarks>
/// Some tests change the cached EnableBulkCopyCatalogMetadataDiscovery switch, so this class
/// runs in the serialized AppContext switch collection.
/// </remarks>
[Collection(AppContextSwitchTestCollection.Name)]
public sealed class SqlBulkCopyTests
{
    /// <summary>Supplies native vectors with both reader SQL-type classifications.</summary>
    public static TheoryData<object, bool> NativeVectorJsonInputs
    {
        get
        {
            TheoryData<object, bool> data = new()
            {
                { new SqlVector<float>(new[] { 1.5f }), false },
                { new SqlVector<float>(new[] { 1.5f }), true }
            };
            #if NET
            data.Add(new SqlVector<Half>(new[] { (Half)1.5f }), false);
            data.Add(new SqlVector<Half>(new[] { (Half)1.5f }), true);
            #endif
            return data;
        }
    }

    /// <summary>Supplies existing JSON text and SQL-null representations.</summary>
    public static TheoryData<object, bool, bool> JsonInputs => new()
    {
        { "[1.5]", false, false },
        { new SqlString("[1.5]"), false, true },
        { DBNull.Value, true, false },
        { SqlString.Null, true, true },
        { SqlVector<float>.CreateNull(1), true, true }
    };

    /// <summary>Rejects vectors before the JSON writer or Debug-only representation assertions.</summary>
    /// <param name="value">The native vector value.</param>
    /// <param name="isSqlType">The source reader's SQL-type classification.</param>
    [Theory]
    [MemberData(nameof(NativeVectorJsonInputs))]
    public void ConvertValue_NativeVectorForJson_ThrowsConversionError(object value, bool isSqlType)
    {
        // Arrange
        using SqlBulkCopy copy = new("Server=<server>");
        TdsColumnMetadata metadata = new(0)
        {
            column = "v",
            DbType = SqlDbTypeExtensions.Json,
            TdsType = TdsEnums.SQLJSON,
            MetaType = MetaType.s_MetaJson,
            IsNullable = true
        };
        MethodInfo? method = typeof(SqlBulkCopy).GetMethod(
            "ConvertValue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object[] arguments = { value, metadata, false, isSqlType, false };

        // Act
        Action convert = () => method.Invoke(copy, arguments);

        // Assert
        TargetInvocationException error = Assert.Throws<TargetInvocationException>(convert);
        Assert.IsType<InvalidOperationException>(error.InnerException);
        Assert.IsType<InvalidCastException>(error.GetBaseException());
    }

    /// <summary>Preserves existing JSON input representations and null handling.</summary>
    /// <param name="value">The source value.</param>
    /// <param name="isNull">Whether the source reports SQL NULL.</param>
    /// <param name="isSqlType">The source reader's SQL-type classification.</param>
    [Theory]
    [MemberData(nameof(JsonInputs))]
    public void ConvertValue_JsonInput_PreservesValue(object value, bool isNull, bool isSqlType)
    {
        // Arrange
        using SqlBulkCopy copy = new("Server=<server>");
        TdsColumnMetadata metadata = new(0)
        {
            column = "v",
            DbType = SqlDbTypeExtensions.Json,
            TdsType = TdsEnums.SQLJSON,
            MetaType = MetaType.s_MetaJson,
            IsNullable = true
        };
        MethodInfo? method = typeof(SqlBulkCopy).GetMethod(
            "ConvertValue", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object[] arguments = { value, metadata, isNull, isSqlType, false };

        // Act
        object? result = method.Invoke(copy, arguments);

        // Assert
        Assert.Same(value, result);
        Assert.Equal(isSqlType, arguments[3]);
        Assert.Equal(false, arguments[4]);
    }

    /// <summary>Leaves JSON destinations unchanged without consuming the source reader.</summary>
    /// <param name="sourceType">The reader's declared CLR field type.</param>
    /// <param name="encrypted">Whether the destination metadata is encrypted.</param>
    [Theory]
    [InlineData(typeof(SqlVector<float>), false)]
    [InlineData(typeof(SqlVector<float>), true)]
    [InlineData(typeof(string), false)]
    #if NET
    [InlineData(typeof(SqlVector<Half>), false)]
    [InlineData(typeof(SqlVector<Half>), true)]
    #endif
    public void GetVectorBulkMetadata_JsonDestination_PreservesMetadata(
        Type sourceType, bool encrypted)
    {
        // Arrange
        using DataTable table = new();
        table.Columns.Add("v", sourceType);
        table.Rows.Add(DBNull.Value);
        using DataTableReader reader = table.CreateDataReader();
        using SqlBulkCopy copy = new("Server=<server>");

        // Establish source metadata without opening a server connection.
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo? rowSource = typeof(SqlBulkCopy).GetField("_rowSource", Flags);
        FieldInfo? rowSourceType = typeof(SqlBulkCopy).GetField("_rowSourceType", Flags);
        MethodInfo? getMetadata = typeof(SqlBulkCopy).GetMethod("GetVectorBulkMetadata", Flags);
        Assert.NotNull(rowSource);
        Assert.NotNull(rowSourceType);
        Assert.NotNull(getMetadata);
        rowSource.SetValue(copy, reader);
        rowSourceType.SetValue(copy, Enum.Parse(rowSourceType.FieldType, "DbDataReader"));
        TdsColumnMetadata destination = new(1)
        {
            column = "v",
            DbType = SqlDbTypeExtensions.Json,
            TdsType = TdsEnums.SQLJSON,
            MetaType = MetaType.s_MetaJson,
            length = int.MaxValue,
            IsNullable = true,
            IsEncrypted = encrypted
        };

        // Act
        object? result = getMetadata.Invoke(copy, new object[] { destination, 0 });

        // Assert
        TdsColumnMetadata incoming = Assert.IsType<TdsColumnMetadata>(result);
        Assert.Same(destination, incoming);
        Assert.Equal(SqlDbTypeExtensions.Json, destination.DbType);
        Assert.Same(MetaType.s_MetaJson, destination.MetaType);
        Assert.Null(destination.collation);

        // Metadata inspection must leave the first row unread.
        Assert.True(reader.Read());
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Verifies that catalog discovery reads catalog views but omits the column alias temp table
    /// when no mapping uses SQL Graph aliases.
    /// </summary>
    [Fact]
    public void CreateInitialQuery_CatalogWithoutAliases_OmitsColumnAliases()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableBulkCopyCatalogMetadataDiscovery = true;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Contains("[sys].[all_columns]", query);
        Assert.Contains("EXEC(N'SELECT ' + @Column_Names", query);
        Assert.Contains("EXEC ..sp_tablecollations_100 N'[dbo].[T]';", query);
        Assert.DoesNotContain("#Column_Aliases", query);
    }

    /// <summary>
    /// Verifies that catalog discovery creates, reads and drops the column alias temp table when
    /// a mapping uses SQL Graph aliases.
    /// </summary>
    [Fact]
    public void CreateInitialQuery_CatalogWithAliases_IncludesColumnAliases()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableBulkCopyCatalogMetadataDiscovery = true;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: true);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Contains("CREATE TABLE #Column_Aliases", query);
        Assert.Contains("INSERT INTO #Column_Aliases", query);
        Assert.Contains("DELETE FROM #Column_Aliases", query);
        Assert.Contains("FROM #Column_Aliases", query);
        Assert.Contains("DROP TABLE #Column_Aliases", query);
        Assert.Contains("[sys].[all_columns]", query);
    }

    /// <summary>
    /// Verifies that the pre-6.0 query, which does not read catalog views or create temp tables,
    /// is used by default and when catalog discovery is disabled.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public void CreateInitialQuery_CatalogDisabled_ReturnsLegacyQuery(bool? switchValue)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableBulkCopyCatalogMetadataDiscovery = switchValue;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        string expected =
            "SELECT @@TRANCOUNT;" + Environment.NewLine +
            "SET FMTONLY ON;" + Environment.NewLine +
            "SELECT * FROM [dbo].[T];" + Environment.NewLine +
            "SET FMTONLY OFF;" + Environment.NewLine +
            "EXEC ..sp_tablecollations_100 N'[dbo].[T]';";
        Assert.Equal(expected, NormalizeNewLines(query));
    }

    /// <summary>
    /// Verifies that disabled catalog discovery never reads sys.all_columns, even when alias
    /// resolution is requested.
    /// </summary>
    [Fact]
    public void CreateInitialQuery_CatalogDisabledWithAliases_ReturnsLegacyQuery()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableBulkCopyCatalogMetadataDiscovery = false;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: true);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Contains("SELECT * FROM [dbo].[T];", query);
        Assert.DoesNotContain("all_columns", query);
        Assert.DoesNotContain("#Column_Aliases", query);
    }

    /// <summary>
    /// Verifies that SQL Graph alias mappings request alias resolution only when catalog
    /// discovery is enabled.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void ShouldResolveColumnAliases_GraphAliasMapping_DependsOnSwitch(
        bool? switchValue, bool expected)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableBulkCopyCatalogMetadataDiscovery = switchValue;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Add("ToId", "$to_id");
        FieldInfo? field = typeof(SqlBulkCopy).GetField(
            "_localColumnMappings", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(copy, copy.ColumnMappings);

        // Act
        bool result = copy.ShouldResolveColumnAliases();

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Supplies destination names and the legacy query lines they produce.</summary>
    public static TheoryData<string, string, string> LegacyDestinationNames => new()
    {
        { "#T", "SELECT * FROM [#T];", "EXEC tempdb..sp_tablecollations_100 N'.[#T]';" },
        {
            "Db.dbo.T",
            "SELECT * FROM [Db].[dbo].[T];",
            "EXEC [Db]..sp_tablecollations_100 N'[dbo].[T]';"
        },
        {
            "[My]]Db].[my'schema].[my'table]",
            "SELECT * FROM [My]]Db].[my'schema].[my'table];",
            "EXEC [My]]Db]..sp_tablecollations_100 N'[my''schema].[my''table]';"
        }
    };

    /// <summary>
    /// Verifies that the legacy query quotes and escapes destination names and targets the
    /// right database for the collation lookup.
    /// </summary>
    [Theory]
    [MemberData(nameof(LegacyDestinationNames))]
    public void CreateInitialQuery_LegacyDestinationName_QuotesNames(
        string destination, string expectedSelect, string expectedCollations)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.EnableBulkCopyCatalogMetadataDiscovery = false;
        using SqlBulkCopy copy = CreateBulkCopy(destination, resolveAliases: false);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        string[] lines = NormalizeNewLines(query).Split(
            new[] { Environment.NewLine }, StringSplitOptions.None);
        Assert.Equal(5, lines.Length);
        Assert.Equal(expectedSelect, lines[2]);
        Assert.Equal(expectedCollations, lines[4]);
    }

    private static SqlBulkCopy CreateBulkCopy(string destination, bool resolveAliases)
    {
        SqlBulkCopy copy = new(new SqlConnection(), SqlBulkCopyOptions.Default, null)
        {
            DestinationTableName = destination
        };
        FieldInfo? field = typeof(SqlBulkCopy).GetField(
            "_localColumnMappingsResolveAliases", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(copy, resolveAliases);
        return copy;
    }

    // Raw string literals use the source file's line endings, which may differ from the host's.
    private static string NormalizeNewLines(string value) =>
        value.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
}
