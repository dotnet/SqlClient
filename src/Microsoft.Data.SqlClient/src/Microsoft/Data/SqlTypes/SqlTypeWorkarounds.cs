// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Microsoft.Data.SqlTypes
{
    /// <summary>
    /// This type provides workarounds for the separation between System.Data.Common
    /// and Microsoft.Data.SqlClient.  The latter wants to access internal members of the former, and
    /// this class provides ways to do that.  We must review and update this implementation any time the
    /// implementation of the corresponding types in System.Data.Common change.
    /// </summary>
    internal static partial class SqlTypeWorkarounds
    {
        #region Work around inability to access SqlXml.CreateSqlXmlReader
        private static readonly XmlReaderSettings s_defaultXmlReaderSettings = new() { ConformanceLevel = ConformanceLevel.Fragment };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsCloseInput = new() { ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };
        private static readonly XmlReaderSettings s_defaultXmlReaderSettingsAsyncCloseInput = new() { Async = true, ConformanceLevel = ConformanceLevel.Fragment, CloseInput = true };

        internal const SqlCompareOptions SqlStringValidSqlCompareOptionMask =
            SqlCompareOptions.IgnoreCase | SqlCompareOptions.IgnoreWidth |
            SqlCompareOptions.IgnoreNonSpace | SqlCompareOptions.IgnoreKanaType |
            SqlCompareOptions.BinarySort | SqlCompareOptions.BinarySort2;

        internal static XmlReader SqlXmlCreateSqlXmlReader(Stream stream, bool closeInput = false, bool async = false)
        {
            Debug.Assert(closeInput || !async, "Currently we do not have pre-created settings for !closeInput+async");

            XmlReaderSettings settingsToUse = closeInput ?
                (async ? s_defaultXmlReaderSettingsAsyncCloseInput : s_defaultXmlReaderSettingsCloseInput) :
                s_defaultXmlReaderSettings;

            return XmlReader.Create(stream, settingsToUse);
        }

        internal static XmlReader SqlXmlCreateSqlXmlReader(TextReader textReader, bool closeInput = false, bool async = false)
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
            const int MinDay = -53690;                // Jan 1 1753
            const int MaxDay = 2958463;               // Dec 31 9999 is this many days from Jan 1 1900
            const int MinTime = 0;                    // 00:00:0:000PM
            const int MaxTime = SQLTicksPerDay - 1; // = 25919999,  11:59:59:997PM

            if (daypart < MinDay || daypart > MaxDay || timepart < MinTime || timepart > MaxTime)
            {
                throw new OverflowException(SQLResource.DateTimeOverflowMessage);
            }

            long baseDateTicks = new DateTime(1900, 1, 1).Ticks;
            long dayticks = daypart * TimeSpan.TicksPerDay;
            long timeticks = ((long)(timepart / SQLTicksPerMillisecond + 0.5)) * TimeSpan.TicksPerMillisecond;

            return new DateTime(baseDateTicks + dayticks + timeticks);
        }
        #endregion
    }
}
