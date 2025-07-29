// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Data.Common;

#if NETFRAMEWORK
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient.Utilities;
#endif

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReader[@name="default"]/*'/>
        public new SqlDataReader ExecuteReader()
        {
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteReader | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            SqlStatistics statistics = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                return ExecuteReader(CommandBehavior.Default);
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReader[@name="CommandBehavior"]/*'/>
        public new SqlDataReader ExecuteReader(CommandBehavior behavior)
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif

            // Reset _pendingCancel upon entry into any Execute - used to synchronize state
            // between entry into Execute* API and the thread obtaining the stateObject.
            _pendingCancel = false;

            // @TODO: Do we want to use a command scope here like nonquery and xml? or is operation id ok?
            #if NET
            Guid operationId = s_diagnosticListener.WriteCommandBefore(this, _transaction);
            Exception e = null;
            #endif

            using var eventScope = TryEventScope.Create($"SqlCommand.ExecuteReader | API | Object Id {ObjectID}");
            // @TODO: Do we want to have a correlation trace event here like nonquery and xml?
            // @TODO: Basically, this doesn't follow the same pattern as nonquery, scalar, or xml. Doesn't seem right.

            SqlStatistics statistics = null;
            bool success = false;
            int? sqlExceptionNumber = null;

            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                WriteBeginExecuteEvent();

                SqlDataReader result = IsProviderRetriable
                    ? RunExecuteReaderWithRetry(behavior, RunBehavior.ReturnImmediately, returnStream: true)
                    : RunExecuteReader(behavior, RunBehavior.ReturnImmediately, returnStream: true);
                success = true;
                return result;
            }
            // @TODO: CER Exception Handling was removed here (see GH#3581)
            catch (Exception ex)
            {
                #if NET
                e = ex;
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

                #if NET
                if (e is not null)
                {
                    s_diagnosticListener.WriteCommandError(operationId, this, _transaction, e);
                }
                else
                {
                    s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                }
                #endif
            }
        }

        internal SqlDataReader RunExecuteReader(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            [CallerMemberName] string method = "")
        {
            SqlDataReader reader = RunExecuteReader(
                cmdBehavior,
                runBehavior,
                returnStream,
                completion: null,
                timeout: CommandTimeout,
                task: out Task unused,
                usedCache: out _,
                method: method);

            // @TODO: This really isn't necessary...
            Debug.Assert(unused == null, "returned task during synchronous execution");
            return reader;
        }

        #endregion

        #region Private Methods



        private SqlDataReader RunExecuteReaderWithRetry(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            [CallerMemberName] string method = "") =>
            RetryLogicProvider.Execute(
                this,
                () => RunExecuteReader(cmdBehavior, runBehavior, returnStream, method));

        #endregion
    }
}
