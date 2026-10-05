// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Text;
using Xunit;

namespace Microsoft.Data.SqlClient.Parser.Tokens;

/// <summary>
/// Verifies column metadata defaults, flag isolation, derived properties, and cloning.
/// </summary>
public class TdsColumnMetadataTests
{
    /// <summary>
    /// Ensures a new column preserves its ordinal and starts without optional metadata or flags.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Constructor_InitializesPropertiesWithDefaultValues(int ordinal)
    {
        var metadata = new TdsColumnMetadata(ordinal);

        Assert.Equal(ordinal, metadata.Ordinal);
        Assert.Null(metadata.baseColumn);
        Assert.Null(metadata.column);
        Assert.Equal(0, metadata.op);
        Assert.Equal(0, metadata.tableNum);
        Assert.Equal(0, metadata.Operand);
        Assert.Null(metadata.ServerName);
        Assert.Null(metadata.CatalogName);
        Assert.Null(metadata.SchemaName);
        Assert.Null(metadata.TableName);
        Assert.False(metadata.IsColumnSet);
        Assert.False(metadata.IsDifferentName);
        Assert.False(metadata.IsExpression);
        Assert.False(metadata.IsHidden);
        Assert.False(metadata.IsIdentity);
        Assert.False(metadata.IsKey);
        Assert.Equal(0, metadata.Updatability);
        Assert.True(metadata.IsReadOnly);
        Assert.False(metadata.Is2008DateTimeType);
        Assert.False(metadata.IsLargeUdt);
        Assert.False(metadata.IsNullable);
        Assert.False(metadata.IsMultiValued);
        Assert.Null(metadata.UdtTypeInfo);
        Assert.Null(metadata.XmlTypeInfo);
    }

    /// <summary>
    /// Ensures cloning preserves column-specific and inherited metadata, including packed flags.
    /// </summary>
    [Fact]
    public void Clone_CopiesAllProperties()
    {
        var original = new TdsColumnMetadata(7)
        {
            baseColumn = "OriginalColumn",
            column = "ColumnAlias",
            op = 3,
            tableNum = 2,
            tableName = new TdsTableName(new[] { "TestServer", "TestCatalog", "dbo", "TestTable" }),
            Operand = 42,
            IsColumnSet = true,
            IsDifferentName = true,
            IsExpression = true,
            IsHidden = true,
            IsIdentity = true,
            IsKey = true,
            Updatability = 2,
            CodePage = 1251,
            DbType = SqlDbType.Decimal,
            Encoding = Encoding.Unicode,
            MetaType = MetaType.GetMetaTypeFromSqlDbType(SqlDbType.Decimal, false),
            TdsType = 0x6A,
            IsEncrypted = true,
            IsNullable = true,
            IsMultiValued = true,
            length = 17,
            precision = 28,
            scale = 8,
            collation = new SqlCollation(0x00, 0x00),
            BaseTypeInfo = new TdsTypeInfo { DbType = SqlDbType.Int },
            CipherMetadata = new SqlCipherMetadata(default, 1, 2, "TestAlgorithm", 1, 1)
        };

        // Exercise the virtual override through the base type as well.
        TdsTypeInfo typeInfo = original;
        var clone = Assert.IsType<TdsColumnMetadata>(typeInfo.Clone());

        Assert.NotSame(original, clone);
        Assert.Equal(original.Ordinal, clone.Ordinal);
        Assert.Equal(original.baseColumn, clone.baseColumn);
        Assert.Equal(original.column, clone.column);
        Assert.Equal(original.op, clone.op);
        Assert.Equal(original.tableNum, clone.tableNum);
        Assert.Equal(original.Operand, clone.Operand);
        Assert.Equal(original.ServerName, clone.ServerName);
        Assert.Equal(original.CatalogName, clone.CatalogName);
        Assert.Equal(original.SchemaName, clone.SchemaName);
        Assert.Equal(original.TableName, clone.TableName);
        Assert.Equal(original.IsColumnSet, clone.IsColumnSet);
        Assert.Equal(original.IsDifferentName, clone.IsDifferentName);
        Assert.Equal(original.IsExpression, clone.IsExpression);
        Assert.Equal(original.IsHidden, clone.IsHidden);
        Assert.Equal(original.IsIdentity, clone.IsIdentity);
        Assert.Equal(original.IsKey, clone.IsKey);
        Assert.Equal(original.Updatability, clone.Updatability);
        Assert.Equal(original.IsReadOnly, clone.IsReadOnly);
        Assert.Equal(original.CodePage, clone.CodePage);
        Assert.Equal(original.DbType, clone.DbType);
        Assert.Same(original.Encoding, clone.Encoding);
        Assert.Same(original.MetaType, clone.MetaType);
        Assert.Equal(original.TdsType, clone.TdsType);
        Assert.Equal(original.IsEncrypted, clone.IsEncrypted);
        Assert.Equal(original.IsNullable, clone.IsNullable);
        Assert.Equal(original.IsMultiValued, clone.IsMultiValued);
        Assert.Equal(original.length, clone.length);
        Assert.Equal(original.precision, clone.precision);
        Assert.Equal(original.scale, clone.scale);
        Assert.Same(original.collation, clone.collation);
        Assert.Same(original.BaseTypeInfo, clone.BaseTypeInfo);
        Assert.Same(original.CipherMetadata, clone.CipherMetadata);
        Assert.Null(clone.UdtTypeInfo);
        Assert.Null(clone.XmlTypeInfo);

        clone.column = "ChangedAlias";
        clone.tableName.TableName = "ChangedTable";
        clone.IsKey = false;
        clone.IsNullable = false;
        clone.Updatability = 0;
        Assert.Equal("ColumnAlias", original.column);
        Assert.Equal("TestTable", original.TableName);
        Assert.True(original.IsKey);
        Assert.True(original.IsNullable);
        Assert.Equal(2, original.Updatability);
    }

    /// <summary>
    /// Ensures the derived clone owns independent copies of optional UDT and XML metadata.
    /// </summary>
    [Fact]
    public void Clone_WithUdtAndXmlTypeInfo_CreatesDeepCopies()
    {
        var original = new TdsColumnMetadata(0)
        {
            UdtTypeInfo = new TdsUdtTypeInfo
            {
                AssemblyQualifiedName = "MyAssembly.MyType, MyAssembly, Version=1.0.0.0",
                DatabaseName = "TestDatabase",
                SchemaName = "dbo",
                TypeName = "MyUdtType",
                Type = typeof(string)
            },
            XmlTypeInfo = new TdsXmlTypeInfo
            {
                Database = "TestDatabase",
                OwningSchema = "dbo",
                Name = "TestSchemaCollection"
            }
        };

        var clone = original.Clone();

        Assert.NotNull(clone.UdtTypeInfo);
        Assert.NotSame(original.UdtTypeInfo, clone.UdtTypeInfo);
        Assert.Equal(original.UdtTypeInfo.AssemblyQualifiedName,
            clone.UdtTypeInfo.AssemblyQualifiedName);
        Assert.Equal(original.UdtTypeInfo.DatabaseName, clone.UdtTypeInfo.DatabaseName);
        Assert.Equal(original.UdtTypeInfo.SchemaName, clone.UdtTypeInfo.SchemaName);
        Assert.Equal(original.UdtTypeInfo.TypeName, clone.UdtTypeInfo.TypeName);
        Assert.Equal(original.UdtTypeInfo.Type, clone.UdtTypeInfo.Type);
        Assert.NotNull(clone.XmlTypeInfo);
        Assert.NotSame(original.XmlTypeInfo, clone.XmlTypeInfo);
        Assert.Equal(original.XmlTypeInfo.Database, clone.XmlTypeInfo.Database);
        Assert.Equal(original.XmlTypeInfo.OwningSchema, clone.XmlTypeInfo.OwningSchema);
        Assert.Equal(original.XmlTypeInfo.Name, clone.XmlTypeInfo.Name);

        clone.UdtTypeInfo.TypeName = "ChangedType";
        clone.XmlTypeInfo.Name = "ChangedSchemaCollection";
        Assert.Equal("MyUdtType", original.UdtTypeInfo.TypeName);
        Assert.Equal("TestSchemaCollection", original.XmlTypeInfo.Name);
    }

    /// <summary>
    /// Ensures each packed boolean flag can be set and cleared without altering other flags.
    /// </summary>
    [Fact]
    public void Flags_SetAndClear_PreserveOtherFlagsAndUpdatability()
    {
        var metadata = new TdsColumnMetadata(0) { Updatability = 2 };
        Action<bool>[] setters =
        {
            value => metadata.IsColumnSet = value,
            value => metadata.IsDifferentName = value,
            value => metadata.IsExpression = value,
            value => metadata.IsHidden = value,
            value => metadata.IsIdentity = value,
            value => metadata.IsKey = value
        };
        Func<bool>[] getters =
        {
            () => metadata.IsColumnSet,
            () => metadata.IsDifferentName,
            () => metadata.IsExpression,
            () => metadata.IsHidden,
            () => metadata.IsIdentity,
            () => metadata.IsKey
        };

        for (int i = 0; i < setters.Length; i++)
        {
            setters[i](true);
            for (int j = 0; j < getters.Length; j++)
            {
                Assert.Equal(j <= i, getters[j]());
            }
            Assert.Equal(2, metadata.Updatability);
        }

        for (int i = 0; i < setters.Length; i++)
        {
            setters[i](false);
            for (int j = 0; j < getters.Length; j++)
            {
                Assert.Equal(j > i, getters[j]());
            }
            Assert.Equal(2, metadata.Updatability);
        }
    }

    /// <summary>
    /// Ensures updatability uses only its two bits and determines read-only status without
    /// overwriting the other column flags, including when transitioning back to zero.
    /// </summary>
    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(1, 1, false)]
    [InlineData(2, 2, false)]
    [InlineData(3, 3, false)]
    [InlineData(4, 0, true)]
    [InlineData(255, 3, false)]
    public void Updatability_MasksValueAndPreservesFlags(byte value, byte expected, bool isReadOnly)
    {
        var metadata = new TdsColumnMetadata(0)
        {
            IsColumnSet = true,
            IsDifferentName = true,
            IsExpression = true,
            IsHidden = true,
            IsIdentity = true,
            IsKey = true,
            Updatability = 3
        };

        metadata.Updatability = value;

        Assert.Equal(expected, metadata.Updatability);
        Assert.Equal(isReadOnly, metadata.IsReadOnly);
        Assert.True(metadata.IsColumnSet);
        Assert.True(metadata.IsDifferentName);
        Assert.True(metadata.IsExpression);
        Assert.True(metadata.IsHidden);
        Assert.True(metadata.IsIdentity);
        Assert.True(metadata.IsKey);
    }

    /// <summary>
    /// Ensures only the four SQL Server 2008 date/time types are classified as such.
    /// </summary>
    [Theory]
    [InlineData(SqlDbType.Date, true)]
    [InlineData(SqlDbType.Time, true)]
    [InlineData(SqlDbType.DateTime2, true)]
    [InlineData(SqlDbType.DateTimeOffset, true)]
    [InlineData(SqlDbType.DateTime, false)]
    [InlineData(SqlDbType.SmallDateTime, false)]
    [InlineData(SqlDbType.Int, false)]
    public void Is2008DateTimeType_ClassifiesDbType(SqlDbType dbType, bool expected)
    {
        var metadata = new TdsColumnMetadata(0) { DbType = dbType };

        Assert.Equal(expected, metadata.Is2008DateTimeType);
    }

    /// <summary>
    /// Ensures large UDT classification requires both the UDT type and maximum length.
    /// </summary>
    [Theory]
    [InlineData(SqlDbType.Udt, int.MaxValue, true)]
    [InlineData(SqlDbType.Udt, int.MaxValue - 1, false)]
    [InlineData(SqlDbType.Udt, 0, false)]
    [InlineData(SqlDbType.VarBinary, int.MaxValue, false)]
    public void IsLargeUdt_RequiresUdtAndMaximumLength(SqlDbType dbType, int length, bool expected)
    {
        var metadata = new TdsColumnMetadata(0) { DbType = dbType, length = length };

        Assert.Equal(expected, metadata.IsLargeUdt);
    }
}
