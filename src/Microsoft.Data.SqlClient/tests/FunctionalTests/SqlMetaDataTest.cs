// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Reflection;
using Microsoft.Data.SqlClient.Server;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlMetaDataTest
    {
        [Theory]
        [MemberData(
            nameof(SqlMetaDataAdjustValues),
            // xUnit can't consistently serialize the data for this test, so we
            // disable enumeration of the test data to avoid warnings on the
            // console.
            DisableDiscoveryEnumeration = true)]
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
        [MemberData(
            nameof(SqlMetaDataMaxLengthTrimValues),
            DisableDiscoveryEnumeration = true)]
        public void AdjustWithGreaterThanMaxLengthValues(SqlDbType dbType, object value)
        {
            int maxLength = 4;
            SqlMetaData metaData = new SqlMetaData(
                "col1",
                dbType,
                maxLength,
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

        [Theory]
        [MemberData(
            nameof(SqlMetaDataInvalidValues),
            DisableDiscoveryEnumeration = true)]
        public void AdjustWithInvalidType_Throws(SqlDbType dbType, object expected)
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
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }


        [Fact]
        public void AdjustWithNullBytes()
        {
            SqlMetaData metaData = new SqlMetaData(
                "col1",
                SqlDbType.Binary,
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

            byte[] array = null;
            object actual = metaData.Adjust(array);
            Assert.Null(actual);
        }

        [Fact]
        public void AdjustWithNullChars()
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

            char[] array = null;
            object actual = metaData.Adjust(array);
            Assert.Null(actual);
        }

        [Fact]
        public void AdjustWithNullString()
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

        [Fact]
        public void AdjustWithOutOfRangeDateTime()
        {
            SqlMetaData metaData = new SqlMetaData(
                "col1",
                SqlDbType.SmallDateTime,
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

            DateTime date = new DateTime(2080, 06, 06, 23, 59, 29, 999);
            ArgumentException ex = Assert.ThrowsAny<ArgumentException>(() =>
            {
                object actual = metaData.Adjust(date);
            });
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AdjustWithOutOfRangeTimeSpan_Throws()
        {
            SqlMetaData metaData = new SqlMetaData(
                "col1",
                SqlDbType.Time,
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

            TimeSpan outOfRangeTimespan = new TimeSpan(TimeSpan.TicksPerDay);
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                object actual = metaData.Adjust(outOfRangeTimespan);
            });
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void AdjustXml()
        {
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Xml, "NorthWindDb", "Schema", "ObjectName");
            SqlXml xml = metaData.Adjust(SqlXml.Null);
            Assert.True(xml.IsNull);
        }

        [Fact]
        public void ConstructorWithDefaultLocale()
        {
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.NText, true, true, SortOrder.Ascending, 0);
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(CultureInfo.CurrentCulture.LCID, metaData.LocaleId);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Fact]
        public void ConstructorWithDefaultLocaleInvalidType_Throws()
        {
            SqlDbType invalidType = SqlDbType.Structured;
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", invalidType, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<object[]> ConstructorCharData()
        {
            return new object[][]
            {
                new object[] { SqlDbType.Char },
                new object[] { SqlDbType.VarChar },
                new object[] { SqlDbType.NChar },
                new object[] { SqlDbType.NVarChar }
            };
        }

        [Theory]
        [MemberData(
            nameof(ConstructorCharData)
#if NETFRAMEWORK
            // .NET Framework puts system enums in something called the Global
            // Assembly Cache (GAC), and xUnit refuses to serialize enums that
            // live there.  So for .NET Framework, we disable enumeration of the
            // test data to avoid warnings on the console when running tests.
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void ConstructorWithMaxLengthAndDefaultLocale(SqlDbType dbType)
        {
            const int maxLength = 5;
            SqlMetaData metaData = new SqlMetaData("col1", dbType, maxLength, true, true, SortOrder.Ascending, 0);
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(CultureInfo.CurrentCulture.LCID, metaData.LocaleId);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Fact]
        public void ConstructorWithMaxLengthAndDefaultLocaleInvalidType_Throws()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Int, 5, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [MemberData(
            nameof(ConstructorCharData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void ConstructorWithMaxLengthAndLocale(SqlDbType dbType)
        {
            long maxLength = 5L;
            long locale = 0L;
            SqlMetaData metaData = new SqlMetaData("col1", dbType, maxLength, locale, SqlCompareOptions.IgnoreCase, true, true, SortOrder.Ascending, 0);
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(locale, metaData.LocaleId);
            Assert.Equal(maxLength, metaData.MaxLength);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Fact]
        public void ConstructorWithMaxLengthAndLocaleInvalidType_Throws()
        {
            long locale = 0L;
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Int, 5, locale, SqlCompareOptions.IgnoreCase, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<object[]> ConstructorTextData()
        {
            return new object[][]
            {
                new object[] { SqlDbType.NText },
                new object[] { SqlDbType.Text }
            };
        }

        [Theory]
        [MemberData(
            nameof(ConstructorTextData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void ConstructorWithMaxLengthTextAndDefaultLocale(SqlDbType dbType)
        {
            long maxLength = SqlMetaData.Max;
            SqlMetaData metaData = new SqlMetaData("col1", dbType, maxLength, true, true, SortOrder.Ascending, 0);
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(CultureInfo.CurrentCulture.LCID, metaData.LocaleId);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Theory]
        [MemberData(
            nameof(ConstructorTextData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void ConstructorWithMaxLengthTextAndLocale(SqlDbType dbType)
        {
            long maxLength = SqlMetaData.Max;
            long locale = 0L;
            SqlMetaData metaData = new SqlMetaData("col1", dbType, maxLength, locale, SqlCompareOptions.IgnoreCase, true, true, SortOrder.Ascending, 0);
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(locale, metaData.LocaleId);
            Assert.Equal(maxLength, metaData.MaxLength);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Theory]
        [MemberData(
            nameof(ConstructorCharData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        [MemberData(
            nameof(ConstructorTextData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void ConstructorWithInvalidMaxLengthAndLocale_Throws(SqlDbType dbType)
        {
            int invalidMaxLength = -2;
            long locale = 0L;
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", dbType, invalidMaxLength, locale, SqlCompareOptions.IgnoreCase, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("out of range", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorWithInvalidMaxLengthAndLocaleCompareOptionsBinarySortAndIgnoreCase_Throws()
        {
            long maxLength = SqlMetaData.Max;
            long locale = 0L;
            SqlCompareOptions invalidCompareOptions = SqlCompareOptions.BinarySort | SqlCompareOptions.IgnoreCase;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.NText, maxLength, locale, invalidCompareOptions, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        public static IEnumerable<object[]> ConstructorBinaryData()
        {
            return new object[][]
            {
                new object[] { SqlDbType.Binary },
                new object[] { SqlDbType.VarBinary },
                new object[] { SqlDbType.Image }
            };
        }

        [Theory]
        [MemberData(
            nameof(ConstructorCharData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        [MemberData(
            nameof(ConstructorTextData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        [MemberData(
            nameof(ConstructorBinaryData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void ConstructorWithInvalidMaxLengthDefaultLocale_Throws(SqlDbType dbType)
        {
            int invalidMaxLength = -2;
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", dbType, invalidMaxLength, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("out of range", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorWithLongName_Throws()
        {
            string invalidName = new string('c', 256);

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData(invalidName, SqlDbType.Decimal, 2, 2, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("long", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorWithNullName_Throws()
        {
            string invalidName = null;

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData(invalidName, SqlDbType.Decimal, 2, 2, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorWithInvalidSortOrder_Throws()
        {
            SortOrder invalidSortOrder = (SortOrder)5;
            ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Int, true, true, invalidSortOrder, 0);
            });
            Assert.Contains("SortOrder", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConstructorWithInvalidSortOrderSortOrdinal_Throws()
        {
            SortOrder invalidSortOrder = SortOrder.Unspecified;
            int invalidMatchToSortOrdinal = 0;
            InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Int, true, true, invalidSortOrder, invalidMatchToSortOrdinal);
            });
            Assert.Contains("sort order and ordinal", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DbTypeDatabaseOwningSchemaObjectNameConstructorWithInvalidDbTypeName_Throws()
        {
            ArgumentException ex = Assert.ThrowsAny<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col2", SqlDbType.Int, "NorthWindDb", "schema", "name");
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
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
        public void DecimalConstructorWithNullUdt()
        {
            SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Decimal, 5, 2, 2, 0, SqlCompareOptions.BinarySort, null);
            Assert.Equal("col1", metaData.Name);
            Assert.Equal(SqlDbType.Decimal, metaData.SqlDbType);
            Assert.Equal(5, metaData.MaxLength);
            Assert.Equal(2, metaData.Precision);
            Assert.Equal(2, metaData.Scale);
            Assert.Equal(0, metaData.LocaleId);
            Assert.Null(metaData.Type);
        }

        [Fact]
        public void DecimalConstructorWithPrecisionOutOfRange_Throws()
        {
            byte precision = 1;
            byte scale = 2;
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Decimal, precision, scale, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("precision", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void DecimalConstructorWithPrecisionOutofRange2_Throws()
        {
            byte precision = SqlDecimal.MaxPrecision;
            precision += 1;
            byte scale = 2;
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Decimal, precision, scale, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("precision", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // TODO: This condition can never be met because SqlDecimal.MaxPrecision == SqlDecimal.MaxScale
        // and there's a check that scale cannot exceed precision, so we cannot test this exception.
        //[Fact]
        //public void DecimalConstructorWithScaleOutOfRange_Throws()
        //{
        //    byte precision = SqlDecimal.MaxPrecision;
        //    byte scale = SqlDecimal.MaxScale;
        //    scale += 1;

        //    ArgumentException ex = Assert.Throws<ArgumentException>(() =>
        //    {
        //        SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Decimal, precision, scale, true, true, SortOrder.Ascending, 0);
        //    });
        //    Assert.NotNull(ex);
        //    Assert.NotEmpty(ex.Message);
        //    Assert.Contains("scale", ex.Message, StringComparison.OrdinalIgnoreCase);
        //}

        public static IEnumerable<object[]> ConstructorGenericData()
        {
            return new object[][]
            {
                new object[] { SqlDbType.Variant, null },
                new object[] { SqlDbType.Udt, typeof(Address) }
            };
        }

        [Theory]
        [MemberData(
            nameof(ConstructorGenericData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void GenericConstructorWithoutXmlSchema(SqlDbType dbType, Type udt)
        {
            if (udt != null)
            {
                Type t = udt.GetInterface("IBinarySerialize", true);
                Assert.Equal(typeof(Microsoft.SqlServer.Server.IBinarySerialize), t);
            }
            SqlMetaData metaData = new SqlMetaData("col2", dbType, 16, 2, 2, 2, SqlCompareOptions.IgnoreCase, udt, true, true, SortOrder.Ascending, 0);
            Assert.Equal(dbType, metaData.SqlDbType);
            Assert.True(metaData.UseServerDefault);
            Assert.True(metaData.IsUniqueKey);
            Assert.Equal(SortOrder.Ascending, metaData.SortOrder);
            Assert.Equal(0, metaData.SortOrdinal);
        }

        [Fact]
        public void GenericConstructorWithoutXmlSchemaWithInvalidDbType_Throws()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col2", (SqlDbType)999, 16, 2, 2, 2, SqlCompareOptions.IgnoreCase, null, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [MemberData(
            nameof(SqlMetaDataInferredValues),
            DisableDiscoveryEnumeration = true)]
        public void InferFromValue(SqlDbType expectedDbType, object value)
        {
            SqlMetaData metaData = SqlMetaData.InferFromValue(value, "col1");
            Assert.Equal(expectedDbType, metaData.SqlDbType);
        }

        [Theory]
        [InlineData((SByte)1)]
        [InlineData((UInt16)1)]
        [InlineData((UInt32)1)]
        [InlineData((UInt64)1)]
        public void InferFromValueWithInvalidValue_Throws(object value)
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = SqlMetaData.InferFromValue(value, "col1");
            });
            Assert.NotNull(ex);
            Assert.NotEmpty(ex.Message);
            Assert.Contains("invalid", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InferFromValueWithNull_Throws()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                SqlMetaData metaData = SqlMetaData.InferFromValue(null, "col1");
            });
            Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void InferFromValueWithUdtValue_Throws()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                Address address = new Address();
                SqlMetaData metaData = SqlMetaData.InferFromValue(address, "col1");
            });
            Assert.Contains("address", ex.Message, StringComparison.OrdinalIgnoreCase);
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

        public static IEnumerable<object[]> ConstructorTimeData()
        {
            return new object[][]
            {
                new object[] { SqlDbType.Time },
                new object[] { SqlDbType.DateTime2 },
                new object[] { SqlDbType.DateTimeOffset }
            };
        }

        [Theory]
        [MemberData(
            nameof(ConstructorTimeData)
#if NETFRAMEWORK
            , DisableDiscoveryEnumeration = true
#endif
            )]
        public void TimeConstructorWithOutOfRange_Throws(SqlDbType dbType)
        {
            byte precision = 8;
            byte scale = 8;

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", dbType, 5, precision, scale, 0, SqlCompareOptions.BinarySort, null, true, true, SortOrder.Ascending, 0);
            });
            Assert.Contains("scale", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void TimeConstructorWithInvalidType_Throws()
        {
            byte precision = 2;
            byte scale = 2;

            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Int, precision, scale);
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
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
        public static void InvalidUdtEcxeption_Throws()
        {
            SqlServer.Server.InvalidUdtException e = 
                Assert.Throws<SqlServer.Server.InvalidUdtException> (() => new SqlMetaData("col1", SqlDbType.Udt, typeof(int), "UdtTestDb.dbo.Address"));

            Assert.Equal("'System.Int32' is an invalid user defined type, reason: no UDT attribute.", e.Message);
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

        [Fact]
        public void UdtConstructorWithInvalidType_Throws()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Int, typeof(int));
            });
            Assert.Contains("dbType", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void UdtConstructorWithNull_Throws()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Udt, null);
            });
            Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
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
        public void XmlConstructorWithNullObjectName_Throws()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(() =>
            {
                SqlMetaData metaData = new SqlMetaData("col1", SqlDbType.Xml, "NorthWindDb", "schema", null);
            });
            Assert.Contains("null", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        #region Test values
        public static readonly object[][] SqlMetaDataDateTimeValues =
        {
            new object[] {SqlDbType.DateTime, new SqlDateTime(DateTime.UtcNow)},
            new object[] {SqlDbType.Date, DateTime.Today},
            new object[] {SqlDbType.DateTime, DateTime.Today},
            new object[] {SqlDbType.DateTime2, DateTime.Today},
            new object[] {SqlDbType.SmallDateTime, DateTime.Today},
        };

        public static readonly object[][] SqlMetaDataMaxLengthTrimValues =
        {
            new object[] {SqlDbType.Binary, new SqlBinary(new byte[] { 1, 2, 3, 4, 5 })},
            new object[] {SqlDbType.Binary, new byte[] { 1, 2, 3, 4, 5 }},
            new object[] {SqlDbType.Char, "Tests"},
            new object[] {SqlDbType.Char, "T"},
            new object[] {SqlDbType.Char, new char[]{'T','e','s','t','s'}},
            new object[] {SqlDbType.NChar, "T"},
            new object[] {SqlDbType.NChar, "Tests"},
            new object[] {SqlDbType.VarChar, "Tests" },
            new object[] {SqlDbType.VarChar, new SqlString("Tests")},
            new object[] {SqlDbType.VarChar, new char[]{'T','e','s','t','s'}},
            new object[] {SqlDbType.NVarChar, "Tests"},
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
            new object[] {SqlDbType.UniqueIdentifier, new DateTimeOffset(new DateTime(0), TimeSpan.Zero)},
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
            new object[] {SqlDbType.SmallMoney, (decimal)int.MaxValue},
            new object[] {SqlDbType.SmallMoney, "Money" },
            new object[] {SqlDbType.Bit, 1.0M },
            new object[] {SqlDbType.Bit, DateTime.Today},
        };

        public static readonly object[][] SqlMetaDataAdjustValues =
        {
            new object[] {SqlDbType.Int, null},
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
            new object[] {SqlDbType.VarChar, "T"},
            new object[] {SqlDbType.VarChar, "Test"},
            new object[] {SqlDbType.VarChar, new SqlString("T")},
            new object[] {SqlDbType.VarChar, new SqlString("Test")},
            new object[] {SqlDbType.VarChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NVarChar, 'T'},
            new object[] {SqlDbType.NVarChar, "T"},
            new object[] {SqlDbType.NVarChar, "Test"},
            new object[] {SqlDbType.NVarChar, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.NVarChar, new SqlString("T")},
            new object[] {SqlDbType.NVarChar, new SqlString("Test")},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NText, "T"},
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
            new object[] {SqlDbType.Binary, new SqlBytes(new byte[] { 1, 2, 3, 4, 5 })},
            new object[] {SqlDbType.VarBinary, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.Timestamp, SqlBinary.Null},
            new object[] {SqlDbType.Timestamp, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Timestamp, new SqlBinary(new byte[] { 1, 2, 3, 4, 5 })},
            new object[] {SqlDbType.Timestamp, new SqlBytes()},
            new object[] {SqlDbType.Image, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.Image, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.Time, new TimeSpan(0, 0, 1)},
            new object[] {SqlDbType.DateTimeOffset, new DateTimeOffset(new DateTime(0), TimeSpan.Zero)},
            new object[] {SqlDbType.UniqueIdentifier, SqlGuid.Null},
            new object[] {SqlDbType.UniqueIdentifier, Guid.Empty},
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
            new object[] {SqlDbType.Decimal, new SqlDecimal(10.01M) },
            new object[] {SqlDbType.Decimal, 10.01M },
            new object[] {SqlDbType.NVarChar, "" },
            new object[] {SqlDbType.NVarChar, 'T'},
            new object[] {SqlDbType.NVarChar, "T"},
            new object[] {SqlDbType.NVarChar, "Test"},
            new object[] {SqlDbType.NVarChar, new string('a', 4001)},
            new object[] {SqlDbType.NVarChar, new char[] {}},
            new object[] {SqlDbType.NVarChar, new char[] {'T','e','s', 't'}},
            new object[] {SqlDbType.NVarChar, new char[4001]},
            new object[] {SqlDbType.NVarChar, new SqlString("T")},
            new object[] {SqlDbType.NVarChar, new SqlString("Test")},
            new object[] {SqlDbType.NVarChar, new SqlString("")},
            new object[] {SqlDbType.NVarChar, new SqlString(new string('a', 4001))},
            new object[] {SqlDbType.NVarChar, SqlString.Null},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] { 'T' })},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] { 'T', 'e', 's', 't' })},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[] {})},
            new object[] {SqlDbType.NVarChar, new SqlChars(new char[4001])},
            new object[] {SqlDbType.NVarChar, SqlChars.Null},
            new object[] {SqlDbType.VarBinary, new SqlBytes(new byte[] { })},
            new object[] {SqlDbType.VarBinary, new SqlBytes(new byte[] { 1 })},
            new object[] {SqlDbType.VarBinary, new SqlBytes(new byte[8001])},
            new object[] {SqlDbType.VarBinary, SqlBytes.Null},
            new object[] {SqlDbType.VarBinary, SqlBinary.Null},
            new object[] {SqlDbType.VarBinary, new SqlBinary(new byte[] { })},
            new object[] {SqlDbType.VarBinary, new SqlBinary(new byte[] { 1 })},
            new object[] {SqlDbType.VarBinary, new SqlBinary(new byte[8001])},
            new object[] {SqlDbType.VarBinary, new byte[] { }},
            new object[] {SqlDbType.VarBinary, new byte[] { 1 }},
            new object[] {SqlDbType.VarBinary, new byte[8001]},
            new object[] {SqlDbType.Time, new TimeSpan(0, 0, 1)},
            new object[] {SqlDbType.Time, new TimeSpan(TimeSpan.TicksPerDay - 1)},
            new object[] {SqlDbType.DateTimeOffset, new DateTimeOffset(new DateTime(0), TimeSpan.Zero)},
            new object[] {SqlDbType.DateTimeOffset, new DateTimeOffset(DateTime.Now)},
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
