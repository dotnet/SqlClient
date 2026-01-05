// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Reflection;
using Xunit;

using SwitchesHelper = Microsoft.Data.SqlClient.Tests.Common.LocalAppContextSwitchesHelper;

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
        public void Constructor2_Name_Null()
        {
            SqlParameter p = new SqlParameter(null, DBNull.Value);
            Assert.Equal(string.Empty, p.ParameterName);
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

        [Theory]
        [InlineData(15)]
        [InlineData(1044)]
        [InlineData(1047)]
        [InlineData(1056)]
        [InlineData(1065)]
        [InlineData(1068)]
        [InlineData(1070)]
        [InlineData(1071)]
        [InlineData(1081)]
        [InlineData(1082)]
        [InlineData(1083)]
        [InlineData(1087)]
        [InlineData(1090)]
        [InlineData(1091)]
        [InlineData(1092)]
        [InlineData(1093)]
        [InlineData(1101)]
        [InlineData(1105)]
        [InlineData(1106)]
        [InlineData(1107)]
        [InlineData(1108)]
        [InlineData(1114)]
        [InlineData(1121)]
        [InlineData(1122)]
        [InlineData(1123)]
        [InlineData(1125)]
        [InlineData(1133)]
        [InlineData(1146)]
        [InlineData(1148)]
        [InlineData(1150)]
        [InlineData(1152)]
        [InlineData(1153)]
        [InlineData(1155)]
        [InlineData(1157)]
        [InlineData(1164)]
        [InlineData(2074)]
        [InlineData(2092)]
        [InlineData(2107)]
        [InlineData(2143)]
        [InlineData(3076)]
        [InlineData(3098)]
        [InlineData(5124)]
        [InlineData(5146)]
        [InlineData(8218)]
        public void LocaleId(int lcid)
        {
            // To verify all the cases in SqlCollation.FirstSupportedCollationVersion
            SqlParameter parameter = new SqlParameter();
            Assert.Equal(0, parameter.LocaleId);
            parameter.LocaleId = lcid;
            Assert.Equal(parameter.LocaleId, lcid);
        }

        [Fact]
        public void LocaleIdOutOfRange_Throws()
        {
            SqlParameter parameter = new SqlParameter();
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => { parameter.LocaleId = -1; });
            Assert.NotNull(ex.ParamName);
            Assert.Contains("LocaleId", ex.ParamName, StringComparison.OrdinalIgnoreCase);
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

        public static IEnumerable<object[]> ParameterSupportedData()
        {
            return new object[][]
            {
                new object[]{ DbType.AnsiString, SqlDbType.VarChar },
                new object[]{ DbType.AnsiStringFixedLength, SqlDbType.Char },
                new object[]{ DbType.Binary, SqlDbType.VarBinary },
                new object[]{ DbType.Boolean, SqlDbType.Bit },
                new object[]{ DbType.Byte, SqlDbType.TinyInt },
                new object[]{ DbType.Currency, SqlDbType.Money },
                new object[]{ DbType.Date, SqlDbType.Date },
                new object[]{ DbType.DateTime, SqlDbType.DateTime },
                new object[]{ DbType.DateTime2, SqlDbType.DateTime2 },
                new object[]{ DbType.DateTimeOffset, SqlDbType.DateTimeOffset },
                new object[]{ DbType.Decimal, SqlDbType.Decimal },
                new object[]{ DbType.Double, SqlDbType.Float },
                new object[]{ DbType.Guid, SqlDbType.UniqueIdentifier },
                new object[]{ DbType.Int16, SqlDbType.SmallInt },
                new object[]{ DbType.Int32, SqlDbType.Int },
                new object[]{ DbType.Int64, SqlDbType.BigInt },
                new object[]{ DbType.Object, SqlDbType.Variant },
                new object[]{ DbType.Single, SqlDbType.Real },
                new object[]{ DbType.String, SqlDbType.NVarChar },
                new object[]{ DbType.Time, SqlDbType.Time },
                new object[]{ DbType.Xml, SqlDbType.Xml }
            };
        }
        
        [Theory]
        [MemberData(
            nameof(ParameterSupportedData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void Parameter_Supported(DbType dbType, SqlDbType sqlDbType)
        {
            var parameter = new SqlParameter();
            parameter.DbType = dbType;
            Assert.Equal(dbType, parameter.DbType);
            Assert.Equal(sqlDbType, parameter.SqlDbType);
        }

        public static IEnumerable<object[]> ParameterNotSupportedData()
        {
            return new object[][]
            {
                new object[]{ DbType.SByte },
                new object[]{ DbType.UInt16 },
                new object[]{ DbType.UInt32 },
                new object[]{ DbType.UInt64 },
                new object[]{ DbType.VarNumeric }
            };
        }

        [Theory]
        [MemberData(
            nameof(ParameterNotSupportedData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
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
                Assert.True(ex.Message.IndexOf("666", StringComparison.Ordinal) != -1);
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

        [Fact]
        public void CreateParameterWithValidXmlSchema()
        {
            string xmlDatabase = "database";
            string xmlSchema = "schema";
            string xmlName = "name";

            SqlParameter parameter = new SqlParameter("@name", SqlDbType.Int, 4, ParameterDirection.Input, 0, 0, "name", DataRowVersion.Original, false, 1, xmlDatabase, xmlSchema, xmlName);

            Assert.Equal(xmlDatabase, parameter.XmlSchemaCollectionDatabase);
            Assert.Equal(xmlSchema, parameter.XmlSchemaCollectionOwningSchema);
            Assert.Equal(xmlName, parameter.XmlSchemaCollectionName);
        }

        [Fact]
        public void CreateParameterWithEmptyXmlSchema()
        {
            SqlParameter parameter = new SqlParameter("@name", SqlDbType.Int, 4, ParameterDirection.Input, 0, 0, "name", DataRowVersion.Original, false, 1, string.Empty, string.Empty, string.Empty);

            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionOwningSchema);
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionName);
        }

        [Fact]
        public void CreateParameterWithNullXmlSchema()
        {
            SqlParameter parameter = new SqlParameter("@name", SqlDbType.Int, 4, ParameterDirection.Input, 0, 0, "name", DataRowVersion.Original, false, 1, null, null, null);

            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionOwningSchema);
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionName);
        }

        [Fact]
        public void CreateParameterWithoutXmlSchema()
        {
            SqlParameter parameter = new SqlParameter();

            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionOwningSchema);
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionName);
        }

        [Fact]
        public void SetParameterXmlSchema()
        {
            SqlParameter parameter = new SqlParameter();

            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionName);

            // verify that if we set it to null we still get an empty string back
            parameter.XmlSchemaCollectionName = null;
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionName);

            // verify that if we set a value we get it back
            parameter.XmlSchemaCollectionName = "name";
            Assert.Equal("name", parameter.XmlSchemaCollectionName);

            // verify that if we set it explicitly to null it reverts to empty string
            parameter.XmlSchemaCollectionName = null;
            Assert.Equal(string.Empty, parameter.XmlSchemaCollectionName);
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


        private static readonly object _parameterLegacyScaleLock = new();

        [Theory]
        [InlineData(null, 7, true)]
        [InlineData(0, 7, true)]
        [InlineData(1, 1, true)]
        [InlineData(2, 2, true)]
        [InlineData(3, 3, true)]
        [InlineData(4, 4, true)]
        [InlineData(5, 5, true)]
        [InlineData(6, 6, true)]
        [InlineData(7, 7, true)]
        [InlineData(null, 7, false)]
        [InlineData(0, 0, false)]
        [InlineData(1, 1, false)]
        [InlineData(2, 2, false)]
        [InlineData(3, 3, false)]
        [InlineData(4, 4, false)]
        [InlineData(5, 5, false)]
        [InlineData(6, 6, false)]
        [InlineData(7, 7, false)]
        public void SqlDateTime2Scale_Legacy(int? setScale, byte outputScale, bool legacyVarTimeZeroScaleSwitchValue)
        {
            lock (_parameterLegacyScaleLock)
            {
                using SwitchesHelper switches = new SwitchesHelper();
                switches.LegacyVarTimeZeroScaleBehaviour =
                    legacyVarTimeZeroScaleSwitchValue;

                var parameter = new SqlParameter
                {
                    DbType = DbType.DateTime2
                };
                if (setScale.HasValue)
                {
                    parameter.Scale = (byte)setScale.Value;
                }

                var actualScale = (byte)typeof(SqlParameter).GetMethod("GetActualScale", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(parameter, null);

                Assert.Equal(outputScale, actualScale);
            }
        }
    }
}
