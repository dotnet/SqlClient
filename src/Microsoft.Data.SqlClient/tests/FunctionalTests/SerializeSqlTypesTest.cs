// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using Xunit;
using System.IO;
using System.Runtime.Serialization;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SerializeSqlTypesTests
    {
        private T SerializeAndDeserialize<T>(T value, string expectedSerializedValue)
        {
            DataContractSerializer serializer = new DataContractSerializer(typeof(T));
            using (MemoryStream memoryStream = new MemoryStream())
            {
                serializer.WriteObject(memoryStream, value);
                memoryStream.Position = 0;

                string serializedValue = new StreamReader(memoryStream).ReadToEnd();

                Assert.Equal(expectedSerializedValue, serializedValue);

                memoryStream.Position = 0;
                T deserializedValue = (T)serializer.ReadObject(memoryStream);
                return deserializedValue;
            }
        }
        [Fact]
        public void SerializeAndDeserializeSqlTypes()
        {
            SqlBoolean sqlBooleanMax = new SqlBoolean(true);
            Assert.StrictEqual<SqlBoolean>(sqlBooleanMax, SerializeAndDeserialize<SqlBoolean>(sqlBooleanMax, "<boolean>true</boolean>"));

            SqlBoolean sqlBooleanMin = new SqlBoolean(false);
            Assert.StrictEqual<SqlBoolean>(sqlBooleanMin, SerializeAndDeserialize<SqlBoolean>(sqlBooleanMin, "<boolean>false</boolean>"));

            SqlByte sqlByteMax = new SqlByte(255);
            Assert.StrictEqual<SqlByte>(sqlByteMax, SerializeAndDeserialize<SqlByte>(sqlByteMax, "<unsignedByte>255</unsignedByte>"));

            SqlByte sqlByteMin = new SqlByte(0);
            Assert.StrictEqual<SqlByte>(sqlByteMin, SerializeAndDeserialize<SqlByte>(sqlByteMin, "<unsignedByte>0</unsignedByte>"));

            SqlDateTime sqlDateTimeMax = new SqlDateTime(9999, 12, 31, 23, 59, 59);
            Assert.StrictEqual<SqlDateTime>(sqlDateTimeMax, SerializeAndDeserialize<SqlDateTime>(sqlDateTimeMax, "<dateTime>9999-12-31T23:59:59.000</dateTime>"));

            SqlDateTime sqlDateTimeMin = new SqlDateTime(1753, 1, 1, 0, 0, 0);
            Assert.StrictEqual<SqlDateTime>(sqlDateTimeMin, SerializeAndDeserialize<SqlDateTime>(sqlDateTimeMin, "<dateTime>1753-01-01T00:00:00.000</dateTime>"));

            SqlDouble sqlDoubleMaxDec = new SqlDouble(0.999999999999999);
            Assert.StrictEqual<SqlDouble>(sqlDoubleMaxDec, SerializeAndDeserialize<SqlDouble>(sqlDoubleMaxDec, "<double>0.999999999999999</double>"));

            SqlDouble sqlDoubleMinDec = new SqlDouble(999999999999999);
            Assert.StrictEqual<SqlDouble>(sqlDoubleMinDec, SerializeAndDeserialize<SqlDouble>(sqlDoubleMinDec, "<double>999999999999999</double>"));

            SqlInt16 sqlInt16Max = new SqlInt16(32767);
            Assert.StrictEqual<SqlInt16>(sqlInt16Max, SerializeAndDeserialize<SqlInt16>(sqlInt16Max, "<short>32767</short>"));

            SqlInt16 sqlInt16Min = new SqlInt16(-32768);
            Assert.StrictEqual<SqlInt16>(sqlInt16Min, SerializeAndDeserialize<SqlInt16>(sqlInt16Min, "<short>-32768</short>"));

            SqlInt32 sqlInt32Max = new SqlInt32(2147483647);
            Assert.StrictEqual<SqlInt32>(sqlInt32Max, SerializeAndDeserialize<SqlInt32>(sqlInt32Max, "<int>2147483647</int>"));

            SqlInt32 sqlInt32Min = new SqlInt32(-2147483648);
            Assert.StrictEqual<SqlInt32>(sqlInt32Min, SerializeAndDeserialize<SqlInt32>(sqlInt32Min, "<int>-2147483648</int>"));

            SqlInt64 sqlInt64Max = new SqlInt64(9223372036854775807);
            Assert.StrictEqual<SqlInt64>(sqlInt64Max, SerializeAndDeserialize<SqlInt64>(sqlInt64Max, "<long>9223372036854775807</long>"));

            SqlInt64 sqlInt64Min = new SqlInt64(-9223372036854775808);
            Assert.StrictEqual<SqlInt64>(sqlInt64Min, SerializeAndDeserialize<SqlInt64>(sqlInt64Min, "<long>-9223372036854775808</long>"));

            SqlString sqlString = new SqlString("abcdefghijklmnopqrstuvwxyz");
            SqlString sqlStringDeserialized = SerializeAndDeserialize<SqlString>(sqlString, "<string>abcdefghijklmnopqrstuvwxyz</string>");
            // Cannot use StrictEqual because information such as LCID is lost when the SqlString is serialized
            Assert.Equal(sqlString.Value, sqlStringDeserialized.Value);
        }
    }
}
