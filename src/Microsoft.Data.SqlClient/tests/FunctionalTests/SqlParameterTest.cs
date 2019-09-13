// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlParameterTests
    {
        [Fact]
        public void ParameterPrecisionOnInterfaceType()
        {
            SqlParameter parameter = new SqlParameter();
            IDbDataParameter interfaceParameter = parameter;
            interfaceParameter.Precision = 10;
            interfaceParameter.Scale = 5;

            Assert.Equal(10, interfaceParameter.Precision);
            Assert.Equal(5, interfaceParameter.Scale);

            Assert.Equal(10, parameter.Precision);
            Assert.Equal(5, parameter.Scale);
        }

        [Fact]
        public void ParameterPrecisionOnBaseType()
        {
            SqlParameter parameter = new SqlParameter();
            DbParameter baseParameter = parameter;
            baseParameter.Precision = 10;
            baseParameter.Scale = 5;

            Assert.Equal(10, baseParameter.Precision);
            Assert.Equal(5, baseParameter.Scale);

            Assert.Equal(10, parameter.Precision);
            Assert.Equal(5, parameter.Scale);
        }

        [Fact]
        public void Constructor1()
        {
            SqlParameter p = new SqlParameter();

            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal(0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Null(p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Null(p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }

        [Fact]
        public void Constructor2_Value_DateTime()
        {
            DateTime value = new DateTime(2004, 8, 24);
            SqlParameter p = new SqlParameter("address", value);

            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal("address", p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal (0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            Assert.Equal(new SqlDateTime(value), p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Equal(value, p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }

        [Fact]
        public void Constructor2_Value_Null()
        {
            SqlParameter p = new SqlParameter("address", null);

            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal("address", p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal(0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Null(p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Null(p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }

        [Fact]
        public void Constructor2_Value_DBNull()
        {
            SqlParameter p = new SqlParameter("address", DBNull.Value);

            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal("address", p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal(0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(SqlString.Null, p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Equal(DBNull.Value, p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }

        [Fact]
        public void Constructor2_Name_Null()
        {
            SqlParameter p = new SqlParameter(null, DBNull.Value);
            Assert.Equal(string.Empty, p.ParameterName);
        }

        [Theory]
        [InlineData(SqlDbType.BigInt, DbType.Int64)]
        [InlineData(SqlDbType.Binary, DbType.Binary)]
        [InlineData(SqlDbType.Bit, DbType.Boolean)]
        [InlineData(SqlDbType.Char, DbType.AnsiStringFixedLength)]
        [InlineData(SqlDbType.Date, DbType.Date)]
        [InlineData(SqlDbType.DateTime, DbType.DateTime)]
        [InlineData(SqlDbType.DateTimeOffset, DbType.DateTimeOffset)]
        [InlineData(SqlDbType.Decimal, DbType.Decimal)]
        [InlineData(SqlDbType.Float, DbType.Double)]
        [InlineData(SqlDbType.Image, DbType.Binary)]
        [InlineData(SqlDbType.Int, DbType.Int32)]
        [InlineData(SqlDbType.Money, DbType.Currency)]
        [InlineData(SqlDbType.NChar, DbType.StringFixedLength)]
        [InlineData(SqlDbType.NText, DbType.String)]
        [InlineData(SqlDbType.NVarChar, DbType.String)]
        [InlineData(SqlDbType.Real, DbType.Single)]
        [InlineData(SqlDbType.SmallDateTime, DbType.DateTime)]
        [InlineData(SqlDbType.SmallInt, DbType.Int16)]
        [InlineData(SqlDbType.SmallMoney, DbType.Currency)]
        [InlineData(SqlDbType.Structured, DbType.Object)]
        [InlineData(SqlDbType.Text, DbType.AnsiString)]
        [InlineData(SqlDbType.Time, DbType.Time)]
        [InlineData(SqlDbType.Timestamp, DbType.Binary)]
        [InlineData(SqlDbType.TinyInt, DbType.Byte)]
        [InlineData(SqlDbType.Udt, DbType.Object)]
        [InlineData(SqlDbType.UniqueIdentifier, DbType.Guid)]
        [InlineData(SqlDbType.VarBinary, DbType.Binary)]
        [InlineData(SqlDbType.VarChar, DbType.AnsiString)]
        [InlineData(SqlDbType.Variant, DbType.Object)]
        [InlineData(SqlDbType.Xml, DbType.Xml)]
        public void Constructor3_Types(SqlDbType sqlDbType, DbType dbType)
        {
            SqlParameter p = new SqlParameter("types", sqlDbType);

            Assert.Equal(dbType, p.DbType);
            Assert.Equal(sqlDbType, p.SqlDbType);
            Assert.Null(p.Value);
        }

        [Fact]
        public void Constructor4()
        {
            SqlParameter p = new SqlParameter("", SqlDbType.Bit, 1001);
            Assert.Equal(1001, p.Size);
        }

        [Fact]
        public void Constructor4_MaxSize()
        {
            SqlParameter p = new SqlParameter("", SqlDbType.Bit, int.MaxValue);
            Assert.Equal(int.MaxValue, p.Size);
        }

        [Fact]
        public void Constructor4_MinSize()
        {
            Assert.Throws<ArgumentException>(() => new SqlParameter("", SqlDbType.Image, int.MinValue));
        }

        [Theory]
        [InlineData(null, "")]
        [InlineData("SourceColumn", "SourceColumn")]
        [InlineData(" SourceColumn ", " SourceColumn ")]
        public void Constructor5(string value, string expected)
        {
            SqlParameter p = new SqlParameter("", SqlDbType.Bit, int.MaxValue, value);
            Assert.Equal(expected, p.SourceColumn);
        }

        [Theory]
        [InlineData(ParameterDirection.Input, true, 0, 0, "", "", DataRowVersion.Current)]
        [InlineData(ParameterDirection.InputOutput, true, 0, 0, null, "", DataRowVersion.Default)]
        [InlineData(ParameterDirection.Output, false, 0, 255, "Col", "Col", DataRowVersion.Original)]
        [InlineData(ParameterDirection.ReturnValue, false, 99, 100, " Col ", " Col ", DataRowVersion.Proposed)]
        public void Constructor6(ParameterDirection parameterDirection, bool isNullable, byte precision, byte scale, string sourceColumn, string expectedSourceColumn, DataRowVersion dataRowVersion)
        {
            SqlParameter p = new SqlParameter("", SqlDbType.Bit, int.MaxValue, parameterDirection, isNullable, precision, scale, sourceColumn, dataRowVersion, null);

            Assert.Equal(parameterDirection, p.Direction);
            Assert.Equal(isNullable, p.IsNullable);
            Assert.Equal(precision, p.Precision);
            Assert.Equal(scale, p.Scale);
            Assert.Equal(expectedSourceColumn, p.SourceColumn);
            Assert.Equal(dataRowVersion, p.SourceVersion);
        }

        [Theory]
        [InlineData(ParameterDirection.Input, 0, 0, null, "", DataRowVersion.Current, false, null, "", null, "", null, "")]
        [InlineData(ParameterDirection.InputOutput, 255, 255, "", "", DataRowVersion.Default, true, "", "", "", "", "", "")]
        [InlineData(ParameterDirection.Output, 99, 100, "Source", "Source", DataRowVersion.Original, false, "aaa", "aaa", "bbb", "bbb", "ccc", "ccc")]
        [InlineData(ParameterDirection.ReturnValue, 100, 99, " Source ", " Source ", DataRowVersion.Proposed, true, " aaa ", " aaa ", " bbb ", " bbb ", " ccc ", " ccc ")]
        public void Constructor7(ParameterDirection parameterDirection, byte precision, byte scale, string sourceColumn, string expectedSourceColumn, DataRowVersion dataRowVersion, bool sourceColumnNullMapping, string xmlSchemaCollectionDatabase, string expectedXmlSchemaCollectionDatabase, string xmlSchemaCollectionOwningSchema, string expectedXmlSchemaCollectionOwningSchema, string xmlSchemaCollectionName, string expectedXmlSchemaCollectionName)
        {
            SqlParameter p = new SqlParameter("", SqlDbType.Bit, int.MaxValue, parameterDirection, precision, scale, sourceColumn, dataRowVersion, sourceColumnNullMapping, null, xmlSchemaCollectionDatabase, xmlSchemaCollectionOwningSchema, xmlSchemaCollectionName);

            Assert.Equal(parameterDirection, p.Direction);
            Assert.Equal(precision, p.Precision);
            Assert.Equal(scale, p.Scale);
            Assert.Equal(expectedSourceColumn, p.SourceColumn);
            Assert.Equal(dataRowVersion, p.SourceVersion);
            Assert.Equal(expectedXmlSchemaCollectionDatabase, p.XmlSchemaCollectionDatabase);
            Assert.Equal(expectedXmlSchemaCollectionName, p.XmlSchemaCollectionName);
            Assert.Equal(expectedXmlSchemaCollectionOwningSchema, p.XmlSchemaCollectionOwningSchema);
        }
    }   
}