// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
        
        private Task InternalExecuteNonQuery(
            TaskCompletionSource<object> completion, // @TODO: Create internally, don't pass in if possible
            bool sendToPipe, // @TODO: Must always be false!
            int timeout,
            out bool usedCache,
            bool asyncWrite = false,
            bool isRetry = false,
            [CallerMemberName] string methodName = "")
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.InternalExecuteNonQuery | INFO | " +
                $"Object Id {ObjectID}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"AsyncCommandInProgress={_activeConnection?.AsyncCommandInProgress}");

            bool isAsync = completion is not null;
            usedCache = false;

            SqlStatistics statistics = Statistics;
            _rowsAffected = -1;
            
            // @TODO: Break into smaller methods ("full" and "simple")

            // This function may throw for an invalid connection
            if (!isRetry)
            {
                ValidateCommand(isAsync, methodName);
            }

            // Only call after validate - requires non-null connection!
            CheckNotificationStateAndAutoEnlist();

            Task task = null;

            Debug.Assert(!sendToPipe, "Trying to send non-context command to pipe");

            // Always Encrypted generally operates only on parameterized queries. However,
            // enclave-based Always encrypted also supports unparameterized queries. We skip
            // this block for enclave-based always encrypted so that we can make a call to SQL
            // Server to get the encryption information
            if (!ShouldUseEnclaveBasedWorkflow && !_batchRPCMode &&
                CommandType is CommandType.Text && GetParameterCount(_parameters) == 0)
            {
                if (statistics is not null)
                {
                    // @TODO: IsDirty contains IsPrepared - this is confusing!
                    if (!IsDirty && IsPrepared)
                    {
                        statistics.SafeIncrement(ref statistics._preparedExecs);
                    }
                    else
                    {
                        statistics.SafeIncrement(ref statistics._unpreparedExecs);
                    }
                }

                // We should never get here for a retry since we only have retries for parameters.
                Debug.Assert(!isRetry);
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.InternalExecuteNonQuery | INFO | " +
                    $"Object Id {ObjectID}," +
                    $" RPC execute method name {methodName}, " +
                    $"isAsync {isAsync}, " +
                    $"isRetry {isRetry}");

                task = RunExecuteNonQueryTds(methodName, isAsync, timeout, asyncWrite);
            }
            else
            {
                // Otherwise, use a full-fledged execute that can handle parameters and sprocs
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.InternalExecuteNonQuery | INFO | " +
                    $"Object Id {ObjectID}, " +
                    $"RPC execute method name {methodName}, " +
                    $"isAsync {isAsync}, " +
                    $"isRetry {isRetry}");

                SqlDataReader reader = RunExecuteReader(
                    CommandBehavior.Default,
                    RunBehavior.UntilDone,
                    returnStream: false,
                    completion,
                    timeout,
                    out task,
                    out usedCache,
                    asyncWrite,
                    isRetry,
                    methodName);

                if (reader is not null)
                {
                    if (task is not null)
                    {
                        task = AsyncHelper.CreateContinuationTaskWithState(
                            task,
                            state: reader,
                            onSuccess: static state => ((SqlDataReader)state).Close());
                    }
                    else
                    {
                        reader.Close();
                    }
                }
            }

            return task;
            // @TODO: CER Exception Handling was removed here (see GH#3581)
        }
        
        private Task InternalExecuteNonQueryWithRetry( // @TODO: Task is ignored
            bool sendToPipe,
            int timeout,
            out bool usedCache, // @TODO: Always discarded!
            bool asyncWrite,
            bool isRetry,
            [CallerMemberName] string methodName = "")
        {
            bool innerUsedCache = false;
            Task result = RetryLogicProvider.Execute(
                sender: this,
                function: () => InternalExecuteNonQuery(
                    completion: null,
                    sendToPipe,
                    timeout,
                    out innerUsedCache,
                    asyncWrite,
                    isRetry,
                    methodName));
            
            usedCache = innerUsedCache;
            return result;
        }
        
        // @TODO: Sort args, drop TDS from name
        private Task RunExecuteNonQueryTds(string methodName, bool isAsync, int timeout, bool asyncWrite)
        {
            Debug.Assert(!asyncWrite || isAsync, "AsyncWrite should be always accompanied by Async");

            bool processFinallyBlock = true;
            try
            {
                Task reconnectTask = _activeConnection.ValidateAndReconnect(beforeDisconnect: null, timeout);
                if (reconnectTask is not null)
                {
                    long reconnectionStart = ADP.TimerCurrent();
                    if (isAsync)
                    {
                        TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
                        _activeConnection.RegisterWaitingForReconnect(completion.Task);
                        _reconnectionCompletionSource = completion;
                        RunExecuteNonQueryTdsSetupReconnnectContinuation(
                            methodName,
                            isAsync,
                            timeout,
                            asyncWrite,
                            reconnectTask,
                            reconnectionStart,
                            completion);
                        return completion.Task;
                    }
                    else
                    {
                        AsyncHelper.WaitForCompletion(
                            reconnectTask,
                            timeout,
                            static () => throw SQL.CR_ReconnectTimeout());
                        timeout = TdsParserStaticMethods.GetRemainingTimeout(timeout, reconnectionStart);
                    }
                }

                if (asyncWrite)
                {
                    _activeConnection.AddWeakReference(this, SqlReferenceCollection.CommandTag);
                }

                GetStateObject();

                // Reset the encryption state in case it has been set by a previous command.
                ResetEncryptionState();

                // We just send over the raw text with no annotation - no parameters are sent over,
                // no data reader is returned.
                SqlClientEventSource.Log.TryTraceEvent(
                    "SqlCommand.RunExecuteNonQueryTds | Info | " +
                    $"Object Id {ObjectID}, " +
                    $"Activity Id {ActivityCorrelator.Current}, " +
                    $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                    $"Command executed as SQLBATCH, Command Text '{CommandText}'");

                Task executeTask = _stateObj.Parser.TdsExecuteSQLBatch(
                    CommandText,
                    timeout,
                    Notification,
                    _stateObj,
                    sync: true);
                Debug.Assert(executeTask is null, "Shouldn't get a task when doing sync writes");

                NotifyDependency();
                if (isAsync)
                {
                    _activeConnection.GetOpenTdsConnection(methodName).IncrementAsyncCount();
                }
                else
                {
                    Debug.Assert(_stateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
                    TdsOperationStatus result = _stateObj.Parser.TryRun(
                        RunBehavior.UntilDone,
                        cmdHandler: this,
                        dataStream: null,
                        bulkCopyHandler: null,
                        _stateObj,
                        out _);

                    if (result is not TdsOperationStatus.Done)
                    {
                        throw SQL.SynchronousCallMayNotPend();
                    }
                }
            }
            catch (Exception e)
            {
                processFinallyBlock = ADP.IsCatchableExceptionType(e);
                throw;
            }
            finally
            {
                if (processFinallyBlock && !isAsync)
                {
                    // When executing Async, we need to keep the _stateObj alive...
                    PutStateObject();
                }
            }

            return null;
        }
        
        #endregion
    }
}
