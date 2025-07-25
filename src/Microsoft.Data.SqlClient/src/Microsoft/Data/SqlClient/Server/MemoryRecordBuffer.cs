// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Server
{
    // Class for implementing a record object used in out-of-proc scenarios.
    internal sealed class MemoryRecordBuffer : SmiTypedGetterSetter
    {
        private SqlRecordBuffer[] _buffer;

        internal MemoryRecordBuffer(SmiMetaData[] metaData)
        {
            Debug.Assert(metaData != null, "invalid attempt to instantiate MemoryRecordBuffer with null SmiMetaData[]");

            _buffer = new SqlRecordBuffer[metaData.Length];

            for (int i = 0; i < _buffer.Length; ++i)
            {
                _buffer[i] = new SqlRecordBuffer(metaData[i]);
            }
        }

        #region Read/Write
        protected override bool CanGet => true;

        protected override bool CanSet => true;
        #endregion

        #region Getters
        // Null test
        //      valid for all types
        public override bool IsDBNull(int ordinal)
        {
            return _buffer[ordinal].IsNull;
        }

        // Check what type current sql_variant value is
        //      valid for SqlDbType.Variant
        public override SmiMetaData GetVariantType(int ordinal)
        {
            return _buffer[ordinal].VariantType;
        }

        //  valid for SqlDbType.Bit
        public override bool GetBoolean(int ordinal)
        {
            return _buffer[ordinal].Boolean;
        }

        //  valid for SqlDbType.TinyInt
        public override byte GetByte(int ordinal)
        {
            return _buffer[ordinal].Byte;
        }

        // valid for SqlDbTypes: Binary, VarBinary, Image, Udt, Xml, Char, VarChar, Text, NChar, NVarChar, NText
        //  (Character type support needed for ExecuteXmlReader handling)
        public override long GetBytesLength(int ordinal)
        {
            return _buffer[ordinal].BytesLength;
        }
        public override int GetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            return _buffer[ordinal].GetBytes(fieldOffset, buffer, bufferOffset, length);
        }

        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        public override long GetCharsLength(int ordinal)
        {
            return _buffer[ordinal].CharsLength;
        }
        public override int GetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            return _buffer[ordinal].GetChars(fieldOffset, buffer, bufferOffset, length);
        }
        public override string GetString(int ordinal)
        {
            return _buffer[ordinal].String;
        }

        // valid for SqlDbType.SmallInt
        public override short GetInt16(int ordinal)
        {
            return _buffer[ordinal].Int16;
        }

        // valid for SqlDbType.Int
        public override int GetInt32(int ordinal)
        {
            return _buffer[ordinal].Int32;
        }

        // valid for SqlDbType.BigInt, SqlDbType.Money, SqlDbType.SmallMoney
        public override long GetInt64(int ordinal)
        {
            return _buffer[ordinal].Int64;
        }

        // valid for SqlDbType.Real
        public override float GetSingle(int ordinal)
        {
            return _buffer[ordinal].Single;
        }

        // valid for SqlDbType.Float
        public override double GetDouble(int ordinal)
        {
            return _buffer[ordinal].Double;
        }

        // valid for SqlDbType.Numeric (uses SqlDecimal since Decimal cannot hold full range)
        public override SqlDecimal GetSqlDecimal(int ordinal)
        {
            return _buffer[ordinal].SqlDecimal;
        }

        // valid for DateTime, SmallDateTime, Date, and DateTime2
        public override DateTime GetDateTime(int ordinal)
        {
            return _buffer[ordinal].DateTime;
        }

        // valid for UniqueIdentifier
        public override Guid GetGuid(int ordinal)
        {
            return _buffer[ordinal].Guid;
        }

        // valid for SqlDbType.Time
        public override TimeSpan GetTimeSpan(int ordinal)
        {
            return _buffer[ordinal].TimeSpan;
        }

        // valid for DateTimeOffset
        public override DateTimeOffset GetDateTimeOffset(int ordinal)
        {
            return _buffer[ordinal].DateTimeOffset;
        }
        #endregion

        #region Setters
        
        // Set value to null
        //  valid for all types
        public override void SetDBNull(int ordinal)
        {
            _buffer[ordinal].SetNull();
        }

        //  valid for SqlDbType.Bit
        public override void SetBoolean(int ordinal, bool value)
        {
            _buffer[ordinal].Boolean = value;
        }

        //  valid for SqlDbType.TinyInt
        public override void SetByte(int ordinal, byte value)
        {
            _buffer[ordinal].Byte = value;
        }

        // Semantics for SetBytes are to modify existing value, not overwrite
        //  Use in combination with SetLength to ensure overwriting when necessary
        // valid for SqlDbTypes: Binary, VarBinary, Image, Udt, Xml
        //      (VarBinary assumed for variants)
        public override int SetBytes(int ordinal, long fieldOffset, byte[] buffer, int bufferOffset, int length)
        {
            return _buffer[ordinal].SetBytes(fieldOffset, buffer, bufferOffset, length);
        }
        public override void SetBytesLength(int ordinal, long length)
        {
            _buffer[ordinal].BytesLength = length;
        }

        // Semantics for SetChars are to modify existing value, not overwrite
        //  Use in combination with SetLength to ensure overwriting when necessary
        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        //      (NVarChar and global clr collation assumed for variants)
        public override int SetChars(int ordinal, long fieldOffset, char[] buffer, int bufferOffset, int length)
        {
            return _buffer[ordinal].SetChars(fieldOffset, buffer, bufferOffset, length);
        }
        public override void SetCharsLength(int ordinal, long length)
        {
            _buffer[ordinal].CharsLength = length;
        }

        // valid for character types: Char, VarChar, Text, NChar, NVarChar, NText
        public override void SetString(int ordinal, string value, int offset, int length)
        {
            Debug.Assert(offset == 0 && length <= value.Length, "Invalid string length or offset"); // for sqlvariant, length could be less than value.Length

            _buffer[ordinal].String = value.Substring(offset, length); // Perf test shows that Substring method has already optimized the case where length = value.Length
        }

        // valid for SqlDbType.SmallInt
        public override void SetInt16(int ordinal, short value)
        {
            _buffer[ordinal].Int16 = value;
        }

        // valid for SqlDbType.Int
        public override void SetInt32(int ordinal, int value)
        {
            _buffer[ordinal].Int32 = value;
        }

        // valid for SqlDbType.BigInt, SqlDbType.Money, SqlDbType.SmallMoney
        public override void SetInt64(int ordinal, long value)
        {
            _buffer[ordinal].Int64 = value;
        }

        // valid for SqlDbType.Real
        public override void SetSingle(int ordinal, float value)
        {
            _buffer[ordinal].Single = value;
        }

        // valid for SqlDbType.Float
        public override void SetDouble(int ordinal, double value)
        {
            _buffer[ordinal].Double = value;
        }

        // valid for SqlDbType.Numeric (uses SqlDecimal since Decimal cannot hold full range)
        public override void SetSqlDecimal(int ordinal, SqlDecimal value)
        {
            _buffer[ordinal].SqlDecimal = value;
        }

        // valid for DateTime, SmallDateTime, Date, and DateTime2
        public override void SetDateTime(int ordinal, DateTime value)
        {
            _buffer[ordinal].DateTime = value;
        }

        // valid for UniqueIdentifier
        public override void SetGuid(int ordinal, Guid value)
        {
            _buffer[ordinal].Guid = value;
        }

        // SqlDbType.Time
        public override void SetTimeSpan(int ordinal, TimeSpan value)
        {
            _buffer[ordinal].TimeSpan = value;
        }

        // DateTimeOffset
        public override void SetDateTimeOffset(int ordinal, DateTimeOffset value)
        {
            _buffer[ordinal].DateTimeOffset = value;
        }

        // valid for SqlDbType.Variant
        public override void SetVariantMetaData(int ordinal, SmiMetaData metaData)
        {
            _buffer[ordinal].VariantType = metaData;
        }
        #endregion
    }
}
