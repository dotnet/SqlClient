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
        private static bool _makeReadAsyncBlocking;
        public static bool MakeReadAsyncBlocking
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return AppContext.TryGetSwitch(MakeReadAsyncBlockingString, out _makeReadAsyncBlocking) ? _makeReadAsyncBlocking : false;
            }
        }
    }
}
