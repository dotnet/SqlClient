// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient
{
    internal static class LocalAppContextSwitches
    {
        internal const string MakeReadAsyncBlockingString = @"Switch.Microsoft.Data.SqlClient.MakeReadAsyncBlocking";
        private static int _makeReadAsyncBlocking;
        public static bool MakeReadAsyncBlocking
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return LocalAppContext.GetCachedSwitchValue(MakeReadAsyncBlockingString, ref _makeReadAsyncBlocking);
            }
        }

        internal const string UseMinimumLoginTimeoutString = @"Switch.Microsoft.Data.SqlClient.UseOneSecFloorInTimeoutCalculationDuringLogin";
        private static int _useMinimumLoginTimeout;
        public static bool UseMinimumLoginTimeout
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return LocalAppContext.GetCachedSwitchValue(UseMinimumLoginTimeoutString, ref _useMinimumLoginTimeout);
            }
        }

        internal const string DisableTNIRByDefaultString = @"Switch.Microsoft.Data.SqlClient.DisableTNIRByDefaultInConnectionString";
        private static int _disableTNIRByDefault;
        public static bool DisableTNIRByDefault
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return LocalAppContext.GetCachedSwitchValue(DisableTNIRByDefaultString, ref _disableTNIRByDefault);
            }
        }
    }
}
