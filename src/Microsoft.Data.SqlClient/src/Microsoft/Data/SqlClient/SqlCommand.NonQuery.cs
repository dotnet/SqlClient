// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteNonQuery[@name="default"]/*'/>
        public override int ExecuteNonQuery()
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif
            
            // Reset _pendingCancel upon entry into any Execute - used to synchronize state
            // between entry into Execute* API and the thread obtaining the stateObject.
            _pendingCancel = false;
            
            #if NET
            using var diagnosticScope = s_diagnosticListener.CreateCommandScope(this, _transaction);
            #endif

            using var eventScope = TryEventScope.Create($"SqlCommand.ExecuteNonQuery | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteNonQuery | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            SqlStatistics statistics = null;
            bool success = false;
            int? sqlExceptionNumber = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                WriteBeginExecuteEvent();

                if (IsProviderRetriable)
                {
                    InternalExecuteNonQueryWithRetry(
                        sendToPipe: false,
                        timeout: CommandTimeout,
                        usedCache: out _,
                        asyncWrite: false,
                        isRetry: false);
                }
                else
                {
                    InternalExecuteNonQuery(
                        completion: null,
                        sendToPipe: false,
                        timeout: CommandTimeout,
                        usedCache: out _);
                }

                success = true;

                return _rowsAffected;
            }
            catch (Exception ex)
            {
                #if NET
                diagnosticScope.SetException(ex);
                #endif

                if (ex is SqlException sqlException)
                {
                    sqlExceptionNumber = sqlException.Number;
                }

                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                WriteEndExecuteEvent(success, sqlExceptionNumber, synchronous: true);
            }
        }
        
        #endregion
        
        #region Private Methods
        
        #endregion
    }
}
