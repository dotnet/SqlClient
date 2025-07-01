// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;
using Microsoft.Data.SqlClient;

#if NETFRAMEWORK
using System.Reflection;
using System.Runtime.InteropServices;
#endif

namespace Microsoft.Data.SqlTypes
{
    /// <summary>
    /// This type provides workarounds for the separation between System.Data.Common
    /// and Microsoft.Data.SqlClient.  The latter wants to access internal members of the former, and
    /// this class provides ways to do that.  We must review and update this implementation any time the
    /// implementation of the corresponding types in System.Data.Common change.
    /// </summary>
    internal static class SqlTypeWorkarounds
    {
        #region Work around inability to access SqlXml.CreateSqlXmlReader
        
        private static readonly XmlReaderSettings s_defaultXmlReaderSettings = new() { ConformanceLevel = ConformanceLevel.Fragment };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsCloseInput = new() { ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsAsyncCloseInput = new() { Async = true, ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };

        internal const SqlCompareOptions SqlStringValidSqlCompareOptionMask =
            SqlCompareOptions.BinarySort |
            SqlCompareOptions.BinarySort2 |
            SqlCompareOptions.IgnoreCase |
            SqlCompareOptions.IgnoreWidth |
            SqlCompareOptions.IgnoreNonSpace |
            SqlCompareOptions.IgnoreKanaType;

        internal static XmlReader SqlXmlCreateSqlXmlReader(Stream stream, bool closeInput, bool async)
        {
            Debug.Assert(closeInput || !async, "Currently we do not have pre-created settings for !closeInput+async");

            XmlReaderSettings settingsToUse = closeInput
                ? async 
                    ? s_defaultXmlReaderSettingsAsyncCloseInput
                    : s_defaultXmlReaderSettingsCloseInput
                : s_defaultXmlReaderSettings;

            return XmlReader.Create(stream, settingsToUse);
        }
        
        #endregion

        #region Work around inability to access SqlDateTime.ToDateTime
        internal static DateTime SqlDateTimeToDateTime(int daypart, int timepart)
        {
            // Values need to match those from SqlDateTime
            const double SQLTicksPerMillisecond = 0.3;
            const int SQLTicksPerSecond = 300;
            const int SQLTicksPerMinute = SQLTicksPerSecond * 60;
            const int SQLTicksPerHour = SQLTicksPerMinute * 60;
            const int SQLTicksPerDay = SQLTicksPerHour * 24;
            //const int MinDay = -53690;                // Jan 1 1753
            const uint MinDayOffset = 53690;            // postive value of MinDay used to pull negative values up to 0 so a single check can be used
            const uint MaxDay = 2958463;               // Dec 31 9999 is this many days from Jan 1 1900
            const uint MaxTime = SQLTicksPerDay - 1; // = 25919999,  11:59:59:997PM
            const long BaseDateTicks = 599266080000000000L;//new DateTime(1900, 1, 1).Ticks;

            // casting to uint wraps negative values to large positive ones above the valid 
            // ranges so the lower bound doesn't need to be checked
            if ((uint)(daypart + MinDayOffset) > (MaxDay + MinDayOffset) || (uint)timepart > MaxTime)
            {
                ThrowOverflowException();
            }

            long dayticks = daypart * TimeSpan.TicksPerDay;
            double timePartPerMs = timepart / SQLTicksPerMillisecond;
            timePartPerMs += 0.5;
            long timeTicks = ((long)timePartPerMs) * TimeSpan.TicksPerMillisecond;
            long totalTicks = BaseDateTicks + dayticks + timeTicks;
            return new DateTime(totalTicks);
        }

        // this method is split out of SqlDateTimeToDateTime for performance reasons
        // it is faster to make a method call than it is to incorporate the asm for this
        // method in the calling method.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Exception ThrowOverflowException() => throw SQL.DateTimeOverflow();

        #endregion
        
        #if NETFRAMEWORK
        
        #region Work around inability to access `new SqlBinary(byte[], bool)`

        private static readonly Func<byte[], SqlBinary> ByteArrayToSqlBinaryFactory =
            CreateFactory<SqlBinary, byte[], bool>(value => new SqlBinary(value));

        internal static SqlBinary ByteArrayToSqlBinary(byte[] value) =>
            ByteArrayToSqlBinaryFactory(value);
        
        #endregion
        
        #region Work around inability to access SqlDecimal internal representation

        /// <summary>
        /// Implementation that mimics netcore's WriteTdsValue method.
        /// </summary>
        /// <remarks>
        /// Although calls to this method could just be replaced with calls to
        /// <see cref="SqlDecimal.Data"/>, using this mimic method allows netfx and netcore
        /// implementations to be more cleanly switched.
        /// </remarks>
        /// <param name="value">SqlDecimal value to get data from.</param>
        /// <param name="outSpan">Span to write data to.</param>
        internal static void SqlDecimalWriteTdsValue(SqlDecimal value, Span<uint> outSpan)
        {
            Debug.Assert(outSpan.Length == 4, "Output span must be 4 elements long.");
            
            int[] data = value.Data;
            outSpan[0] = (uint)data[0];
            outSpan[1] = (uint)data[1];
            outSpan[2] = (uint)data[2];
            outSpan[3] = (uint)data[3];
        }
        
        #endregion
        
        #region Work around inability to access `new SqlGuid(byte[], bool)`

        private static readonly Func<byte[], SqlGuid> ByteArrayToSqlGuidFactory =
            CreateFactory<SqlGuid, byte[], bool>(value => new SqlGuid(value));

        internal static SqlGuid ByteArrayToSqlGuid(byte[] value) =>
            ByteArrayToSqlGuidFactory(value);
        
        #endregion
        
        #region Work around inability to access `new SqlMoney(long, int)` and internal representation

        private static readonly Func<long, SqlMoney> LongToSqlMoneyFactory =
            CreateFactory<SqlMoney, long, int>(value => new SqlMoney((decimal)value / 10000));

        private static readonly Func<SqlMoney, long> SqlMoneyToLongFactory =
            CreateSqlMoneyToLongFactory();

        /// <summary>
        /// Constructs a SqlMoney from a long value without scaling.
        /// </summary>
        /// <param name="value">Internal representation of SqlMoney value.</param>
        internal static SqlMoney LongToSqlMoney(long value) =>
            LongToSqlMoneyFactory(value);

        /// <summary>
        /// Deconstructs a SqlMoney into a long value with scaling.
        /// </summary>
        /// <param name="value">SqlMoney value</param>
        internal static long SqlMoneyToLong(SqlMoney value) =>
            SqlMoneyToLongFactory(value);

        private static unsafe Func<SqlMoney, long> CreateSqlMoneyToLongFactory()
        {
            try
            {
                // Look up the offsets in SqlMoney for the internal representation member
                int? valueOffset = GetFieldOffset<SqlMoney, long>("m_value");
                
                if (valueOffset is not null)
                {
                    // Get the address of the value and read the field directly from memory
                    // Note: Just a reminder since we don't mess with pointers often in C#, the
                    //    address of value is being cast to a byte* b/c pointer arithmetic adds/
                    //    subtracts sizeof(ptrType) * offset. Since we want to increment by single
                    //    bytes, we use byte*.
                    var func = (SqlMoney value) =>
                    {
                        // Note: An older version of this workaround called into
                        //     ToSqlInternalRepresentation which would throw an exception on null
                        //     SqlMoney. This check maintains the behavior.
                        if (value.IsNull)
                        {
                            throw new SqlNullValueException();
                        }

                        byte* bytePtr = (byte*)&value;
                        return *(long*)(bytePtr + valueOffset.Value);
                    };
                    
                    // Force JIT compilation of the function
                    func(SqlMoney.Zero);

                    return func;
                }
            }
            catch
            {
                // Reflection failed, fall through to using conversion via decimal
            }
            
            // @TODO: SqlMoney.ToSqlInternalRepresentation will throw on SqlMoney.IsNull, the fallback will not.
            SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.CreateSqlMoneyToLongFactory | Info | SqlMoney.ToInternalRepresentation(SqlMoney) not found. Less efficient fallback method will be used.");
            return value => value.IsNull ? 0 : (long)(value.ToDecimal() * 10000);
        }
            
        #endregion

        private static unsafe Func<TValue, TInstance> CreateFactory<TInstance, TValue, TIgnored>(
            Func<TValue, TInstance> fallbackFactory)
            where TInstance : struct
        {
            // The logic of this method is that there are special internal methods that can create
            // Sql* types without the need for copying. These methods are internal to System.Data,
            // so we cannot access them, even they are so much faster. To get around this, we
            // take a small perf hit to discover them via reflection in exchange for the faster
            // perf. If reflection fails, we fall back and use the publicly available ctor, but
            // it will be much slower.
            // The TIgnored type is an extra argument to the ctor that differentiates this internal
            // ctor from the public ctor.
            
            try
            {
                // Look for TInstance constructor that takes TValue, TIgnored
                ConstructorInfo ctor = typeof(TInstance).GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    types: new[] { typeof(TValue), typeof(TIgnored) },
                    modifiers: null);

                if (ctor is not null)
                {
                    // Use function pointer for maximum performance on repeated calls.
                    // This avoids delegate allocation overhead and is nearly as fast as direct
                    // calls to the constructor
                    IntPtr fnPtr;

                    TInstance FastFactory(TValue value)
                    {
                        TInstance result = default;
                        ((delegate* managed<ref TInstance, TValue, TIgnored, void>)fnPtr)(
                            ref result,
                            value,
                            default /*ignored*/);
                        return result;
                    }

                    // Force JIT compilation with a dummy function pointer first
                    static void DummyNoOp(ref TInstance @this, TValue value, TIgnored ignored) { }
                    fnPtr = (IntPtr)(delegate* managed<ref TInstance, TValue, TIgnored, void>)(&DummyNoOp);
                    FastFactory(default);

                    // Replace with real constructor function pointer
                    fnPtr = ctor.MethodHandle.GetFunctionPointer();
                    return FastFactory;
                }
            }
            catch
            {
                // Reflection failed, fall through to use the slow conversion.
            }
            
            // If reflection failed, or the ctor couldn't be found, fallback to construction using
            // the fallback factory. This will be much slower, but ensures conversion can still
            // happen.
            SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.CreateFactory | Info | {0}..ctor({1}, {2}) not found. Less efficient fallback method will be used.", typeof(TInstance).Name, typeof(TValue).Name, typeof(TIgnored).Name);
            return fallbackFactory;
        }
        
        private static int? GetFieldOffset<TInstance, TValue>(string fieldName)
            where TInstance : struct
        {
            FieldInfo field = typeof(TInstance).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            return field is not null && field.FieldType == typeof(TValue)
                ? (int)Marshal.OffsetOf<TInstance>(fieldName)
                : null;
        }
        
        #endif
    }
}
