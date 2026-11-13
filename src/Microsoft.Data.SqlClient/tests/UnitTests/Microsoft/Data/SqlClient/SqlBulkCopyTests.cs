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
/// Some tests change the cached UseBulkCopyCatalogMetadataDiscovery switch, so this class
/// runs in the serialized AppContext switch collection.
/// </remarks>
[Collection(AppContextSwitchTestCollection.Name)]
public sealed class SqlBulkCopyTests
{
    /// <summary>Skips hidden and unmapped rows without losing the text column's collation.</summary>
    [Fact]
    public void GetColumnCollation_HiddenAndUnmappedColumns_ReturnsMatchingCollation()
    {
        // Arrange
        Result collations = new(new TdsColumnMetadataToken(4));
        foreach (string name in new[] { "Id", "ValidFrom", "ValidTo", "Unmapped", "TextValue" })
        {
            Row row = new(4);
            row.DataFields[1] = new SqlString(name);
            row.DataFields[3] = name == "TextValue"
                ? new SqlString("Japanese_XJIS_100_CI_AS")
                : SqlString.Null;
            collations.AddRow(row);
        }
        int index = 0;

        // Act
        object idCollation = SqlBulkCopy.GetColumnCollation(collations, "Id", ref index);
        object textCollation = SqlBulkCopy.GetColumnCollation(collations, "TextValue", ref index);

        // Assert
        Assert.Equal(SqlString.Null, idCollation);
        Assert.Equal(new SqlString("Japanese_XJIS_100_CI_AS"), textCollation);
        Assert.Equal(collations.Count, index);
    }

    /// <summary>Reports inconsistent server metadata instead of using another column's collation.</summary>
    [Fact]
    public void GetColumnCollation_MissingColumn_ThrowsInvalidOperation()
    {
        // Arrange
        Result collations = new(new TdsColumnMetadataToken(4));
        int index = 0;

        // Act
        Action lookup = () => SqlBulkCopy.GetColumnCollation(collations, "Missing", ref index);

        // Assert
        Assert.Throws<InvalidOperationException>(lookup);
    }

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
        switches.UseBulkCopyCatalogMetadataDiscovery = true;
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
        switches.UseBulkCopyCatalogMetadataDiscovery = true;
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
        switches.UseBulkCopyCatalogMetadataDiscovery = switchValue;
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
        switches.UseBulkCopyCatalogMetadataDiscovery = false;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: true);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Contains("SELECT * FROM [dbo].[T];", query);
        Assert.DoesNotContain("all_columns", query);
        Assert.DoesNotContain("#Column_Aliases", query);
    }

    /// <summary>
    /// Verifies that SQL Graph alias mappings request alias resolution in automatic and forced
    /// catalog modes, but not when catalog discovery is disabled.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public void ShouldResolveColumnAliases_GraphAliasMapping_DependsOnSwitch(
        bool? switchValue, bool expected)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = switchValue;
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

    /// <summary>Verifies automatic discovery selects catalog metadata up front for aliases.</summary>
    [Fact]
    public void CreateInitialQuery_AutomaticWithAliases_ReturnsCatalogQuery()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = null;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: true);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Contains("[sys].[all_columns]", query);
        Assert.Contains("CREATE TABLE #Column_Aliases", query);
    }

    /// <summary>Verifies only automatic discovery retries missing destination names.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void ShouldUseCatalogMetadataFallback_MissingDestination_DependsOnMode(
        bool? mode, bool expected)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = mode;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Add("Source", "Hidden");
        SetLocalColumnMappings(copy);
        TdsColumnMetadataToken metadata = new(1);
        metadata[0].column = "Visible";

        // Act
        bool result = copy.ShouldUseCatalogMetadataFallback(metadata);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Verifies visible destination names, including quoted names, need no retry.</summary>
    [Theory]
    [InlineData("Visible")]
    [InlineData("[Visible]")]
    public void ShouldUseCatalogMetadataFallback_MatchingDestination_ReturnsFalse(string destination)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = null;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Add("MissingSource", destination);
        SetLocalColumnMappings(copy);
        TdsColumnMetadataToken metadata = new(1);
        metadata[0].column = "Visible";

        // Act
        bool result = copy.ShouldUseCatalogMetadataFallback(metadata);

        // Assert
        Assert.False(result);
    }

    /// <summary>Verifies ordinal mappings do not request catalog fallback.</summary>
    [Fact]
    public void ShouldUseCatalogMetadataFallback_OrdinalMapping_ReturnsFalse()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = null;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Add(0, 0);
        SetLocalColumnMappings(copy);

        // Act
        bool result = copy.ShouldUseCatalogMetadataFallback(new TdsColumnMetadataToken(1));

        // Assert
        Assert.False(result);
    }

    /// <summary>Verifies ordinal mappings use catalog discovery unless explicitly disabled.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void CreateInitialQuery_OrdinalMapping_RespectsDiscoveryMode(
        bool? mode, bool expectedCatalog)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = mode;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Clear();
        copy.ColumnMappings.Add(0, 1);
        SetLocalColumnMappings(copy);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Equal(expectedCatalog, query.Contains("[sys].[all_columns]"));
    }

    /// <summary>Verifies automatic default mappings preserve catalog destination positions.</summary>
    [Fact]
    public void CreateInitialQuery_AutomaticDefaultMappings_UsesCatalog()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = null;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Clear();
        SetLocalColumnMappings(copy);

        // Act
        string query = copy.CreateInitialQuery();

        // Assert
        Assert.Contains("[sys].[all_columns]", query);
    }

    /// <summary>Verifies a named-mapping cache is replaced before using destination ordinals.</summary>
    [Fact]
    public void IsCachedMetadataValid_AutomaticOrdinalMapping_RejectsFastCache()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = null;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        copy.ColumnMappings.Clear();
        copy.ColumnMappings.Add(0, 1);
        SetLocalColumnMappings(copy);
        typeof(SqlBulkCopy).GetProperty(nameof(SqlBulkCopy.CachedMetadata),
            BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(copy, new BulkCopySimpleResultSet { UsesCatalogMetadata = false });

        // Act
        bool result = copy.IsCachedMetadataValid();

        // Assert
        Assert.False(result);
    }

    /// <summary>Verifies cache reuse retains alias results for subsequent alias mappings.</summary>
    [Fact]
    public void CacheMetadataIfEnabled_ReusedAliasCache_RetainsAliasCapability()
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = null;
        using SqlBulkCopy copy = new(new SqlConnection(), SqlBulkCopyOptions.CacheMetadata, null);
        copy.ColumnMappings.Add("Source", "Visible");
        SetLocalColumnMappings(copy);
        BulkCopySimpleResultSet metadata = new() { UsesCatalogMetadata = true };
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo cache = typeof(SqlBulkCopy).GetMethod("CacheMetadataIfEnabled", flags)!;
        FieldInfo aliases = typeof(SqlBulkCopy).GetField("_localColumnMappingsResolveAliases",
            flags)!;
        aliases.SetValue(copy, true);
        cache.Invoke(copy, new object[] { metadata });

        // Act
        aliases.SetValue(copy, false);
        cache.Invoke(copy, new object[] { metadata });
        aliases.SetValue(copy, true);
        bool result = copy.IsCachedMetadataValid();

        // Assert
        Assert.True(result);
        Assert.Same(metadata, copy.CachedMetadata);
    }

    /// <summary>Verifies cached discovery matches forced modes and either automatic path.</summary>
    [Theory]
    [InlineData(null, false, true)]
    [InlineData(null, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void IsCachedMetadataValid_DiscoveryMode_DependsOnCachedQuery(
        bool? mode, bool usesCatalog, bool expected)
    {
        // Arrange
        using LocalAppContextSwitchesHelper switches = new();
        switches.UseBulkCopyCatalogMetadataDiscovery = mode;
        using SqlBulkCopy copy = CreateBulkCopy("dbo.T", resolveAliases: false);
        PropertyInfo? property = typeof(SqlBulkCopy).GetProperty(nameof(SqlBulkCopy.CachedMetadata),
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        property.SetValue(copy, new BulkCopySimpleResultSet { UsesCatalogMetadata = usesCatalog });

        // Act
        bool result = copy.IsCachedMetadataValid();

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>Sets the operation mappings normally initialized before metadata discovery.</summary>
    private static void SetLocalColumnMappings(SqlBulkCopy copy)
    {
        FieldInfo? field = typeof(SqlBulkCopy).GetField(
            "_localColumnMappings", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(copy, copy.ColumnMappings);
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
        switches.UseBulkCopyCatalogMetadataDiscovery = false;
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
        copy.ColumnMappings.Add("Source", "Visible");
        SetLocalColumnMappings(copy);
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
