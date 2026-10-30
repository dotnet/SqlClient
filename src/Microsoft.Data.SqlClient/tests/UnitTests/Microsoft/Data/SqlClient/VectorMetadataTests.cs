// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Data.SqlClient.Parser;
using Microsoft.Data.SqlClient.Parser.Tokens;
using Microsoft.Data.SqlTypes;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies shared vector mappings, SQL spelling, and stable cached capability metadata.
/// </summary>
public sealed class VectorMetadataTests
{
    /// <summary>
    /// Provides the runtime's ordinary enum metadata without an explicit serialization marker.
    /// </summary>
    private enum UnannotatedEnum
    {
        Value
    }

    /// <summary>
    /// Keeps native source recognition distinct from Framework string projections.
    /// </summary>
    [Fact]
    public void NativeVectorTypesDoNotIncludeTextProjections()
    {
        Assert.True(MetaType.TryGetVectorElementType(typeof(SqlVector<float>), out byte type));
        Assert.Equal((byte)MetaType.SqlVectorElementType.Float32, type);
        Assert.False(MetaType.TryGetVectorElementType(typeof(string), out _));
        Assert.False(MetaType.TryGetVectorElementType(typeof(SqlString), out _));
        Assert.False(MetaType.TryGetVectorElementType(typeof(object), out _));
        Assert.False(MetaType.TryGetVectorElementType(typeof(SqlVector<int>), out _));
        Assert.False(MetaType.TryGetVectorElementType(null, out _));
        #if NET
        Assert.True(MetaType.TryGetVectorElementType(typeof(SqlVector<Half>), out type));
        Assert.Equal((byte)MetaType.SqlVectorElementType.Float16, type);
        #endif
    }

    /// <summary>
    /// Preserves the ordinary and provider-specific reader type contracts on each framework.
    /// </summary>
    [Fact]
    public void ReaderVectorTypesPreserveFrameworkProjections()
    {
        byte float32 = (byte)MetaType.SqlVectorElementType.Float32;
        byte float16 = (byte)MetaType.SqlVectorElementType.Float16;
        Assert.Equal(typeof(SqlVector<float>), MetaType.GetVectorFieldType(float32, false));
        Assert.Equal(typeof(SqlVector<float>), MetaType.GetVectorFieldType(float32, true));
        #if NET
        Assert.Equal(typeof(SqlVector<Half>), MetaType.GetVectorFieldType(float16, false));
        Assert.Equal(typeof(SqlVector<Half>), MetaType.GetVectorFieldType(float16, true));
        #else
        Assert.Equal(typeof(string), MetaType.GetVectorFieldType(float16, false));
        Assert.Equal(typeof(SqlString), MetaType.GetVectorFieldType(float16, true));
        #endif
        Assert.Throws<NotSupportedException>(() => MetaType.GetVectorFieldType(0xFF, false));
        Assert.Throws<NotSupportedException>(() => MetaType.GetVectorFieldType(0xFF, true));
    }

    /// <summary>
    /// Shares declaration formatting while preserving float32's existing shorthand.
    /// </summary>
    /// <param name="elementType">The native base-type tag.</param>
    /// <param name="expected">The complete SQL declaration.</param>
    [Theory]
    [InlineData((byte)MetaType.SqlVectorElementType.Float32, "vector(3)")]
    [InlineData((byte)MetaType.SqlVectorElementType.Float16, "vector(3, float16)")]
    public void VectorDeclarationPreservesSqlSpelling(byte elementType, string expected)
    {
        StringBuilder builder = new("vector");
        MetaType.AppendVectorTypeArguments(builder, 3, elementType);
        Assert.Equal(expected, builder.ToString());
    }

    /// <summary>
    /// Does not silently format an unknown base type as float32.
    /// </summary>
    [Fact]
    public void VectorDeclarationRejectsUnknownBaseType()
    {
        Assert.Throws<NotSupportedException>(() =>
            MetaType.AppendVectorTypeArguments(new StringBuilder(), 3, 0xFF));
    }

    /// <summary>
    /// Uses an explicit UTF-8 varchar(max) wire representation for vector JSON fallback,
    /// without consuming the reader or changing unrelated JSON/encrypted metadata.
    /// </summary>
    /// <param name="sourceType">The reader's declared CLR field type.</param>
    /// <param name="encrypted">Whether the destination metadata is encrypted.</param>
    /// <param name="usesText">Whether vector fallback should select varchar(max).</param>
    [Theory]
    [InlineData(typeof(SqlVector<float>), false, true)]
    [InlineData(typeof(SqlVector<float>), true, false)]
    [InlineData(typeof(string), false, false)]
    #if NET
    [InlineData(typeof(SqlVector<Half>), false, true)]
    [InlineData(typeof(SqlVector<Half>), true, false)]
    #endif
    public void JsonVectorReaderSelectsExplicitTextMetadata(
        Type sourceType, bool encrypted, bool usesText)
    {
        using DataTable table = new();
        table.Columns.Add("v", sourceType);
        table.Rows.Add(DBNull.Value);
        using DataTableReader reader = table.CreateDataReader();
        using SqlBulkCopy copy = new("Server=<server>");
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
        TdsColumnMetadata incoming = Assert.IsType<TdsColumnMetadata>(
            getMetadata.Invoke(copy, new object[] { destination, 0 }));
        if (usesText)
        {
            Assert.NotSame(destination, incoming);
            Assert.Equal(SqlDbType.VarChar, incoming.DbType);
            Assert.Same(MetaType.MetaMaxVarChar, incoming.MetaType);
            Assert.Equal(TdsEnums.SQLBIGVARCHAR, incoming.TdsType);
            Assert.Equal(int.MaxValue, incoming.length);
            Assert.True(incoming.collation.IsUTF8);
            Assert.Equal(65001, incoming.Encoding.CodePage);
            Assert.Equal(65001, incoming.CodePage);
            Assert.True(incoming.IsNullable);
            Assert.Equal(destination.Ordinal, incoming.Ordinal);
            Assert.Equal(destination.column, incoming.column);
        }
        else
        {
            Assert.Same(destination, incoming);
        }

        Assert.Equal(SqlDbTypeExtensions.Json, destination.DbType);
        Assert.Same(MetaType.s_MetaJson, destination.MetaType);
        Assert.Null(destination.collation);
        Assert.True(reader.Read());
        Assert.False(reader.Read());
    }

    /// <summary>
    /// Keeps the runtime's ordinary enum metadata and Framework serialization behavior.
    /// </summary>
    [Fact]
    public void VectorSupportEnumRemainsSerializable()
    {
        Assert.Equal(
            Attribute.IsDefined(typeof(UnannotatedEnum), typeof(SerializableAttribute)),
            Attribute.IsDefined(typeof(SqlVectorTypeSupport), typeof(SerializableAttribute)));
        #if NETFRAMEWORK
        Assert.True(typeof(SqlVectorTypeSupport).IsSerializable);
        #endif
    }

    /// <summary>
    /// Captures creation metadata and server version during construction instead of retaining
    /// a dependency on later changes to the connection's mutable capabilities.
    /// </summary>
    /// <param name="version">The negotiated vector feature version.</param>
    /// <param name="format">The expected creation format, or null when vectors are absent.</param>
    /// <param name="parameters">The creation parameters, or null when vectors are absent.</param>
    [Theory]
    [InlineData(TdsEnums.VECTOR_VERSION_NOT_SUPPORTED, null, null)]
    [InlineData(TdsEnums.VECTOR_VERSION_FLOAT32, "vector({0})", "dimensions")]
    [InlineData(TdsEnums.VECTOR_VERSION_FLOAT16, "vector({0}, {1})", "dimensions,base_type")]
    public void MetadataFactoryCapturesCapabilities(
        byte version, string? format, string? parameters)
    {
        ConnectionCapabilities capabilities = new()
        {
            ServerMajorVersion = 17,
            ServerBuildNumber = 123,
            JsonType = true,
            VectorVersion = version
        };
        string serverVersion = capabilities.ServerVersion;
        using Stream? stream = typeof(SqlConnection).Assembly.GetManifestResourceStream(
            "Microsoft.Data.SqlClient.SqlMetaData.xml");
        Assert.NotNull(stream);
        using SqlMetaDataFactory factory = new(stream, capabilities);
        capabilities.Reset();

        FieldInfo? field = typeof(SqlMetaDataFactory)
            .GetField("_collectionDataSet", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        DataSet data = Assert.IsType<DataSet>(field.GetValue(factory));
        DataTable dataTypes = Assert.IsType<DataTable>(
            data.Tables[DbMetaDataCollectionNames.DataTypes]);
        DataRow[] rows = dataTypes.Select("TypeName = 'vector'");
        if (format == null)
        {
            Assert.Empty(rows);
        }
        else
        {
            DataRow vector = Assert.Single(rows);
            Assert.Equal(format, vector[DbMetaDataColumnNames.CreateFormat]);
            Assert.Equal(parameters, vector[DbMetaDataColumnNames.CreateParameters]);
        }
        Assert.Single(dataTypes.Select("TypeName = 'json'"));

        using SqlConnection connection = new();
        using DataTable information = factory.GetSchema(
            connection, DbMetaDataCollectionNames.DataSourceInformation, null);
        Assert.Equal(serverVersion, information.Rows[0][DbMetaDataColumnNames.DataSourceProductVersion]);
    }
}
