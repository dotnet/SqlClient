// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Data.Common
{
    internal static partial class ADP
    {
        internal static Timer UnsafeCreateTimer(TimerCallback callback, object state, int dueTime, int period)
        {
            // Don't capture the current ExecutionContext and its AsyncLocals onto 
            // a global timer causing them to live forever
            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                return new Timer(callback, state, dueTime, period);
            }
            finally
            {
                // Restore the current ExecutionContext
                if (restoreFlow)
                    ExecutionContext.RestoreFlow();
            }
        }

        //
        // COM+ exceptions
        //
        internal static PlatformNotSupportedException DbTypeNotSupported(string dbType) => new(StringsHelper.GetString(Strings.SQL_DbTypeNotSupportedOnThisPlatform, dbType));

        // IDbConnection.BeginTransaction, OleDbTransaction.Begin
        internal static ArgumentOutOfRangeException InvalidIsolationLevel(IsolationLevel value)
        {
#if DEBUG
            switch (value)
            {
                case IsolationLevel.Unspecified:
                case IsolationLevel.Chaos:
                case IsolationLevel.ReadUncommitted:
                case IsolationLevel.ReadCommitted:
                case IsolationLevel.RepeatableRead:
                case IsolationLevel.Serializable:
                case IsolationLevel.Snapshot:
                    Debug.Fail("valid IsolationLevel " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(IsolationLevel), (int)value);
        }

        // IDataParameter.Direction
        internal static ArgumentOutOfRangeException InvalidParameterDirection(ParameterDirection value)
        {
#if DEBUG
            switch (value)
            {
                case ParameterDirection.Input:
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                case ParameterDirection.ReturnValue:
                    Debug.Fail("valid ParameterDirection " + value.ToString());
                    break;
            }
#endif
            return InvalidEnumerationValue(typeof(ParameterDirection), (int)value);
        }

        //
        // : IDbCommand
        //
        internal static Exception InvalidCommandTimeout(int value, [CallerMemberName] string property = "")
            => Argument(StringsHelper.GetString(Strings.ADP_InvalidCommandTimeout, value.ToString(CultureInfo.InvariantCulture)), property);
    }
}
