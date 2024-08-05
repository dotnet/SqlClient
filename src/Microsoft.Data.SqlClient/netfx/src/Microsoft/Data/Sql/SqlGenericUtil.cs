// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.Sql
{
    using System;
    using Microsoft.Data;
    using Microsoft.Data.Common;

    internal sealed class SqlGenericUtil
    {

        private SqlGenericUtil() { /* prevent utility class from being instantiated*/ }

        //
        // Sql generic exceptions
        //

        //
        // Sql.Definition
        //

        internal static Exception NullCommandText()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.Sql_NullCommandText));
        }
        internal static Exception MismatchedMetaDataDirectionArrayLengths()
        {
            return ADP.Argument(StringsHelper.GetString(Strings.Sql_MismatchedMetaDataDirectionArrayLengths));
        }
    }

}//namespace

