// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlTypes
{

    using System;
    using Microsoft.Data;

    internal sealed class SQLResource
    {

        private SQLResource() { /* prevent utility class from being instantiated*/ }

        internal static readonly String NullString = StringsHelper.GetString(Strings.SqlMisc_NullString);

        internal static readonly String MessageString = StringsHelper.GetString(Strings.SqlMisc_MessageString);

        internal static readonly String ArithOverflowMessage = StringsHelper.GetString(Strings.SqlMisc_ArithOverflowMessage);

        internal static readonly String DivideByZeroMessage = StringsHelper.GetString(Strings.SqlMisc_DivideByZeroMessage);

        internal static readonly String NullValueMessage = StringsHelper.GetString(Strings.SqlMisc_NullValueMessage);

        internal static readonly String TruncationMessage = StringsHelper.GetString(Strings.SqlMisc_TruncationMessage);

        internal static readonly String DateTimeOverflowMessage = StringsHelper.GetString(Strings.SqlMisc_DateTimeOverflowMessage);

        internal static readonly String ConcatDiffCollationMessage = StringsHelper.GetString(Strings.SqlMisc_ConcatDiffCollationMessage);

        internal static readonly String CompareDiffCollationMessage = StringsHelper.GetString(Strings.SqlMisc_CompareDiffCollationMessage);

        internal static readonly String InvalidFlagMessage = StringsHelper.GetString(Strings.SqlMisc_InvalidFlagMessage);

        internal static readonly String NumeToDecOverflowMessage = StringsHelper.GetString(Strings.SqlMisc_NumeToDecOverflowMessage);

        internal static readonly String ConversionOverflowMessage = StringsHelper.GetString(Strings.SqlMisc_ConversionOverflowMessage);

        internal static readonly String InvalidDateTimeMessage = StringsHelper.GetString(Strings.SqlMisc_InvalidDateTimeMessage);

        internal static readonly String TimeZoneSpecifiedMessage = StringsHelper.GetString(Strings.SqlMisc_TimeZoneSpecifiedMessage);

        internal static readonly String InvalidArraySizeMessage = StringsHelper.GetString(Strings.SqlMisc_InvalidArraySizeMessage);

        internal static readonly String InvalidPrecScaleMessage = StringsHelper.GetString(Strings.SqlMisc_InvalidPrecScaleMessage);

        internal static readonly String FormatMessage = StringsHelper.GetString(Strings.SqlMisc_FormatMessage);

        internal static readonly String NotFilledMessage = StringsHelper.GetString(Strings.SqlMisc_NotFilledMessage);

        internal static readonly String AlreadyFilledMessage = StringsHelper.GetString(Strings.SqlMisc_AlreadyFilledMessage);

        internal static readonly String ClosedXmlReaderMessage = StringsHelper.GetString(Strings.SqlMisc_ClosedXmlReaderMessage);

        internal static String InvalidOpStreamClosed(String method)
        {
            return StringsHelper.GetString(Strings.SqlMisc_InvalidOpStreamClosed, method);
        }

        internal static String InvalidOpStreamNonWritable(String method)
        {
            return StringsHelper.GetString(Strings.SqlMisc_InvalidOpStreamNonWritable, method);
        }

        internal static String InvalidOpStreamNonReadable(String method)
        {
            return StringsHelper.GetString(Strings.SqlMisc_InvalidOpStreamNonReadable, method);
        }

        internal static String InvalidOpStreamNonSeekable(String method)
        {
            return StringsHelper.GetString(Strings.SqlMisc_InvalidOpStreamNonSeekable, method);
        }
    } // SqlResource

} // namespace System
