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
                executeTask: out Task unused,
                usedCache: out _,
                method: method);

            // @TODO: This really isn't necessary...
            Debug.Assert(unused == null, "returned task during synchronous execution");
            return reader;
        }

        #endregion

        #region Private Methods

        // @TODO: We're passing way too many arguments around here... can we simplify this some?
        private SqlDataReader RunExecuteReader(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            TaskCompletionSource<object> completion,
            int timeout,
            out Task executeTask,
            out bool usedCache, // @TODO: This can be eliminated if we do not retry via recursion
            bool asyncWrite = false,
            bool isRetry = false,
            [CallerMemberName] string method = "")
        {
            bool isAsync = completion is not null;

            usedCache = false;
            executeTask = null;

            _rowsAffected = -1;
            _rowsAffectedBySpDescribeParameterEncryption = -1;

            if ((cmdBehavior & CommandBehavior.SingleRow) != 0)
            {
                // CommandBehavior.SingleRow implies CommandBehavior.SingleResult
                cmdBehavior |= CommandBehavior.SingleResult;
            }

            // This function may throw for an invalid connection
            if (!isRetry)
            {
                ValidateCommand(isAsync, method);
            }

            // Only call after validate - requires non-null connection!
            CheckNotificationStateAndAutoEnlist();

            SqlStatistics statistics = Statistics;
            if (statistics is not null)
            {
                if ((!IsDirty && IsPrepared && !_hiddenPrepare) ||
                    (IsPrepared && _execType == EXECTYPE.PREPAREPENDING))
                {
                    statistics.SafeIncrement(ref statistics._preparedExecs);
                }
                else
                {
                    statistics.SafeIncrement(ref statistics._unpreparedExecs);
                }
            }

            // Reset the encryption related state of the command and its parameters.
            ResetEncryptionState();

            if (IsColumnEncryptionEnabled)
            {
                // @TODO: Make this a separate method

                PrepareForTransparentEncryption(
                    isAsync,
                    timeout,
                    completion,
                    out Task prepareEncryptionTask,
                    asyncWrite: asyncWrite && isAsync,
                    out usedCache,
                    isRetry);

                long firstAttemptStart = ADP.TimerCurrent();
                try
                {
                    return RunExecuteReaderTdsWithTransparentParameterEncryption(
                        cmdBehavior,
                        runBehavior,
                        returnStream,
                        isAsync,
                        timeout,
                        out executeTask,
                        asyncWrite: asyncWrite && isAsync,
                        isRetry,
                        ds: null,
                        describeParameterEncryptionTask: prepareEncryptionTask);
                }
                catch (EnclaveDelegate.RetryableEnclaveQueryExecutionException)
                {
                    if (isRetry)
                    {
                        // Do not retry after the second attempt.
                        throw;
                    }

                    // @TODO: This same pattern is used below. Can we do this without a recursive call?
                    // Retry if the command failed with an appropriate error.
                    // First invalidate the entry from the cache, so that we refresh our encryption
                    // metadata.
                    SqlQueryMetadataCache.GetInstance().InvalidateCacheEntry(this);
                    InvalidateEnclaveSession();

                    return RunExecuteReader(
                        cmdBehavior,
                        runBehavior,
                        returnStream,
                        completion,
                        TdsParserStaticMethods.GetRemainingTimeout(timeout, firstAttemptStart),
                        out executeTask,
                        out usedCache,
                        isAsync,
                        isRetry: true,
                        method);
                }
                catch (SqlException ex)
                {
                    // We only want to retry once, so don't retry if we are already in retry.
                    // If we didn't use the cache, we don't want to retry
                    if (isRetry || (!usedCache && !ShouldUseEnclaveBasedWorkflow))
                    {
                        throw;
                    }

                    // Check if we have an error indicating that we can retry.
                    bool shouldRetry = false;
                    foreach (SqlError error in ex.Errors)
                    {
                        if ((usedCache && error.Number == TdsEnums.TCE_CONVERSION_ERROR_CLIENT_RETRY) ||
                            (ShouldUseEnclaveBasedWorkflow && error.Number == TdsEnums.TCE_ENCLAVE_INVALID_SESSION_HANDLE))
                        {
                            shouldRetry = true;
                            break;
                        }
                    }

                    if (!shouldRetry)
                    {
                        throw;
                    }

                    // Retry if the command failed with an appropriate error.
                    // First invalidate the entry from the cache, so that we refresh our encryption
                    // metadata.
                    SqlQueryMetadataCache.GetInstance().InvalidateCacheEntry(this);
                    InvalidateEnclaveSession();

                    return RunExecuteReader(
                        cmdBehavior,
                        runBehavior,
                        returnStream,
                        completion,
                        TdsParserStaticMethods.GetRemainingTimeout(timeout, firstAttemptStart),
                        out executeTask,
                        out usedCache,
                        asyncWrite: isAsync,
                        isRetry: true,
                        method);
                }
            }
            else
            {
                // @TODO: Reminder, this is where we execute if transparent parameter encryption is not needed.
                usedCache = false;
                return RunExecuteReaderTds(
                    cmdBehavior,
                    runBehavior,
                    returnStream,
                    isAsync,
                    timeout,
                    out executeTask,
                    asyncWrite: asyncWrite && isAsync,
                    isRetry);
            }
            // @TODO: CER Exception Handling was removed here (see GH#3581)
        }

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
