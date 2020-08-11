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
            Assert.Equal(0, p.Size);
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


        [Fact]
        public void CompareInfo()
        {
            SqlParameter parameter = new SqlParameter();
            Assert.Equal(SqlCompareOptions.None, parameter.CompareInfo);
            parameter.CompareInfo = SqlCompareOptions.IgnoreNonSpace;
            Assert.Equal(SqlCompareOptions.IgnoreNonSpace, parameter.CompareInfo);
        }

        [Fact]
        public void InferType_Byte()
        {
            byte value = 0x0a;

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.TinyInt, param.SqlDbType);
            Assert.Equal(DbType.Byte, param.DbType);
        }

        [Fact]
        public void InferType_ByteArray()
        {
            byte[] value = new byte[] { 0x0a, 0x0d };

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.VarBinary, param.SqlDbType);
            Assert.Equal(DbType.Binary, param.DbType);
        }

        [Fact]
        public void InferType_Char()
        {
            char value = 'X';
            string string_value = "X";

            SqlParameter p = new SqlParameter();
            p.Value = value;
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter();
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter();
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter("name", value);
            Assert.Equal(value, p.Value);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter("name", 5);
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter("name", SqlDbType.NVarChar);
            p.Value = value;
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(value, p.Value);
        }

        [Fact]
        public void InferType_CharArray()
        {
            char[] value = new char[] { 'A', 'X' };
            string string_value = "AX";

            SqlParameter p = new SqlParameter();
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter();
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter();
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter("name", value);
            Assert.Equal(value, p.Value);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter("name", 5);
            p.Value = value;
            Assert.Equal(value, p.Value);
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(string_value, p.Value);

            p = new SqlParameter("name", SqlDbType.NVarChar);
            p.Value = value;
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(value, p.Value);
        }

        [Fact]
        public void InferType_DateTime()
        {
            DateTime value;
            SqlParameter param;

            value = DateTime.Now;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.DateTime, param.SqlDbType);
            Assert.Equal(DbType.DateTime, param.DbType);

            value = DateTime.Now;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.DateTime, param.SqlDbType);
            Assert.Equal(DbType.DateTime, param.DbType);

            value = new DateTime(1973, 8, 13);
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.DateTime, param.SqlDbType);
            Assert.Equal(DbType.DateTime, param.DbType);
        }

        [Fact]
        public void InferType_Decimal()
        {
            decimal value;
            SqlParameter param;

            value = decimal.MaxValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Decimal, param.SqlDbType);
            Assert.Equal(DbType.Decimal, param.DbType);

            value = decimal.MinValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Decimal, param.SqlDbType);
            Assert.Equal(DbType.Decimal, param.DbType);

            value = 214748.364m;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Decimal, param.SqlDbType);
            Assert.Equal(DbType.Decimal, param.DbType);
        }

        [Fact]
        public void InferType_Double()
        {
            double value;
            SqlParameter param;

            value = double.MaxValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Float, param.SqlDbType);
            Assert.Equal(DbType.Double, param.DbType);

            value = Double.MinValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Float, param.SqlDbType);
            Assert.Equal(DbType.Double, param.DbType);

            value = 0d;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Float, param.SqlDbType);
            Assert.Equal(DbType.Double, param.DbType);
        }

        [Fact]
        public void InferType_Enum()
        {
            SqlParameter param;

            param = new SqlParameter();
            param.Value = ByteEnum.A;
            Assert.Equal(SqlDbType.TinyInt, param.SqlDbType);
            Assert.Equal(DbType.Byte, param.DbType);

            param = new SqlParameter();
            param.Value = Int64Enum.A;
            Assert.Equal(SqlDbType.BigInt, param.SqlDbType);
            Assert.Equal(DbType.Int64, param.DbType);
        }

        [Fact]
        public void InferType_Guid()
        {
            Guid value = Guid.NewGuid();

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.UniqueIdentifier, param.SqlDbType);
            Assert.Equal(DbType.Guid, param.DbType);
        }

        [Fact]
        public void InferType_Int16()
        {
            short value;
            SqlParameter param;

            value = short.MaxValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.SmallInt, param.SqlDbType);
            Assert.Equal(DbType.Int16, param.DbType);

            value = short.MinValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.SmallInt, param.SqlDbType);
            Assert.Equal(DbType.Int16, param.DbType);

            value = (short)0;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.SmallInt, param.SqlDbType);
            Assert.Equal(DbType.Int16, param.DbType);
        }

        [Fact]
        public void InferType_Int32()
        {
            int value;
            SqlParameter param;

            value = int.MaxValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Int, param.SqlDbType);
            Assert.Equal(DbType.Int32, param.DbType);

            value = int.MinValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Int, param.SqlDbType);
            Assert.Equal(DbType.Int32, param.DbType);

            value = 0;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Int, param.SqlDbType);
            Assert.Equal(DbType.Int32, param.DbType);
        }

        [Fact]
        public void InferType_Int64()
        {
            long value;
            SqlParameter param;

            value = long.MaxValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.BigInt, param.SqlDbType);
            Assert.Equal(DbType.Int64, param.DbType);

            value = long.MinValue;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.BigInt, param.SqlDbType);
            Assert.Equal(DbType.Int64, param.DbType);

            value = 0L;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.BigInt, param.SqlDbType);
            Assert.Equal(DbType.Int64, param.DbType);
        }

        [Fact]
        public void InferType_Invalid()
        {
            object[] notsupported = new object[] {
                        ushort.MaxValue,
                        uint.MaxValue,
                        ulong.MaxValue,
                        sbyte.MaxValue,
                        new SqlParameter ()
                    };

            SqlParameter param = new SqlParameter();

            for (int i = 0; i < notsupported.Length; i++)
            {
                param.Value = notsupported[i];
                try
                {
                    SqlDbType type = param.SqlDbType;
                }
                catch (ArgumentException ex)
                {
                    // The parameter data type of ... is invalid
                    Assert.Equal(typeof(ArgumentException), ex.GetType());
                    Assert.Null(ex.InnerException);
                    Assert.NotNull(ex.Message);
                    Assert.Null(ex.ParamName);
                }

                try
                {
                    DbType type = param.DbType;
                }
                catch (ArgumentException ex)
                {
                    // The parameter data type of ... is invalid
                    Assert.Equal(typeof(ArgumentException), ex.GetType());
                    Assert.Null(ex.InnerException);
                    Assert.NotNull(ex.Message);
                    Assert.Null(ex.ParamName);
                }
            }
        }

        [Fact]
        public void InferType_Object()
        {
            object value = new object();

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Variant, param.SqlDbType);
            Assert.Equal(DbType.Object, param.DbType);
        }

        [Fact]
        public void InferType_Single()
        {
            float value = float.MaxValue;

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Real, param.SqlDbType);
            Assert.Equal(DbType.Single, param.DbType);
        }

        [Fact]
        public void InferType_String()
        {
            string value = "some text";

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.NVarChar, param.SqlDbType);
            Assert.Equal(DbType.String, param.DbType);
        }

        [Fact]
        public void InferType_TimeSpan()
        {
            TimeSpan value = new TimeSpan(4, 6, 23);

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Time, param.SqlDbType);
            Assert.Equal(DbType.Time, param.DbType);
        }

        [Fact]
        public void InferType_DateTimeOffset()
        {
            DateTimeOffset value = new DateTimeOffset(new DateTime(2019, 10, 15), new TimeSpan(1, 0, 0));

            SqlParameter param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.DateTimeOffset, param.SqlDbType);
            Assert.Equal(DbType.DateTimeOffset, param.DbType);
        }

        [Fact]
        public void LocaleId()
        {
            SqlParameter parameter = new SqlParameter();
            Assert.Equal(0, parameter.LocaleId);
            parameter.LocaleId = 15;
            Assert.Equal(15, parameter.LocaleId);
        }

        [Fact]
        public void ParameterNullTest()
        {
            SqlParameter param = new SqlParameter("param", SqlDbType.Decimal);
            Assert.Equal(0, param.Scale);
            param.Value = DBNull.Value;
            Assert.Equal(0, param.Scale);

            param = new SqlParameter("param", SqlDbType.Int);
            Assert.Equal(0, param.Scale);
            param.Value = DBNull.Value;
            Assert.Equal(0, param.Scale);
        }

        [Fact]
        public void ParameterType()
        {
            SqlParameter p;

            // If Type is not set, then type is inferred from the value
            // assigned. The Type should be inferred everytime Value is assigned
            // If value is null or DBNull, then the current Type should be reset to NVarChar.
            p = new SqlParameter();
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            p.Value = DBNull.Value;
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            p.Value = 1;
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);
            p.Value = DBNull.Value;
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            p.Value = new byte[] { 0x0a };
            Assert.Equal(DbType.Binary, p.DbType);
            Assert.Equal(SqlDbType.VarBinary, p.SqlDbType);
            p.Value = null;
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            p.Value = DateTime.Now;
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.Value = null;
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);

            // If DbType is set, then the SqlDbType should not be
            // inferred from the value assigned.
            p = new SqlParameter();
            p.DbType = DbType.DateTime;
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.Value = 1;
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.Value = null;
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.Value = DBNull.Value;
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);

            // If SqlDbType is set, then the DbType should not be
            // inferred from the value assigned.
            p = new SqlParameter();
            p.SqlDbType = SqlDbType.VarBinary;
            Assert.Equal(SqlDbType.VarBinary, p.SqlDbType);
            p.Value = 1;
            Assert.Equal(SqlDbType.VarBinary, p.SqlDbType);
            p.Value = null;
            Assert.Equal(SqlDbType.VarBinary, p.SqlDbType);
            p.Value = DBNull.Value;
            Assert.Equal(SqlDbType.VarBinary, p.SqlDbType);
        }

        [Fact]
        public void InferType_Boolean()
        {
            bool value;
            SqlParameter param;

            value = false;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Bit, param.SqlDbType);
            Assert.Equal(DbType.Boolean, param.DbType);

            value = true;
            param = new SqlParameter();
            param.Value = value;
            Assert.Equal(SqlDbType.Bit, param.SqlDbType);
            Assert.Equal(DbType.Boolean, param.DbType);
        }

        [Fact]
        public void ParameterName()
        {
            SqlParameter p = new SqlParameter();
            p.ParameterName = "name";
            Assert.Equal("name", p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);

            p.ParameterName = null;
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);

            p.ParameterName = " ";
            Assert.Equal(" ", p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);

            p.ParameterName = " name ";
            Assert.Equal(" name ", p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);

            p.ParameterName = string.Empty;
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);
        }

        [Fact]
        public void ResetDbType()
        {
            SqlParameter p;

            p = new SqlParameter("foo", 42);
            p.ResetDbType();
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);
            Assert.Equal(42, p.Value);

            p.DbType = DbType.DateTime;
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.ResetDbType();
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);

            p = new SqlParameter("foo", SqlDbType.Int);
            p.ResetDbType();
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);

            p.SqlDbType = SqlDbType.DateTime;
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.ResetDbType();
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);

            p = new SqlParameter();
            p.Value = DateTime.MaxValue;
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            p.Value = null;
            p.ResetDbType();
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);

            p = new SqlParameter("foo", SqlDbType.VarChar);
            p.Value = DateTime.MaxValue;
            p.ResetDbType();
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            Assert.Equal(DateTime.MaxValue, p.Value);

            p = new SqlParameter("foo", SqlDbType.VarChar);
            p.Value = DBNull.Value;
            p.ResetDbType();
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(DBNull.Value, p.Value);

            p = new SqlParameter("foo", SqlDbType.VarChar);
            p.Value = null;
            p.ResetDbType();
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Null(p.Value);
        }

        [Theory]
        [InlineData(DbType.AnsiString, SqlDbType.VarChar)]
        [InlineData(DbType.AnsiStringFixedLength, SqlDbType.Char)]
        [InlineData(DbType.Binary, SqlDbType.VarBinary)]
        [InlineData(DbType.Boolean, SqlDbType.Bit)]
        [InlineData(DbType.Byte, SqlDbType.TinyInt)]
        [InlineData(DbType.Currency, SqlDbType.Money)]
        [InlineData(DbType.Date, SqlDbType.Date)]
        [InlineData(DbType.DateTime, SqlDbType.DateTime)]
        [InlineData(DbType.DateTime2, SqlDbType.DateTime2)]
        [InlineData(DbType.DateTimeOffset, SqlDbType.DateTimeOffset)]
        [InlineData(DbType.Decimal, SqlDbType.Decimal)]
        [InlineData(DbType.Double, SqlDbType.Float)]
        [InlineData(DbType.Guid, SqlDbType.UniqueIdentifier)]
        [InlineData(DbType.Int16, SqlDbType.SmallInt)]
        [InlineData(DbType.Int32, SqlDbType.Int)]
        [InlineData(DbType.Int64, SqlDbType.BigInt)]
        [InlineData(DbType.Object, SqlDbType.Variant)]
        [InlineData(DbType.Single, SqlDbType.Real)]
        [InlineData(DbType.String, SqlDbType.NVarChar)]
        [InlineData(DbType.Time, SqlDbType.Time)]
        [InlineData(DbType.Xml, SqlDbType.Xml)]
        public void Parameter_Supported(DbType dbType, SqlDbType sqlDbType)
        {
            var parameter = new SqlParameter();
            parameter.DbType = dbType;
            Assert.Equal(dbType, parameter.DbType);
            Assert.Equal(sqlDbType, parameter.SqlDbType);
        }

        [Theory]
        [InlineData(DbType.SByte)]
        [InlineData(DbType.UInt16)]
        [InlineData(DbType.UInt32)]
        [InlineData(DbType.UInt64)]
        [InlineData(DbType.VarNumeric)]
        public void Parameter_NotSupported(DbType dbType)
        {
            var parameter = new SqlParameter();
            Assert.Throws<ArgumentException>(() => parameter.DbType = dbType);
        }

        [Fact]
        public void ResetSqlDbType()
        {
            SqlParameter p1 = new SqlParameter("foo", 42);

            Assert.Equal(42, p1.Value);
            Assert.Equal(DbType.Int32, p1.DbType);
            Assert.Equal(SqlDbType.Int, p1.SqlDbType);

            p1.ResetSqlDbType();
            Assert.Equal(DbType.Int32, p1.DbType);
            Assert.Equal(SqlDbType.Int, p1.SqlDbType);

            p1.SqlDbType = SqlDbType.DateTime;
            Assert.Equal(DbType.DateTime, p1.DbType);
            Assert.Equal(SqlDbType.DateTime, p1.SqlDbType);

            p1.ResetSqlDbType();
            Assert.Equal(DbType.Int32, p1.DbType);
            Assert.Equal(SqlDbType.Int, p1.SqlDbType);

            SqlParameter p2 = new SqlParameter("foo", SqlDbType.Int);
            Assert.Null(p2.Value);
            Assert.Equal(DbType.Int32, p2.DbType);
            Assert.Equal(SqlDbType.Int, p2.SqlDbType);

            p2.ResetSqlDbType();
            Assert.Equal(DbType.String, p2.DbType);
            Assert.Equal(SqlDbType.NVarChar, p2.SqlDbType);

            p2.SqlDbType = SqlDbType.DateTime;
            Assert.Equal(DbType.DateTime, p2.DbType);
            Assert.Equal(SqlDbType.DateTime, p2.SqlDbType);

            p2.ResetSqlDbType();
            Assert.Equal(DbType.String, p2.DbType);
            Assert.Equal(SqlDbType.NVarChar, p2.SqlDbType);
        }

        [Fact]
        public void SourceColumn()
        {
            SqlParameter p = new SqlParameter();
            p.SourceColumn = "name";
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal("name", p.SourceColumn);

            p.SourceColumn = null;
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);

            p.SourceColumn = " ";
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(" ", p.SourceColumn);

            p.SourceColumn = " name ";
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(" name ", p.SourceColumn);

            p.SourceColumn = string.Empty;
            Assert.Equal(string.Empty, p.ParameterName);
            Assert.Equal(string.Empty, p.SourceColumn);
        }

        [Fact]
        public void SourceColumnNullMapping()
        {
            SqlParameter p = new SqlParameter();
            Assert.False(p.SourceColumnNullMapping);
            p.SourceColumnNullMapping = true;
            Assert.True(p.SourceColumnNullMapping);
            p.SourceColumnNullMapping = false;
            Assert.False(p.SourceColumnNullMapping);
        }

        [Fact]
        public void SqlDbTypeTest()
        {
            SqlParameter p = new SqlParameter("zipcode", 3510);
            p.SqlDbType = SqlDbType.DateTime;
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            Assert.Equal(3510, p.Value);
            p.SqlDbType = SqlDbType.VarChar;
            Assert.Equal(DbType.AnsiString, p.DbType);
            Assert.Equal(SqlDbType.VarChar, p.SqlDbType);
            Assert.Equal(3510, p.Value);
        }

        [Fact]
        public void SqlDbTypeTest_Value_Invalid()
        {
            SqlParameter p = new SqlParameter("zipcode", 3510);
            try
            {
                p.SqlDbType = (SqlDbType)666;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                // The SqlDbType enumeration value, 666, is
                // invalid
                Assert.Equal(typeof(ArgumentOutOfRangeException), ex.GetType());
                Assert.Null(ex.InnerException);
                Assert.NotNull(ex.Message);
                Assert.True(ex.Message.IndexOf("666") != -1);
                Assert.Equal("SqlDbType", ex.ParamName);
            }
        }

        [Fact]
        public void SqlValue()
        {
            SqlParameter parameter = new SqlParameter();
            Assert.Null(parameter.SqlValue);

            object value;

            value = "Char";
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlString), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlString)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = true;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Bit, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlBoolean), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlBoolean)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = (byte)0x0a;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.TinyInt, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlByte), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlByte)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = new DateTime(2008, 6, 4);
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.DateTime, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlDateTime), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlDateTime)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = Guid.NewGuid();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.UniqueIdentifier, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlGuid), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlGuid)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = (short)5;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.SmallInt, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlInt16), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlInt16)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = 10;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Int, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlInt32), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlInt32)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = 56L;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.BigInt, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlInt64), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlInt64)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            parameter.SqlValue = 45.5D;
            Assert.Equal(SqlDbType.Float, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlDouble), parameter.SqlValue.GetType());
            Assert.Equal(45.5D, ((SqlDouble)parameter.SqlValue).Value);
            Assert.Equal(45.5D, parameter.Value);

            value = 45m;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Decimal, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlDecimal), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlDecimal)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = 45f;
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Real, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlSingle), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlSingle)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            value = new byte[] { 0x0d, 0x0a };
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlBinary), parameter.SqlValue.GetType());
            Assert.Equal(value, ((SqlBinary)parameter.SqlValue).Value);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            value = 'X';
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlString), parameter.SqlValue.GetType());
            Assert.Equal("X", ((SqlString)parameter.SqlValue).Value);
            Assert.Equal("X", parameter.Value);

            value = new char[] { 'X', 'A' };
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlString), parameter.SqlValue.GetType());
            Assert.Equal("XA", ((SqlString)parameter.SqlValue).Value);
            Assert.Equal("XA", parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlBinary()
        {
            SqlParameter parameter;
            SqlBinary value = new SqlBinary(new byte[] { 0x0d, 0x0a });

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlBinary.Null;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.Equal(SqlBinary.Null, parameter.SqlValue);
            Assert.Equal(SqlBinary.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlBoolean()
        {
            SqlParameter parameter;
            SqlBoolean value = new SqlBoolean(false);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Bit, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlBoolean.Null;
            Assert.Equal(SqlDbType.Bit, parameter.SqlDbType);
            Assert.Equal(SqlBoolean.Null, parameter.SqlValue);
            Assert.Equal(SqlBoolean.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Bit, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlByte()
        {
            SqlParameter parameter;
            SqlByte value = new SqlByte(0x0d);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.TinyInt, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlByte.Null;
            Assert.Equal(SqlDbType.TinyInt, parameter.SqlDbType);
            Assert.Equal(SqlByte.Null, parameter.SqlValue);
            Assert.Equal(SqlByte.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.TinyInt, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlBytes()
        {
            SqlParameter parameter;
            SqlBytes value = new SqlBytes(new byte[] { 0x0d, 0x0a });

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.Same(value, parameter.SqlValue);
            Assert.Same(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlBytes.Null;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlBytes), parameter.SqlValue.GetType());
            Assert.True(((SqlBytes)parameter.SqlValue).IsNull);
            Assert.NotNull(parameter.Value);
            Assert.Equal(typeof(SqlBytes), parameter.Value.GetType());
            Assert.True(((SqlBytes)parameter.Value).IsNull);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.VarBinary, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlChars()
        {
            SqlParameter parameter;
            SqlChars value = new SqlChars(new char[] { 'X', 'A' });

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.Same(value, parameter.SqlValue);
            Assert.Same(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlChars.Null;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlChars), parameter.SqlValue.GetType());
            Assert.True(((SqlChars)parameter.SqlValue).IsNull);
            Assert.NotNull(parameter.Value);
            Assert.Equal(typeof(SqlChars), parameter.Value.GetType());
            Assert.True(((SqlChars)parameter.Value).IsNull);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.Same(value, parameter.SqlValue);
            Assert.Same(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlDateTime()
        {
            SqlParameter parameter;
            SqlDateTime value = new SqlDateTime(DateTime.Now);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.DateTime, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlDateTime.Null;
            Assert.Equal(SqlDbType.DateTime, parameter.SqlDbType);
            Assert.Equal(SqlDateTime.Null, parameter.SqlValue);
            Assert.Equal(SqlDateTime.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.DateTime, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlDecimal()
        {
            SqlParameter parameter;
            SqlDecimal value = new SqlDecimal(45m);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Decimal, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlDecimal.Null;
            Assert.Equal(SqlDbType.Decimal, parameter.SqlDbType);
            Assert.Equal(SqlDecimal.Null, parameter.SqlValue);
            Assert.Equal(SqlDecimal.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Decimal, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlDouble()
        {
            SqlParameter parameter;
            SqlDouble value = new SqlDouble(4.5D);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Float, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlDouble.Null;
            Assert.Equal(SqlDbType.Float, parameter.SqlDbType);
            Assert.Equal(SqlDouble.Null, parameter.SqlValue);
            Assert.Equal(SqlDouble.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Float, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlGuid()
        {
            SqlParameter parameter;
            SqlGuid value = new SqlGuid(Guid.NewGuid());

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.UniqueIdentifier, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlGuid.Null;
            Assert.Equal(SqlDbType.UniqueIdentifier, parameter.SqlDbType);
            Assert.Equal(SqlGuid.Null, parameter.SqlValue);
            Assert.Equal(SqlGuid.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.UniqueIdentifier, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlInt16()
        {
            SqlParameter parameter;
            SqlInt16 value = new SqlInt16((short)5);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.SmallInt, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlInt16.Null;
            Assert.Equal(SqlDbType.SmallInt, parameter.SqlDbType);
            Assert.Equal(SqlInt16.Null, parameter.SqlValue);
            Assert.Equal(SqlInt16.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.SmallInt, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlInt32()
        {
            SqlParameter parameter;
            SqlInt32 value = new SqlInt32(5);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Int, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlInt32.Null;
            Assert.Equal(SqlDbType.Int, parameter.SqlDbType);
            Assert.Equal(SqlInt32.Null, parameter.SqlValue);
            Assert.Equal(SqlInt32.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Int, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlInt64()
        {
            SqlParameter parameter;
            SqlInt64 value = new SqlInt64(5L);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.BigInt, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlInt64.Null;
            Assert.Equal(SqlDbType.BigInt, parameter.SqlDbType);
            Assert.Equal(SqlInt64.Null, parameter.SqlValue);
            Assert.Equal(SqlInt64.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.BigInt, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlMoney()
        {
            SqlParameter parameter;
            SqlMoney value = new SqlMoney(45m);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Money, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlMoney.Null;
            Assert.Equal(SqlDbType.Money, parameter.SqlDbType);
            Assert.Equal(SqlMoney.Null, parameter.SqlValue);
            Assert.Equal(SqlMoney.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Money, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlSingle()
        {
            SqlParameter parameter;
            SqlSingle value = new SqlSingle(45f);

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Real, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlSingle.Null;
            Assert.Equal(SqlDbType.Real, parameter.SqlDbType);
            Assert.Equal(SqlSingle.Null, parameter.SqlValue);
            Assert.Equal(SqlSingle.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Real, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlString()
        {
            SqlParameter parameter;
            SqlString value = new SqlString("XA");

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlString.Null;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.Equal(SqlString.Null, parameter.SqlValue);
            Assert.Equal(SqlString.Null, parameter.Value);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.NVarChar, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Equal(value, parameter.Value);
        }

        [Fact]
        public void SqlTypes_SqlXml()
        {
            SqlParameter parameter;
            SqlXml value = new SqlXml(new System.Xml.XmlTextReader(new System.IO.StringReader("<test>CoreFx</test>")));

            parameter = new SqlParameter();
            parameter.SqlValue = value;
            Assert.Equal(SqlDbType.Xml, parameter.SqlDbType);
            Assert.Equal(value, parameter.SqlValue);
            Assert.Same(value, parameter.Value);

            parameter = new SqlParameter();
            parameter.SqlValue = SqlXml.Null;
            Assert.Equal(SqlDbType.Xml, parameter.SqlDbType);
            Assert.NotNull(parameter.SqlValue);
            Assert.Equal(typeof(SqlXml), parameter.SqlValue.GetType());
            Assert.True(((SqlXml)parameter.SqlValue).IsNull);
            Assert.NotNull(parameter.Value);
            Assert.Equal(typeof(SqlXml), parameter.Value.GetType());
            Assert.True(((SqlXml)parameter.Value).IsNull);

            parameter = new SqlParameter();
            parameter.Value = value;
            Assert.Equal(SqlDbType.Xml, parameter.SqlDbType);
            Assert.Same(value, parameter.SqlValue);
            Assert.Same(value, parameter.Value);
        }

        [Fact]
        public void Value()
        {
            SqlParameter p;

            p = new SqlParameter("name", (object)null);
            p.Value = 42;
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);
            Assert.Equal(42, p.Value);

            p.Value = DBNull.Value;
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Equal(DBNull.Value, p.Value);

            p.Value = DateTime.MaxValue;
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(SqlDbType.DateTime, p.SqlDbType);
            Assert.Equal(DateTime.MaxValue, p.Value);

            p.Value = null;
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(SqlDbType.NVarChar, p.SqlDbType);
            Assert.Null(p.Value);

            p = new SqlParameter("zipcode", SqlDbType.Int);
            p.Value = DateTime.MaxValue;
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);
            Assert.Equal(DateTime.MaxValue, p.Value);

            p.Value = null;
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);
            Assert.Null(p.Value);

            p.Value = DBNull.Value;
            Assert.Equal(DbType.Int32, p.DbType);
            Assert.Equal(SqlDbType.Int, p.SqlDbType);
            Assert.Equal(DBNull.Value, p.Value);
        }

        [Fact]
        public void XmlSchemaTest()
        {
            SqlParameter p1 = new SqlParameter();

            Assert.Equal(string.Empty, p1.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionOwningSchema);

            p1.XmlSchemaCollectionDatabase = "database";
            Assert.Equal("database", p1.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionOwningSchema);

            p1.XmlSchemaCollectionName = "name";
            Assert.Equal("database", p1.XmlSchemaCollectionDatabase);
            Assert.Equal("name", p1.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionOwningSchema);

            p1.XmlSchemaCollectionOwningSchema = "schema";
            Assert.Equal("database", p1.XmlSchemaCollectionDatabase);
            Assert.Equal("name", p1.XmlSchemaCollectionName);
            Assert.Equal("schema", p1.XmlSchemaCollectionOwningSchema);

            p1.XmlSchemaCollectionDatabase = null;
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionDatabase);
            Assert.Equal("name", p1.XmlSchemaCollectionName);
            Assert.Equal("schema", p1.XmlSchemaCollectionOwningSchema);

            p1.XmlSchemaCollectionName = "";
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionDatabase);
            Assert.Equal("", p1.XmlSchemaCollectionName);
            Assert.Equal("schema", p1.XmlSchemaCollectionOwningSchema);

            p1.XmlSchemaCollectionOwningSchema = "  a  ";
            Assert.Equal(string.Empty, p1.XmlSchemaCollectionDatabase);
            Assert.Equal("", p1.XmlSchemaCollectionName);
            Assert.Equal("  a  ", p1.XmlSchemaCollectionOwningSchema);
        }

        private enum ByteEnum : byte
        {
            A = 0x0a,
            B = 0x0d
        }

        private enum Int64Enum : long
        {
            A = long.MinValue,
            B = long.MaxValue
        }
    }
}
