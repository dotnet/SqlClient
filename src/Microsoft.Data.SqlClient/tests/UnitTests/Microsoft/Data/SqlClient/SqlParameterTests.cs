// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Microsoft.Data.SqlClient
{
    public class SqlParameterTests
    {
        private const string TestParameterName = "p_somerandomparametername";
        
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

        [Fact]
        public void Constructor_NameValue_NameNull()
        {
            // Act
            SqlParameter p = new SqlParameter(null, DBNull.Value);
            
            // Assert
            Assert.Equal(string.Empty, p.ParameterName);
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
            SqlParameter p = new SqlParameter(TestParameterName, sqlDbType);

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
            SqlParameter p = new SqlParameter(TestParameterName, SqlDbType.Bit, size);

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
            Action action = () => _ = new SqlParameter(TestParameterName, SqlDbType.Bit, size);

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
            SqlParameter p = new SqlParameter(TestParameterName, SqlDbType.Bit, 123, sourceColumn);

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
        
        #if NET
        [Fact]
        public void Constructor_NameValue_ValueDateOnly()
        {
            // Arrange
            DateOnly value = new DateOnly(2004, 08, 24);
            
            // Act
            SqlParameter p = new SqlParameter(TestParameterName, value);
            
            // Assert
            Assert.Equal(DbType.Date, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(TestParameterName, p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal(0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.Date, p.SqlDbType);
            Assert.Equal(value, p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Equal(value, p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }
        #endif
        
        [Fact]
        public void Constructor_NameValue_ValueDbNull()
        {
            // Act
            SqlParameter p = new SqlParameter(TestParameterName, DBNull.Value);
            
            // Assert
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(TestParameterName, p.ParameterName);
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
        
        // @TODO: We should have a entire suite of these for all the supported types
        [Fact]
        public void Constructor_NameValue_ValueDateTime()
        {
            // Arrange
            DateTime value = new DateTime(2004, 08, 24);
            
            // Act
            SqlParameter p = new SqlParameter(TestParameterName, value);
            
            // Assert
            // @TODO: Have a sparse list of properties to test that differ from default
            Assert.Equal(DbType.DateTime, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(TestParameterName, p.ParameterName);
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
        public void Constructor_NameValue_ValueNull()
        {
            // Act
            SqlParameter p = new SqlParameter(TestParameterName, null);
            
            // Assert
            Assert.Equal(DbType.String, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(TestParameterName, p.ParameterName);
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
        
        #if NET
        [Fact]
        public void Constructor_NameValue_ValueTimeOnly()
        {
            // Arrange
            TimeOnly value = new TimeOnly(9, 7, 42, 321);
            
            // Act
            SqlParameter p = new SqlParameter(TestParameterName, value);
            
            // Assert
            Assert.Equal(DbType.Time, p.DbType);
            Assert.Equal(ParameterDirection.Input, p.Direction);
            Assert.False(p.IsNullable);
            Assert.Equal(0, p.LocaleId);
            Assert.Equal(0, p.Offset);
            Assert.Equal(TestParameterName, p.ParameterName);
            Assert.Equal(0, p.Precision);
            Assert.Equal(0, p.Scale);
            Assert.Equal(0, p.Size);
            Assert.Equal(string.Empty, p.SourceColumn);
            Assert.False(p.SourceColumnNullMapping);
            Assert.Equal(DataRowVersion.Current, p.SourceVersion);
            Assert.Equal(SqlDbType.Time, p.SqlDbType);
            Assert.Equal(value, p.SqlValue);
            Assert.Equal(string.Empty, p.TypeName);
            Assert.Equal(string.Empty, p.UdtTypeName);
            Assert.Equal(value, p.Value);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionDatabase);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionName);
            Assert.Equal(string.Empty, p.XmlSchemaCollectionOwningSchema);
        }     
        #endif
        
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
                TestParameterName,
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
                TestParameterName,
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
        
        #region Infer Type From Value

        public static TheoryData<object, DbType, SqlDbType, object?> InferTypeFromValue_Valid_Data =>
            new TheoryData<object, DbType, SqlDbType, object?>
            {
                // Boolean
                { false,                                    DbType.Boolean,  SqlDbType.Bit,              null  },
                { true,                                     DbType.Boolean,  SqlDbType.Bit,              null  },
                
                // Byte
                { (byte)0x0a,                               DbType.Byte,     SqlDbType.TinyInt,          null  },

                // Byte[]
                { new byte[] { 0x0a, 0x0d },                DbType.Binary,   SqlDbType.VarBinary,        null  },

                // Char
                { 'X',                                      DbType.String,   SqlDbType.NVarChar,         "X"   },

                // Char[]
                { new char[] { 'f', 'o', 'o'},              DbType.String,   SqlDbType.NVarChar,         "foo" },

                // DateTime
                { DateTime.Now,                             DbType.DateTime, SqlDbType.DateTime,         null  },
                { new DateTime(1973, 8, 13),                DbType.DateTime, SqlDbType.DateTime,         null  },

                // DateTimeOffset
                { 
                    new DateTimeOffset(new DateTime(23, 12, 23), new TimeSpan(1, 0, 0) ),
                    DbType.DateTimeOffset,
                    SqlDbType.DateTimeOffset,
                    null
                },
                
                // Decimal
                { decimal.MinValue,                         DbType.Decimal,  SqlDbType.Decimal,          null  },
                { decimal.Zero,                             DbType.Decimal,  SqlDbType.Decimal,          null  },
                { 214748.364m,                              DbType.Decimal,  SqlDbType.Decimal,          null  },
                { decimal.MaxValue,                         DbType.Decimal,  SqlDbType.Decimal,          null  },

                // Double
                { double.MinValue,                          DbType.Double,   SqlDbType.Float,            null  },
                { 0d,                                       DbType.Double,   SqlDbType.Float,            null  },
                { 1234.5678d,                               DbType.Double,   SqlDbType.Float,            null  },
                { double.MaxValue,                          DbType.Double,   SqlDbType.Float,            null  },
                // @TODO: NaN? Infinity?

                // Enum
                { ByteEnum.A,                               DbType.Byte,     SqlDbType.TinyInt,          null  },
                { LongEnum.A,                               DbType.Int64,    SqlDbType.BigInt,           null  },
                
                // Guid
                { Guid.NewGuid(),                           DbType.Guid,     SqlDbType.UniqueIdentifier, null  },
                
                // Int16
                { short.MinValue,                           DbType.Int16,    SqlDbType.SmallInt,         null  },
                { (short)0,                                 DbType.Int16,    SqlDbType.SmallInt,         null  },
                { short.MaxValue,                           DbType.Int16,    SqlDbType.SmallInt,         null  },
                
                // Int32
                { int.MinValue,                             DbType.Int32,    SqlDbType.Int,              null  },
                { 0,                                        DbType.Int32,    SqlDbType.Int,              null  },
                { int.MaxValue,                             DbType.Int32,    SqlDbType.Int,              null  },
                
                // Int64
                { long.MinValue,                            DbType.Int64,    SqlDbType.BigInt,           null  },
                { (long)0,                                  DbType.Int64,    SqlDbType.BigInt,           null  },
                { long.MaxValue,                            DbType.Int64,    SqlDbType.BigInt,           null  },
                
                // Object
                { new object(),                             DbType.Object,   SqlDbType.Variant,          null  },
                
                // Single
                { float.MinValue,                           DbType.Single,   SqlDbType.Real,             null  },
                { 0f,                                       DbType.Single,   SqlDbType.Real,             null  },
                { 1234.5678f,                               DbType.Single,   SqlDbType.Real,             null  },
                { float.MaxValue,                           DbType.Single,   SqlDbType.Real,             null  },
                // @TODO: Nan? Infinity?
                
                // String
                { "some text",                              DbType.String,   SqlDbType.NVarChar,         null  },
                
                // TimeSpan
                { new TimeSpan(4, 6, 23),                   DbType.Time,     SqlDbType.Time,             null  },
                
                #if NET
                { DateOnly.FromDateTime(DateTime.Now.Date), DbType.Date,     SqlDbType.Date,             null  },
                { TimeOnly.FromDateTime(DateTime.Now),      DbType.Time,     SqlDbType.Time,             null  },
                #endif
            };

        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Value_InferType_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(InferTypeFromValue_Valid_Data))]
        #endif
        public void InferTypeFromValue_Valid(
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
            // @TODO: Earlier versions of these tests sometimes tested the *order* of reading these properties.
            //    While theoretically worth checking, getters ideally shouldn't be modifying state. Verify the
            //    SqlParameter code is operating sensibly.
            Assert.Equal(expectedDbType, p.DbType);
            Assert.Equal(expectedSqlDbType, p.SqlDbType);

            // - If value is not expected to change, use the input value as the expected
            expectedValue ??= value;
            Assert.Equal(expectedValue, p.Value);
        }

        public static TheoryData<object, SqlDbType, object?> InferTypeFromSqlValue_Valid_Data =>
            new TheoryData<object, SqlDbType, object?>()
            {
                // SqlBinary
                { new SqlBinary([0x0a, 0x0b]),           SqlDbType.VarBinary },
                { SqlBinary.Null,                        SqlDbType.VarBinary },
                
                // SqlBoolean
                { new SqlBoolean(false),                 SqlDbType.Bit },
                { SqlBoolean.Null,                       SqlDbType.Bit },
                
                // SqlByte
                { new SqlByte(0x0d),                     SqlDbType.TinyInt },
                { SqlByte.Null,                          SqlDbType.TinyInt },
                
                // SqlBytes
                { new SqlBytes([0x0a, 0x0b]),            SqlDbType.VarBinary },
                { SqlBytes.Null,                         SqlDbType.VarBinary },
                
                // SqlChars
                { new SqlChars(['F', 'o', 'o']),         SqlDbType.NVarChar },
                { SqlChars.Null,                         SqlDbType.NVarChar },
                
                // SqlDateTime
                { new SqlDateTime(DateTime.Now),         SqlDbType.DateTime },
                { SqlDateTime.Null,                      SqlDbType.DateTime },
                
                // SqlDecimal
                { new SqlDecimal(45m),                   SqlDbType.Decimal },
                { SqlDecimal.Null,                       SqlDbType.Decimal },
                
                // SqlDouble
                { new SqlDouble(4.50),                   SqlDbType.Float },
                { SqlDouble.Null,                        SqlDbType.Float },

                // SqlGuid
                { new SqlGuid(Guid.NewGuid()),           SqlDbType.UniqueIdentifier },
                { SqlGuid.Null,                          SqlDbType.UniqueIdentifier },

                // SqlInt16
                { new SqlInt16((short)5),                SqlDbType.SmallInt },
                { SqlInt16.Null,                         SqlDbType.SmallInt },

                // SqlInt32
                { new SqlInt32(5),                       SqlDbType.Int },
                { SqlInt32.Null,                         SqlDbType.Int },

                // SqlInt64
                { new SqlInt64(5L),                      SqlDbType.BigInt },
                { SqlInt64.Null,                         SqlDbType.BigInt },

                // SqlMoney
                { new SqlMoney(45m),                     SqlDbType.Money, null },
                { SqlMoney.Null,                         SqlDbType.Money, null },

                // SqlSingle
                { new SqlSingle(45f),                    SqlDbType.Real, null },
                { SqlSingle.Null,                        SqlDbType.Real, null },

                // SqlString
                { new SqlString("foo"),                  SqlDbType.NVarChar, null },
                { SqlString.Null,                        SqlDbType.NVarChar, null },
            };
        
        [Theory]
        #if NETFRAMEWORK
        // netfx stores system enums in the GAC, and xUnit cannot serialize enums that live there.
        // Thus, we disable enumeration of these to avoid a bunch of warnings in the console.
        [MemberData(nameof(Value_InferType_Data), DisableDiscoveryEnumeration = true)]
        #else
        [MemberData(nameof(InferTypeFromSqlValue_Valid_Data))]
        #endif
        public void InferTypeFromSqlValue_Valid(object value, SqlDbType expectedSqlDbType, object? expectedValue)
        {
            // Arrange
            SqlParameter p = new SqlParameter();
            
            // Act
            p.SqlValue = value;
            
            // Assert
            Assert.Equal(expectedSqlDbType, p.SqlDbType);
            
            // - If value is not expected to change, use the input value as the expected
            expectedValue ?? = value;
            Assert.Equal(expectedValue, p.Value);
        }
        
        public void InferTypeFromValue_Invalid()
        {
            
        }

        #endregion

        private enum ByteEnum : byte
        {
            A = 0x0a,
            B = 0x0d,
        }

        private enum LongEnum : long
        {
            A = long.MinValue,
            B = long.MinValue,
        }
    }
}
