// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
#if NET
using Microsoft.Data.SqlClient.SNI;
#endif

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class TdsParserStateObjectHelper
    {
        private static readonly FieldInfo s_forceAllPends = typeof(TdsParserStateObject).GetField("s_forceAllPends", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_skipSendAttention = typeof(TdsParserStateObject).GetField("s_skipSendAttention", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_forceSyncOverAsyncAfterFirstPend = typeof(TdsParserStateObject).GetField("s_forceSyncOverAsyncAfterFirstPend", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_failAsyncPends = typeof(TdsParserStateObject).GetField("s_failAsyncPends", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly FieldInfo s_forcePendingReadsToWaitForUser = typeof(TdsParserStateObject).GetField("s_forcePendingReadsToWaitForUser", BindingFlags.Static | BindingFlags.NonPublic);
#if NET
        private static readonly Type s_tdsParserStateObjectManaged = typeof(TdsParserStateObjectManaged);
        private static readonly FieldInfo s_tdsParserStateObjectManagedSessionHandle = typeof(TdsParserStateObjectManaged).GetField("_sessionHandle", BindingFlags.Instance | BindingFlags.NonPublic);
#endif

        internal static bool ForceAllPends
        {
            get { return (bool)s_forceAllPends.GetValue(null); }
            set { s_forceAllPends.SetValue(null, value); }
        }

        internal static bool SkipSendAttention
        {
            get { return (bool)s_skipSendAttention.GetValue(null); }
            set { s_skipSendAttention.SetValue(null, value); }
        }

        internal static bool ForceSyncOverAsyncAfterFirstPend
        {
            get { return (bool)s_forceSyncOverAsyncAfterFirstPend.GetValue(null); }
            set { s_forceSyncOverAsyncAfterFirstPend.SetValue(null, value); }
        }

        internal static bool ForcePendingReadsToWaitForUser
        {
            get { return (bool)s_forcePendingReadsToWaitForUser.GetValue(null); }
            set { s_forcePendingReadsToWaitForUser.SetValue(null, value); }
        }

        internal static bool FailAsyncPends
        {
            get { return (bool)s_failAsyncPends.GetValue(null); }
            set { s_failAsyncPends.SetValue(null, value); }
        }

        internal static object GetSessionHandle(object stateObject)
        {
#if NETFRAMEWORK

            throw new ArgumentException("Library being tested does not implement TdsParserStateObjectManaged", nameof(stateObject));
#else
            if (stateObject == null)
            {
                throw new ArgumentNullException(nameof(stateObject));
            }

            if (!s_tdsParserStateObjectManaged.IsInstanceOfType(stateObject))
            {
                throw new ArgumentException("Object provided was not a TdsParserStateObjectManaged", nameof(stateObject));
            }
            return s_tdsParserStateObjectManagedSessionHandle.GetValue(stateObject);
#endif
        }
    }
}
