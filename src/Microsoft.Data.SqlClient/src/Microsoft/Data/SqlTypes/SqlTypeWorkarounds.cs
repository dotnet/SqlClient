// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using Microsoft.Data.SqlClient;

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
        private static readonly XmlReaderSettings s_defaultXmlReaderSettings = new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsCloseInput = new XmlReaderSettings() { ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsAsyncCloseInput = new XmlReaderSettings() { Async = true, ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };

        internal const SqlCompareOptions SqlStringValidSqlCompareOptionMask =
            SqlCompareOptions.IgnoreCase | SqlCompareOptions.IgnoreWidth |
            SqlCompareOptions.IgnoreNonSpace | SqlCompareOptions.IgnoreKanaType |
            SqlCompareOptions.BinarySort | SqlCompareOptions.BinarySort2;

        internal static XmlReader SqlXmlCreateSqlXmlReader(Stream stream, bool closeInput, bool async)
        {
            Debug.Assert(closeInput || !async, "Currently we do not have pre-created settings for !closeInput+async");

            XmlReaderSettings settingsToUse = closeInput ?
                (async ? s_defaultXmlReaderSettingsAsyncCloseInput : s_defaultXmlReaderSettingsCloseInput) :
                s_defaultXmlReaderSettings;

            return XmlReader.Create(stream, settingsToUse);
        }

        internal static XmlReader SqlXmlCreateSqlXmlReader(TextReader textReader, bool closeInput, bool async)
        {
            Debug.Assert(closeInput || !async, "Currently we do not have pre-created settings for !closeInput+async");

            XmlReaderSettings settingsToUse = closeInput ?
               (async ? s_defaultXmlReaderSettingsAsyncCloseInput : s_defaultXmlReaderSettingsCloseInput) :
               s_defaultXmlReaderSettings;

            return XmlReader.Create(textReader, settingsToUse);
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

        #region Work around inability to access SqlMoney.ctor(long, int) and SqlMoney.ToSqlInternalRepresentation
        private static readonly Func<long, SqlMoney> s_sqlMoneyfactory = CtorHelper.CreateFactory<SqlMoney, long, int>(); // binds to SqlMoney..ctor(long, int) if it exists

        /// <summary>
        /// Constructs a SqlMoney from a long value without scaling. The ignored parameter exists
        /// only to distinguish this constructor from the constructor that takes a long.
        /// Used only internally.
        /// </summary>
        internal static SqlMoney SqlMoneyCtor(long value, int ignored)
        {
            SqlMoney val;
            if (s_sqlMoneyfactory is not null)
            {
                val = s_sqlMoneyfactory(value);
            }
            else
            {
                val = new SqlMoney(((decimal)value) / 10000);
            }

            return val;
        }

        internal static long SqlMoneyToSqlInternalRepresentation(SqlMoney money)
        {
            return SqlMoneyHelper.GetSqlMoneyToLong(money);
        }

        internal static class SqlMoneyHelper
        {
            private static readonly MethodInfo s_toSqlInternalRepresentation = GetFastSqlMoneyToLong();

            internal static long GetSqlMoneyToLong(SqlMoney money)
            {
                if (s_toSqlInternalRepresentation is not null)
                {
                    try
                    {
                        return (long)s_toSqlInternalRepresentation.Invoke(money, null);
                    }
                    catch
                    {
                        // If an exception occurs for any reason, swallow & use the fallback code path.
                    }
                }

                return FallbackSqlMoneyToLong(money);
            }

            private static MethodInfo GetFastSqlMoneyToLong()
            {
                MethodInfo toSqlInternalRepresentation = typeof(SqlMoney).GetMethod("ToSqlInternalRepresentation",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.ExactBinding,
                    null, CallingConventions.Any, new Type[] { }, null);

                if (toSqlInternalRepresentation is not null && toSqlInternalRepresentation.ReturnType == typeof(long))
                {
                    return toSqlInternalRepresentation;
                }

                SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.GetFastSqlMoneyToLong | Info | SqlMoney.ToSqlInternalRepresentation() not found. Less efficient fallback method will be used.");
                return null; // missing the expected method - cannot use fast path
            }

            // Used in case we can't use a [Serializable]-like mechanism.
            private static long FallbackSqlMoneyToLong(SqlMoney value)
            {
                if (value.IsNull)
                {
                    return default;
                }
                else
                {
                    decimal data = value.ToDecimal();
                    return (long)(data * 10000);
                }
            }
        }
        #endregion

        #region Work around inability to access SqlDecimal._data1/2/3/4
        internal static void SqlDecimalExtractData(SqlDecimal d, out uint data1, out uint data2, out uint data3, out uint data4)
        {
            SqlDecimalHelper.Decompose(d, out data1, out data2, out data3, out data4);
        }

        private static class SqlDecimalHelper
        {
            private static readonly bool s_canUseFastPath = GetFastDecomposers(ref s_fiData1, ref s_fiData2, ref s_fiData3, ref s_fiData4);
            private static FieldInfo s_fiData1;
            private static FieldInfo s_fiData2;
            private static FieldInfo s_fiData3;
            private static FieldInfo s_fiData4;

            internal static void Decompose(SqlDecimal value, out uint data1, out uint data2, out uint data3, out uint data4)
            {
                if (s_canUseFastPath)
                {
                    try
                    {
                        data1 = (uint)s_fiData1.GetValue(value);
                        data2 = (uint)s_fiData2.GetValue(value);
                        data3 = (uint)s_fiData3.GetValue(value);
                        data4 = (uint)s_fiData4.GetValue(value);
                        return;
                    }
                    catch
                    {
                        // If an exception occurs for any reason, swallow & use the fallback code path.
                    }
                }

                FallbackDecomposer(value, out data1, out data2, out data3, out data4);
            }

            private static bool GetFastDecomposers(ref FieldInfo fiData1, ref FieldInfo fiData2, ref FieldInfo fiData3, ref FieldInfo fiData4)
            {
                // This takes advantage of the fact that for [Serializable] types, the member fields are implicitly
                // part of the type's serialization contract. This includes the fields' names and types. By default,
                // [Serializable]-compliant serializers will read all the member fields and shove the data into a
                // SerializationInfo dictionary. We mimic this behavior in a manner consistent with the [Serializable]
                // pattern, but much more efficiently.
                //
                // In order to make sure we're staying compliant, we need to gate our checks to fulfill some core
                // assumptions. Importantly, the type must be [Serializable] but cannot be ISerializable, as the
                // presence of the interface means that the type wants to be responsible for its own serialization,
                // and that member fields are not guaranteed to be part of the serialization contract. Additionally,
                // we need to check for [OnSerializing] and [OnDeserializing] methods, because we cannot account
                // for any logic which might be present within them.

                if (!typeof(SqlDecimal).IsSerializable)
                {
                    SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposers | Info | SqlDecimal isn't Serializable. Less efficient fallback method will be used.");
                    return false; // type is not serializable - cannot use fast path assumptions
                }

                if (typeof(ISerializable).IsAssignableFrom(typeof(SqlDecimal)))
                {
                    SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposers | Info | SqlDecimal is ISerializable. Less efficient fallback method will be used.");
                    return false; // type contains custom logic - cannot use fast path assumptions
                }

                foreach (var method in typeof(SqlDecimal).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.IsDefined(typeof(OnDeserializingAttribute)) || method.IsDefined(typeof(OnDeserializedAttribute)))
                    {
                        SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposers | Info | SqlDecimal contains custom serialization logic. Less efficient fallback method will be used.");
                        return false; // type contains custom logic - cannot use fast path assumptions
                    }
                }

                // GetSerializableMembers filters out [NonSerialized] fields for us automatically.

                foreach (var candidate in FormatterServices.GetSerializableMembers(typeof(SqlDecimal)))
                {
                    if (candidate is FieldInfo fi && fi.FieldType == typeof(uint))
                    {
                        if (fi.Name == "m_data1")
                        {
                            fiData1 = fi;
                        }
                        else if (fi.Name == "m_data2")
                        {
                            fiData2 = fi;
                        }
                        else if (fi.Name == "m_data3")
                        {
                            fiData3 = fi;
                        }
                        else if (fi.Name == "m_data4")
                        {
                            fiData4 = fi;
                        }
                    }
                }

                if (fiData1 is null || fiData2 is null || fiData3 is null || fiData4 is null)
                {
                    SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.SqlDecimalHelper.GetFastDecomposers | Info | Expected SqlDecimal fields are missing. Less efficient fallback method will be used.");
                    return false; // missing one of the expected member fields - cannot use fast path assumptions
                }

                return true;
            }

            // Used in case we can't use a [Serializable]-like mechanism.
            private static void FallbackDecomposer(SqlDecimal value, out uint data1, out uint data2, out uint data3, out uint data4)
            {
                if (value.IsNull)
                {
                    data1 = default;
                    data2 = default;
                    data3 = default;
                    data4 = default;
                }
                else
                {
                    int[] data = value.Data; // allocation
                    data4 = (uint)data[3]; // write in reverse to avoid multiple bounds checks
                    data3 = (uint)data[2];
                    data2 = (uint)data[1];
                    data1 = (uint)data[0];
                }
            }
        }
        #endregion

        #region Work around inability to access SqlBinary.ctor(byte[], bool)
        private static readonly Func<byte[], SqlBinary> s_sqlBinaryfactory = CtorHelper.CreateFactory<SqlBinary, byte[], bool>(); // binds to SqlBinary..ctor(byte[], bool) if it exists

        internal static SqlBinary SqlBinaryCtor(byte[] value, bool ignored)
        {
            SqlBinary val;
            if (s_sqlBinaryfactory is not null)
            {
                val = s_sqlBinaryfactory(value);
            }
            else
            {
                val = new SqlBinary(value);
            }

            return val;
        }
        #endregion

        #region Work around inability to access SqlGuid.ctor(byte[], bool)
        private static readonly Func<byte[], SqlGuid> s_sqlGuidfactory = CtorHelper.CreateFactory<SqlGuid, byte[], bool>(); // binds to SqlGuid..ctor(byte[], bool) if it exists

        internal static SqlGuid SqlGuidCtor(byte[] value, bool ignored)
        {
            SqlGuid val;
            if (s_sqlBinaryfactory is not null)
            {
                val = s_sqlGuidfactory(value);
            }
            else
            {
                val = new SqlGuid(value);
            }

            return val;
        }
        #endregion

        private static class CtorHelper
        {
            // Returns null if .ctor(TValue, TIgnored) cannot be found.
            // Caller should have fallback logic in place in case the API doesn't exist.
            internal unsafe static Func<TValue, TInstance> CreateFactory<TInstance, TValue, TIgnored>() where TInstance : struct
            {
                try
                {
                    ConstructorInfo fullCtor = typeof(TInstance).GetConstructor(
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.ExactBinding,
                        null, new[] { typeof(TValue), typeof(TIgnored) }, null);
                    if (fullCtor is not null)
                    {
                        // Need to use fnptr rather than delegate since MulticastDelegate expects to point to a MethodInfo,
                        // not a ConstructorInfo. The convention for invoking struct ctors is that the caller zeros memory,
                        // then passes a ref to the zeroed memory as the implicit arg0 "this". We don't need to worry
                        // about keeping this pointer alive; the fact that we're instantiated over TInstance will do it
                        // for us.
                        //
                        // On Full Framework, creating a delegate to InvocationHelper before invoking it for the first time
                        // will cause the delegate to point to the pre-JIT stub, which has an expensive preamble. Instead,
                        // we invoke InvocationHelper manually with a captured no-op fnptr. We'll then replace it with the
                        // real fnptr before creating a new delegate (pointing to the real codegen, not the stub) and
                        // returning that new delegate to our caller.

                        static void DummyNoOp(ref TInstance @this, TValue value, TIgnored ignored)
                        { }

                        IntPtr fnPtr;
                        TInstance InvocationHelper(TValue value)
                        {
                            TInstance retVal = default; // ensure zero-inited
                            ((delegate* managed<ref TInstance, TValue, TIgnored, void>)fnPtr)(ref retVal, value, default);
                            return retVal;
                        }

                        fnPtr = (IntPtr)(delegate* managed<ref TInstance, TValue, TIgnored, void>)(&DummyNoOp);
                        InvocationHelper(default); // no-op to trigger JIT

                        fnPtr = fullCtor.MethodHandle.GetFunctionPointer(); // replace before returning to caller
                        return InvocationHelper;
                    }
                }
                catch
                {
                }

                SqlClientEventSource.Log.TryTraceEvent("SqlTypeWorkarounds.CtorHelper.CreateFactory | Info | {0}..ctor({1}, {2}) not found. Less efficient fallback method will be used.", typeof(TInstance).Name, typeof(TValue).Name, typeof(TIgnored).Name);
                return null; // factory not found or an exception occurred
            }
        }
    }
}
