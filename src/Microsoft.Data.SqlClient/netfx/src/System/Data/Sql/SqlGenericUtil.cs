//------------------------------------------------------------------------------
// <copyright file="SqlGenericUtil.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <owner current="true" primary="true">blained</owner>
// <owner current="true" primary="false">laled</owner>
//------------------------------------------------------------------------------

namespace Microsoft.Data.Sql {
    using System;
    using Microsoft.Data;
    using Microsoft.Data.Common;
    using System.Diagnostics;

    sealed internal class SqlGenericUtil {

        private SqlGenericUtil() { /* prevent utility class from being insantiated*/ }

        //
        // Sql generic exceptions
        //

        //
        // Sql.Definition
        //

        static internal Exception NullCommandText() {
            return ADP.Argument(StringsHelper.GetString(Strings.Sql_NullCommandText));
        }
        static internal Exception MismatchedMetaDataDirectionArrayLengths() {
            return ADP.Argument(StringsHelper.GetString(Strings.Sql_MismatchedMetaDataDirectionArrayLengths));
        }
    }

 }//namespace

