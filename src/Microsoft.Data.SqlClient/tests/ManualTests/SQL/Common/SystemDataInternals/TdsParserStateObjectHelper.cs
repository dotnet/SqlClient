// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class TdsParserStateObjectHelper
    {
        private static readonly FieldInfo s_forceAllPends;
        private static readonly FieldInfo s_skipSendAttention;
        private static readonly FieldInfo s_forceSyncOverAsyncAfterFirstPend;
        private static readonly FieldInfo s_failAsyncPends;
        private static readonly FieldInfo s_forcePendingReadsToWaitForUser;
        private static readonly Type s_tdsParserStateObjectManaged;
        private static readonly FieldInfo s_tdsParserStateObjectManagedSessionHandle;

        static TdsParserStateObjectHelper()
        {
            Assembly assembly = typeof(Microsoft.Data.SqlClient.SqlConnection).GetTypeInfo().Assembly;
            Assert.True(assembly is not null, nameof(assembly));

            Type tdsParserStateObject = assembly.GetType("Microsoft.Data.SqlClient.TdsParserStateObject");
            Assert.True(tdsParserStateObject is not null, nameof(tdsParserStateObject));

            s_forceAllPends = tdsParserStateObject.GetField("s_forceAllPends", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(s_forceAllPends is not null, nameof(s_forceAllPends));

            s_skipSendAttention = tdsParserStateObject.GetField("s_skipSendAttention", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(s_skipSendAttention is not null, nameof(s_skipSendAttention));

            s_forceSyncOverAsyncAfterFirstPend = tdsParserStateObject.GetField("s_forceSyncOverAsyncAfterFirstPend", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(s_forceSyncOverAsyncAfterFirstPend is not null, nameof(s_forceSyncOverAsyncAfterFirstPend));

            s_failAsyncPends = tdsParserStateObject.GetField("s_failAsyncPends", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(s_failAsyncPends is not null, nameof(s_failAsyncPends));

            s_forcePendingReadsToWaitForUser = tdsParserStateObject.GetField("s_forcePendingReadsToWaitForUser", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.True(s_forcePendingReadsToWaitForUser is not null, nameof(s_forcePendingReadsToWaitForUser));

            // These managed SNI handles are allowed to be null, since they
            // won't exist in .NET Framework builds.
            s_tdsParserStateObjectManaged =
                assembly.GetType("Microsoft.Data.SqlClient.ManagedSni.TdsParserStateObjectManaged");
            s_tdsParserStateObjectManagedSessionHandle = null;
            if (s_tdsParserStateObjectManaged is not null)
            {
                s_tdsParserStateObjectManagedSessionHandle =
                    s_tdsParserStateObjectManaged.GetField(
                        "_sessionHandle",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                // If we have the managed SNI type, we must have the session
                // handle field.
                Assert.True(
                    s_tdsParserStateObjectManagedSessionHandle is not null,
                    nameof(s_tdsParserStateObjectManagedSessionHandle));
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

        internal static object GetSessionHandle(object stateObject)
        {
            if (stateObject == null)
            {
                throw new ArgumentNullException(nameof(stateObject));
            }
            if (s_tdsParserStateObjectManaged is null)
            {
                throw new ArgumentException("Library being tested does not implement TdsParserStateObjectManaged", nameof(stateObject));
            }
            if (!s_tdsParserStateObjectManaged.IsInstanceOfType(stateObject))
            {
                throw new ArgumentException("Object provided was not a TdsParserStateObjectManaged", nameof(stateObject));
            }
            return s_tdsParserStateObjectManagedSessionHandle.GetValue(stateObject);
        }
    }
}
