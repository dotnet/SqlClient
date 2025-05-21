// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlBuffer
    {
        internal enum StorageType
        {
            Empty = 0,
            Boolean,
            Byte,
            DateTime,
            Decimal,
            Double,
            Int16,
            Int32,
            Int64,
            Guid,
            Money,
            Single,
            String,
            SqlBinary,
            SqlCachedBuffer,
            SqlGuid,
            SqlXml,
            Date,
            DateTime2,
            DateTimeOffset,
            Time,
            Json,
        }

        internal struct NumericInfo
        {
            // This is used to store Decimal data
            internal int _data1;
            internal int _data2;
            internal int _data3;
            internal int _data4;
            internal byte _precision;
            internal byte _scale;
            internal bool _positive;
        }

        internal struct TimeInfo
        {
            internal long _ticks;
            internal byte _scale;
        }

        internal struct DateTime2Info
        {
            internal int _date;
            internal TimeInfo _timeInfo;
        }

        internal struct DateTimeOffsetInfo
        {
            internal DateTime2Info _dateTime2Info;
            internal short _offset;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Storage
        {
            [FieldOffset(0)]
            internal bool _boolean;
            [FieldOffset(0)]
            internal byte _byte;
            [FieldOffset(0)]
            internal DateTimeInfo _dateTimeInfo;
            [FieldOffset(0)]
            internal double _double;
            [FieldOffset(0)]
            internal NumericInfo _numericInfo;
            [FieldOffset(0)]
            internal short _int16;
            [FieldOffset(0)]
            internal int _int32;
            [FieldOffset(0)]
            internal long _int64;     // also used to store Money, UtcDateTime, Date , and Time
            [FieldOffset(0)]
            internal Guid _guid;
            [FieldOffset(0)]
            internal float _single;
            [FieldOffset(0)]
            internal TimeInfo _timeInfo;
            [FieldOffset(0)]
            internal DateTime2Info _dateTime2Info;
            [FieldOffset(0)]
            internal DateTimeOffsetInfo _dateTimeOffsetInfo;
        }

        #region Member Variables

        private StorageType _type;
        
        // Storage for value types, eg, bool, int32, datetime2.
        private Storage _value;
        
        // Storage for reference types, eg, String, SqlBinary, SqlCachedBuffer, SqlGuid.
        private object _object;    // String, SqlBinary, SqlCachedBuffer, SqlGuid, SqlString, SqlXml

        #endregion
        
        internal SqlBuffer()
        {
        }

        private SqlBuffer(SqlBuffer value)
        { // Clone
            // value types
            IsNull = value.IsNull;
            _type = value._type;
            // ref types - should also be read only unless at some point we allow this data
            // to be mutable, then we will need to copy
            _value = value._value;
            _object = value._object;
        }

        #region General Properties
        
        internal bool IsEmpty => _type == StorageType.Empty;

        internal bool IsNull { get; private set; }

        internal StorageType VariantInternalStorageType => _type;

        #endregion
        
        #region Type Conversion Properties
        
        internal bool Boolean
        {
            get => GetValue(StorageType.Boolean, _value._boolean);
            set => SetValue(StorageType.Boolean, ref _value._boolean, value);
        }
        
        internal byte Byte
        {
            get => GetValue(StorageType.Byte, _value._byte);
            set => SetValue(StorageType.Byte, ref _value._byte, value);
        }
        
        internal DateTime DateTime
        {
            get
            {
                ThrowIfNull();
                return _type switch
                {
                    StorageType.Date => DateTime.MinValue.AddDays(_value._int32),
                    StorageType.DateTime => _value._dateTimeInfo.ToDateTime(),
                    // @TODO: Move conversion into DateTime2Info struct
                    StorageType.DateTime2 => new DateTime(GetTicksFromDateTime2Info(_value._dateTime2Info)),
                    _ => (DateTime)Value,
                };
            }
        }
        
        internal double Double
        {
            get => GetValue(StorageType.Double, _value._double);
            set => SetValue(StorageType.Double, ref _value._double, value);
        }
        
        internal Guid Guid
        {
            get
            {
                ThrowIfNull();
                return _type switch
                {
                    StorageType.Guid => _value._guid,
                    StorageType.SqlGuid => ((SqlGuid)_object).Value,
                    _ => (Guid)Value,
                };
            }
            set => SetValue(StorageType.Guid, ref _value._guid, value);
        }
        
        internal short Int16
        {
            get => GetValue(StorageType.Int16, _value._int16);
            set => SetValue(StorageType.Int16, ref _value._int16, value);
        }
        
        internal int Int32
        {
            get => GetValue(StorageType.Int32, _value._int32);
            set => SetValue(StorageType.Int32, ref _value._int32, value);
        }
        
        internal long Int64
        {
            get => GetValue(StorageType.Int64, _value._int64);
            set => SetValue(StorageType.Int64, ref _value._int64, value);
        }
        
        internal float Single
        {
            get => GetValue(StorageType.Single, _value._single);
            set => SetValue(StorageType.Single, ref _value._single, value);
        }
        
        
        internal SqlBinary SqlBinary
        {
            get => _type == StorageType.SqlBinary
                ? IsNull ? SqlBinary.Null : (SqlBinary)_object
                : (SqlBinary)SqlValue;
            set => SetObject(StorageType.SqlBinary, value);
        }

        internal SqlBoolean SqlBoolean
        {
            get => _type == StorageType.Boolean
                ? IsNull ? SqlBoolean.Null : new SqlBoolean(_value._boolean)
                : (SqlBoolean)SqlValue;
        }

        internal SqlByte SqlByte
        {
            get => _type == StorageType.Byte
                ? IsNull ? SqlByte.Null : new SqlByte(_value._byte)
                : (SqlByte)SqlValue;
        }

        internal SqlCachedBuffer SqlCachedBuffer
        {
            get => _type == StorageType.SqlCachedBuffer
                ? IsNull ? SqlCachedBuffer.Null : (SqlCachedBuffer)_object
                : (SqlCachedBuffer)SqlValue;
            set => SetObject(StorageType.SqlCachedBuffer, value);
        }
        
        internal SqlDateTime SqlDateTime
        {
            // @TODO: Add helper to DateTimeInfo struct
            get => _type == StorageType.DateTime
                ? IsNull ? SqlDateTime.Null : new SqlDateTime(_value._dateTimeInfo.DayPart, _value._dateTimeInfo.TimePart)
                : (SqlDateTime)SqlValue;
        }
        
        internal SqlDouble SqlDouble
        {
            get => _type == StorageType.Double
                ? IsNull ? SqlDouble.Null : new SqlDouble(_value._double)
                : (SqlDouble)SqlValue;
        }
        
        internal SqlGuid SqlGuid
        {
            get => _type switch
            {
                StorageType.Guid => IsNull ? SqlGuid.Null : new SqlGuid(_value._guid),
                StorageType.SqlGuid => IsNull ? SqlGuid.Null : (SqlGuid)_object,
                _ => (SqlGuid)SqlValue
            };
            set => SetObject(StorageType.SqlGuid, value);
        }
        
        internal SqlInt16 SqlInt16
        {
            get => _type == StorageType.Int16
                ? IsNull ? SqlInt16.Null : new SqlInt16(_value._int16)
                : (SqlInt16)SqlValue;
        }

        internal SqlInt32 SqlInt32
        {
            get => _type == StorageType.Int32
                ? IsNull ? SqlInt32.Null : new SqlInt32(_value._int32)
                : (SqlInt32)SqlValue;
        }

        internal SqlInt64 SqlInt64
        {
            get => _type == StorageType.Int64
                ? IsNull ? SqlInt64.Null : new SqlInt64(_value._int64)
                : (SqlInt64)SqlValue;
        }
        
        internal SqlJson SqlJson
        {
            get => StorageType.Json == _type
                ? IsNull ? SqlJson.Null : new SqlJson((string)_object)
                : (SqlJson)SqlValue;
        }
        
        internal SqlSingle SqlSingle
        {
            get => _type == StorageType.Single
                ? IsNull ? SqlSingle.Null : new SqlSingle(_value._single)
                : (SqlSingle)SqlValue;
        }
        
        internal SqlXml SqlXml
        {
            get => _type == StorageType.SqlXml
                ? IsNull ? SqlXml.Null : (SqlXml)_object
                : (SqlXml)SqlValue;
            set => SetObject(StorageType.SqlXml, value);
        }
        
        #endregion

        internal byte[] ByteArray
        {
            get
            {
                ThrowIfNull(); // Must be checked here because SqlBinary allows null.
                return SqlBinary.Value;
            }
        }

        #region Decimal
        internal decimal Decimal
        {
            get
            {
                ThrowIfNull();

                if (StorageType.Decimal == _type)
                {
                    if (_value._numericInfo._data4 != 0 || _value._numericInfo._scale > 28)
                    {
                        // Only removing trailing zeros from a decimal part won't hit its value!
                        if (_value._numericInfo._scale > 0)
                        {
                            int zeroCnt = FindTrailingZerosAndPrec((uint)_value._numericInfo._data1, (uint)_value._numericInfo._data2, 
                                                                   (uint)_value._numericInfo._data3, (uint)_value._numericInfo._data4, 
                                                                   _value._numericInfo._scale, out int precision);

                            int minScale = _value._numericInfo._scale - zeroCnt; // minimum possible sacle after removing the trailing zeros.

                            if (zeroCnt > 0 && minScale <= 28 && precision <= 29)
                            {
                                SqlDecimal sqlValue = new(_value._numericInfo._precision, _value._numericInfo._scale, _value._numericInfo._positive, 
                                                          _value._numericInfo._data1, _value._numericInfo._data2, 
                                                          _value._numericInfo._data3, _value._numericInfo._data4);

                                int integral = precision - minScale;
                                int newPrec = 29;

                                if (integral != 1 && precision != 29)
                                {
                                    newPrec = 28;
                                }

                                try
                                {
                                    // Precision could be 28 or 29
                                    // ex: (precision == 29 && scale == 28)
                                    // valid:   (+/-)7.1234567890123456789012345678
                                    // invalid: (+/-)8.1234567890123456789012345678
                                    return SqlDecimal.ConvertToPrecScale(sqlValue, newPrec, newPrec - integral).Value;
                                }
                                catch (OverflowException)
                                {
                                    throw new OverflowException(SQLResource.ConversionOverflowMessage);
                                }
                            }
                        }
                        throw new OverflowException(SQLResource.ConversionOverflowMessage);
                    }
                    return new decimal(_value._numericInfo._data1, _value._numericInfo._data2, _value._numericInfo._data3, !_value._numericInfo._positive, _value._numericInfo._scale);
                }
                if (StorageType.Money == _type)
                {
                    long l = _value._int64;
                    bool isNegative = false;
                    if (l < 0)
                    {
                        isNegative = true;
                        l = -l;
                    }
                    return new decimal((int)(l & 0xffffffff), (int)(l >> 32), 0, isNegative, 4);
                }
                return (decimal)Value; // anything else we haven't thought of goes through boxing.
            }
        }

        /// <summary>
        /// Returns number of trailing zeros using the supplied parameters.
        /// </summary>
        /// <param name="data1">An 32-bit unsigned integer which will be combined with data2, data3, and data4</param>
        /// <param name="data2">An 32-bit unsigned integer which will be combined with data1, data3, and data4</param>
        /// <param name="data3">An 32-bit unsigned integer which will be combined with data1, data2, and data4</param>
        /// <param name="data4">An 32-bit unsigned integer which will be combined with data1, data2, and data3</param>
        /// <param name="scale">The number of decimal places</param>
        /// <param name="valuablePrecision">OUT |The number of digits without trailing zeros</param>
        /// <returns>Number of trailing zeros</returns>
        private static int FindTrailingZerosAndPrec(uint data1, uint data2, uint data3, uint data4, byte scale, out int valuablePrecision)
        {
            // Make local copy of data to avoid modifying input.
            Span<uint> rgulNumeric = stackalloc uint[4] { data1, data2, data3, data4 };
            int zeroCnt = 0;    //Number of trailing zero digits
            int precCnt = 0;    //Valuable precision
            uint uiRem = 0;     //Remainder of a division by 10
            int len = 4;        // Max possible items

            //Retrieve each digit from the lowest significant digit
            while (len > 1 || rgulNumeric[0] != 0)
            {
                SqlDecimalDivBy(rgulNumeric, ref len, 10, out uiRem);
                if (uiRem == 0 && precCnt == 0)
                {
                    zeroCnt++;
                }
                else
                {
                    precCnt++;
                }
            }

            if (uiRem == 0)
            {
                zeroCnt = scale;
            }

            // if scale of the number has not been reached, pad remaining number with zeros.
            if (zeroCnt + precCnt <= scale)
            {
                precCnt = scale - zeroCnt + 1;
            }
            valuablePrecision = precCnt;
            return zeroCnt;
        }

        /// <summary>
        /// Multi-precision one super-digit divide in place.
        /// U = U / D,
        /// R = U % D
        /// (Length of U can decrease)
        /// </summary>
        /// <param name="data">InOut | U</param>
        /// <param name="len">InOut | Number of items with non-zero value in U between 1 to 4</param>
        /// <param name="divisor">In     | D</param>
        /// <param name="remainder">Out    | R</param>
        private static void SqlDecimalDivBy(Span<uint> data, ref int len, uint divisor, out uint remainder)
        {
            uint uiCarry = 0;
            ulong ulAccum;
            ulong ulDivisor = (ulong)divisor;
            int iLen = len;

            while (iLen > 0)
            {
                iLen--;
                ulAccum = (((ulong)uiCarry) << 32) + (ulong)(data[iLen]);
                data[iLen] = (uint)(ulAccum / ulDivisor);
                uiCarry = (uint)(ulAccum - (ulong)data[iLen] * ulDivisor);  // (ULONG) (ulAccum % divisor)
            }
            remainder = uiCarry;

            // Normalize multi-precision number - remove leading zeroes
            while (len > 1 && data[len - 1] == 0)
            { len--; }
        }
        #endregion

        internal string String
        {
            get
            {
                ThrowIfNull();

                if (StorageType.String == _type || StorageType.Json == _type)
                {
                    return (string)_object;
                }
                else if (StorageType.SqlCachedBuffer == _type)
                {
                    return ((SqlCachedBuffer)(_object)).ToString();
                }
                return (string)Value; // anything else we haven't thought of goes through boxing.
            }
        }

        // use static list of format strings indexed by scale for perf
        private static readonly string[] s_sql2008DateTimeOffsetFormatByScale = new string[] {
                "yyyy-MM-dd HH:mm:ss zzz",
                "yyyy-MM-dd HH:mm:ss.f zzz",
                "yyyy-MM-dd HH:mm:ss.ff zzz",
                "yyyy-MM-dd HH:mm:ss.fff zzz",
                "yyyy-MM-dd HH:mm:ss.ffff zzz",
                "yyyy-MM-dd HH:mm:ss.fffff zzz",
                "yyyy-MM-dd HH:mm:ss.ffffff zzz",
                "yyyy-MM-dd HH:mm:ss.fffffff zzz",
        };

        private static readonly string[] s_sql2008DateTime2FormatByScale = new string[] {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.f",
                "yyyy-MM-dd HH:mm:ss.ff",
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-dd HH:mm:ss.ffff",
                "yyyy-MM-dd HH:mm:ss.fffff",
                "yyyy-MM-dd HH:mm:ss.ffffff",
                "yyyy-MM-dd HH:mm:ss.fffffff",
        };

        private static readonly string[] s_sql2008TimeFormatByScale = new string[] {
                "HH:mm:ss",
                "HH:mm:ss.f",
                "HH:mm:ss.ff",
                "HH:mm:ss.fff",
                "HH:mm:ss.ffff",
                "HH:mm:ss.fffff",
                "HH:mm:ss.ffffff",
                "HH:mm:ss.fffffff",
        };

        internal string Sql2008DateTimeString
        {
            get
            {
                ThrowIfNull();

                if (StorageType.Date == _type)
                {
                    return DateTime.ToString("yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);
                }
                if (StorageType.Time == _type)
                {
                    byte scale = _value._timeInfo._scale;
                    return new DateTime(_value._timeInfo._ticks).ToString(s_sql2008TimeFormatByScale[scale], DateTimeFormatInfo.InvariantInfo);
                }
                if (StorageType.DateTime2 == _type)
                {
                    byte scale = _value._dateTime2Info._timeInfo._scale;
                    return DateTime.ToString(s_sql2008DateTime2FormatByScale[scale], DateTimeFormatInfo.InvariantInfo);
                }
                if (StorageType.DateTimeOffset == _type)
                {
                    DateTimeOffset dto = DateTimeOffset;
                    byte scale = _value._dateTimeOffsetInfo._dateTime2Info._timeInfo._scale;
                    return dto.ToString(s_sql2008DateTimeOffsetFormatByScale[scale], DateTimeFormatInfo.InvariantInfo);
                }
                return (string)Value; // anything else we haven't thought of goes through boxing.
            }
        }

        internal SqlString Sql2008DateTimeSqlString
        {
            get
            {
                if (StorageType.Date == _type ||
                    StorageType.Time == _type ||
                    StorageType.DateTime2 == _type ||
                    StorageType.DateTimeOffset == _type)
                {
                    if (IsNull)
                    {
                        return SqlString.Null;
                    }
                    return new SqlString(Sql2008DateTimeString);
                }
                return (SqlString)SqlValue; // anything else we haven't thought of goes through boxing.
            }
        }

        internal TimeSpan Time
        {
            get
            {
                ThrowIfNull();

                if (StorageType.Time == _type)
                {
                    return new TimeSpan(_value._timeInfo._ticks);
                }

                return (TimeSpan)Value; // anything else we haven't thought of goes through boxing.
            }
        }

#if NET
        internal TimeOnly TimeOnly
        {
            get
            {
                ThrowIfNull();

                if (StorageType.Time == _type)
                {
                    return new TimeOnly(_value._timeInfo._ticks);
                }

                return (TimeOnly)Value; // anything else we haven't thought of goes through boxing.
            }
        }

        internal DateOnly DateOnly
        {
            get
            {
                ThrowIfNull();

                if (StorageType.Date == _type)
                {
                    return DateOnly.MinValue.AddDays(_value._int32);
                }
                return (DateOnly)Value; // anything else we haven't thought of goes through boxing.
            }
        }
#endif

        internal DateTimeOffset DateTimeOffset
        {
            get
            {
                ThrowIfNull();

                if (StorageType.DateTimeOffset == _type)
                {
                    TimeSpan offset = new TimeSpan(0, _value._dateTimeOffsetInfo._offset, 0);
                    // datetime part presents time in UTC
                    return new DateTimeOffset(GetTicksFromDateTime2Info(_value._dateTimeOffsetInfo._dateTime2Info) + offset.Ticks, offset);
                }

                return (DateTimeOffset)Value; // anything else we haven't thought of goes through boxing.
            }
        }

        private static long GetTicksFromDateTime2Info(DateTime2Info dateTime2Info)
        {
            return (dateTime2Info._date * TimeSpan.TicksPerDay + dateTime2Info._timeInfo._ticks);
        }


        

        

        internal SqlDecimal SqlDecimal
        {
            get
            {
                if (StorageType.Decimal == _type)
                {
                    if (IsNull)
                    {
                        return SqlDecimal.Null;
                    }
                    return new SqlDecimal(_value._numericInfo._precision,
                                          _value._numericInfo._scale,
                                          _value._numericInfo._positive,
                                          _value._numericInfo._data1,
                                          _value._numericInfo._data2,
                                          _value._numericInfo._data3,
                                          _value._numericInfo._data4
                                          );
                }
                return (SqlDecimal)SqlValue; // anything else we haven't thought of goes through boxing.
            }
        }

        

        

        

        internal SqlMoney SqlMoney
        {
            get
            {
                if (StorageType.Money == _type)
                {
                    if (IsNull)
                    {
                        return SqlMoney.Null;
                    }
#if NET
                    return SqlMoney.FromTdsValue(_value._int64);
#else
                    return SqlTypeWorkarounds.SqlMoneyCtor(_value._int64, 1/*ignored*/);
#endif
                }
                return (SqlMoney)SqlValue; // anything else we haven't thought of goes through boxing.
            }
        }

        

        internal SqlString SqlString
        {
            get
            {
                // String and Json storage type are both strings.
                if (StorageType.String == _type || StorageType.Json == _type)
                {
                    if (IsNull)
                    {
                        return SqlString.Null;
                    }
                    return new SqlString((string)_object);
                }
                else if (StorageType.SqlCachedBuffer == _type)
                {
                    SqlCachedBuffer data = (SqlCachedBuffer)(_object);
                    if (data.IsNull)
                    {
                        return SqlString.Null;
                    }
                    return data.ToSqlString();
                }
                return (SqlString)SqlValue; // anything else we haven't thought of goes through boxing.
            }
        }

        

        internal object SqlValue
        {
            get
            {
                switch (_type)
                {
                    case StorageType.Empty:
                        return DBNull.Value;
                    case StorageType.Boolean:
                        return SqlBoolean;
                    case StorageType.Byte:
                        return SqlByte;
                    case StorageType.DateTime:
                        return SqlDateTime;
                    case StorageType.Decimal:
                        return SqlDecimal;
                    case StorageType.Double:
                        return SqlDouble;
                    case StorageType.Int16:
                        return SqlInt16;
                    case StorageType.Int32:
                        return SqlInt32;
                    case StorageType.Int64:
                        return SqlInt64;
                    case StorageType.Guid:
                        return SqlGuid;
                    case StorageType.Money:
                        return SqlMoney;
                    case StorageType.Single:
                        return SqlSingle;
                    case StorageType.String:
                        return SqlString;
                    case StorageType.Json:
                        return SqlJson;
                    case StorageType.SqlCachedBuffer:
                        {
                            SqlCachedBuffer data = (SqlCachedBuffer)(_object);
                            if (data.IsNull)
                            {
                                return SqlXml.Null;
                            }
                            return data.ToSqlXml();
                        }

                    case StorageType.SqlBinary:
                    case StorageType.SqlGuid:
                        return _object;

                    case StorageType.SqlXml:
                        if (IsNull)
                        {
                            return SqlXml.Null;
                        }
                        Debug.Assert(_object != null);
                        return (SqlXml)_object;

                    case StorageType.Date:
                    case StorageType.DateTime2:
                        if (IsNull)
                        {
                            return DBNull.Value;
                        }
                        return DateTime;

                    case StorageType.DateTimeOffset:
                        if (IsNull)
                        {
                            return DBNull.Value;
                        }
                        return DateTimeOffset;

                    case StorageType.Time:
                        if (IsNull)
                        {
                            return DBNull.Value;
                        }
                        return Time;
                }
                return null; // need to return the value as an object of some SQL type
            }
        }


        // these variables store pre-boxed bool values to be used when returning a boolean
        // in a object typed location, if these are not used a new value is boxed each time
        // one is needed which leads to a lot of garbage which needs to be collected
        private static readonly object s_cachedTrueObject = true;
        private static readonly object s_cachedFalseObject = false;

        internal object Value
        {
            get
            {
                if (IsNull)
                {
                    return DBNull.Value;
                }
                switch (_type)
                {
                    case StorageType.Empty:
                        return DBNull.Value;
                    case StorageType.Boolean:
                        return Boolean ? s_cachedTrueObject : s_cachedFalseObject; // return pre-boxed values for perf
                    case StorageType.Byte:
                        return Byte;
                    case StorageType.DateTime:
                        return DateTime;
                    case StorageType.Decimal:
                        return Decimal;
                    case StorageType.Double:
                        return Double;
                    case StorageType.Int16:
                        return Int16;
                    case StorageType.Int32:
                        return Int32;
                    case StorageType.Int64:
                        return Int64;
                    case StorageType.Guid:
                        return Guid;
                    case StorageType.Money:
                        return Decimal;
                    case StorageType.Single:
                        return Single;
                    case StorageType.String:
                        return String;
                    case StorageType.SqlBinary:
                        return ByteArray;
                    case StorageType.SqlCachedBuffer:
                        {
                            // If we have a CachedBuffer, it's because it's an XMLTYPE column
                            // and we have to return a string when they're asking for the CLS
                            // value of the column.
                            return ((SqlCachedBuffer)(_object)).ToString();
                        }
                    case StorageType.SqlGuid:
                        return Guid;
                    case StorageType.SqlXml:
                        {
                            // XMLTYPE columns must be returned as string when asking for the CLS value
                            SqlXml data = (SqlXml)_object;
                            string s = data.Value;
                            return s;
                        }
                    case StorageType.Date:
                        return DateTime;
                    case StorageType.DateTime2:
                        return DateTime;
                    case StorageType.DateTimeOffset:
                        return DateTimeOffset;
                    case StorageType.Time:
                        return Time;
                    case StorageType.Json:
                        return String;
                }
                return null; // need to return the value as an object of some CLS type
            }
        }

        internal Type GetTypeFromStorageType(bool isSqlType)
        {
            if (isSqlType)
            {
                switch (_type)
                {
                    case StorageType.Empty:
                        return null;
                    case StorageType.Boolean:
                        return typeof(SqlBoolean);
                    case StorageType.Byte:
                        return typeof(SqlByte);
                    case StorageType.DateTime:
                        return typeof(SqlDateTime);
                    case StorageType.Decimal:
                        return typeof(SqlDecimal);
                    case StorageType.Double:
                        return typeof(SqlDouble);
                    case StorageType.Int16:
                        return typeof(SqlInt16);
                    case StorageType.Int32:
                        return typeof(SqlInt32);
                    case StorageType.Int64:
                        return typeof(SqlInt64);
                    case StorageType.Guid:
                        return typeof(SqlGuid);
                    case StorageType.Money:
                        return typeof(SqlMoney);
                    case StorageType.Single:
                        return typeof(SqlSingle);
                    case StorageType.String:
                        return typeof(SqlString);
                    case StorageType.SqlCachedBuffer:
                        return typeof(SqlString);
                    case StorageType.SqlBinary:
                        return typeof(object);
                    case StorageType.SqlGuid:
                        return typeof(SqlGuid);
                    case StorageType.SqlXml:
                        return typeof(SqlXml);
                    case StorageType.Json:
                        return typeof(SqlJson);
                        // Time Date DateTime2 and DateTimeOffset have no direct Sql type to contain them
                }
            }
            else
            { //Is CLR Type
                switch (_type)
                {
                    case StorageType.Empty:
                        return null;
                    case StorageType.Boolean:
                        return typeof(bool);
                    case StorageType.Byte:
                        return typeof(byte);
                    case StorageType.DateTime:
                        return typeof(DateTime);
                    case StorageType.Decimal:
                        return typeof(decimal);
                    case StorageType.Double:
                        return typeof(double);
                    case StorageType.Int16:
                        return typeof(short);
                    case StorageType.Int32:
                        return typeof(int);
                    case StorageType.Int64:
                        return typeof(long);
                    case StorageType.Guid:
                        return typeof(Guid);
                    case StorageType.Money:
                        return typeof(decimal);
                    case StorageType.Single:
                        return typeof(float);
                    case StorageType.String:
                        return typeof(string);
                    case StorageType.SqlBinary:
                        return typeof(byte[]);
                    case StorageType.SqlCachedBuffer:
                        return typeof(string);
                    case StorageType.SqlGuid:
                        return typeof(Guid);
                    case StorageType.SqlXml:
                        return typeof(string);
                    case StorageType.Date:
                        return typeof(DateTime);
                    case StorageType.DateTime2:
                        return typeof(DateTime);
                    case StorageType.DateTimeOffset:
                        return typeof(DateTimeOffset);
                    case StorageType.Json:
                        return typeof(string);
#if NET
                    case StorageType.Time:
                        return typeof(TimeOnly);
#endif
                }
            }

            return null; // need to return the value as an object of some CLS type            
        }

        internal static SqlBuffer[] CreateBufferArray(int length)
        {
            SqlBuffer[] buffers = new SqlBuffer[length];
            for (int i = 0; i < buffers.Length; ++i)
            {
                buffers[i] = new SqlBuffer();
            }
            return buffers;
        }

        internal static SqlBuffer[] CloneBufferArray(SqlBuffer[] values)
        {
            SqlBuffer[] copy = new SqlBuffer[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                copy[i] = new SqlBuffer(values[i]);
            }
            return copy;
        }

        internal static void Clear(SqlBuffer[] values)
        {
            if (values != null)
            {
                for (int i = 0; i < values.Length; ++i)
                {
                    values[i].Clear();
                }
            }
        }

        internal void Clear()
        {
            IsNull = false;
            _type = StorageType.Empty;
            _object = null;
        }

        #if NETFRAMEWORK
        internal void SetToDate(DateTime date)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.Date;
            _value._int32 = date.Subtract(DateTime.MinValue).Days;
            IsNull = false;
        }
        #endif

        internal void SetToDateTime(int daypart, int timepart)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            _value._dateTimeInfo.DayPart = daypart;
            _value._dateTimeInfo.TimePart = timepart;
            _type = StorageType.DateTime;
            IsNull = false;
        }
        
        #if NETFRAMEWORK
        internal void SetToDateTime2(DateTime dateTime, byte scale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.DateTime2;
            _value._dateTime2Info._timeInfo._ticks = dateTime.TimeOfDay.Ticks;
            _value._dateTime2Info._timeInfo._scale = scale;
            _value._dateTime2Info._date = dateTime.Subtract(DateTime.MinValue).Days;
            IsNull = false;
        }
        #endif

        internal void SetToDecimal(byte precision, byte scale, bool positive, int[] bits)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            _value._numericInfo._precision = precision;
            _value._numericInfo._scale = scale;
            _value._numericInfo._positive = positive;
            _value._numericInfo._data1 = bits[0];
            _value._numericInfo._data2 = bits[1];
            _value._numericInfo._data3 = bits[2];
            _value._numericInfo._data4 = bits[3];
            _type = StorageType.Decimal;
            IsNull = false;
        }

        internal void SetToMoney(long value)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            _value._int64 = value;
            _type = StorageType.Money;
            IsNull = false;
        }

        internal void SetToNullOfType(StorageType storageType)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            _type = storageType;
            IsNull = true;
            _object = null;
        }

        internal void SetToString(string value)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            _object = value;
            _type = StorageType.String;
            IsNull = false;
        }

        internal void SetToJson(string value)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            _object = value;
            _type = StorageType.Json;
            IsNull = false;
        }

        internal void SetToDate(ReadOnlySpan<byte> bytes)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.Date;
            _value._int32 = GetDateFromByteArray(bytes);
            IsNull = false;
        }

        internal void SetToTime(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.Time;
            FillInTimeInfo(ref _value._timeInfo, bytes, scale, denormalizedScale);
            IsNull = false;
        }

        internal void SetToTime(TimeSpan timeSpan, byte scale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.Time;
            _value._timeInfo._ticks = timeSpan.Ticks;
            _value._timeInfo._scale = scale;
            IsNull = false;
        }

        internal void SetToDateTime2(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            int length = bytes.Length;
            _type = StorageType.DateTime2;
            FillInTimeInfo(ref _value._dateTime2Info._timeInfo, bytes.Slice(0, length - 3), scale, denormalizedScale); // remaining 3 bytes is for date
            _value._dateTime2Info._date = GetDateFromByteArray(bytes.Slice(length - 3)); // 3 bytes for date
            IsNull = false;
        }

        internal void SetToDateTimeOffset(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");
            int length = bytes.Length;
            _type = StorageType.DateTimeOffset;
            FillInTimeInfo(ref _value._dateTimeOffsetInfo._dateTime2Info._timeInfo, bytes.Slice(0, length - 5), scale, denormalizedScale); // remaining 5 bytes are for date and offset
            _value._dateTimeOffsetInfo._dateTime2Info._date = GetDateFromByteArray(bytes.Slice(length - 5)); // 3 bytes for date
            _value._dateTimeOffsetInfo._offset = (short)(bytes[length - 2] + (bytes[length - 1] << 8)); // 2 bytes for offset (Int16)
            IsNull = false;
        }

        internal void SetToDateTimeOffset(DateTimeOffset dateTimeOffset, byte scale)
        {
            Debug.Assert(IsEmpty, "setting value a second time?");

            _type = StorageType.DateTimeOffset;
            DateTime utcDateTime = dateTimeOffset.UtcDateTime; // timeInfo stores the utc datetime of a datatimeoffset
            _value._dateTimeOffsetInfo._dateTime2Info._timeInfo._ticks = utcDateTime.TimeOfDay.Ticks;
            _value._dateTimeOffsetInfo._dateTime2Info._timeInfo._scale = scale;
            _value._dateTimeOffsetInfo._dateTime2Info._date = utcDateTime.Subtract(DateTime.MinValue).Days;
            _value._dateTimeOffsetInfo._offset = (short)dateTimeOffset.Offset.TotalMinutes;
            IsNull = false;
        }

        private static void FillInTimeInfo(ref TimeInfo timeInfo, ReadOnlySpan<byte> timeBytes, byte scale, byte denormalizedScale)
        {
            int length = timeBytes.Length;
            Debug.Assert(3 <= length && length <= 5, "invalid data length for timeInfo: " + length);
            Debug.Assert(0 <= scale && scale <= 7, "invalid scale: " + scale);
            Debug.Assert(0 <= denormalizedScale && denormalizedScale <= 7, "invalid denormalized scale: " + denormalizedScale);

            long tickUnits = timeBytes[0] + ((long)timeBytes[1] << 8) + ((long)timeBytes[2] << 16);
            if (length > 3)
            {
                tickUnits += ((long)timeBytes[3] << 24);
            }
            if (length > 4)
            {
                tickUnits += ((long)timeBytes[4] << 32);
            }
            timeInfo._ticks = tickUnits * TdsEnums.TICKS_FROM_SCALE[scale];

            // Once the deserialization has been completed using the value scale, we need to set the actual denormalized scale, 
            // coming from the data type, on the original result, so that it has the proper scale setting.
            // This only applies for values that got serialized/deserialized for encryption. Otherwise, both scales should be equal.
            timeInfo._scale = denormalizedScale;
        }

        private static int GetDateFromByteArray(ReadOnlySpan<byte> buf)
        {
            byte thirdByte = buf[2]; // reordered to optimize JIT generated bounds checks to a single instance, review generated asm before changing
            return buf[0] + (buf[1] << 8) + (thirdByte << 16);
        }

        private void ThrowIfNull()
        {
            if (IsNull)
            {
                throw new SqlNullValueException();
            }
        }
        // [Field]As<T> method explanation:
        // these methods are used to bridge generic to non-generic access to value type fields on the storage struct
        // where typeof(T) == typeof(field) 
        //   1) RyuJIT will recognize the pattern of (T)(object)T as being redundant and eliminate 
        //   the T and object casts leaving T, so while this looks like it will put every value type instance in a box the 
        //   generated assembly will be short and direct
        //   2) another jit may not recognize the pattern and should emit the code as seen. this will box and then unbox the
        //   value type which is no worse than the mechanism that this code replaces
        // where typeof(T) != typeof(field)
        //   the jit will emit all the cast operations as written. this will put the value into a box and then attempt to
        //   cast it, because it is an object no conversions are used and this will generate the desired InvalidCastException       
        //   for example users cannot widen a short to an int preserving external expectations 

        internal T ByteAs<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._byte;
        }

        internal T BooleanAs<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._boolean;
        }

        internal T Int32As<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._int32;
        }

        internal T Int16As<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._int16;
        }

        internal T Int64As<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._int64;
        }

        internal T DoubleAs<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._double;
        }

        internal T SingleAs<T>()
        {
            ThrowIfNull();
            return (T)(object)_value._single;
        }
        
        #region Private Helpers
        
        private T GetValue<T>(StorageType storageType, T value)
        {
            ThrowIfNull();

            return _type == storageType
                ? value
                : (T)Value; // Types we cannot directly convert to (ie, everything except for
                            // `storageType`) will need to converted via boxing.
        }

        private void SetObject<T>(StorageType storageType, T value)
            where T : INullable
        {
            Debug.Assert(IsEmpty, "Value is being set a second time.");

            _type = storageType;
            _object = value;
            IsNull = value.IsNull;
        }
        
        private void SetValue<T>(StorageType storageType, ref T valueField, T value)
        {
            Debug.Assert(IsEmpty, "Value is being set a second time.");

            _type = storageType;
            valueField = value;
            IsNull = false;
        }
        
        #endregion
        
        #region Private Structs
        
        /// <summary>
        /// Used to store DateTime information.
        /// </summary>
        private struct DateTimeInfo
        {
            /// <summary>
            /// Number of days since 1900-00-00 MINUS 53690.
            /// </summary>
            internal int DayPart { get; set; }
            
            /// <summary>
            /// Number of SQL ticks since 00:00:00.
            /// Note: this is not the same as CLR ticks.
            /// </summary>
            internal int TimePart { get; set; }

            /// <summary>
            /// Generates a new DateTime object from the SQL DATETIME information.
            /// </summary>
            internal DateTime ToDateTime()
            {
                // SQL DATETIME is represented as two integers, the number of days since 1900-01-01
                // and the number of (SQL) ticks since 00:00:00.
                
                // Values that come from SqlDateTime
                const double SqlTicksPerMillisecond = 0.3;
                const int SqlTicksPerSecond = 300;
                const int SqlTicksPerMinute = SqlTicksPerSecond * 60;
                const int SqlTicksPerHour = SqlTicksPerMinute * 60;
                const int SqlTicksPerDay = SqlTicksPerHour * 24;
                
                // This is added to date to bring negative days up to 0.
                const uint MinDays = 53690;

                // 9999-12-31 (max date) is this many days after 1900-01-01 (min date)
                const uint MaxDays = 2958463;
                const uint OffsetMaxDays = MaxDays + MinDays;

                // Maximum time that can be stored in a DateTime (ie, 23:59:59.997) 
                const uint MaxTicks = SqlTicksPerDay - 1;

                // Number of ticks to add to a new DateTime to get to 1900-01-01
                const long BaseDateTicks = 599266080000000000L;
                
                // 1) Check boundaries
                //    a) Days must be:               min_days < days < max_days
                //       Which can be simplified to: 0 < days+min_days < max_days+min_days
                //       If days+min_days is still negative, casting to uint will cause it to
                //       overflow, simplifying to:   uint(days+min_days) < max_days+min_days
                //   b) Time must be:                0 < time < max_time
                //      If time is negative, casting to uint will cause it to overflow, simplifying
                //      to:                          uint(time) < max_time
                if ((uint)(DayPart + MinDays) > OffsetMaxDays ||
                    (uint)TimePart > MaxTicks)
                {
                    throw SQL.DateTimeOverflow();
                }
                
                // 2) Calculate (CLR) ticks in the days
                long dayTicks = DayPart * TimeSpan.TicksPerDay;
                
                // 3) Calculate (CLR) ticks in time part
                double timeInMilliseconds = (TimePart / SqlTicksPerMillisecond) + 0.5;
                long timeTicks = (long)timeInMilliseconds * TimeSpan.TicksPerMillisecond;
                
                // 4) Combine ticks and generate DateTime object
                long totalTicks = BaseDateTicks + dayTicks + timeTicks;
                return new DateTime(totalTicks);
            }
        }
        
        #endregion
    }
}
