// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Extensions.Azure;

internal static class SqlClientEventSource
{
    public static class Log
    {
        public static void TryTraceEvent(string s) { }
        public static void TryTraceEvent<T1>(string s, T1? t1) { }
        public static void TryTraceEvent<T1, T2>(string s, T1? t1, T2? t2) { }
    }

}
