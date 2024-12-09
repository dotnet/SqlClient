// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Text;
using Microsoft.Data.SqlClient.Server;
using Microsoft.SqlServer.Types;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlDataRecordTest
    {
        [Fact]
        public void GetDataTypeName_ReturnsMetaDataTypeIfUdtType()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.Udt, typeof(TestUdt), "sql_TestUdt")
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Equal("Microsoft.Data.SqlClient.Tests.TestUdt", record.GetDataTypeName(0));
        }

        [Fact]
        public void GetDataTypeName_ReturnsTypeFromMetaTypeIfNotUdt()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Equal("nvarchar", record.GetDataTypeName(0));
        }

        [Fact]
        public void GetFieldType_ReturnMetaDataTypeIfUdtType()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.Udt, typeof(TestUdt), "sql_TestUdt")
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

#if NET
            Assert.Equal(typeof(object), record.GetFieldType(0));
#else
            Assert.Equal(typeof(TestUdt), record.GetFieldType(0));
#endif
            Assert.Equal(typeof(object), record.GetSqlFieldType(0));
        }

        [Theory]
        [ClassData(typeof(DbTypeData))]
        public void GetFieldType_ReturnMetaTypeClassTypeIfNotUdt(SqlDbType dbType, int? length, Type expectedClrType, Type expectedSqlType)
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                length == null ? new SqlMetaData("col1", dbType) : new SqlMetaData("col1", dbType, length.Value)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Equal(expectedClrType, record.GetFieldType(0));
            Assert.Equal(expectedSqlType, record.GetSqlFieldType(0));
        }

        [Fact]
        public void Ctor_ThrowsIfNullMetadata()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                null
            };

            Assert.Throws<ArgumentNullException>(() => new SqlDataRecord(null));
            Assert.Throws<ArgumentNullException>(() => new SqlDataRecord(metaData));
        }

        [Fact]
        public void IDataRecord_GetData_ThrowsNotSupported()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Throws<NotSupportedException>(() => ((IDataRecord)record).GetData(0));
        }

        [Fact]
        public void GetValues_ThrowsIfNull()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Throws<ArgumentNullException>(() => record.GetValues(null));
            Assert.Throws<ArgumentNullException>(() => record.GetSqlValues(null));
        }

        [Fact]
        public void GetValues_IfValuesBiggerThanColumnCount_LastArrayItemKeptEmpty()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            record.SetString(0, "test");
            record.SetSqlInt32(1, 2);

            object[] values = new object[5];
            int columnCount = record.GetValues(values);

            for (int i = 2; i < 5; i++)
            {
                Assert.Null(values[i]);
            }
            Assert.Equal(2, columnCount);

            values = new object[5];
            columnCount = record.GetSqlValues(values);

            for (int i = 2; i < 5; i++)
            {
                Assert.Null(values[i]);
            }
            Assert.Equal(2, columnCount);
        }

        [Fact]
        public void GetValues_IfValuesShorterThanColumnCount_FillOnlyFirstColumn()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            record.SetString(0, "test");
            record.SetSqlInt32(1, 2);

            object[] values = new object[1];
            int columnCount = record.GetValues(values);

            Assert.Equal("test", values[0]);
            Assert.Equal(1, columnCount);

            columnCount = record.GetSqlValues(values);

            Assert.Equal(new SqlString("test"), values[0]);
            Assert.Equal(1, columnCount);
        }

        [Fact]
        public void GetValues_FillsArrayAndRespectColumnOrder()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            record.SetString(0, "test");
            record.SetSqlInt32(1, 2);

            object[] values = new object[2];
            int columnCount = record.GetValues(values);

            Assert.Equal("test", values[0]);
            Assert.Equal(2, values[1]);
            Assert.Equal(2, columnCount);

            columnCount = record.GetSqlValues(values);

            Assert.Equal(new SqlString("test"), values[0]);
            Assert.Equal(new SqlInt32(2), values[1]);
            Assert.Equal(2, columnCount);
        }

        [Fact]
        public void SetValues_ThrowsIfNull()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Throws<ArgumentNullException>(() => record.SetValues(null));
        }

        [Fact]
        public void SetValues_ThrowsIfTypeMismatch()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int),
                new SqlMetaData("col3", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);
            object[] values = new object[3] { "one", "2", "three" };

            Assert.Throws<InvalidCastException>(() => record.SetValues(values));
            Assert.True(record.IsDBNull(0));
        }

        [Fact]
        public void SetValues_IfValuesBiggerThanColumnCount_LastArrayItemIgnored()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            object[] values = new object[5] { "test", 2, null, null, null };
            int columnCount = record.SetValues(values);

            Assert.Equal((string)values[0], record.GetString(0));
            Assert.Equal((int)values[1], record.GetInt32(1));
            Assert.Equal(2, columnCount);
        }

        [Fact]
        public void SetValues_IfValuesShorterThanColumnCount_FillOnlyFirstColumn()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            record.SetString(0, "test");
            record.SetSqlInt32(1, 2);

            object[] values = new object[1] { "test2" };
            int columnCount = record.SetValues(values);

            Assert.Equal("test2", record.GetString(0));
            Assert.Equal(2, record.GetInt32(1));

            Assert.Equal(1, columnCount);
        }

        [Fact]
        public void SetSingleValue_ThrowsIfTypeMismatch()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int),
                new SqlMetaData("col3", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Throws<InvalidCastException>(() => record.SetValue(1, "2"));
        }
        [Fact]
        public void GetName_ReturnsNameOfColumn()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50)
            };

            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Equal("col1", record.GetName(0));
        }

        [Fact]
        public void GetOrdinal_ThrowsAgumentNull_IfNameIsNull()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
           {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
           };
            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Throws<ArgumentNullException>(() => record.GetOrdinal(null));
        }

        [Fact]
        public void GetOrdinal_ThrowsOutOfRange_IfNameIsNotAColumn()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);


            Assert.Throws<IndexOutOfRangeException>(() => record.GetOrdinal("outofrange"));

            Assert.Throws<IndexOutOfRangeException>(() => record.GetOrdinal("col1 "));

        }

        [Fact]
        public void GetOrdinal_ReturnsIndexOfColumn()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Equal(1, record.GetOrdinal("col2"));
        }
        [Fact]
        public void GetOrdinal_ReturnsIndexOfColumn_CaseInsensitive()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 50),
                new SqlMetaData("col2", SqlDbType.Int)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Equal(1, record.GetOrdinal("Col2"));
        }

        [Fact]
        public void GetChar_ThrowsNotSupported()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.Char, 100)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            record.SetValue(0, 'c');
            Assert.Throws<NotSupportedException>(() => record.GetChar(0));
        }

        [Fact]
        public void SetChar_ThrowsNotSupported()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.Char, 100)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            Assert.Throws<NotSupportedException>(() => record.SetChar(0, 'c'));
        }

        [Theory]
        [ClassData(typeof(GetUdtTypeTestData))]
        public void GetUdt_ReturnsValue(Type udtType, object value, string serverTypeName)
        {
            SqlMetaData[] metadata = new SqlMetaData[] { new SqlMetaData(nameof(udtType.Name), SqlDbType.Udt, udtType, serverTypeName) };

            SqlDataRecord record = new SqlDataRecord(metadata);

            record.SetValue(0, value);

            Assert.Equal(value.ToString(), record.GetValue(0).ToString());
        }

        [Theory]
        [ClassData(typeof(GetXXXBadTypeTestData))]
        public void GetXXX_ThrowsIfBadType(Func<SqlDataRecord, object> getXXX)
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 1)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            record.SetValue(0, "a");
            Assert.Throws<InvalidCastException>(() => getXXX(record));

        }

        [Theory]
        [InlineData(-1)]
        [InlineData(1)]
        public void InvalidIndexAccess_Throws(int index)
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.NVarChar, 1)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);

            Assert.Throws<IndexOutOfRangeException>(() => record.GetSqlMetaData(index));
            Assert.Throws<IndexOutOfRangeException>(() => record.SetDBNull(index));
            Assert.Throws<IndexOutOfRangeException>(() => record.IsDBNull(index));
            Assert.Throws<IndexOutOfRangeException>(() => record.GetValue(index));
        }

        [Theory]
        [ClassData(typeof(GetFixedLengthCheckValueTestData))]
        public void GetFixedLength_ReturnValue(SqlDbType dbType, object value,
            Action<SqlDataRecord, object> setXXX, Func<SqlDataRecord, object> getXXX,
            Action<object, object> equalityAssertion)
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", dbType)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            setXXX(record, value);

            Assert.False(record.IsDBNull(0));
            equalityAssertion(value, getXXX(record));

            record.SetDBNull(0);
            Assert.True(record.IsDBNull(0));
        }

        [Theory]
        [ClassData(typeof(GetVariableLengthCheckValueTestData))]
        public void GetVariableLength_ReturnValue(SqlDbType dbType, object value,
            Action<SqlDataRecord, object> setXXX, Func<SqlDataRecord, object> getXXX,
            Action<object, object> equalityAssertion)
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", dbType, 50)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            setXXX(record, value);

            Assert.False(record.IsDBNull(0));
            equalityAssertion(value, getXXX(record));

            record.SetDBNull(0);
            Assert.True(record.IsDBNull(0));
        }

        [Fact]
        public void GetSqlXml_ReturnValue()
        {
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.Xml)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);

            string xmlString = "<xmlRoot />";
            using System.IO.MemoryStream xmlMS = new System.IO.MemoryStream(Encoding.Unicode.GetBytes(xmlString));
            SqlXml value = new SqlXml(xmlMS);
            record.SetValue(0, value);

            Assert.False(record.IsDBNull(0));
            Assert.Equal(xmlString, record.GetSqlXml(0).Value);
            Assert.Equal(xmlString, record.GetString(0));
        }

        [Fact]
        public void GetBytes_SetBytes_Succeed()
        {
            byte[] digits = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            byte[] zeroBytes = new byte[digits.Length];
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.VarBinary, digits.Length)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            byte[] buffer = new byte[8];
            long byteCount;

            zeroBytes.AsSpan().Clear();
            buffer.AsSpan().Fill(0xFF);

            Assert.True(record.IsDBNull(0));
            record.SetBytes(0, 0, zeroBytes, 0, zeroBytes.Length);
            Assert.False(record.IsDBNull(0));

            // Read the first 8 bytes, confirming that 8 bytes were read and that all are zero
            byteCount = record.GetBytes(0, 0, buffer, 0, buffer.Length);
            Assert.Equal(buffer.Length, byteCount);
            Assert.All(buffer, (b) => Assert.Equal(0, b));

            // Write four bytes (index 8-11) from the sequence to the record. Read them back, confirming
            // that they've been written and that the surrounding bytes remain zero.
            record.SetBytes(0, 2, digits, 8, 4);
            byteCount = record.GetBytes(0, 2, buffer, 2, 4);
            Assert.Equal(0, buffer[0]);
            Assert.Equal(0, buffer[1]);
            Assert.Equal(9, buffer[2]);
            Assert.Equal(10, buffer[3]);
            Assert.Equal(11, buffer[4]);
            Assert.Equal(12, buffer[5]);
            Assert.Equal(0, buffer[6]);
            Assert.Equal(0, buffer[7]);

            Assert.Equal(4, byteCount);
        }

        [Fact]
        public void GetChars_SetChars_Succeed()
        {
            char[] alpha = new char[16] { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p' };
            char[] zeroChars = new char[alpha.Length];
            SqlMetaData[] metaData = new SqlMetaData[]
            {
                new SqlMetaData("col1", SqlDbType.VarChar, alpha.Length)
            };
            SqlDataRecord record = new SqlDataRecord(metaData);
            char[] buffer = new char[8];
            long charCount;

            zeroChars.AsSpan().Fill('0');
            buffer.AsSpan().Fill('Z');

            Assert.True(record.IsDBNull(0));
            record.SetChars(0, 0, zeroChars, 0, zeroChars.Length);
            Assert.False(record.IsDBNull(0));

            // Read the first 8 chars, confirming that 8 chars were read and that all are '0'
            charCount = record.GetChars(0, 0, buffer, 0, buffer.Length);
            Assert.Equal(buffer.Length, charCount);
            Assert.All(buffer, (b) => Assert.Equal('0', b));

            // Write four chars (index 8-11) from the sequence to the record. Read them back, confirming
            // that they've been written and that the surrounding chars remain '0'.
            record.SetChars(0, 2, alpha, 8, 4);
            charCount = record.GetChars(0, 2, buffer, 2, 4);
            Assert.Equal('0', buffer[0]);
            Assert.Equal('0', buffer[1]);
            Assert.Equal('i', buffer[2]);
            Assert.Equal('j', buffer[3]);
            Assert.Equal('k', buffer[4]);
            Assert.Equal('l', buffer[5]);
            Assert.Equal('0', buffer[6]);
            Assert.Equal('0', buffer[7]);

            Assert.Equal(4, charCount);

            string resultantString = record.GetString(0);
            Assert.Equal("00ijkl0000000000", resultantString);
        }
    }

    public class GetXXXBadTypeTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetGuid(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetInt16(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetInt32(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetInt64(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetFloat(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetDouble(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetDecimal(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetDateTime(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetDateTimeOffset(0)) };
            yield return new object[] { new Func<SqlDataRecord, object>(r => r.GetTimeSpan(0)) };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class GetUdtTypeTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { typeof(SqlGeography), SqlGeography.Point(43, -81, 4326), "Geography" };
            yield return new object[] { typeof(SqlGeometry), SqlGeometry.Point(43, -81, 4326), "Geometry" };
            yield return new object[] { typeof(SqlHierarchyId), SqlHierarchyId.Parse("/"), "HierarchyId" };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public class DbTypeData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { SqlDbType.BigInt, null, typeof(long), typeof(SqlInt64) };
            yield return new object[] { SqlDbType.Binary, 50, typeof(byte[]), typeof(SqlBinary) };
            yield return new object[] { SqlDbType.Bit, null, typeof(bool), typeof(SqlBoolean) };
            yield return new object[] { SqlDbType.Char, 50, typeof(string), typeof(SqlString) };
            yield return new object[] { SqlDbType.Date, null, typeof(DateTime), typeof(DateTime) };
            yield return new object[] { SqlDbType.DateTime, null, typeof(DateTime), typeof(SqlDateTime) };
            yield return new object[] { SqlDbType.DateTime2, null, typeof(DateTime), typeof(DateTime) };
            yield return new object[] { SqlDbType.DateTimeOffset, null, typeof(DateTimeOffset), typeof(DateTimeOffset) };
            yield return new object[] { SqlDbType.Decimal, null, typeof(decimal), typeof(SqlDecimal) };
            yield return new object[] { SqlDbType.Float, null, typeof(double), typeof(SqlDouble) };
            yield return new object[] { SqlDbType.Image, -1, typeof(byte[]), typeof(SqlBinary) };
            yield return new object[] { SqlDbType.Int, null, typeof(int), typeof(SqlInt32) };
            yield return new object[] { SqlDbType.Money, null, typeof(decimal), typeof(SqlMoney) };
            yield return new object[] { SqlDbType.NChar, 50, typeof(string), typeof(SqlString) };
            yield return new object[] { SqlDbType.NText, -1, typeof(string), typeof(SqlString) };
            yield return new object[] { SqlDbType.NVarChar, 50, typeof(string), typeof(SqlString) };
            yield return new object[] { SqlDbType.Real, null, typeof(float), typeof(SqlSingle) };
            yield return new object[] { SqlDbType.SmallDateTime, null, typeof(DateTime), typeof(SqlDateTime) };
            yield return new object[] { SqlDbType.SmallInt, null, typeof(short), typeof(SqlInt16) };
            yield return new object[] { SqlDbType.SmallMoney, null, typeof(decimal), typeof(SqlMoney) };
            yield return new object[] { SqlDbType.Text, -1, typeof(string), typeof(SqlString) };
            yield return new object[] { SqlDbType.Time, null, typeof(TimeSpan), typeof(TimeSpan) };
            yield return new object[] { SqlDbType.Timestamp, null, typeof(byte[]), typeof(SqlBinary) };
            yield return new object[] { SqlDbType.UniqueIdentifier, null, typeof(Guid), typeof(SqlGuid) };
            yield return new object[] { SqlDbType.VarBinary, 50, typeof(byte[]), typeof(SqlBinary) };
            yield return new object[] { SqlDbType.VarChar, 50, typeof(string), typeof(SqlString) };
            yield return new object[] { SqlDbType.Variant, null, typeof(object), typeof(object) };
            yield return new object[] { SqlDbType.Xml, null, typeof(string), typeof(SqlXml) };
        }

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    public abstract class GetXXXCheckValueTestData
        : IEnumerable<object[]>
    {
        public abstract IEnumerator<object[]> GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private static IEnumerable<object[]> GenerateCombination<T>(SqlDbType dbType, T value,
                Action<SqlDataRecord, object> setter,
                params Func<SqlDataRecord, object>[] getters)
        {
            Action<object, object> equalityAssertion = (o1, o2) => Assert.Equal((T)o1, (T)o2, EqualityComparer<T>.Default);

            foreach (Func<SqlDataRecord, object> getter in getters)
            {
                yield return new object[] { dbType, value,
                        new Action<SqlDataRecord, object>((r, o) => r.SetValue(0, o)), getter, equalityAssertion };
                yield return new object[] { dbType, value, setter, getter, equalityAssertion };
            }
        }

        protected static IEnumerable<object[]> GenerateClrCombination<T>(SqlDbType dbType, T value,
            Action<SqlDataRecord, object> setter,
            params Func<SqlDataRecord, object>[] getters)
        {
            Action<object, object> equalityAssertion = (o1, o2) => Assert.Equal((T)o1, (T)o2, EqualityComparer<T>.Default);

            foreach (object[] data in GenerateCombination(dbType, value, setter, getters))
            {
                yield return data;
            }
            foreach (object[] data in GenerateCombination(dbType, value, setter, r => (T)r[0], r => (T)r["col1"]))
            {
                yield return data;
            }

            yield return new object[] { dbType, value,
                    new Action<SqlDataRecord, object>((r, o) => r.SetValue(0, o)),
                    new Func<SqlDataRecord, object>(r => r.GetValue(0)),
                    equalityAssertion };
            yield return new object[] { dbType, value, setter, new Func<SqlDataRecord, object>(r => r.GetValue(0)), equalityAssertion };
        }

        protected static IEnumerable<object[]> GenerateSqlCombination<T>(SqlDbType dbType, T value,
            Action<SqlDataRecord, object> setter,
            params Func<SqlDataRecord, object>[] getters)
            => GenerateCombination(dbType, value, setter, getters);
    }

    public class GetFixedLengthCheckValueTestData : GetXXXCheckValueTestData
    {
        public override IEnumerator<object[]> GetEnumerator()
        {
            foreach (object[] data in GenerateClrCombination(SqlDbType.UniqueIdentifier, Guid.NewGuid(),
                (r, o) => r.SetGuid(0, (Guid)o),
                r => r.GetGuid(0), r => r.GetSqlGuid(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.UniqueIdentifier, new SqlGuid(Guid.NewGuid()),
                (r, o) => r.SetSqlGuid(0, (SqlGuid)o),
                r => new SqlGuid(r.GetGuid(0)), r => r.GetSqlGuid(0), r => new SqlGuid((Guid)r[0]), r => new SqlGuid((Guid)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.TinyInt, (byte)1,
                (r, o) => r.SetByte(0, (byte)o),
                r => r.GetByte(0), r => r.GetSqlByte(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.TinyInt, new SqlByte(1),
                (r, o) => r.SetSqlByte(0, (SqlByte)o),
                r => new SqlByte(r.GetByte(0)), r => r.GetSqlByte(0), r => new SqlByte((byte)r[0]), r => new SqlByte((byte)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.SmallInt, (short)123,
                (r, o) => r.SetInt16(0, (short)o),
                r => r.GetInt16(0), r => r.GetSqlInt16(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.SmallInt, new SqlInt16(123),
                (r, o) => r.SetSqlInt16(0, (SqlInt16)o),
                r => new SqlInt16(r.GetInt16(0)), r => r.GetSqlInt16(0), r => new SqlInt16((short)r[0]), r => new SqlInt16((short)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Int, 123456,
                (r, o) => r.SetInt32(0, (int)o),
                r => r.GetInt32(0), r => r.GetSqlInt32(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Int, new SqlInt32(123456),
                (r, o) => r.SetSqlInt32(0, (SqlInt32)o),
                r => new SqlInt32(r.GetInt32(0)), r => r.GetSqlInt32(0), r => new SqlInt32((int)r[0]), r => new SqlInt32((int)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.BigInt, (long)123456789,
                (r, o) => r.SetInt64(0, (long)o),
                r => r.GetInt64(0), r => r.GetSqlInt64(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.BigInt, new SqlInt64(123456789),
                (r, o) => r.SetSqlInt64(0, (SqlInt64)o),
                r => new SqlInt64(r.GetInt64(0)), r => r.GetSqlInt64(0), r => new SqlInt64((long)r[0]), r => new SqlInt64((long)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Float, (double)1.2,
                (r, o) => r.SetDouble(0, (double)o),
                r => r.GetDouble(0), r => r.GetSqlDouble(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Float, new SqlDouble(1.2),
                (r, o) => r.SetSqlDouble(0, (SqlDouble)o),
                r => new SqlDouble(r.GetDouble(0)), r => r.GetSqlDouble(0), r => new SqlDouble((double)r[0]), r => new SqlDouble((double)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Real, (float)1.2,
                (r, o) => r.SetFloat(0, (float)o),
                r => r.GetFloat(0), r => r.GetSqlSingle(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Real, new SqlSingle(1.2),
                (r, o) => r.SetSqlSingle(0, (SqlSingle)o),
                r => new SqlSingle(r.GetFloat(0)), r => r.GetSqlSingle(0), r => new SqlSingle((float)r[0]), r => new SqlSingle((float)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Decimal, 1.2m,
                (r, o) => r.SetDecimal(0, (decimal)o),
                r => r.GetDecimal(0), r => r.GetSqlDecimal(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Decimal, new SqlDecimal(1.2),
                (r, o) => r.SetSqlDecimal(0, (SqlDecimal)o),
                r => new SqlDecimal(r.GetDecimal(0)), r => r.GetSqlDecimal(0), r => new SqlDecimal((decimal)r[0]), r => new SqlDecimal((decimal)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Money, new SqlMoney(1.2),
                (r, o) => r.SetSqlMoney(0, (SqlMoney)o),
                r => new SqlMoney(r.GetDecimal(0)), r => r.GetSqlMoney(0), r => new SqlMoney((decimal)r[0]), r => new SqlMoney((decimal)r["col1"])))
            {
                yield return data;
            }

            // The precision of a datetime in SQL Server is limited. Hardcode a value which will always compare correctly
            foreach (object[] data in GenerateClrCombination(SqlDbType.DateTime, new DateTime(2010, 1, 1, 12, 34, 56),
                (r, o) => r.SetDateTime(0, (DateTime)o),
                r => r.GetDateTime(0), r => r.GetSqlDateTime(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.DateTime, new SqlDateTime(2010, 1, 1, 12, 34, 56),
                (r, o) => r.SetSqlDateTime(0, (SqlDateTime)o),
                r => new SqlDateTime(r.GetDateTime(0)), r => r.GetSqlDateTime(0), r => new SqlDateTime((DateTime)r[0]), r => new SqlDateTime((DateTime)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.SmallDateTime, new DateTime(2010, 1, 1, 12, 34, 56),
                (r, o) => r.SetDateTime(0, (DateTime)o),
                r => r.GetDateTime(0), r => r.GetSqlDateTime(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.SmallDateTime, new SqlDateTime(2010, 1, 1, 12, 34, 56),
                (r, o) => r.SetSqlDateTime(0, (SqlDateTime)o),
                r => new SqlDateTime(r.GetDateTime(0)), r => r.GetSqlDateTime(0), r => new SqlDateTime((DateTime)r[0]), r => new SqlDateTime((DateTime)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.DateTime2, new DateTime(2010, 1, 1, 12, 34, 56),
                (r, o) => r.SetDateTime(0, (DateTime)o),
                r => r.GetDateTime(0), r => r.GetSqlDateTime(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.DateTime2, new SqlDateTime(2010, 1, 1, 12, 34, 56),
                (r, o) => r.SetSqlDateTime(0, (SqlDateTime)o),
                r => new SqlDateTime(r.GetDateTime(0)), r => r.GetSqlDateTime(0), r => new SqlDateTime((DateTime)r[0]), r => new SqlDateTime((DateTime)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Date, new DateTime(2010, 1, 1),
                (r, o) => r.SetDateTime(0, (DateTime)o),
                r => r.GetDateTime(0), r => r.GetSqlDateTime(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Date, new SqlDateTime(2010, 1, 1),
                (r, o) => r.SetSqlDateTime(0, (SqlDateTime)o),
                r => new SqlDateTime(r.GetDateTime(0)), r => r.GetSqlDateTime(0), r => new SqlDateTime((DateTime)r[0]), r => new SqlDateTime((DateTime)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.DateTimeOffset, DateTimeOffset.Now,
                (r, o) => r.SetDateTimeOffset(0, (DateTimeOffset)o),
                r => r.GetDateTimeOffset(0)))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Time, TimeSpan.FromHours(1),
                (r, o) => r.SetTimeSpan(0, (TimeSpan)o),
                r => r.GetTimeSpan(0)))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Bit, bool.Parse(bool.TrueString),
                (r, o) => r.SetBoolean(0, (bool)o),
                r => r.GetBoolean(0), r => r.GetSqlBoolean(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Bit, new SqlBoolean(bool.Parse(bool.TrueString)),
                (r, o) => r.SetSqlBoolean(0, (SqlBoolean)o),
                r => new SqlBoolean(r.GetBoolean(0)), r => r.GetSqlBoolean(0), r => new SqlBoolean((bool)r[0]), r => new SqlBoolean((bool)r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateClrCombination(SqlDbType.Xml, "<xml />",
                (r, o) => r.SetString(0, (string)o),
                r => r.GetString(0), r => r.GetSqlString(0).Value, r => r.GetSqlXml(0).Value))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.Xml, "<xml />",
                (r, o) => r.SetSqlXml(0, CreateSqlXmlFromString((string)o)),
                r => r.GetString(0), r => r.GetSqlXml(0).Value, r => (string)r[0], r => (string)r["col1"]))
            {
                yield return data;
            }
        }

        private static SqlXml CreateSqlXmlFromString(string xmlString)
        {
            byte[] xmlBytes = Encoding.Unicode.GetBytes(xmlString);

            return new SqlXml(new System.IO.MemoryStream(xmlBytes));
        }
    }

    public class GetVariableLengthCheckValueTestData : GetXXXCheckValueTestData
    {
        public override IEnumerator<object[]> GetEnumerator()
        {
            SqlDbType[] dbTypes = new SqlDbType[] { SqlDbType.VarChar, SqlDbType.NVarChar, SqlDbType.Char, SqlDbType.NChar };
            byte[] binaryValue = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

            foreach (SqlDbType characterType in dbTypes)
            {
                foreach (object[] data in GenerateClrCombination(characterType, "string",
                    (r, o) => r.SetString(0, (string)o),
                    r => r.GetString(0), r => r.GetSqlString(0).Value))
                {
                    yield return data;
                }

                foreach (object[] data in GenerateSqlCombination(characterType, new SqlString("string"),
                    (r, o) => r.SetSqlString(0, (SqlString)o),
                    r => new SqlString(r.GetString(0)), r => r.GetSqlString(0), r => new SqlString((string)r[0]), r => new SqlString((string)r["col1"])))
                {
                    yield return data;
                }

                foreach (object[] data in GenerateSqlCombination(characterType, new SqlString("string"),
                    (r, o) => r.SetSqlChars(0, new SqlChars((SqlString)o)),
                    r => new SqlString(r.GetString(0)), r => r.GetSqlString(0), r => r.GetSqlChars(0).ToSqlString(),
                    r => new SqlString((string)r[0]), r => new SqlString((string)r["col1"])))
                {
                    yield return data;
                }
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.VarBinary, new SqlBinary(binaryValue),
                (r, o) => r.SetSqlBinary(0, (SqlBinary)o),
                r => r.GetSqlBinary(0), r => r.GetSqlBytes(0).ToSqlBinary(), r => new SqlBinary((byte[])r[0]), r => new SqlBinary((byte[])r["col1"])))
            {
                yield return data;
            }

            foreach (object[] data in GenerateSqlCombination(SqlDbType.VarBinary, new SqlBinary(binaryValue),
                (r, o) => r.SetSqlBytes(0, new SqlBytes((SqlBinary)o)),
                r => r.GetSqlBinary(0), r => r.GetSqlBytes(0).ToSqlBinary(), r => new SqlBinary((byte[])r[0]), r => new SqlBinary((byte[])r["col1"])))
            {
                yield return data;
            }
        }
    }
    [SqlServer.Server.SqlUserDefinedType(SqlServer.Server.Format.UserDefined)]
    public class TestUdt {}
}
