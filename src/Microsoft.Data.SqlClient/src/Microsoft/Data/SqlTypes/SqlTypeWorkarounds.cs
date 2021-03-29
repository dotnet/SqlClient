// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        /// <summary>
        /// Constructs a SqlMoney from a long value without scaling. The ignored parameter exists
        /// only to distinguish this constructor from the constructor that takes a long.
        /// Used only internally.
        /// </summary>
        internal static SqlMoney SqlMoneyCtor(long value, int ignored)
        {
            var c = default(SqlMoneyCaster);

            // Same behavior as the internal SqlMoney.ctor(long, bool) overload
            c.Fake._fNotNull = true;
            c.Fake._value = value;

            return c.Real;
        }

        internal static long SqlMoneyToSqlInternalRepresentation(SqlMoney money)
        {
            var c = default(SqlMoneyCaster);
            c.Real = money;

            // Same implementation as the internal SqlMoney.ToSqlInternalRepresentation implementation
            if (money.IsNull)
            {
                throw new SqlNullValueException();
            }
            return c.Fake._value;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SqlMoneyLookalike // exact same shape as SqlMoney, but with accessible fields
        {
            internal bool _fNotNull;
            internal long _value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SqlMoneyCaster
        {
            [FieldOffset(0)]
            internal SqlMoney Real;
            [FieldOffset(0)]
            internal SqlMoneyLookalike Fake;
        }
        #endregion

        #region Work around inability to access SqlDecimal._data1/2/3/4
        internal static void SqlDecimalExtractData(SqlDecimal d, out uint data1, out uint data2, out uint data3, out uint data4)
        {
            // Extract the four data elements from SqlDecimal.
            var c = default(SqlDecimalCaster);
            c.Real = d;
            data1 = c.Fake._data1;
            data2 = c.Fake._data2;
            data3 = c.Fake._data3;
            data4 = c.Fake._data4;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SqlDecimalLookalike // exact same shape as SqlDecimal, but with accessible fields
        {
            internal byte _bStatus;
            internal byte _bLen;
            internal byte _bPrec;
            internal byte _bScale;
            internal uint _data1;
            internal uint _data2;
            internal uint _data3;
            internal uint _data4;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SqlDecimalCaster
        {
            [FieldOffset(0)]
            internal SqlDecimal Real;
            [FieldOffset(0)]
            internal SqlDecimalLookalike Fake;
        }
        #endregion

        #region Work around inability to access SqlBinary.ctor(byte[], bool)
        internal static SqlBinary SqlBinaryCtor(byte[] value, bool ignored)
        {
            // Construct a SqlBinary without allocating/copying the byte[].  This provides
            // the same behavior as SqlBinary.ctor(byte[], bool).
            var c = default(SqlBinaryCaster);
            c.Fake._value = value;
            return c.Real;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SqlBinaryLookalike
        {
            internal byte[] _value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SqlBinaryCaster
        {
            [FieldOffset(0)]
            internal SqlBinary Real;
            [FieldOffset(0)]
            internal SqlBinaryLookalike Fake;
        }
        #endregion

        #region Work around inability to access SqlGuid.ctor(byte[], bool)
        internal static SqlGuid SqlGuidCtor(byte[] value, bool ignored)
        {
            // Construct a SqlGuid without allocating/copying the byte[].  This provides
            // the same behavior as SqlGuid.ctor(byte[], bool).
            var c = default(SqlGuidCaster);
            c.Fake._value = value;
            return c.Real;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SqlGuidLookalike
        {
            internal byte[] _value;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SqlGuidCaster
        {
            [FieldOffset(0)]
            internal SqlGuid Real;
            [FieldOffset(0)]
            internal SqlGuidLookalike Fake;
        }
        #endregion
    }
}
