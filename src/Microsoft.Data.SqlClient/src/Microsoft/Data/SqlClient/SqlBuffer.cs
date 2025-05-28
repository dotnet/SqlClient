// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlTypes;

namespace Microsoft.Data.SqlClient
{
    internal sealed class SqlBuffer
    {
        #region Constants
        // These variables store pre-boxed bool values to be used when returning a boolean as an
        // object. If these are not used a new value is boxed each time one is needed which leads
        // to a lot of garbage which needs to be collected
        private static readonly object True = true;
        private static readonly object False = false;
        
        // These formats work with DateTime stricts
        private static readonly string[] Sql2008DateTime2Formats = new[] {
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.f",
            "yyyy-MM-dd HH:mm:ss.ff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss.ffff",
            "yyyy-MM-dd HH:mm:ss.fffff",
            "yyyy-MM-dd HH:mm:ss.ffffff",
            "yyyy-MM-dd HH:mm:ss.fffffff",
        };
        
        // These formats work with DateTimeOffset structs
        private static readonly string[] Sql2008DateTimeOffsetFormats = new[] {
            "yyyy-MM-dd HH:mm:ss zzz",
            "yyyy-MM-dd HH:mm:ss.f zzz",
            "yyyy-MM-dd HH:mm:ss.ff zzz",
            "yyyy-MM-dd HH:mm:ss.fff zzz",
            "yyyy-MM-dd HH:mm:ss.ffff zzz",
            "yyyy-MM-dd HH:mm:ss.fffff zzz",
            "yyyy-MM-dd HH:mm:ss.ffffff zzz",
            "yyyy-MM-dd HH:mm:ss.fffffff zzz",
        };
        
        // These formats only work with TimeSpan structs
        private static readonly string[] Sql2008TimeFormats = new string[] {
            @"hh\:mm\:ss",
            @"hh\:mm\:ss\.f",
            @"hh\:mm\:ss\.ff",
            @"hh\:mm\:ss\.fff",
            @"hh\:mm\:ss\.ffff",
            @"hh\:mm\:ss\.fffff",
            @"hh\:mm\:ss\.ffffff",
            @"hh\:mm\:ss\.fffffff",
        };
        
        #endregion
        
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
            Vector,
        }

        internal struct TimeInfo
        {
            internal long _ticks;
            internal byte _scale;

            internal void FromByteArray(ReadOnlySpan<byte> timeBytes, byte scale, byte denormalizedScale)
            {
                // Prefetch the length for guaranteed performance.
                int length = timeBytes.Length;
                
                Debug.Assert(length >= 3 && length <= 5, "typeBytes must have 3-5 items in it.");
                Debug.Assert(scale <= 7, "scale must be less than 8");
                Debug.Assert(denormalizedScale <= 7, "denormalizedScale mut be less than 8");

                // Deserialize the timeBytes into a long
                // Note: we cannot use binary primitives here since timeBytes is variable length
                //    and will never be 8 bytes.
                long tickUnits = 0;
                for (int i = 0; i < length; i++)
                {
                    tickUnits += (long)timeBytes[i] << (8 * i);
                }
                
                // Calculate true ticks from deserialized value and scale
                _ticks = tickUnits * TdsEnums.TICKS_FROM_SCALE[scale];
                
                // Once the deserialization has been completed using the value scale, we need to
                // set the actual denormalized scale, coming from the data type, on the original
                // result, so that it has the proper scale setting. This only applies for values
                // that got serialized/deserialized for encryption. Otherwise, both scales should
                // be equal.
                _scale = denormalizedScale;
            }
            
            internal void FromTimeSpanAndScale(TimeSpan timeSpan, byte scale)
            {
                _ticks = timeSpan.Ticks;
                _scale = scale;
            }
        }

        internal struct VectorInfo
        {
            internal int _elementCount;
            internal byte _elementType;
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
            [FieldOffset(0)]
            internal VectorInfo _vectorInfo;
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

        internal Type ClrType
        {
            get => _type switch
            {
                StorageType.Boolean         => typeof(bool),
                StorageType.Byte            => typeof(byte),
                StorageType.DateTime        => typeof(DateTime),
                StorageType.DateTime2       => typeof(DateTime),
                StorageType.DateTimeOffset  => typeof(DateTimeOffset),
                StorageType.Decimal         => typeof(decimal),
                StorageType.Double          => typeof(double),
                StorageType.Empty           => null,
                StorageType.Guid            => typeof(Guid),
                StorageType.Int16           => typeof(short),
                StorageType.Int32           => typeof(int),
                StorageType.Int64           => typeof(long),
                StorageType.Json            => typeof(string),
                StorageType.Money           => typeof(double),
                StorageType.Single          => typeof(float),
                StorageType.SqlBinary       => typeof(byte[]),
                StorageType.SqlCachedBuffer => typeof(string),
                StorageType.SqlGuid         => typeof(Guid),
                StorageType.SqlXml          => typeof(string),
                StorageType.String          => typeof(string),
                StorageType.Vector          => typeof(byte[]),
                
                // @TODO: IF this property is meant to return the type that you'll get back when
                //    calling Value, then this isn't correct.
                #if NET
                StorageType.Time            => typeof(TimeSpan),
                #endif
                
                _                           => null
            };
        }
        
        internal bool IsEmpty => _type == StorageType.Empty;

        internal bool IsNull { get; private set; }

        internal Type SqlType
        {
            get => _type switch
            {
                StorageType.Boolean         => typeof(SqlBoolean),
                StorageType.Byte            => typeof(SqlByte),
                StorageType.DateTime        => typeof(SqlDateTime),
                StorageType.Decimal         => typeof(SqlDecimal),
                StorageType.Double          => typeof(SqlDouble),
                StorageType.Empty           => null,
                StorageType.Guid            => typeof(SqlGuid),
                StorageType.Int16           => typeof(SqlInt16),
                StorageType.Int32           => typeof(SqlInt32),
                StorageType.Int64           => typeof(SqlInt64),
                StorageType.Json            => typeof(SqlJson),
                StorageType.Money           => typeof(SqlMoney),
                StorageType.Single          => typeof(SqlSingle),
                StorageType.SqlBinary       => typeof(object),
                StorageType.SqlCachedBuffer => typeof(SqlString),
                StorageType.SqlGuid         => typeof(SqlGuid),
                StorageType.SqlXml          => typeof(SqlXml),
                StorageType.String          => typeof(SqlString),
                StorageType.Vector          => typeof(object),
                _                           => null
            };
        }
        
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

        internal byte[] ByteArray
        {
            get
            {
                if (_type is not StorageType.Vector)
                {
                    // Must be checked here because SqlBinary allows null.
                    ThrowIfNull();
                }

                return SqlBinary.Value;
            }
        }
        
        #if NET
        internal DateOnly DateOnly
        {
            get
            {
                ThrowIfNull();
                return _type == StorageType.Date
                    ? DateOnly.MinValue.AddDays(_value._int32)
                    : (DateOnly)Value;
            }
        }
        #endif
        
        internal DateTime DateTime
        {
            get
            {
                ThrowIfNull();
                return _type switch
                {
                    StorageType.Date => DateTime.MinValue.AddDays(_value._int32),
                    StorageType.DateTime => _value._dateTimeInfo.ToDateTime(),
                    StorageType.DateTime2 => _value._dateTime2Info.ToDateTime(),
                    _ => (DateTime)Value,
                };
            }
        }

        internal DateTimeOffset DateTimeOffset
        {
            get
            {
                ThrowIfNull();
                return _type == StorageType.DateTimeOffset
                    ? _value._dateTimeOffsetInfo.ToDateTimeOffset()
                    : (DateTimeOffset)Value;
            }
        }

        internal decimal Decimal
        {
            get
            {
                ThrowIfNull();
                return _type switch
                {
                    StorageType.Decimal => _value._numericInfo.ToDecimal(),
                    StorageType.Money => GetSqlMoneyFromLong(_value._int64).ToDecimal(),
                    _ => (decimal)Value,
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
        
        internal SqlString Sql2008DateTimeSqlString
        {
            get => _type is StorageType.Date or StorageType.DateTime2 or StorageType.DateTimeOffset or StorageType.Time
                ? IsNull ? SqlString.Null : new SqlString(Sql2008DateTimeString)
                : (SqlString)SqlValue;
        }
        
        internal string Sql2008DateTimeString
        {
            get
            {
                ThrowIfNull();

                string formatString;
                switch (_type)
                {
                    case StorageType.Date:
                        formatString = "yyyy-MM-dd";
                        return DateTime.ToString(formatString, DateTimeFormatInfo.InvariantInfo);
                    case StorageType.DateTime2:
                        formatString = Sql2008DateTime2Formats[_value._dateTime2Info._timeInfo._scale];
                        return DateTime.ToString(formatString, DateTimeFormatInfo.InvariantInfo);
                    case StorageType.DateTimeOffset:
                        formatString = Sql2008DateTimeOffsetFormats[_value._dateTimeOffsetInfo._dateTime2Info._timeInfo._scale];
                        return DateTimeOffset.ToString(formatString, DateTimeFormatInfo.InvariantInfo);
                    case StorageType.Time:
                        formatString = Sql2008TimeFormats[_value._timeInfo._scale];
                        return Time.ToString(formatString, DateTimeFormatInfo.InvariantInfo);
                    default:
                        return (string)Value;
                }
            }
        }
        
        internal SqlBinary SqlBinary
        {
            get => _type is StorageType.SqlBinary or StorageType.Vector
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
            get => _type == StorageType.DateTime
                ? IsNull ? SqlDateTime.Null : _value._dateTimeInfo.ToSqlDateTime()
                : (SqlDateTime)SqlValue;
        }

        internal SqlDecimal SqlDecimal
        {
            get => _type == StorageType.Decimal
                ? IsNull ? SqlDecimal.Null : _value._numericInfo.ToSqlDecimal()
                : (SqlDecimal)SqlValue;
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

        internal SqlMoney SqlMoney
        {
            get => _type == StorageType.Money
                ? IsNull ? SqlMoney.Null : GetSqlMoneyFromLong(_value._int64)
                : (SqlMoney)SqlValue;
        }
        
        internal SqlSingle SqlSingle
        {
            get => _type == StorageType.Single
                ? IsNull ? SqlSingle.Null : new SqlSingle(_value._single)
                : (SqlSingle)SqlValue;
        }

        internal SqlString SqlString
        {
            get => _type switch
            {
                StorageType.Json => IsNull ? SqlString.Null : new SqlString((string)_object),
                StorageType.String => IsNull ? SqlString.Null : new SqlString((string)_object),
                StorageType.SqlCachedBuffer => IsNull ? SqlString.Null : ((SqlCachedBuffer)_object).ToSqlString(),
                StorageType.Vector => IsNull ? SqlString.Null : new SqlString(GetSqlVector().GetString()),
                _ => (SqlString)SqlValue
            };
        }

        internal object SqlValue
        {
            get => _type switch
            {
                StorageType.Boolean         => SqlBoolean,
                StorageType.Byte            => SqlByte,
                StorageType.Date            => IsNull ? DBNull.Value : DateTime,
                StorageType.DateTime        => SqlDateTime,
                StorageType.DateTime2       => IsNull ? DBNull.Value : DateTime,
                StorageType.DateTimeOffset  => IsNull ? DBNull.Value : DateTimeOffset,
                StorageType.Decimal         => SqlDecimal,
                StorageType.Double          => SqlDouble,
                StorageType.Empty           => DBNull.Value,
                StorageType.Guid            => SqlGuid,
                StorageType.Int16           => SqlInt16,
                StorageType.Int32           => SqlInt32,
                StorageType.Int64           => SqlInt64,
                StorageType.Json            => SqlJson,
                StorageType.Money           => SqlMoney,
                StorageType.Single          => SqlSingle,
                StorageType.SqlBinary       => _object,
                StorageType.SqlCachedBuffer => IsNull ? SqlXml.Null : ((SqlCachedBuffer)_object).ToSqlXml(),
                StorageType.SqlGuid         => _object,
                StorageType.SqlXml          => IsNull ? SqlXml.Null : (SqlXml)_object,
                StorageType.String          => SqlString,
                StorageType.Time            => IsNull ? DBNull.Value : Time,
                StorageType.Vector          => GetSqlVector(),
                _ => null
            };
        }
        
        internal SqlXml SqlXml
        {
            get => _type == StorageType.SqlXml
                ? IsNull ? SqlXml.Null : (SqlXml)_object
                : (SqlXml)SqlValue;
            set => SetObject(StorageType.SqlXml, value);
        }

        internal string String
        {
            get
            {
                ThrowIfNull();
                return _type switch
                {
                    StorageType.Json => (string)_object,
                    StorageType.String => (string)_object,
                    StorageType.SqlCachedBuffer => ((SqlCachedBuffer)_object).ToString(),
                    StorageType.Vector => GetSqlVector().GetString(),
                    _ => (string)Value
                };
            }
        }

        internal TimeSpan Time
        {
            get
            {
                ThrowIfNull();
                return _type == StorageType.Time
                    ? new TimeSpan(_value._timeInfo._ticks)
                    : (TimeSpan)Value;
            }
        }
        
        #if NET
        internal TimeOnly TimeOnly
        {
            get
            {
                ThrowIfNull();
                return _type == StorageType.Time
                    ? new TimeOnly(_value._timeInfo._ticks)
                    : (TimeOnly)Value;
            }
        }
        #endif
        
        internal object Value
        {
            get => IsNull
                ? DBNull.Value
                : _type switch
                {
                    StorageType.Boolean        => Boolean ? True : False, // Return pre-boxed values for perf
                    StorageType.Byte           => Byte,
                    StorageType.Date           => DateTime,
                    StorageType.DateTime       => DateTime,
                    StorageType.DateTime2      => DateTime,
                    StorageType.DateTimeOffset => DateTimeOffset,
                    StorageType.Decimal        => Decimal,
                    StorageType.Double         => Double,
                    StorageType.Empty          => DBNull.Value,
                    StorageType.Int16          => Int16,
                    StorageType.Int32          => Int32,
                    StorageType.Int64          => Int64,
                    StorageType.Json           => String,
                    StorageType.Guid           => Guid,
                    StorageType.Money          => Decimal,
                    StorageType.Single         => Single,
                    StorageType.SqlBinary      => ByteArray,
                    StorageType.SqlGuid        => Guid,
                    StorageType.String         => String,
                    StorageType.Time           => Time,
                    StorageType.Vector         => ByteArray,
                    
                    // @TODO: Verify that these follow the same pattern as other types
                    //     (ie, ClrType => (cast)Value)
                    // If we have a cached buffer, it's because it's an XMLTYPE column and we have
                    // to return a string when they're asking for the CLR value of the column.
                    StorageType.SqlCachedBuffer => ((SqlCachedBuffer)_object).ToString(),
                    StorageType.SqlXml          => ((SqlXml)_object).Value,
                    
                    _ => null
                };
        }
        
        #endregion

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
        
        internal SqlVector<T> GetSqlVector<T>() where T : unmanaged
        {
            if (_type is StorageType.Vector)
            {
                if (IsNull)
                {
                    return new SqlVector<T>(_value._vectorInfo._elementCount);
                }
                return new SqlVector<T>(SqlBinary.Value);
            }
            return (SqlVector<T>)SqlValue;
        }
        
        internal VectorInfo GetVectorInfo()
        {
            if (_type == StorageType.Vector)
            {
                return _value._vectorInfo;
            }
            throw new InvalidOperationException();
        }

        #region Set Methods
        
        #if NETFRAMEWORK
        internal void SetToDate(DateTime date) =>
            SetValue(StorageType.Date, ref _value._int32, date.Subtract(DateTime.MinValue).Days);
        #endif

        internal void SetToDate(ReadOnlySpan<byte> bytes)
        {
            // NOTE: Reordered to optimize JIT generated bounds checks to a single instance,
            //     review generated asm before changing.
            // @TODO: Verify that ^^^ is still accurate/needed
            byte thirdByte = bytes[2]; // 
            int dateValue = bytes[0] + (bytes[1] << 8) + (thirdByte << 16);
            SetValue(StorageType.Date, ref _value._int32, dateValue);
        }
        
        internal void SetToDateTime(int dayPart, int timePart)
        {
            SetTypeAndIsNull(StorageType.DateTime, false);
            _value._dateTimeInfo.FromDateTimeData(dayPart, timePart);
        }
        
        #if NETFRAMEWORK
        internal void SetToDateTime2(DateTime dateTime, byte scale)
        {
            SetTypeAndIsNull(StorageType.DateTime2, false);
            _value._dateTime2Info.FromDateTimeAndScale(dateTime, scale);
        }
        #endif
        
        internal void SetToDateTime2(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
        {
            SetTypeAndIsNull(StorageType.DateTime2, false);
            _value._dateTime2Info.FromByteArray(bytes, scale, denormalizedScale);
        }
        
        internal void SetToDateTimeOffset(DateTimeOffset dateTimeOffset, byte scale)
        {
            SetTypeAndIsNull(StorageType.DateTimeOffset, false);
            _value._dateTimeOffsetInfo.FromDateTimeOffsetAndScale(dateTimeOffset, scale);
        }
        
        internal void SetToDateTimeOffset(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
        {
            SetTypeAndIsNull(StorageType.DateTimeOffset, false);
            _value._dateTimeOffsetInfo.FromByteArray(bytes, scale, denormalizedScale);
        }

        internal void SetToDecimal(byte precision, byte scale, bool positive, int[] bits)
        {
            SetTypeAndIsNull(StorageType.Decimal, false);
            _value._numericInfo.FromDecimalData(precision, scale, positive, bits);
        }

        internal void SetToJson(string value)
        {
            SetTypeAndIsNull(StorageType.Json, false);
            _object = value;
        }
        
        internal void SetToMoney(long value) =>
            SetValue(StorageType.Money, ref _value._int64, value);

        internal void SetToNullOfType(StorageType storageType)
        {
            SetTypeAndIsNull(storageType, true);
            _object = null;
        }

        internal void SetToString(string value)
        {
            SetTypeAndIsNull(StorageType.String, false);
            _object = value;
        }

        internal void SetToTime(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
        {
            SetTypeAndIsNull(StorageType.Time, false);
            _value._timeInfo.FromByteArray(bytes, scale, denormalizedScale);
        }

        internal void SetToTime(TimeSpan timeSpan, byte scale)
        {
            SetTypeAndIsNull(StorageType.Time, false);
            _value._timeInfo.FromTimeSpanAndScale(timeSpan, scale);
        }
        
        internal void SetToVectorInfo(int elementCount, byte elementType, bool isNull)
        {
            SetTypeAndIsNull(StorageType.Vector, isNull);
            _value._vectorInfo._elementCount = elementCount;
            _value._vectorInfo._elementType = elementType;
        }

        #endregion        

        

        

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
        
        private static SqlMoney GetSqlMoneyFromLong(long value)
        {
            #if NET
            return SqlMoney.FromTdsValue(value);
            #else
            return SqlTypeWorkarounds.SqlMoneyCtor(value, 1);
            #endif
        }

        private ISqlVector GetSqlVector()
        {
            MetaType.SqlVectorElementType elementType = (MetaType.SqlVectorElementType)_value._vectorInfo._elementType;
            switch (elementType)
            {
                case MetaType.SqlVectorElementType.Float32:
                    return GetSqlVector<float>();
                default:
                    throw SQL.VectorTypeNotSupported(elementType.ToString());
            }
        }
        
        private T GetValue<T>(StorageType storageType, T value)
        {
            ThrowIfNull();

            return _type == storageType
                ? value
                : (T)Value; // Types we cannot directly convert to (ie, everything except for
                            // `storageType` will need to converted via boxing.
        }

        private void SetObject<T>(StorageType storageType, T value)
            where T : INullable
        {
            SetTypeAndIsNull(storageType, value.IsNull);
            _object = value;
        }
        
        private void SetValue<T>(StorageType storageType, ref T valueField, T value)
        {
            SetTypeAndIsNull(storageType, false);
            valueField = value;
        }

        private void SetTypeAndIsNull(StorageType storageType, bool isNull)
        {
            Debug.Assert(IsEmpty, "Value is being set a second time.");

            IsNull = isNull;
            _type = storageType;
        }
        
        #endregion
        
        #region Private Structs
        
        /// <summary>
        /// Used to store DATETIME information.
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

            internal void FromDateTimeData(int dayPart, int timePart)
            {
                DayPart = dayPart;
                TimePart = timePart;
            }
            
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

            /// <summary>
            /// Generates a new SqlDateTime object from the SQL DATETIME information.
            /// </summary>
            internal SqlDateTime ToSqlDateTime() => 
                new SqlDateTime(DayPart, TimePart);
        }
        
        /// <summary>
        /// Used to store DATETIME2 information.
        /// </summary>
        private struct DateTime2Info
        {
            // @TODO: Move to properties
            
            /// <summary>
            /// Number of days since 0001-01-01.
            /// </summary>
            internal int _date;
            
            /// <summary>
            /// Time component of the DATETIME2 value.
            /// </summary>
            internal TimeInfo _timeInfo;

            internal void FromByteArray(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
            {
                int length = bytes.Length;

                // Set time from time bytes
                ReadOnlySpan<byte> timeBytes = bytes.Slice(0, length - 3);
                _timeInfo.FromByteArray(timeBytes, scale, denormalizedScale);

                // Set days from day bytes 
                // NOTE: Reordered to optimize JIT generated bounds checks to a single instance,
                //     review generated asm before changing.
                // @TODO: Verify that ^^^ is still accurate/needed
                ReadOnlySpan<byte> dateBytes = bytes.Slice(length - 3);
                byte thirdByte = dateBytes[2];
                _date = dateBytes[0] + (dateBytes[1] << 8) + (thirdByte << 16);
            }
            
            internal void FromDateTimeAndScale(DateTime dateTime, byte scale)
            {
                _date = dateTime.Subtract(DateTime.MinValue).Days;
                _timeInfo.FromTimeSpanAndScale(dateTime.TimeOfDay, scale);
            }
            
            /// <summary>
            /// Generates a new DateTime object from the SQL DATETIME2 information.
            /// </summary>
            internal DateTime ToDateTime()
            {
                long ticks = _date * TimeSpan.TicksPerDay + _timeInfo._ticks;
                return new DateTime(ticks);
            }
        }
        
        /// <summary>
        /// Used to store DATETIMEOFFSET information.
        /// </summary>
        private struct DateTimeOffsetInfo
        {
            // @TODO: Move to properties
            
            /// <summary>
            /// DateTime component of the DATETIMEOFFSET value.
            /// </summary>
            internal DateTime2Info _dateTime2Info;
            
            /// <summary>
            /// Timezone offset component of the DATETIMEOFFSET value, in minutes.
            /// </summary>
            internal short _offset;

            internal void FromByteArray(ReadOnlySpan<byte> bytes, byte scale, byte denormalizedScale)
            {
                int length = bytes.Length;

                // Set DATETIME2 info from bytes
                ReadOnlySpan<byte> dateTime2Bytes = bytes.Slice(0, length - 2);
                _dateTime2Info.FromByteArray(dateTime2Bytes, scale, denormalizedScale);
                
                // Set offset from remaining bytes
                ReadOnlySpan<byte> offsetBytes = bytes.Slice(length - 2);
                _offset = BinaryPrimitives.ReadInt16LittleEndian(offsetBytes);
            }

            internal void FromDateTimeOffsetAndScale(DateTimeOffset dateTimeOffset, byte scale)
            {
                _dateTime2Info.FromDateTimeAndScale(dateTimeOffset.UtcDateTime, scale);
                _offset = (short)dateTimeOffset.Offset.TotalMinutes;
            }
            
            /// <summary>
            /// Generates a new DateTimeOffset object from the SQL DATETIMEOFFSET information.
            /// </summary>
            internal DateTimeOffset ToDateTimeOffset()
            {
                DateTime dateTime = _dateTime2Info.ToDateTime();
                TimeSpan offset = new TimeSpan(0, _offset, 0);
                return new DateTimeOffset(dateTime, offset);
            }
        }
        
        /// <summary>
        /// Used to store DECIMAL/NUMERIC type information.
        /// </summary>
        private struct NumericInfo
        {
            /// <summary>
            /// Low 32 bits of the integer value.
            /// </summary>
            internal int _data1;
            
            /// <summary>
            /// Middle 32 bits of the integer value.
            /// </summary>
            internal int _data2;
            
            /// <summary>
            /// High 32 bits of the integer value.
            /// </summary>
            internal int _data3;
            
            /// <summary>
            /// Extended 32 bits of the integer value.
            /// </summary>
            internal int _data4;
            
            /// <summary>
            /// Prevision of the value (ie, the number of significant digits to retain). Minimum of
            /// 1, maximum of 38.
            /// </summary>
            internal byte _precision;
            
            /// <summary>
            /// Scale to apply to the integer value (ie, the integer will be multiplied by 1/10^x).
            /// </summary>
            internal byte _scale;
            
            /// <summary>
            /// Whether the number is positive or negative.
            /// </summary>
            internal bool _positive;

            internal void FromDecimalData(byte precision, byte scale, bool positive, int[] integerBlocks)
            {
                Debug.Assert(integerBlocks.Length == 4, "Integer blocks must contain low, mid, high, and extended bits");

                _precision = precision;
                _scale = scale;
                _positive = positive;
                _data1 = integerBlocks[0];
                _data2 = integerBlocks[1];
                _data3 = integerBlocks[2];
                _data4 = integerBlocks[3];
            }
            
            internal decimal ToDecimal()
            {
                // SQL DECIMAL/NUMERIC type can store larger numbers than CLR decimal type, if we
                // cannot directly represent the number as a CLR decimal, we can try some tricks to
                // optimize storage before giving up.
                
                // 1) Determine if number can fit within a CLR decimal and directly convert if so.
                if (_data4 == 0 && _scale <= 28)
                {
                    return new decimal(_data1, _data2, _data3, !_positive, _scale);
                }
                
                // 2) Number cannot fit in a CLR decimal, attempt optimization of the value
                if (_scale > 0)
                {
                    // 2.1) Find trailing zeroes and actual precision
                    (int trailingZeroes, int actualPrecision) = FindTrailingZeroesAndPrecision();
                    
                    // 2.2) Calculate minimum scale after removing trailing zeroes
                    int minimumScale = _scale - trailingZeroes;
                    
                    // 2.3) Check if value fits in CLR decimal after optimization
                    if (trailingZeroes > 0 && minimumScale <= 28 && actualPrecision <= 29)
                    {
                        // We can indeed optimize to fit in CLR decimal!
                        // 2.3.1) Calculate target precision for conversion
                        int integerDigits = actualPrecision - minimumScale;
                        int targetPrecision = 29; // Default to maximum precision
                        
                        // 2.3.2) Adjust precision based on integer value size.
                        if (integerDigits != 1 && actualPrecision != 29)
                        {
                            // Integer value is not 1 digit (cannot be zero), and we're not already at
                            // maximum precision. Use 28 for target precision to allow for potential
                            // growth.
                            targetPrecision = 28;
                        }
                        
                        // 2.3.3 Use SqlDecimal to convert to target precision/scale
                        try
                        {
                            int targetScale = targetPrecision - integerDigits;
                            SqlDecimal sqlValue = ToSqlDecimal();
                            return SqlDecimal.ConvertToPrecScale(sqlValue, targetPrecision, targetScale).Value;
                        }
                        catch (OverflowException)
                        {
                            throw new OverflowException(SQLResource.ConversionOverflowMessage);
                        }
                    }
                }
                
                // 3) Optimization was not possible
                throw new OverflowException(SQLResource.ConversionOverflowMessage);
            }
            
            internal SqlDecimal ToSqlDecimal() =>
                new SqlDecimal(_precision, _scale, _positive, _data1, _data2, _data3, _data4);

            private (int trailingZeroes, int valuableDigits) FindTrailingZeroesAndPrecision()
            {
                // Make local copy of the data so we do not modify the internal data.
                Span<uint> integerData = stackalloc uint[] { (uint)_data1, (uint)_data2, (uint)_data3, (uint)_data4 };

                // Repeatedly divide by 10 to determine how many digits are trailing zeroes and how
                // many are non-zero
                int length = 4;          // Number of data blocks that will be used in the calculation
                int trailingZeroes = 0;  // Number of trailing zeroes
                int valuableDigits = 0;  // Digits in the number that are valuable (non-zero)
                uint remainder = 0;      // Remainder after division by 10
                while (length > 1 || integerData[0] != 0)
                {
                    // 1) Divide the number by 10 in-place
                    uint carry = 0;
                    for (int i = length - 1; i >= 0; i--)
                    {
                        ulong accumulator = ((ulong)carry << 32) + integerData[i];
                        integerData[i] = (uint)(accumulator / 10);
                        carry = (uint)(accumulator - integerData[i] * 10);
                    }

                    remainder = carry;
                    
                    // 2) Normalize the multi-precision number, ie, remove the leading zeroes
                    while (length > 1 && integerData[length - 1] == 0)
                    {
                        length--;
                    }
                    
                    // 3) If the working number was divisible by 10, increase trailing zero count.
                    //    Otherwise, increase the number of valuable digits.
                    if (remainder == 0 && valuableDigits == 0)
                    {
                        trailingZeroes++;
                    }
                    else
                    {
                        valuableDigits++;
                    }
                }

                // Handle case where the number divided down to exactly 0. This means all decimal
                // digits are trailing zeroes.
                if (remainder == 0)
                {
                    trailingZeroes = _scale;
                }
                
                // Ensure we account for all decimal places defined by the scale.
                // If we haven't processed enough digits, the remaining are implied leading zeroes,
                // and we need at least one valuable digit to represent the number.
                if (trailingZeroes + valuableDigits <= _scale)
                {
                    valuableDigits = _scale - trailingZeroes + 1;
                }

                return (trailingZeroes, valuableDigits);
            }
        }
        
        #endregion
    }
}
