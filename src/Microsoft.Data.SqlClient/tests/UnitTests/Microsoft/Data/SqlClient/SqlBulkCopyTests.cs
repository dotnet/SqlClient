// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
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
