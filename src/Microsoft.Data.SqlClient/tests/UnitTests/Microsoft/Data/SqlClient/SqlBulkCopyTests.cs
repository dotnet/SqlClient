// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Microsoft.Data.SqlTypes;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>Verifies bulk-copy metadata boundaries without a SQL Server.</summary>
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
}
