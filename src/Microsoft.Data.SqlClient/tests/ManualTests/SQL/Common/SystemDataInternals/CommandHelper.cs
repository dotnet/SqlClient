// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SystemDataInternals
{
    internal static class CommandHelper
    {
        private static Type s_sqlCommand = typeof(SqlCommand);
        private static MethodInfo s_completePendingReadWithSuccess = s_sqlCommand.GetMethod("CompletePendingReadWithSuccess", BindingFlags.NonPublic | BindingFlags.Instance);
        private static MethodInfo s_completePendingReadWithFailure = s_sqlCommand.GetMethod("CompletePendingReadWithFailure", BindingFlags.NonPublic | BindingFlags.Instance);
        public static PropertyInfo s_debugForceAsyncWriteDelay = s_sqlCommand.GetProperty("DebugForceAsyncWriteDelay", BindingFlags.NonPublic | BindingFlags.Static);
        public static FieldInfo s_sleepDuringTryFetchInputParameterEncryptionInfo = s_sqlCommand.GetField(@"_sleepDuringTryFetchInputParameterEncryptionInfo", BindingFlags.Static | BindingFlags.NonPublic);
        public static PropertyInfo s_isDescribeParameterEncryptionRPCCurrentlyInProgress = s_sqlCommand.GetProperty(@"IsDescribeParameterEncryptionRPCCurrentlyInProgress", BindingFlags.Instance | BindingFlags.NonPublic);
        public static FieldInfo s_sqlRPCParameterEncryptionReqArray = s_sqlCommand.GetField(@"_sqlRPCParameterEncryptionReqArray", BindingFlags.Instance | BindingFlags.NonPublic);
        public static FieldInfo s_currentlyExecutingDescribeParameterEncryptionRPC = s_sqlCommand.GetField(@"_currentlyExecutingDescribeParameterEncryptionRPC", BindingFlags.Instance | BindingFlags.NonPublic);
        public static FieldInfo s_rowsAffectedBySpDescribeParameterEncryption = s_sqlCommand.GetField(@"_rowsAffectedBySpDescribeParameterEncryption", BindingFlags.Instance | BindingFlags.NonPublic);
        public static FieldInfo s_sleepDuringRunExecuteReaderTdsForSpDescribeParameterEncryption = s_sqlCommand.GetField(@"_sleepDuringRunExecuteReaderTdsForSpDescribeParameterEncryption", BindingFlags.Static | BindingFlags.NonPublic);
        public static FieldInfo s_sleepAfterReadDescribeEncryptionParameterResults = s_sqlCommand.GetField(@"_sleepAfterReadDescribeEncryptionParameterResults", BindingFlags.Static | BindingFlags.NonPublic);

        internal static void CompletePendingReadWithSuccess(SqlCommand command, bool resetForcePendingReadsToWait)
        {
            s_completePendingReadWithSuccess.Invoke(command, new object[] { resetForcePendingReadsToWait });
        }

        internal static void CompletePendingReadWithFailure(SqlCommand command, int errorCode, bool resetForcePendingReadsToWait)
        {
            s_completePendingReadWithFailure.Invoke(command, new object[] { errorCode, resetForcePendingReadsToWait });
        }

        internal static int ForceAsyncWriteDelay
        {
            get { return (int)s_debugForceAsyncWriteDelay.GetValue(null); }
            set { s_debugForceAsyncWriteDelay.SetValue(null, value); }
        }
    }
}
