// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    public partial class SqlCommand
    {
        #region Public Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteScalar/*'/>
        public override object ExecuteScalar()
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

            using var _ = TryEventScope.Create($"SqlCommand.ExecuteScalar | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteScalar | API | Correlation | " +
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

                // @TODO: Rename ds to reader
                SqlDataReader ds = IsProviderRetriable
                    ? RunExecuteReaderWithRetry(CommandBehavior.Default, RunBehavior.ReturnImmediately, returnStream: true)
                    : RunExecuteReader(CommandBehavior.Default, RunBehavior.ReturnImmediately, returnStream: true);
                success = true;

                return CompleteExecuteScalar(ds, _batchRPCMode);
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteScalarAsync[@name="CancellationToken"]/*'/>
        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            // Do not use retry logic here as ExecuteReaderAsyncInternal handles retry logic
            return InternalExecuteScalarAsync(cancellationToken);
        }
        
        #endregion
        
        #region Private Methods

        private static object CompleteExecuteScalar(SqlDataReader reader, bool returnLastResult)
        {
            object result = null;
            try
            {
                do
                {
                    if (reader.Read() && reader.FieldCount > 0)
                    {
                        result = reader.GetValue(0);
                    }
                } while (returnLastResult && reader.NextResult());
            }
            finally
            {
                // Clean off the wire
                reader.Close();
            }

            return result;
        }
        
        #endregion
    }
}
