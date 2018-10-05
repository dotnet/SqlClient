// ==++==
// 
//   Copyright (c) Microsoft Corporation.  All rights reserved.
// 
// ==--==
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
    }
}
