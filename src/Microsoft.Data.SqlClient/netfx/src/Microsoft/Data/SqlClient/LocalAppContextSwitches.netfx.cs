// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient
{
    internal static partial class LocalAppContextSwitches
    {
        internal const string UseMinimumLoginTimeoutString = @"Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin";
        private static bool _useMinimumLoginTimeout;
        public static bool UseMinimumLoginTimeout
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return AppContext.TryGetSwitch(UseMinimumLoginTimeoutString, out _useMinimumLoginTimeout) ? _useMinimumLoginTimeout : false;
            }
        }

        internal const string DisableTNIRByDefaultString = @"Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString";
        private static bool _disableTNIRByDefault;
        public static bool DisableTNIRByDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return AppContext.TryGetSwitch(DisableTNIRByDefaultString, out _disableTNIRByDefault) ? _disableTNIRByDefault : false;
            }
        }
    }
}
