// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlMetaDataTest
    {
        [Fact]
        public void DecimalConstructor()
        {
            SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Decimal, 2, 2, true, true, SortOrder.Ascending, 1);
            Assert.Equal("col2", metaData.Name);
            Assert.Equal(SqlDbType.Decimal, metaData.SqlDbType);
            Assert.Null(metaData.Type);
            Assert.Equal(2, metaData.Precision);
            Assert.Equal(2, metaData.Scale);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(1, metaData.SortOrdinal);
        }

        [Fact]
        public void DecimalConstructorWithPrecisionScale()
        {
            SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Decimal, 2, 2);
            Assert.Equal("col2", metaData.Name);
            Assert.Equal(SqlDbType.Decimal, metaData.SqlDbType);
            Assert.Null(metaData.Type);
            Assert.Equal(2, metaData.Precision);
            Assert.Equal(2, metaData.Scale);
        }

        [Fact]
        public void StringConstructorWithLocaleCompareOption()
        {
            SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.VarChar, 16, 2, SqlCompareOptions.IgnoreCase, true, true, SortOrder.Ascending, 1);
            Assert.Equal("col2", metaData.Name);
            Assert.Equal(SqlDbType.VarChar, metaData.SqlDbType);
            Assert.Equal(DbType.String, metaData.DbType);
            Assert.Null(metaData.Type);
            Assert.Equal(16, metaData.MaxLength);
            Assert.Equal(2, metaData.LocaleId);
            Assert.Equal(SqlCompareOptions.IgnoreCase, metaData.CompareOptions);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(1, metaData.SortOrdinal);
        }

        // Test UDT constrtuctor without tvp extended properties
        [Fact]
        public void UdtConstructorTest()
        {
            Address address = Address.Parse("123 baker st || Redmond");
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Udt, typeof(Address), "UdtTestDb.dbo.Address");
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(SqlDbType.Udt, metaData.SqlDbType);
            Assert.Equal(address.GetType(), metaData.Type);
            Assert.Equal("UdtTestDb.dbo.Address", metaData.TypeName);
            Assert.False(metaData.UseServerDefault);
            Assert.False(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Unspecified, metaData.SortOrder);
            Assert.Equal(-1, metaData.SortOrdinal);
        }

        [Fact]
        public void UdtConstructorTestWithoutServerTypeName()
        {
            Address address = Address.Parse("123 baker st || Redmond");
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Udt, typeof(Address));
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(SqlDbType.Udt, metaData.SqlDbType);
            Assert.Equal(address.GetType(), metaData.Type);
            Assert.Equal("Address", metaData.TypeName);
            Assert.False(metaData.UseServerDefault);
            Assert.False(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Unspecified, metaData.SortOrder);
            Assert.Equal(-1, metaData.SortOrdinal);
        }

        // Test UDT constrtuctor with tvp extended properties
        [Fact]
        public void UdtConstructorWithTvpTest()
        {
            Address address = Address.Parse("123 baker st || Redmond");
            SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Udt, typeof(Address), "UdtTestDb.dbo.Address", true, true, SortOrder.Ascending, 0);
            Assert.Equal("col2", metaData.Name);
            Assert.Equal(SqlDbType.Udt, metaData.SqlDbType);
            Assert.Equal(address.GetType(), metaData.Type);
            Assert.Equal("UdtTestDb.dbo.Address", metaData.TypeName);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Theory]
        [InlineData(SqlDbType.Variant, null)]
        [InlineData(SqlDbType.Udt, typeof(Address))]
        public void GenericConstructorWithoutXmlSchema(SqlDbType dbType, Type udt)
        {
            SqlMetaData metaData = new SqlMetaData("col2", dbType, 16, 2, 2, 2, SqlCompareOptions.IgnoreCase, udt, true, true, SortOrder.Ascending, 0);
            Assert.Equal(dbType, metaData.SqlDbType);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Fact]
        public void InvalidDbTypeGenericConstructorWithoutXmlSchema_Throws()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() => {
                SqlMetaData metaData = new SqlMetaData("col2", (SqlDbType)999, 16, 2, 2, 2, SqlCompareOptions.IgnoreCase, null, true, true, SortOrder.Ascending, 0);
            });
            Assert.NotNull(ex);
            Assert.NotEmpty(ex.Message);
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void XmlConstructor()
        {
            SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Xml, "NorthWindDb", "schema", "name", true, true, SortOrder.Ascending, 1);
            Assert.Equal("col2", metaData.Name);
            Assert.Equal(SqlDbType.Xml, metaData.SqlDbType);
            Assert.Null(metaData.Type);
            Assert.Equal("NorthWindDb", metaData.XmlSchemaCollectionDatabase);
            Assert.Equal("schema", metaData.XmlSchemaCollectionOwningSchema);
            Assert.Equal("name", metaData.XmlSchemaCollectionName);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(1, metaData.SortOrdinal);
        }
    }
}
