// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlParameterTests
    {
        private const string TestParamName = "p_somerandomeparamname";

        #region Constructor Tests

        [Fact]
        public void Constructor_Parameterless()
        {
            // Act
            SqlParameter p = new SqlParameter();

            // Assert
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

        public static TheoryData<SqlDbType, DbType> Constructor_NameType_Data =>
            new TheoryData<SqlDbType, DbType>
            {
                { SqlDbType.BigInt, DbType.Int64 },
                { SqlDbType.Binary, DbType.Binary },
                { SqlDbType.Bit, DbType.Boolean },
                { SqlDbType.Char, DbType.AnsiStringFixedLength },
                { SqlDbType.Date, DbType.Date },
                { SqlDbType.DateTime, DbType.DateTime },
                { SqlDbType.DateTimeOffset, DbType.DateTimeOffset },
                { SqlDbType.Decimal, DbType.Decimal },
                { SqlDbType.Float, DbType.Double },
                { SqlDbType.Image, DbType.Binary },
                { SqlDbType.Int, DbType.Int32 },
                { SqlDbType.Money, DbType.Currency },
                { SqlDbType.NChar, DbType.StringFixedLength },
                { SqlDbType.NText, DbType.String },
                { SqlDbType.NVarChar, DbType.String },
                { SqlDbType.Real, DbType.Single },
                { SqlDbType.SmallDateTime, DbType.DateTime },
                { SqlDbType.SmallInt, DbType.Int16 },
                { SqlDbType.SmallMoney, DbType.Currency },
                { SqlDbType.Structured, DbType.Object },
                { SqlDbType.Text, DbType.AnsiString },
                { SqlDbType.Time, DbType.Time },
                { SqlDbType.Timestamp, DbType.Binary },
                { SqlDbType.TinyInt, DbType.Byte },
                { SqlDbType.Udt, DbType.Object },
                { SqlDbType.UniqueIdentifier, DbType.Guid },
                { SqlDbType.VarBinary, DbType.Binary },
                { SqlDbType.VarChar, DbType.AnsiString },
                { SqlDbType.Variant, DbType.Object },
                { SqlDbType.Xml, DbType.Xml }
            };

        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Constructor_NameType_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(Constructor_NameType_Data))]
        #endif
        public void Constructor_NameType(SqlDbType sqlDbType, DbType dbType)
        {
            // Act
            SqlParameter p = new SqlParameter(TestParamName, sqlDbType);

            // Assert
            // - Theoretically parameterless tests should validate "default" behavior
            // - Validate that type-specific values are set
            Assert.Equal(dbType, p.DbType);
            Assert.Equal(sqlDbType, p.SqlDbType);
            Assert.Null(p.Value);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1234)]
        [InlineData(int.MaxValue)]
        public void Constructor_NameTypeSize(int size)
        {
            // Act
            SqlParameter p = new SqlParameter(TestParamName, SqlDbType.Bit, size);

            // Assert
            Assert.Equal(size, p.Size);
        }

        // @TODO: -1 is a special case that should be tested
        // @TODO: For vector type, the size is *not* set internally. This probably necessitates a full suite of type tests.

        [Theory]
        [InlineData(-123)]
        [InlineData(int.MinValue)]
        public void Constructor_NameTypeSize_NegativeSize_Throws(int size)
        {
            // Act
            Action action = () => _ = new SqlParameter(TestParamName, SqlDbType.Bit, size);

            // Assert
            Assert.Throws<ArgumentException>(action);
        }

        // @TODO: Theoretically we should have tests that validate type and size parameters.
        [Theory]
        [InlineData(null, "")]
        [InlineData("SourceColumn", "SourceColumn")]
        [InlineData(" SourceColumn ", " SourceColumn ")]
        public void Constructor_NameTypeSizeSource(string? sourceColumn, string expectedSourceColumn)
        {
            // Act
            SqlParameter p = new SqlParameter(TestParamName, SqlDbType.Bit, 123, sourceColumn);

            // Assert
            Assert.Equal(expectedSourceColumn, p.SourceColumn);
        }

        // @TODO: These cases test a large swath of conditions but aren't very systematic. Consider breaking down to separate tests per parameter for more complete coverage.
        public static TheoryData<ParameterDirection, bool, byte, byte, string?, string, DataRowVersion>
            Constructor_NonXmlKitchenSink_Data =>
            new TheoryData<ParameterDirection, bool, byte, byte, string?, string, DataRowVersion>
            {
                { ParameterDirection.Input, true, 0, 0, string.Empty, string.Empty, DataRowVersion.Current },
                { ParameterDirection.InputOutput, true, 0, 0, null, string.Empty, DataRowVersion.Default },
                { ParameterDirection.Output, false, 0, 255, "Col", "Col", DataRowVersion.Original },
                { ParameterDirection.ReturnValue, false, 99, 100, " Col ", " Col ", DataRowVersion.Proposed }
            };

        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Constructor_NonXmlKitchenSink_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(Constructor_NonXmlKitchenSink_Data))]
        #endif
        public void Constructor_NonXmlKitchenSink(
            ParameterDirection parameterDirection,
            bool isNullable,
            byte precision,
            byte scale,
            string? sourceColumn,
            string expectedSourceColumn,
            DataRowVersion dataRowVersion)
        {
            // Act
            SqlParameter p = new SqlParameter(
                TestParamName,
                dbType: SqlDbType.Bit,
                size: int.MaxValue,
                parameterDirection,
                isNullable,
                precision,
                scale,
                sourceColumn,
                dataRowVersion,
                value: null);

            // Assert
            Assert.Equal(parameterDirection, p.Direction);
            Assert.Equal(isNullable, p.IsNullable);
            Assert.Equal(precision, p.Precision);
            Assert.Equal(scale, p.Scale);
            Assert.Equal(expectedSourceColumn, p.SourceColumn);
            Assert.Equal(dataRowVersion, p.SourceVersion);
        }

        // @TODO: These cases test a large swath of conditions but aren't very systematic. Consider breaking down to separate tests per parameter for more complete coverage.
        public static TheoryData<ParameterDirection, byte, byte, string?, string, DataRowVersion, bool, string?, string>
            Constructor_XmlKitchenSink_Data =>
            new TheoryData<ParameterDirection, byte, byte, string?, string, DataRowVersion, bool, string?, string>
            {
                { ParameterDirection.Input, 0, 0, null, "", DataRowVersion.Current, false, null, string.Empty },
                { ParameterDirection.InputOutput, 255, 255, "", "", DataRowVersion.Default, true, string.Empty, string.Empty },
                { ParameterDirection.Output, 99, 100, "Source", "Source", DataRowVersion.Original, false, "aaa", "aaa" },
                { ParameterDirection.ReturnValue, 100, 99, " Source ", " Source ", DataRowVersion.Proposed, true, " aaa ", " aaa " }
            };

        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Constructor_XmlKitchenSink_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(Constructor_XmlKitchenSink_Data))]
        #endif
        public void Constructor_XmlKitchenSink(
            ParameterDirection parameterDirection,
            byte precision,
            byte scale,
            string? inputSourceColumn,
            string expectedSourceColumn,
            DataRowVersion dataRowVersion,
            bool sourceColumnNullMapping,
            string? inputXmlParameter,
            string expectedXmlParameter)
        {
            // Act
            SqlParameter p = new SqlParameter(
                TestParamName,
                dbType: SqlDbType.Bit,
                size: int.MaxValue,
                parameterDirection,
                precision,
                scale,
                inputSourceColumn,
                dataRowVersion,
                sourceColumnNullMapping,
                value: null,
                xmlSchemaCollectionDatabase: inputXmlParameter,
                xmlSchemaCollectionOwningSchema: inputXmlParameter,
                xmlSchemaCollectionName: inputXmlParameter);

            // Assert
            Assert.Equal(parameterDirection, p.Direction);
            Assert.Equal(precision, p.Precision);
            Assert.Equal(scale, p.Scale);
            Assert.Equal(expectedSourceColumn, p.SourceColumn);
            Assert.Equal(dataRowVersion, p.SourceVersion);
            Assert.Equal(expectedXmlParameter, p.XmlSchemaCollectionDatabase);
            Assert.Equal(expectedXmlParameter, p.XmlSchemaCollectionName);
            Assert.Equal(expectedXmlParameter, p.XmlSchemaCollectionOwningSchema);
        }

        #endregion

        #region Property Tests

        public static TheoryData<object, DbType, SqlDbType, object?> Value_InferType_Data =>
            new TheoryData<object, DbType, SqlDbType, object?>
            {
                // Byte
                { (byte)0x0a,                               DbType.Byte,     SqlDbType.TinyInt,   null  },

                // Byte[]
                { new byte[] { 0x0a, 0x0d },                DbType.Binary,   SqlDbType.VarBinary, null  },

                // Char
                { 'X',                                      DbType.String,   SqlDbType.NVarChar,  "X"   },

                // Char[]
                { new char[] { 'f', 'o', 'o'},              DbType.String,   SqlDbType.NVarChar,  "foo" },

                // DateTime
                { DateTime.Now,                             DbType.DateTime, SqlDbType.DateTime,  null  },
                { new DateTime(1973, 8, 13),                DbType.DateTime, SqlDbType.DateTime,  null  },

                // Decimal
                { decimal.MinValue,                         DbType.Decimal,  SqlDbType.Decimal,   null  },
                { decimal.Zero,                             DbType.Decimal,  SqlDbType.Decimal,   null  },
                { 214748.364m,                              DbType.Decimal,  SqlDbType.Decimal,   null  },
                { decimal.MaxValue,                         DbType.Decimal,  SqlDbType.Decimal,   null  },

                // Double
                { double.MinValue,                          DbType.Double,   SqlDbType.Float,     null  },
                { 0d,                                       DbType.Double,   SqlDbType.Float,     null  },
                { 1234.5678d,                               DbType.Double,   SqlDbType.Float,     null  },
                { double.MaxValue,                          DbType.Double,   SqlDbType.Float,     null  },
                // @TODO: NaN? Infinity?

                #if NET
                { DateOnly.FromDateTime(DateTime.Now.Date), DbType.Date,     SqlDbType.Date,      null  },
                { TimeOnly.FromDateTime(DateTime.Now),      DbType.Time,     SqlDbType.Time,      null  },
                #endif
            };

        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Value_InferType_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(Value_InferType_Data))]
        #endif
        public void Value_InferType(
            object value,
            DbType expectedDbType,
            SqlDbType expectedSqlDbType,
            object? expectedValue)
        {
            // Arrange
            // @TODO: Earlier version of these tests sometimes tested setting the value on a named/typed parameter,
            //     expecting different results. Verify the SqlParameter code is operating sensibly.
            SqlParameter p = new SqlParameter();

            // Act
            p.Value = value;

            // Assert
            // @TODO: Earlier versions of these tests sometimes tested the *order* or reading these properties.
            //    While theoretically worth checking, getters ideally shouldn't be modifying state. Verify the
            //    SqlParameter code is operating sensibly.
            Assert.Equal(expectedDbType, p.DbType);
            Assert.Equal(expectedSqlDbType, p.SqlDbType);

            // - If value is not expected to change, use the input value as the expected
            expectedValue ??= value;
            Assert.Equal(expectedValue, p.Value);
        }

        #endregion
    }
}
