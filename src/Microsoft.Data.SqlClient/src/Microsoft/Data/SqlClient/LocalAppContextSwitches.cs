// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient
{
    internal static partial class LocalAppContextSwitches
    {
        internal const string MakeReadAsyncBlockingString = @"Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";
        internal const string LegacyRowVersionNullString = @"Switch.Microsoft.Data.SqlClient.LegacyRowVersionNullBehavior";

        private static bool _makeReadAsyncBlocking;
        private static bool? s_LegacyRowVersionNullBehavior;

        public static bool MakeReadAsyncBlocking
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out _makeReadAsyncBlocking) ? _makeReadAsyncBlocking : false;
            }
        }

        /// <summary>
        /// In System.Data.SqlClient and Microsoft.Data.SqlClient prior to 3.0.0 a field with type Timestamp/RowVersion
        /// would return an empty byte array. This switch contols whether to preserve that behaviour on newer versions
        /// of Microsoft.Data.SqlClient, if this switch returns false an appropriate null value will be returned
        /// </summary>
        public static bool LegacyRowVersionNullBehavior
        {
            get
            {
                if (s_LegacyRowVersionNullBehavior == null)
                {
                    bool value = false;
                    if (AppContext.TryGetSwitch(LegacyRowVersionNullString, out bool providedValue))
                    {
                         value = providedValue;
                    }
                    s_LegacyRowVersionNullBehavior = value;
                }
                return s_LegacyRowVersionNullBehavior.Value;
            }
        }
    }
}
