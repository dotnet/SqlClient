// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using System.Reflection;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlMetaDataTest
    {
        [Theory]
        [MemberData(nameof(SqlMetaDataAdjustValues))]
        [MemberData(nameof(SqlMetaDataDateTimeValues))]
        public void Adjust(SqlDbType dbType, object expected)
        {
            SqlMetaData metaData = new SqlMetaData(
                "col1",
                dbType,
                4,
                2,
                2,
                0,
                SqlCompareOptions.IgnoreCase,
                null,
                true,
                true,
                SortOrder.Ascending,
                0);

            object actual = metaData.Adjust(expected);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(SqlMetaDataInvalidValues))]
        public void AdjustInvalidType_Throws(SqlDbType dbType, object expected)
        {
            SqlMetaData metaData = new SqlMetaData(
                "col1",
                dbType,
                4,
                2,
                2,
                0,
                SqlCompareOptions.IgnoreCase,
                null,
                true,
                true,
                SortOrder.Ascending,
                0);
            ArgumentException ex = Assert.ThrowsAny<ArgumentException>(() =>
            {
                object actual = metaData.Adjust(expected);
            });
            Assert.NotNull(ex);
            Assert.NotEmpty(ex.Message);
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AdjustStringNull()
        {
            SqlMetaData metaData = new SqlMetaData(
                            "col1",
                            SqlDbType.VarChar,
                            4,
                            2,
                            2,
                            0,
                            SqlCompareOptions.IgnoreCase,
                            null,
                            true,
                            true,
                            SortOrder.Ascending,
                            0);
            string value = null;
            string ret = metaData.Adjust(value);
            Assert.Null(ret);
        }

        [Theory]
        [MemberData(nameof(SqlMetaDataMaxLengthTrimValues))]
        public void AdjustMaxLength(SqlDbType dbType, object value)
        {
            SqlMetaData metaData = new SqlMetaData(
                            "col1",
                            dbType,
                            4,
                            2,
                            2,
                            0,
                            SqlCompareOptions.IgnoreCase,
                            null,
                            true,
                            true,
                            SortOrder.Ascending,
                            0);
            object actual = metaData.Adjust(value);
            Assert.NotEqual(value, actual);
        }

        [Fact]
        public void AdjustXml()
        {
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Xml, "NorthWindDb", "Schema", "ObjectName");
            SqlXml xml = metaData.Adjust(SqlXml.Null);
            Assert.True(xml.IsNull);
        }

        [Fact]
        public void AdjustXml_Throws()
        {
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Xml, "NorthWindDb", "Schema", "ObjectName");
            SqlXml xml = metaData.Adjust(SqlXml.Null);
            Assert.True(xml.IsNull);
        }

        [Theory]
        [MemberData(nameof(SqlMetaDataInferredValues))]
        public void InferFromValue(SqlDbType expectedDbType, object value)
        {
            SqlMetaData metaData = SqlMetaData.InferFromValue(value, "col1");
            Assert.Equal(expectedDbType, metaData.SqlDbType);
        }

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
            Assert.Equal(DbType.AnsiString, metaData.DbType);
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
        public void NameDbTypeDatabaseOwningSchemaObjectNameConstructor()
        {
            SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Xml, "NorthWindDb", "schema", "name");
            Assert.Equal("col2", metaData.Name);
            Assert.Equal(SqlDbType.Xml, metaData.SqlDbType);
            Assert.Equal("NorthWindDb", metaData.XmlSchemaCollectionDatabase);
            Assert.Equal("schema", metaData.XmlSchemaCollectionOwningSchema);
            Assert.Equal("name", metaData.XmlSchemaCollectionName);
            Assert.Equal("xml", metaData.TypeName);
        }

        [Fact]
        public void InvalidDbTypeNameDbTypeDatabaseOwningSchemaObjectNameConstructor_Throws()
        {
            ArgumentException ex = Assert.ThrowsAny<ArgumentException>(() => {
                SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Int, "NorthWindDb", "schema", "name");
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

        [Fact]
        public void IsPartialLengthTrueGetPartialLengthMetaData()
        {
            Type sqlMetaDataType = typeof(SqlMetaData);
            SqlMetaData exampleMetaData = new SqlMetaData("col2", SqlDbType.Int);
            FieldInfo isPartialLengthField = sqlMetaDataType.GetField("_partialLength", BindingFlags.NonPublic | BindingFlags.Instance);
            isPartialLengthField.SetValue(exampleMetaData, true);
            MethodInfo method = sqlMetaDataType.GetMethod("GetPartialLengthMetaData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            SqlMetaData metaData =  (SqlMetaData)method.Invoke(exampleMetaData, new object[] { exampleMetaData });
            Assert.Equal(exampleMetaData, metaData);
        }

        [Fact]
        public void NonVarTypeGetPartialLengthMetaData()
        {
            Type sqlMetaDataType = typeof(SqlMetaData);
            SqlMetaData exampleMetaData = new SqlMetaData("col2", SqlDbType.Int);
            MethodInfo method = sqlMetaDataType.GetMethod("GetPartialLengthMetaData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            SqlMetaData metaData = (SqlMetaData)method.Invoke(exampleMetaData, new object[] { exampleMetaData });
            Assert.Equal(exampleMetaData, metaData);
        }

        [Fact]
        public void SqlDataTypeXMLGetPartialLengthMetaData_Throws()
        {
            Type sqlMetaDataType = typeof(SqlMetaData);
            SqlMetaData exampleMetaData = new SqlMetaData("col2", SqlDbType.Xml);
            MethodInfo method = sqlMetaDataType.GetMethod("GetPartialLengthMetaData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            SqlMetaData metaData = null;
            Exception ex = Assert.ThrowsAny<Exception>(() => metaData = (SqlMetaData)method.Invoke(exampleMetaData, new object[] { exampleMetaData }) );
            Assert.NotNull(ex);
            Assert.NotNull(ex.InnerException);
            Assert.IsType<ArgumentException>(ex.InnerException);
            Assert.NotEmpty(ex.InnerException.Message);
            Assert.Contains("metadata", ex.InnerException.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData(SqlDbType.NVarChar)]
        [InlineData(SqlDbType.VarChar)]
        [InlineData(SqlDbType.VarBinary)]
        public void VarSqlDataTypeGetPartialLengthMetaData(SqlDbType sqlDbType)
        {
            Type sqlMetaDataType = typeof(SqlMetaData);
            SqlMetaData exampleMetaData = new SqlMetaData("col2", sqlDbType, 16);
            MethodInfo method = sqlMetaDataType.GetMethod("GetPartialLengthMetaData", BindingFlags.NonPublic | BindingFlags.Static);
            SqlMetaData metaData = metaData = (SqlMetaData)method.Invoke(exampleMetaData, new object[] { exampleMetaData });
            Assert.Equal(exampleMetaData.Name, metaData.Name);
            Assert.Equal(exampleMetaData.SqlDbType, metaData.SqlDbType);
            Assert.Equal(SqlMetaData.Max, metaData.MaxLength);
            Assert.Equal(0, metaData.Precision);
            Assert.Equal(0, metaData.Scale);
            Assert.Equal(exampleMetaData.LocaleId, metaData.LocaleId);
            Assert.Equal(exampleMetaData.CompareOptions, metaData.CompareOptions);
            Assert.Null(metaData.XmlSchemaCollectionDatabase);
            Assert.Null(metaData.XmlSchemaCollectionName);
            Assert.Null(metaData.XmlSchemaCollectionOwningSchema);
            // PartialLength is an interal property
            PropertyInfo isPartialLengthProp = sqlMetaDataType.GetProperty("IsPartialLength", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.True((bool)isPartialLengthProp.GetValue(metaData));
            Assert.Equal(exampleMetaData.Type, metaData.Type);
        }


        #region Test values
        public static readonly object[][] SqlMetaDataDateTimeValues =
        {
            new object[] {SqlDbType.DateTime, new SqlDateTime(DateTime.UtcNow)},
            new object[] {SqlDbType.Date, DateTime.Today},
            new object[] {SqlDbType.DateTime, DateTime.Today},
            new object[] {SqlDbType.SmallDateTime, DateTime.Today},
        };

        public static readonly object[][] SqlMetaDataMaxLengthTrimValues =
        {
            new object[] {SqlDbType.Binary, new SqlBinary(new byte[] { 1, 2, 3, 4, 5 })},
            new object[] {SqlDbType.Binary, new byte[] { 1, 2, 3, 4, 5 }},
            new object[] {SqlDbType.Char, "Tests"},
            new object[] {SqlDbType.NChar, "Tests"},
            new object[] {SqlDbType.VarChar, "Tests" },
            new object[] {SqlDbType.NVarChar, "Tests"},
            new object[] {SqlDbType.NChar, "T"},
            new object[] {SqlDbType.Char, "T"},
            new object[] {SqlDbType.Binary, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.Binary, new byte[] { 1 }},
            new object[] {SqlDbType.Timestamp, new SqlBytes(new byte[] { 1 })},
        };

        public static readonly object[][] SqlMetaDataInvalidValues =
        {
            new object[] {SqlDbType.Char, 'T'},
            new object[] {SqlDbType.NChar, 'T'},
            new object[] {SqlDbType.Text, 'T'},
            new object[] {SqlDbType.NText, 'T'},
            new object[] {SqlDbType.Date, SqlDateTime.Null},
            new object[] {SqlDbType.SmallInt, 1},
            new object[] {SqlDbType.VarChar, SqlInt32.Zero},
            new object[] {SqlDbType.BigInt, (short)1},
            new object[] {SqlDbType.NVarChar, SqlInt16.Zero},
            new object[] {SqlDbType.Text, 10L},
            new object[] {SqlDbType.Binary, SqlInt64.Zero},
            new object[] {SqlDbType.Float, 1.0f},
            new object[] {SqlDbType.NChar, SqlSingle.Zero},
            new object[] {SqlDbType.Timestamp, 1.0d},
            new object[] {SqlDbType.Real, SqlDouble.Zero},
            new object[] {SqlDbType.VarBinary, false},
            new object[] {SqlDbType.NText, SqlBoolean.False},
            new object[] {SqlDbType.Time, (byte)1},
            new object[] {SqlDbType.Bit, SqlByte.Zero},
            new object[] {SqlDbType.Decimal, SqlMoney.Zero},
            new object[] {SqlDbType.SmallMoney, SqlDecimal.Null},
            new object[] {SqlDbType.Money, SqlDecimal.Null},
            new object[] {SqlDbType.Bit, SqlString.Null},
            new object[] {SqlDbType.Int, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.Timestamp, new SqlString("T")},
            new object[] {SqlDbType.Image, SqlChars.Null},
            new object[] {SqlDbType.Int, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.Float, SqlBinary.Null},
            new object[] {SqlDbType.Float, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Float, SqlBytes.Null},
            new object[] {SqlDbType.Float, new SqlBytes(new byte[] { 1, 0, 0, 0 })},
            new object[] {SqlDbType.Float, new byte[] { 1, 0, 0, 0 }},
            new object[] {SqlDbType.TinyInt, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.Bit, SqlBinary.Null},
            new object[] {SqlDbType.Bit, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Decimal, new SqlBytes()},
            new object[] {SqlDbType.Char, new TimeSpan(0, 0, 1)},
            new object[] {SqlDbType.UniqueIdentifier, new DateTimeOffset(new DateTime(0))},
            new object[] {SqlDbType.DateTimeOffset, SqlGuid.Null},
            new object[] {SqlDbType.Date, new SqlDateTime(DateTime.UtcNow)},
            new object[] {SqlDbType.Bit, SqlXml.Null },
            new object[] {SqlDbType.Bit, (sbyte)0},
            new object[] {SqlDbType.Bit, (UInt16)1},
            new object[] {SqlDbType.Bit, (UInt32)1},
            new object[] {SqlDbType.Bit, (UInt64)1},
            new object[] {SqlDbType.Bit, (sbyte)0},
            new object[] {SqlDbType.Int, Guid.Empty},
            new object[] {SqlDbType.NText, 'T'},
            new object[] {SqlDbType.SmallMoney, (decimal)int.MaxValue}
        };

        public static readonly object[][] SqlMetaDataAdjustValues =
        {
            new object[] {SqlDbType.Int, 1},
            new object[] {SqlDbType.Int, SqlInt32.Zero},
            new object[] {SqlDbType.SmallInt, (short)1},
            new object[] {SqlDbType.SmallInt, SqlInt16.Zero},
            new object[] {SqlDbType.BigInt, 10L},
            new object[] {SqlDbType.BigInt, SqlInt64.Zero},
            new object[] {SqlDbType.Real, 1.0f},
            new object[] {SqlDbType.Real, SqlSingle.Zero},
            new object[] {SqlDbType.Float, 1.0d},
            new object[] {SqlDbType.Float, SqlDouble.Zero},
            new object[] {SqlDbType.Bit, false},
            new object[] {SqlDbType.Bit, SqlBoolean.False},
            new object[] {SqlDbType.TinyInt, (byte)1},
            new object[] {SqlDbType.TinyInt, SqlByte.Zero},
            new object[] {SqlDbType.Money, 10.01M },
            new object[] {SqlDbType.Money, SqlMoney.Zero},
            new object[] {SqlDbType.SmallMoney, SqlMoney.Zero},
            new object[] {SqlDbType.SmallMoney, 10.01M },
            new object[] {SqlDbType.Decimal, 0M },
            new object[] {SqlDbType.Decimal, SqlDecimal.Null},
            new object[] {SqlDbType.Char, SqlString.Null},
            new object[] {SqlDbType.Char, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.Char, "Test"},
            new object[] {SqlDbType.Char, new SqlString("T")},
            new object[] {SqlDbType.Char, new SqlString("Test")},
            new object[] {SqlDbType.Char, SqlChars.Null},
            new object[] {SqlDbType.Char, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NChar, SqlString.Null},
            new object[] {SqlDbType.NChar, new char[] {'T','e' ,'s', 't'}},
            new object[] {SqlDbType.NChar, SqlChars.Null},
            new object[] {SqlDbType.NChar, "Test"},
            new object[] {SqlDbType.NChar, new SqlString("T")},
            new object[] {SqlDbType.NChar, new SqlString("Test")},
            new object[] {SqlDbType.NChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.VarChar, 'T'},
            new object[] {SqlDbType.VarChar, "Test"},
            new object[] {SqlDbType.VarChar, new SqlString("T")},
            new object[] {SqlDbType.VarChar, new SqlString("Test")},
            new object[] {SqlDbType.VarChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NVarChar, 'T'},
            new object[] {SqlDbType.NVarChar, "T"},
            new object[] {SqlDbType.NText, "T"},
            new object[] {SqlDbType.VarChar, "T"},
            new object[] {SqlDbType.NVarChar, "Test"},
            new object[] {SqlDbType.NVarChar, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.NVarChar, new SqlString("T")},
            new object[] {SqlDbType.NVarChar, new SqlString("Test")},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NText, "Test"},
            new object[] {SqlDbType.NText, "Tests"},
            new object[] {SqlDbType.NText, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.NText, new SqlString("T")},
            new object[] {SqlDbType.NText, new SqlString("Test")},
            new object[] {SqlDbType.NText, new SqlString(new string('T', 17))},
            new object[] {SqlDbType.NText, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NText, new SqlChars(new char[] { 'T', 'e', '.', 't' })},
            new object[] {SqlDbType.Text, "Tests"},
            new object[] {SqlDbType.Text, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.Text, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.Binary, SqlBinary.Null},
            new object[] {SqlDbType.Binary, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Binary, SqlBytes.Null},
            new object[] {SqlDbType.Binary, new SqlBytes(new byte[] { 1, 0, 0, 0 })},
            new object[] {SqlDbType.Binary, new byte[] { 1, 0, 0, 0 }},
            new object[] {SqlDbType.VarBinary, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.Timestamp, SqlBinary.Null},
            new object[] {SqlDbType.Timestamp, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Timestamp, new SqlBytes()},
            new object[] {SqlDbType.Image, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Image, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.Time, new TimeSpan(0, 0, 1)},
            new object[] {SqlDbType.DateTimeOffset, new DateTimeOffset(new DateTime(0))},
            new object[] {SqlDbType.UniqueIdentifier, SqlGuid.Null},
            new object[] {SqlDbType.UniqueIdentifier, Guid.Empty},
            new object[] {SqlDbType.Int, null},
            new object[] {SqlDbType.Timestamp, new SqlBinary(new byte[] { 1, 2, 3, 4, 5 })},
            new object[] {SqlDbType.Binary, new SqlBytes(new byte[] { 1, 2, 3, 4, 5 })},
        };

        public static readonly object[][] SqlMetaDataInferredValues =
        {
            new object[] {SqlDbType.Int, 1},
            new object[] {SqlDbType.Int, SqlInt32.Zero},
            new object[] {SqlDbType.SmallInt, (short)1},
            new object[] {SqlDbType.SmallInt, SqlInt16.Zero},
            new object[] {SqlDbType.BigInt, 10L},
            new object[] {SqlDbType.BigInt, SqlInt64.Zero},
            new object[] {SqlDbType.Real, 1.0f},
            new object[] {SqlDbType.Real, SqlSingle.Zero},
            new object[] {SqlDbType.Float, 1.0d},
            new object[] {SqlDbType.Float, SqlDouble.Zero},
            new object[] {SqlDbType.Bit, false},
            new object[] {SqlDbType.Bit, SqlBoolean.False},
            new object[] {SqlDbType.TinyInt, (byte)1},
            new object[] {SqlDbType.TinyInt, SqlByte.Zero},
            new object[] {SqlDbType.Money, SqlMoney.Zero},
            new object[] {SqlDbType.Decimal, SqlDecimal.Null},
            // new object[] {SqlDbType.Decimal, new SqlDecimal(2, 2, true, new int[] { 1, 2}) },
            new object[] {SqlDbType.Decimal, 10.01M },
            new object[] {SqlDbType.NVarChar, 'T'},
            new object[] {SqlDbType.NVarChar, "T"},
            new object[] {SqlDbType.NVarChar, "Test"},
            new object[] {SqlDbType.NVarChar, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.NVarChar, new SqlString("T")},
            new object[] {SqlDbType.NVarChar, new SqlString("Test")},
            new object[] {SqlDbType.NVarChar, SqlString.Null},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] { 'T' })},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NVarChar, SqlChars.Null},
            new object[] {SqlDbType.VarBinary, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.VarBinary, SqlBytes.Null},
            new object[] {SqlDbType.Time, new TimeSpan(0, 0, 1)},
            new object[] {SqlDbType.DateTimeOffset, new DateTimeOffset(new DateTime(0))},
            new object[] {SqlDbType.UniqueIdentifier, SqlGuid.Null},
            new object[] {SqlDbType.UniqueIdentifier, Guid.Empty},
            new object[] {SqlDbType.DateTime, new SqlDateTime(DateTime.UtcNow)},
            new object[] {SqlDbType.DateTime, DateTime.Today},
            new object[] {SqlDbType.Xml, new SqlXml()},
            new object[] {SqlDbType.Variant, new object()}
        };
        #endregion
    }
}
