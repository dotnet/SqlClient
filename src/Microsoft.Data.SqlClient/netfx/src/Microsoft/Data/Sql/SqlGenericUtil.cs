// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.Sql
{
    using System;
    using Microsoft.Data;
    using Microsoft.Data.Common;

    sealed internal class SqlGenericUtil
    {

        private SqlGenericUtil() { /* prevent utility class from being instantiated*/ }

        //
        // Sql generic exceptions
        //

        //
        // Sql.Definition
        //

        static internal Exception NullCommandText()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.Sql_NullCommandText));
        }
        static internal Exception MismatchedMetaDataDirectionArrayLengths()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.Sql_MismatchedMetaDataDirectionArrayLengths));
        }
    }

}//namespace

