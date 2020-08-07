// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.Data.SqlTypes
{
    internal static class SQLResource
    {
        internal static string NullString => Strings.SqlMisc_NullString;

        internal static string MessageString => Strings.SqlMisc_MessageString;

        internal static string ArithOverflowMessage => Strings.SqlMisc_ArithOverflowMessage;

        internal static string DivideByZeroMessage => Strings.SqlMisc_DivideByZeroMessage;

        internal static string NullValueMessage => Strings.SqlMisc_NullValueMessage;

        internal static string TruncationMessage => Strings.SqlMisc_TruncationMessage;

        internal static string DateTimeOverflowMessage => Strings.SqlMisc_DateTimeOverflowMessage;

        internal static string ConcatDiffCollationMessage => Strings.SqlMisc_ConcatDiffCollationMessage;

        internal static string CompareDiffCollationMessage => Strings.SqlMisc_CompareDiffCollationMessage;

        internal static string InvalidFlagMessage => Strings.SqlMisc_InvalidFlagMessage;

        internal static string NumeToDecOverflowMessage => Strings.SqlMisc_NumeToDecOverflowMessage;

        internal static string ConversionOverflowMessage => Strings.SqlMisc_ConversionOverflowMessage;

        internal static string InvalidDateTimeMessage => Strings.SqlMisc_InvalidDateTimeMessage;

        internal static string TimeZoneSpecifiedMessage => Strings.SqlMisc_TimeZoneSpecifiedMessage;

        internal static string InvalidArraySizeMessage => Strings.SqlMisc_InvalidArraySizeMessage;

        internal static string InvalidPrecScaleMessage => Strings.SqlMisc_InvalidPrecScaleMessage;

        internal static string FormatMessage => Strings.SqlMisc_FormatMessage;

        internal static string NotFilledMessage => Strings.SqlMisc_NotFilledMessage;

        internal static string AlreadyFilledMessage => Strings.SqlMisc_AlreadyFilledMessage;

        internal static string ClosedXmlReaderMessage => Strings.SqlMisc_ClosedXmlReaderMessage;

        internal static string InvalidOpStreamClosed(string method)
        {
            return System.StringsHelper.Format(Strings.SqlMisc_InvalidOpStreamClosed, method);
        }

        internal static string InvalidOpStreamNonWritable(string method)
        {
            return System.StringsHelper.Format(Strings.SqlMisc_InvalidOpStreamNonWritable, method);
        }

        internal static string InvalidOpStreamNonReadable(string method)
        {
            return System.StringsHelper.Format(Strings.SqlMisc_InvalidOpStreamNonReadable, method);
        }

        internal static string InvalidOpStreamNonSeekable(string method)
        {
            return System.StringsHelper.Format(Strings.SqlMisc_InvalidOpStreamNonSeekable, method);
        }
    } // SqlResource
} // namespace System
