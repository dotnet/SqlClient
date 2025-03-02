// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class TdsParserStateObjectHelper
    {
        private static readonly Assembly s_systemDotData;
        private static readonly Type s_tdsParserStateObject;
        private static readonly FieldInfo s_forceAllPends;
        private static readonly FieldInfo s_skipSendAttention;
        private static readonly FieldInfo s_forceSyncOverAsyncAfterFirstPend;
        private static readonly FieldInfo s_failAsyncPends;
        private static readonly FieldInfo s_forcePendingReadsToWaitForUser;
        private static readonly Type s_tdsParserStateObjectManaged;
        private static readonly FieldInfo s_tdsParserStateObjectManagedSessionHandle;

        static TdsParserStateObjectHelper()
        {
            s_systemDotData = typeof(Microsoft.Data.SqlClient.SqlConnection).GetTypeInfo().Assembly;
            s_tdsParserStateObject = s_systemDotData.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
            s_forceAllPends = s_tdsParserStateObject.GetField("s_forceAllPends", BindingFlags.Static | BindingFlags.NonPublic);
            s_skipSendAttention = s_tdsParserStateObject.GetField("s_skipSendAttention", BindingFlags.Static | BindingFlags.NonPublic);
            s_forceSyncOverAsyncAfterFirstPend = s_tdsParserStateObject.GetField("s_forceSyncOverAsyncAfterFirstPend", BindingFlags.Static | BindingFlags.NonPublic);
            s_failAsyncPends = s_tdsParserStateObject.GetField("s_failAsyncPends", BindingFlags.Static | BindingFlags.NonPublic);
            s_forcePendingReadsToWaitForUser = s_tdsParserStateObject.GetField("s_forcePendingReadsToWaitForUser", BindingFlags.Static | BindingFlags.NonPublic);
            s_tdsParserStateObjectManaged = s_systemDotData.GetType("Microsoft.Data.SqlClient.SNI.TdsParserStateObjectManaged");
            if (s_tdsParserStateObjectManaged != null)
            {
                s_tdsParserStateObjectManagedSessionHandle = s_tdsParserStateObjectManaged.GetField("_sessionHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            }
        }
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

        private static void VerifyObjectIsTdsParserStateObject(object stateObject)
        {
            if (stateObject == null)
            {
                throw new ArgumentNullException(nameof(stateObject));
            }
            if (s_tdsParserStateObjectManaged == null)
            {
                throw new ArgumentException("Library being tested does not implement TdsParserStateObjectManaged", nameof(stateObject));
            }
            if (!s_tdsParserStateObjectManaged.IsInstanceOfType(stateObject))
            {
                throw new ArgumentException("Object provided was not a TdsParserStateObjectManaged", nameof(stateObject));
            }
        }

        internal static object GetSessionHandle(object stateObject)
        {
            VerifyObjectIsTdsParserStateObject(stateObject);
            return s_tdsParserStateObjectManagedSessionHandle.GetValue(stateObject);
        }
    }
}
