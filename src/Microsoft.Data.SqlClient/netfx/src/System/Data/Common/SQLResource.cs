//------------------------------------------------------------------------------
// <copyright file="SQLResource.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>                                                                
// <owner current="true" primary="true">junfang</owner>
// <owner current="true" primary="false">laled</owner>
// <owner current="true" primary="false">blained</owner>
//------------------------------------------------------------------------------

//**************************************************************************
// @File: SQLResource.cs
//
// Create by:	JunFang
//
// Purpose: Implementation of utilities in COM+ SQL Types Library.
//			Includes interface INullable, exceptions SqlNullValueException
//			and SqlTruncateException, and SQLDebug class.
//
// Notes: 
//	
// History:
//
//   10/22/99  JunFang	Created.
//
// @EndHeader@
//**************************************************************************

namespace Microsoft.Data.SqlTypes {

    using System;
    using Microsoft.Data;
    using System.Globalization;

    internal sealed class SQLResource {
        
        private SQLResource() { /* prevent utility class from being insantiated*/ }        
        
        internal static readonly String NullString                  = ResHelper.GetString(Res.SqlMisc_NullString);

        internal static readonly String MessageString               = ResHelper.GetString(Res.SqlMisc_MessageString);

        internal static readonly String ArithOverflowMessage        = ResHelper.GetString(Res.SqlMisc_ArithOverflowMessage);

        internal static readonly String DivideByZeroMessage         = ResHelper.GetString(Res.SqlMisc_DivideByZeroMessage);

        internal static readonly String NullValueMessage            = ResHelper.GetString(Res.SqlMisc_NullValueMessage);

        internal static readonly String TruncationMessage           = ResHelper.GetString(Res.SqlMisc_TruncationMessage);

        internal static readonly String DateTimeOverflowMessage     = ResHelper.GetString(Res.SqlMisc_DateTimeOverflowMessage);

        internal static readonly String ConcatDiffCollationMessage  = ResHelper.GetString(Res.SqlMisc_ConcatDiffCollationMessage);

        internal static readonly String CompareDiffCollationMessage = ResHelper.GetString(Res.SqlMisc_CompareDiffCollationMessage);

        internal static readonly String InvalidFlagMessage          = ResHelper.GetString(Res.SqlMisc_InvalidFlagMessage);

        internal static readonly String NumeToDecOverflowMessage    = ResHelper.GetString(Res.SqlMisc_NumeToDecOverflowMessage);

        internal static readonly String ConversionOverflowMessage   = ResHelper.GetString(Res.SqlMisc_ConversionOverflowMessage);

        internal static readonly String InvalidDateTimeMessage      = ResHelper.GetString(Res.SqlMisc_InvalidDateTimeMessage);

        internal static readonly String TimeZoneSpecifiedMessage      = ResHelper.GetString(Res.SqlMisc_TimeZoneSpecifiedMessage);

        internal static readonly String InvalidArraySizeMessage     = ResHelper.GetString(Res.SqlMisc_InvalidArraySizeMessage);

        internal static readonly String InvalidPrecScaleMessage     = ResHelper.GetString(Res.SqlMisc_InvalidPrecScaleMessage);

        internal static readonly String FormatMessage               = ResHelper.GetString(Res.SqlMisc_FormatMessage);

        internal static readonly String NotFilledMessage            = ResHelper.GetString(Res.SqlMisc_NotFilledMessage);

        internal static readonly String AlreadyFilledMessage        = ResHelper.GetString(Res.SqlMisc_AlreadyFilledMessage);

        internal static readonly String ClosedXmlReaderMessage        = ResHelper.GetString(Res.SqlMisc_ClosedXmlReaderMessage);

        internal static String InvalidOpStreamClosed(String method)
        {
                return ResHelper.GetString(Res.SqlMisc_InvalidOpStreamClosed, method);
        }

        internal static String InvalidOpStreamNonWritable(String method)
        {
                return ResHelper.GetString(Res.SqlMisc_InvalidOpStreamNonWritable, method);
        }

        internal static String InvalidOpStreamNonReadable(String method)
        {
                return ResHelper.GetString(Res.SqlMisc_InvalidOpStreamNonReadable, method);
        }

        internal static String InvalidOpStreamNonSeekable(String method)
        {
                return ResHelper.GetString(Res.SqlMisc_InvalidOpStreamNonSeekable, method);
        }
    } // SqlResource

} // namespace System
