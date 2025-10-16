// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Utilities;

#if NETFRAMEWORK
using System.Security.Permissions;
#endif

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="default"]/*'/>
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public IAsyncResult BeginExecuteReader() =>
            BeginExecuteReader(callback: null, stateObject: null, CommandBehavior.Default);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndstateObject"]/*'/>
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public IAsyncResult BeginExecuteReader(AsyncCallback callback, object stateObject) =>
            BeginExecuteReader(callback, stateObject, CommandBehavior.Default);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="AsyncCallbackAndstateObjectAndCommandBehavior"]/*'/>
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public IAsyncResult BeginExecuteReader(AsyncCallback callback, object stateObject, CommandBehavior behavior)
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif

            SqlClientEventSource.Log.TryCorrelationTraceEvent("SqlCommand.BeginExecuteReader | API | Correlation | Object Id {0}, Behavior {1}, Activity Id {2}, Client Connection Id {3}, Command Text '{4}'", ObjectID, (int)behavior, ActivityCorrelator.Current, Connection?.ClientConnectionId, CommandText);
            return BeginExecuteReaderInternal(behavior, callback, stateObject, 0, isRetry: false);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteReader[@name="CommandBehavior"]/*'/>
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public IAsyncResult BeginExecuteReader(CommandBehavior behavior) =>
            BeginExecuteReader(callback: null, stateObject: null, behavior);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteReader[@name="IAsyncResult2"]/*'/>
        public SqlDataReader EndExecuteReader(IAsyncResult asyncResult)
        {
            try
            {
                return EndExecuteReaderInternal(asyncResult);
            }
            finally
            {
                SqlClientEventSource.Log.TryCorrelationTraceEvent(
                    "SqlCommand.EndExecuteReader | API | Correlation | " +
                    $"Object Id {ObjectID}, " +
                    $"Activity Id {ActivityCorrelator.Current}, " +
                    $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                    $"Command Text '{CommandText}'");
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReader[@name="default"]/*'/>
        public new SqlDataReader ExecuteReader()
        {
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteReader | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
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
            Guid operationId = s_diagnosticListener.WriteCommandBefore(this, _transaction);
            Exception e = null;

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
                e = ex;

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

                if (e is not null)
                {
                    s_diagnosticListener.WriteCommandError(operationId, this, _transaction, e);
                }
                else
                {
                    s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                }
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="default"]/*'/>
        public new Task<SqlDataReader> ExecuteReaderAsync() =>
            ExecuteReaderAsync(CommandBehavior.Default, CancellationToken.None);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="CancellationToken"]/*'/>
        public new Task<SqlDataReader> ExecuteReaderAsync(CancellationToken cancellationToken) =>
            ExecuteReaderAsync(CommandBehavior.Default, cancellationToken);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="CommandBehavior"]/*'/>
        public new Task<SqlDataReader> ExecuteReaderAsync(CommandBehavior behavior) =>
            ExecuteReaderAsync(behavior, CancellationToken.None);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteReaderAsync[@name="commandBehaviorAndCancellationToken"]/*'/>
        public new Task<SqlDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) =>
            IsProviderRetriable
                ? InternalExecuteReaderWithRetryAsync(behavior, cancellationToken)
                : InternalExecuteReaderAsync(behavior, cancellationToken);

        // @TODO: This is only used for synchronous execution
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteDbDataReader[@name="CommandBehavior"]/*'/>
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            // @TODO: Yknow, we use this all over the place. It could be factored out.
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteDbDataReader | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            return ExecuteReader(behavior);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteDbDataReaderAsync/*'/>
        protected override Task<DbDataReader> ExecuteDbDataReaderAsync(
            CommandBehavior behavior,
            CancellationToken cancellationToken)
        {
            return ExecuteReaderAsync(behavior, cancellationToken)
                .ContinueWith<DbDataReader>(
                    static result =>
                    {
                        if (result.IsFaulted)
                        {
                            throw result.Exception.InnerException;
                        }

                        return result.Result;
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnCanceled,
                    TaskScheduler.Default);
        }

        private IAsyncResult BeginExecuteReaderInternal(
            CommandBehavior behavior,
            AsyncCallback callback,
            object stateObject,
            int timeout,
            bool isRetry,
            bool asyncWrite = false)
        {
            TaskCompletionSource<object> globalCompletion = new TaskCompletionSource<object>(stateObject);
            TaskCompletionSource<object> localCompletion = new TaskCompletionSource<object>(stateObject);

            if (!isRetry)
            {
                // Reset _pendingCancel upon entry into any Execute - used to synchronize state
                // between entry into Execute* API and the thread obtaining the stateObject.
                _pendingCancel = false;
            }

            SqlStatistics statistics = null;
            try
            {
                if (!isRetry)
                {
                    statistics = SqlStatistics.StartTimer(Statistics);
                    WriteBeginExecuteEvent();

                    // Special case - done outside of try/catches to prevent putting a stateObj
                    // back into pool when we should not.
                    ValidateAsyncCommand();
                }

                bool usedCache = false;
                Task writeTask = null;
                try
                {
                    // InternalExecuteNonQuery already has reliability block, but if failure will
                    // not put stateObj back into pool.
                    RunExecuteReader(
                        behavior,
                        RunBehavior.ReturnImmediately,
                        returnStream: true,
                        localCompletion,
                        timeout,
                        out writeTask,
                        out usedCache,
                        asyncWrite,
                        isRetry,
                        nameof(BeginExecuteReader));
                }
                catch (Exception e)
                {
                    if (!ADP.IsCatchableOrSecurityExceptionType(e))
                    {
                        // If not catchable - the connection has already been caught and doomed in
                        // RunExecuteReader.
                        throw;
                    }

                    // For async, RunExecuteReader will never put the stateObj back into the pool,
                    // so, do so now.
                    ReliablePutStateObject();
                    if (isRetry || e is not EnclaveDelegate.RetryableEnclaveQueryExecutionException)
                    {
                        throw;
                    }
                }

                if (writeTask is not null)
                {
                    AsyncHelper.ContinueTaskWithState(
                        taskToContinue: writeTask,
                        taskCompletionSource: localCompletion,
                        state1: this,
                        state2: localCompletion,
                        onSuccess: static (this2, localCompletion2) =>
                            this2.BeginExecuteReaderInternalReadStage(localCompletion2));
                }
                else
                {
                    BeginExecuteReaderInternalReadStage(localCompletion);
                }

                // When we use query caching for parameter encryption we need to retry on specific errors.
                // In these cases finalize the call internally and trigger a retry when needed.
                // @TODO: This is way too big to be done as an if statement.
                if (
                    !TriggerInternalEndAndRetryIfNecessary(
                        behavior,
                        stateObject,
                        timeout,
                        usedCache,
                        isRetry,
                        asyncWrite,
                        globalCompletion,
                        localCompletion,
                        endFunc: static (SqlCommand command, IAsyncResult asyncResult, bool isInternal, string endMethod) =>
                        {
                            return command.InternalEndExecuteReader(asyncResult, isInternal, endMethod);
                        },
                        retryFunc: static (SqlCommand command, CommandBehavior behavior, AsyncCallback callback, object stateObject, int timeout, bool isRetry, bool asyncWrite) =>
                        {
                            return command.BeginExecuteReaderInternal(behavior, callback, stateObject, timeout, isRetry, asyncWrite);
                        },
                        nameof(EndExecuteReader)))
                {
                    globalCompletion = localCompletion;
                }

                // Add callback after work is done to avoid overlapping Begin/End methods
                if (callback is not null)
                {
                    globalCompletion.Task.ContinueWith(
                        static (task, state) => ((AsyncCallback)state)(task),
                        state: callback);
                }

                return globalCompletion.Task;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        private void BeginExecuteReaderInternalReadStage(TaskCompletionSource<object> completion)
        {
            Debug.Assert(completion is not null, "CompletionSource should not be null");

            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.BeginExecuteReaderInternalReadStage | INFO | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            // Read SNI does not have catches for async exceptions, handle here.
            try
            {
                // Must finish caching information before ReadSni which can activate the callback
                // before returning
                CachedAsyncState.SetActiveConnectionAndResult(completion, nameof(EndExecuteReader), _activeConnection);
                _stateObj.ReadSni(completion);
            }
            // @TODO: CER Exception Handling was removed here (see GH#3581)
            catch (Exception e)
            {
                // Similarly, if an exception occurs put the stateObj back into the pool.
                // and reset async cache information to allow a second async execute
                CachedAsyncState?.ResetAsyncState();
                ReliablePutStateObject();
                completion.TrySetException(e);
            }
        }

        /// <summary>
        /// Build the RPC record header for sp_execute.
        /// </summary>
        /// <remarks>
        /// Prototype for sp_execute is:
        /// <c>sp_execute(@handle int, param1value, param2value...)</c>
        /// </remarks>
        private _SqlRPC BuildExecute(bool inSchema)
        {
            Debug.Assert(_prepareHandle != s_cachedInvalidPrepareHandle, "Invalid call to sp_execute without a valid handle!");

            const int systemParameterCount = 1;
            int userParameterCount = CountSendableParameters(_parameters);

            _SqlRPC rpc = null;
            GetRPCObject(systemParameterCount, userParameterCount, ref rpc);
            rpc.ProcID = TdsEnums.RPC_PROCID_EXECUTE;
            rpc.rpcName = TdsEnums.SP_EXECUTE;

            // @handle
            SqlParameter sqlParam = rpc.systemParams[0];
            sqlParam.SqlDbType = SqlDbType.Int;
            sqlParam.Size = 4;
            sqlParam.Value = _prepareHandle;
            sqlParam.Direction = ParameterDirection.Input;

            SetUpRPCParameters(rpc, inSchema, _parameters);
            return rpc;
        }

        /// <summary>
        /// Build the RPC record header for sp_executesql and add the parameters.
        /// </summary>
        /// <remarks>
        /// Prototype for sp_executesql is:
        /// sp_executesql(@batch_text nvarchar(4000), @batch_params nvarchar(4000), param1, param2, ...)
        /// </remarks>
        // @TODO Does parameters need to be passed in or can _parameters be used?
        // @TODO: Can we return the RPC here like BuildExecute does?
        private void BuildExecuteSql(
            CommandBehavior behavior,
            string commandText,
            SqlParameterCollection parameters,
            ref _SqlRPC rpc)
        {
            Debug.Assert(_prepareHandle == s_cachedInvalidPrepareHandle, "This command has an existing handle, use sp_execute!");
            Debug.Assert(CommandType is CommandType.Text, "invalid use of sp_executesql for stored proc invocation!");

            int userParamCount = CountSendableParameters(parameters);
            int systemParamCount = userParamCount > 0 ? 2 : 1;

            GetRPCObject(systemParamCount, userParamCount, ref rpc);
            rpc.ProcID = TdsEnums.RPC_PROCID_EXECUTESQL;
            rpc.rpcName = TdsEnums.SP_EXECUTESQL;

            SqlParameter sqlParam;

            // @batch_text
            commandText ??= GetCommandText(behavior);
            sqlParam = rpc.systemParams[0];
            sqlParam.SqlDbType = (commandText.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT
                ? SqlDbType.NVarChar
                : SqlDbType.NText;
            sqlParam.Size = commandText.Length;
            sqlParam.Value = commandText;
            sqlParam.Direction = ParameterDirection.Input;

            // @batch_params
            if (userParamCount > 0)
            {
                // @TODO: Why does batch RPC mode use different parameters?
                string paramList = BuildParamList(_stateObj.Parser, _batchRPCMode ? parameters : _parameters);
                sqlParam = rpc.systemParams[1];
                sqlParam.SqlDbType = (paramList.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT
                    ? SqlDbType.NVarChar
                    : SqlDbType.NText;
                sqlParam.Size = paramList.Length;
                sqlParam.Value = paramList;
                sqlParam.Direction = ParameterDirection.Input;

                // @TODO: This is passed into BuildRPC ... should we do that or vice versa?
                bool inSchema = (behavior & CommandBehavior.SchemaOnly) != 0;
                SetUpRPCParameters(rpc, inSchema, parameters);
            }
        }

        private _SqlRPC BuildPrepExec(CommandBehavior behavior)
        {
            Debug.Assert(CommandType is CommandType.Text, "invalid use of sp_prepexec for stored proc invocation!");

            const int systemParameterCount = 3;
            int userParameterCount = CountSendableParameters(_parameters);

            _SqlRPC rpc = null;
            GetRPCObject(systemParameterCount, userParameterCount, ref rpc);
            rpc.ProcID = TdsEnums.RPC_PROCID_PREPEXEC;
            rpc.rpcName = TdsEnums.SP_PREPEXEC;

            SqlParameter sqlParam;

            // @handle
            sqlParam = rpc.systemParams[0];
            sqlParam.SqlDbType = SqlDbType.Int;
            sqlParam.Value = _prepareHandle;
            sqlParam.Size = 4;
            sqlParam.Direction = ParameterDirection.InputOutput;
            rpc.systemParamOptions[0] = TdsEnums.RPC_PARAM_BYREF;

            // @batch_params
            string paramList = BuildParamList(_stateObj.Parser, _parameters);
            sqlParam = rpc.systemParams[1];
            // @TODO: This pattern is used quite a bit - it could be factored out
            sqlParam.SqlDbType = (paramList.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT
                ? SqlDbType.NVarChar
                : SqlDbType.NText;
            sqlParam.Value = paramList;
            sqlParam.Size = paramList.Length;
            sqlParam.Direction = ParameterDirection.Input;

            // @batch_text
            string text = GetCommandText(behavior);
            sqlParam = rpc.systemParams[2];
            sqlParam.SqlDbType = (text.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT
                ? SqlDbType.NVarChar
                : SqlDbType.NText;
            sqlParam.Size = text.Length;
            sqlParam.Value = text;
            sqlParam.Direction = ParameterDirection.Input;

            SetUpRPCParameters(rpc, inSchema: false, _parameters);
            return rpc;
        }

        /// <summary>
        /// Build the RPC record header for this stored proc and add parameters.
        /// </summary>
        // @TODO: Rename to fit guidelines
        // @TODO: Does parameters need to be passed in or can _parameters be used?
        // @TODO: Can we return the RPC here like BuildExecute does?
        private void BuildRPC(bool inSchema, SqlParameterCollection parameters, ref _SqlRPC rpc)
        {
            Debug.Assert(CommandType is CommandType.StoredProcedure, "Command must be a stored proc to execute an RPC");

            int userParameterCount = CountSendableParameters(parameters);
            GetRPCObject(systemParamCount: 0, userParameterCount, ref rpc);
            rpc.ProcID = 0;

            // TDS Protocol allows rpc name with maximum length of 1046 bytes for ProcName
            // 4-part name 1 + 128 + 1 + 1 + 1 + 128 + 1 + 1 + 1 + 128 + 1 + 1 + 1 + 128 + 1 = 523
            // each char takes 2 bytes. 523 * 2 = 1046
            int commandTextLength = ADP.CharSize * CommandText.Length;
            if (commandTextLength <= MaxRPCNameLength)
            {
                // Just use the raw command text
                rpc.rpcName = CommandText;
            }
            else
            {
                throw ADP.InvalidArgumentLength(nameof(CommandText), MaxRPCNameLength);
            }

            SetUpRPCParameters(rpc, inSchema, parameters);
        }

        private void CleanupExecuteReaderAsync(
            Task<SqlDataReader> task,
            TaskCompletionSource<SqlDataReader> source,
            Guid operationId)
        {
            if (task.IsFaulted)
            {
                Exception e = task.Exception.InnerException;

                if (!_parentOperationStarted)
                {
                    s_diagnosticListener.WriteCommandError(operationId, this, _transaction, e);
                }

                source.SetException(e);
            }
            else
            {
                if (!_parentOperationStarted)
                {
                    s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                }

                if (task.IsCanceled)
                {
                    source.SetCanceled();
                }
                else
                {
                    source.SetResult(task.Result);
                }
            }
        }

        private SqlDataReader CompleteAsyncExecuteReader(bool isInternal, bool forDescribeParameterEncryption)
        {
            SqlDataReader reader = CachedAsyncState.CachedAsyncReader;
            Debug.Assert(reader is not null);

            bool processFinallyBlock = true;
            try
            {
                // @TODO: Evaluate if forDescribeParameterEncryption/shouldCacheForAlwaysEncrypted are always opposites
                FinishExecuteReader(
                    reader,
                    CachedAsyncState.CachedRunBehavior,
                    CachedAsyncState.CachedSetOptions,
                    isInternal,
                    forDescribeParameterEncryption,
                    shouldCacheForAlwaysEncrypted: !forDescribeParameterEncryption);
                return reader;
            }
            catch (Exception e)
            {
                processFinallyBlock = ADP.IsCatchableExceptionType(e);
                throw;
            }
            finally
            {
                if (processFinallyBlock)
                {
                    // Don't reset the state for internal End. The user End will do that eventually.
                    if (!isInternal)
                    {
                        CachedAsyncState.ResetAsyncState();
                    }

                    PutStateObject();
                }
            }
        }

        private SqlDataReader EndExecuteReaderAsync(IAsyncResult asyncResult)
        {
            Debug.Assert(!_internalEndExecuteInitiated || _stateObj is null);

            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.EndExecuteReaderAsync | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            Exception asyncException = ((Task)asyncResult).Exception;
            if (asyncException is not null)
            {
                CachedAsyncState?.ResetAsyncState();
                ReliablePutStateObject();

                throw asyncException.InnerException;
            }

            ThrowIfReconnectionHasBeenCanceled();

            // Lock on _stateObj prevents race with close/cancel
            if (!_internalEndExecuteInitiated)
            {
                lock (_stateObj)
                {
                    return EndExecuteReaderInternal(asyncResult);
                }
            }

            return EndExecuteReaderInternal(asyncResult);
        }

        private SqlDataReader EndExecuteReaderInternal(IAsyncResult asyncResult)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.EndExecuteReaderInternal | API | " +
                $"Object Id {ObjectID}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"MARS={_activeConnection?.Parser?.MARSOn}, " +
                $"AsyncCommandInProgress={_activeConnection?.AsyncCommandInProgress}");

            SqlStatistics statistics = null;
            bool success = false;
            int? sqlExceptionNumber = null;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                SqlDataReader result = InternalEndExecuteReader(
                    asyncResult,
                    isInternal: false,
                    nameof(EndExecuteReader));

                success = true;
                return result;
            }
            catch (Exception e)
            {
                if (e is SqlException sqlException)
                {
                    sqlExceptionNumber = sqlException.Number;
                }

                if (CachedAsyncState is not null)
                {
                    CachedAsyncState.ResetAsyncState();
                }

                if (ADP.IsCatchableExceptionType(e))
                {
                    ReliablePutStateObject();
                }

                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                WriteEndExecuteEvent(success, sqlExceptionNumber, synchronous: false);
            }
        }

        private void FinishExecuteReader(
            SqlDataReader ds,
            RunBehavior runBehavior,
            string resetOptionsString,
            bool isInternal,
            bool forDescribeParameterEncryption,
            bool shouldCacheForAlwaysEncrypted = true)
        {
            // If this is not for internal usage, notify the dependency. If we have already
            // initiated the end internally, the reader should be ready, so just return.
            if (!isInternal && !forDescribeParameterEncryption)
            {
                NotifyDependency();

                if (_internalEndExecuteInitiated)
                {
                    Debug.Assert(_stateObj is null);
                    return;
                }
            }

            if (runBehavior is RunBehavior.UntilDone)
            {
                try
                {
                    Debug.Assert(_stateObj._syncOverAsync, "Should not attempt pends in a synchronous call");
                    TdsOperationStatus result = _stateObj.Parser.TryRun(
                        RunBehavior.UntilDone,
                        cmdHandler: this,
                        ds,
                        bulkCopyHandler: null,
                        _stateObj,
                        out _);

                    if (result is not TdsOperationStatus.Done)
                    {
                        throw SQL.SynchronousCallMayNotPend();
                    }
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        if (_inPrepare)
                        {
                            // The flag is expected to be reset by OnReturnValue. We should receive
                            // the handle unless command execution failed. If it fails, move back
                            // to pending state.
                            _inPrepare = false;                  // reset the flag
                            IsDirty = true;                      // mark command as dirty so it will be
                                                                 // prepared next time we're coming through
                            _execType = EXECTYPE.PREPAREPENDING; // reset execution type to pending
                        }
                    }

                    if (ds is not null)
                    {
                        try
                        {
                            ds.Close();
                        }
                        catch (Exception eClose)
                        {
                            Debug.WriteLine($"Received this exception from SqlDataReader.Close() while in another catch block: {eClose}");
                        }
                    }

                    throw;
                }
            }

            // Bind the parser to the reader if we get this far
            if (ds is not null)
            {
                ds.Bind(_stateObj);
                _stateObj = null; // The reader now owns this...
                ds.ResetOptionsString = resetOptionsString;

                // Bind the reader to this connection now
                _activeConnection.AddWeakReference(ds, SqlReferenceCollection.DataReaderTag);

                // Force this command to start reading data off the wire.
                // This will cause an error to be reported at Execute() time instead of Read() time
                // if the command is not set
                try
                {
                    // This flag indicates if the data reader's metadata should be cached in this
                    // SqlCommand. Metadata associated with sp_describe_parameter_metadata's data
                    // reader should not be cached. Ideally, we should be using
                    // "forDescribeParameterEncryption" flag for this, but this flag's semantics
                    // are overloaded with async workflow and this flag is always false for sync
                    // workflow. Since we are very close to a release and changing the semantics
                    // for "forDescribeParameterEncryption" is risky, we introduced a new parameter
                    // to determine whether we should cache a data reader's metadata or not.
                    if (shouldCacheForAlwaysEncrypted)
                    {
                        _cachedMetaData = ds.MetaData;
                    }
                    else
                    {
                        // We need this call to ensure the data reader is properly initialized, the
                        // getter is initializing state in SqlDataReader.
                        _ = ds.MetaData;
                    }

                    // @TODO: Why does the command set whether the reader is initialized??
                    ds.IsInitialized = true;
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        if (_inPrepare)
                        {
                            // The flag is expected to be reset by OnReturnValue. We should receive
                            // the handle unless command execution failed. If it fails, move back
                            // to pending state.
                            _inPrepare = false;                  // reset the flag
                            IsDirty = true;                      // mark command as dirty so it will be prepared next time we're coming through
                            _execType = EXECTYPE.PREPAREPENDING; // reset execution type to pending
                        }

                        try
                        {
                            ds.Close();
                        }
                        catch (Exception eClose)
                        {
                            Debug.WriteLine($"Received this exception from SqlDataReader.Close() while in another catch block: {eClose}");
                        }
                    }

                    throw;
                }
            }
        }

        private void GenerateEnclavePackage()
        {
            // Skip processing if there are no keys to send to enclave
            if (keysToBeSentToEnclave is null || keysToBeSentToEnclave.IsEmpty)
            {
                return;
            }

            // Validate attestation url is provided when necessary
            if (string.IsNullOrWhiteSpace(_activeConnection.EnclaveAttestationUrl) &&
                _activeConnection.AttestationProtocol is not SqlConnectionAttestationProtocol.None)
            {
                throw SQL.NoAttestationUrlSpecifiedForEnclaveBasedQueryGeneratingEnclavePackage(
                    _activeConnection.Parser.EnclaveType);
            }

            // Validate enclave type
            string enclaveType = _activeConnection.Parser.EnclaveType;
            if (string.IsNullOrWhiteSpace(enclaveType))
            {
                throw SQL.EnclaveTypeNullForEnclaveBasedQuery();
            }

            // Validate protocol type
            SqlConnectionAttestationProtocol attestationProtocol = _activeConnection.AttestationProtocol;
            if (attestationProtocol is SqlConnectionAttestationProtocol.NotSpecified)
            {
                throw SQL.AttestationProtocolNotSpecifiedForGeneratingEnclavePackage();
            }

            // Generate the enclave package
            try
            {
                #if DEBUG
                // @TODO: These should be wrapped with something other than DEBUG since we don't even run tests in debug mode
                // Test-only code for forcing a retryable exception to occur
                if (_forceRetryableEnclaveQueryExecutionExceptionDuringGenerateEnclavePackage)
                {
                    _forceRetryableEnclaveQueryExecutionExceptionDuringGenerateEnclavePackage = false;
                    throw new EnclaveDelegate.RetryableEnclaveQueryExecutionException("testing", null);
                }
                #endif

                enclavePackage = EnclaveDelegate.Instance.GenerateEnclavePackage(
                    attestationProtocol,
                    keysToBeSentToEnclave,
                    CommandText,
                    enclaveType,
                    GetEnclaveSessionParameters(),
                    _activeConnection,
                    command: this);
            }
            catch (EnclaveDelegate.RetryableEnclaveQueryExecutionException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw SQL.ExceptionWhenGeneratingEnclavePackage(e);
            }
        }

        private Task<SqlDataReader> InternalExecuteReaderAsync(
            CommandBehavior commandBehavior,
            CancellationToken cancellationToken)
        {
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.InternalExecuteReaderAsync | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Behavior {(int)commandBehavior}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.InternalExecuteReaderAsync | INFO | " +
                $"Object Id {ObjectID}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            Guid operationId = !_parentOperationStarted ? s_diagnosticListener.WriteCommandBefore(this, _transaction) : Guid.Empty;

            // Connection can be used as state in RegisterForConnectionCloseNotification
            // continuation to avoid an allocation so use it as the state value if possible, but it
            // can be changed if you need it for a more important piece of data that justifies the
            // tuple allocation later.
            TaskCompletionSource<SqlDataReader> source = new TaskCompletionSource<SqlDataReader>(_activeConnection);

            CancellationTokenRegistration registration = new CancellationTokenRegistration();
            if (cancellationToken.CanBeCanceled)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return source.Task;
                }

                registration = cancellationToken.Register(s_cancelIgnoreFailure, state: this);
            }

            Task<SqlDataReader> returnedTask = source.Task;
            ExecuteReaderAsyncCallContext context = null;
            try
            {
                returnedTask = RegisterForConnectionCloseNotification(returnedTask);

                if (_activeConnection?.InnerConnection is SqlInternalConnection sqlInternalConnection)
                {
                    context = Interlocked.Exchange(
                        ref sqlInternalConnection.CachedCommandExecuteReaderAsyncContext,
                        null);
                }

                context ??= new ExecuteReaderAsyncCallContext();
                context.Set(this, source, registration, commandBehavior, operationId);

                Task<SqlDataReader>.Factory.FromAsync(
                    beginMethod: static (callback, state) =>
                    {
                        ExecuteReaderAsyncCallContext args = (ExecuteReaderAsyncCallContext)state;
                        return args.Command.BeginExecuteReaderInternal(
                            args.CommandBehavior,
                            callback,
                            state,
                            args.Command.CommandTimeout,
                            isRetry: false, // @TODO: Wait, this *is* a retry if the we're on the retry part of the reliability helper!
                            asyncWrite: true);
                    },
                    endMethod: static asyncResult =>
                    {
                        ExecuteReaderAsyncCallContext args = (ExecuteReaderAsyncCallContext)asyncResult.AsyncState;
                        return args.Command.EndExecuteReaderAsync(asyncResult);
                    },
                    state: context
                ).ContinueWith(
                    static task =>
                    {
                        ExecuteReaderAsyncCallContext context = (ExecuteReaderAsyncCallContext)task.AsyncState;
                        SqlCommand command = context.Command;
                        Guid operationId = context.OperationId;
                        TaskCompletionSource<SqlDataReader> source = context.TaskCompletionSource;

                        context.Dispose();
                        command.CleanupExecuteReaderAsync(task, source, operationId);
                    },
                    scheduler: TaskScheduler.Default);
            }
            catch (Exception e)
            {
                if (!_parentOperationStarted)
                {
                    s_diagnosticListener.WriteCommandError(operationId, this, _transaction, e);
                }

                source.SetException(e);
                context?.Dispose();
            }

            return returnedTask;
        }

        private Task<SqlDataReader> InternalExecuteReaderWithRetryAsync(
            CommandBehavior commandBehavior,
            CancellationToken cancellationToken)
        {
            return RetryLogicProvider.ExecuteAsync(
                sender: this,
                () => InternalExecuteReaderAsync(commandBehavior, cancellationToken),
                cancellationToken);
        }

        private SqlDataReader InternalEndExecuteReader(IAsyncResult asyncResult, bool isInternal, string endMethod)
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.InternalEndExecuteReader | INFO | " +
                $"Object Id {ObjectID}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"MARS={_activeConnection?.Parser?.MARSOn}, " +
                $"AsyncCommandInProgress={_activeConnection?.AsyncCommandInProgress}");

            VerifyEndExecuteState((Task)asyncResult, endMethod);
            WaitForAsyncResults(asyncResult, isInternal);

            // If column encryption is enabled, also check the state after waiting for the task.
            // It would be better to do this for all cases, but avoiding for compatibility reasons.
            if (IsColumnEncryptionEnabled)
            {
                VerifyEndExecuteState((Task)asyncResult, endMethod, fullCheckForColumnEncryption: true);
            }

            CheckThrowSNIException();

            SqlDataReader reader = CompleteAsyncExecuteReader(isInternal, forDescribeParameterEncryption: false);
            Debug.Assert(_stateObj is null, "non-null state object in InternalEndExecuteReader");
            return reader;
            // @TODO: CER Exception Handling was removed here (see GH#3581)
        }

        // @TODO: We're passing way too many arguments around here... can we simplify this some?
        // task is created in case of pending asynchronous write, returned SqlDataReader should not be utilized until that task is complete
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

        // @TODO: This method *needs* to be broken up into separate ones for each type of execution
        private SqlDataReader RunExecuteReaderTds(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            bool isAsync,
            int timeout,
            out Task task,
            bool asyncWrite,
            bool isRetry, // @TODO: This isn't used in netfx?
            SqlDataReader ds = null,
            bool describeParameterEncryptionRequest = false) // @TODO: This is hidden by an overload
        {
            Debug.Assert(!asyncWrite || isAsync, "AsyncWrite should be always accompanied by Async");

            if (ds == null && returnStream)
            {
                ds = new SqlDataReader(this, cmdBehavior);
            }

            Task reconnectTask = _activeConnection.ValidateAndReconnect(beforeDisconnect: null, timeout);
            if (reconnectTask is not null)
            {
                long reconnectionStart = ADP.TimerCurrent();
                if (isAsync)
                {
                    TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
                    _activeConnection.RegisterWaitingForReconnect(completion.Task);
                    _reconnectionCompletionSource = completion;

                    // Sets up a recursive call
                    RunExecuteReaderTdsSetupReconnectContinuation(
                        cmdBehavior,
                        runBehavior,
                        returnStream,
                        isAsync,
                        timeout,
                        asyncWrite,
                        isRetry,
                        ds,
                        reconnectTask,
                        reconnectionStart,
                        completion);

                    task = completion.Task;
                    return ds;
                }
                else
                {
                    AsyncHelper.WaitForCompletion(
                        reconnectTask,
                        timeout,
                        onTimeout: static () => throw SQL.CR_ReconnectTimeout());
                    timeout = TdsParserStaticMethods.GetRemainingTimeout(timeout, reconnectionStart);
                }
            }

            // Make sure we have good parameter information
            Debug.Assert(_activeConnection.Parser is not null, "TdsParser class should not be null in Command.Execute!");

            bool inSchema = (cmdBehavior & CommandBehavior.SchemaOnly) != 0;

            // Create a new RPC
            _SqlRPC rpc = null;

            task = null;
            string optionSettings = null;
            bool processFinallyBlock = true;
            bool decrementAsyncCountOnFailure = false;

            if (isAsync)
            {
                _activeConnection.GetOpenTdsConnection().IncrementAsyncCount();
                decrementAsyncCountOnFailure = true;
            }

            try
            {
                if (asyncWrite)
                {
                    _activeConnection.AddWeakReference(this, SqlReferenceCollection.CommandTag);
                }

                GetStateObject();
                Task writeTask;

                if (describeParameterEncryptionRequest)
                {
                    // @TODO: Execute as encrypted RPC
                    #if DEBUG
                    if (_sleepDuringRunExecuteReaderTdsForSpDescribeParameterEncryption)
                    {
                        Thread.Sleep(10000);
                    }
                    #endif

                    Debug.Assert(_sqlRPCParameterEncryptionReqArray is not null, "RunExecuteReader rpc array not provided for describe parameter encryption request.");
                    writeTask = _stateObj.Parser.TdsExecuteRPC(
                        this,
                        _sqlRPCParameterEncryptionReqArray,
                        timeout,
                        inSchema,
                        Notification,
                        _stateObj,
                        CommandType is CommandType.StoredProcedure,
                        sync: !asyncWrite);
                }
                else if (_batchRPCMode)
                {
                    // @TODO: Execute as batch RPC
                    Debug.Assert(inSchema == false, "Batch RPC does not support schema only command behavior");
                    Debug.Assert(!IsPrepared, "Batch RPC should not be prepared!");
                    Debug.Assert(!IsDirty, "Batch RPC should not be marked as dirty!");
                    Debug.Assert(_RPCList != null, "RunExecuteReader rpc array not provided");
                    writeTask = _stateObj.Parser.TdsExecuteRPC(
                        this,
                        _RPCList,
                        timeout,
                        inSchema,
                        Notification,
                        _stateObj,
                        CommandType is CommandType.StoredProcedure,
                        sync: !asyncWrite);
                }
                else if (CommandType is CommandType.Text && GetParameterCount(_parameters) == 0)
                {
                    // @TODO: Execute as batch (or text without parameters)
                    // Send over SQL Batch command if we are not a stored proc and have no parameters
                    Debug.Assert(!IsUserPrepared, "CommandType.Text with no params should not be prepared!");

                    if (returnStream)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            "SqlCommand.RunExecuteReaderTds | Info | " +
                            $"Object Id {ObjectID}, " +
                            $"Activity Id {ActivityCorrelator.Current}, " +
                            $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                            $"Command executed as SQLBATCH, " +
                            $"Command Text '{CommandText}'");
                    }

                    string text = GetCommandText(cmdBehavior) + GetResetOptionsString(cmdBehavior);

                    // If the query requires enclave computations, pass the enclave package in the
                    // SqlBatch TDS stream
                    if (requiresEnclaveComputations)
                    {
                        if (enclavePackage is null)
                        {
                            throw SQL.NullEnclavePackageForEnclaveBasedQuery(
                                _activeConnection?.Parser.EnclaveType,
                                _activeConnection?.EnclaveAttestationUrl);
                        }

                        writeTask = _stateObj.Parser.TdsExecuteSQLBatch(
                            text,
                            timeout,
                            Notification,
                            _stateObj,
                            sync: !asyncWrite,
                            enclavePackage: enclavePackage.EnclavePackageBytes);
                    }
                    else
                    {
                        writeTask = _stateObj.Parser.TdsExecuteSQLBatch(
                            text,
                            timeout,
                            Notification,
                            _stateObj,
                            sync: !asyncWrite,
                            enclavePackage: null);
                    }
                }
                else if (CommandType is CommandType.Text)
                {
                    // @TODO: Execute as RPC (or text with parameters)
                    if (IsDirty)
                    {
                        // Can have cached metadata if dirty because of parameters
                        Debug.Assert(_cachedMetaData == null || !_dirty, "dirty query should not have cached metadata!");

                        // Someone changed the command text or the parameter schema so we must
                        // unprepare the command
                        // remember that IsDirty includes test for IsPrepared! @TODO: Very confusing!
                        if (_execType is EXECTYPE.PREPARED)
                        {
                            _hiddenPrepare = true;
                        }

                        Unprepare();
                        IsDirty = false;
                    }

                    if (_execType is EXECTYPE.PREPARED)
                    {
                        Debug.Assert(IsPrepared && _prepareHandle != s_cachedInvalidPrepareHandle, "invalid attempt to call sp_execute without a handle!");
                        rpc = BuildExecute(inSchema);
                    }
                    else if (_execType is EXECTYPE.PREPAREPENDING)
                    {
                        rpc = BuildPrepExec(cmdBehavior);

                        // Next time through, only do an exec
                        _execType = EXECTYPE.PREPARED;
                        _preparedConnectionCloseCount = _activeConnection.CloseCount;
                        _preparedConnectionReconnectCount = _activeConnection.ReconnectCount;

                        // Mark ourselves as preparing the command
                        _inPrepare = true;
                    }
                    else
                    {
                        Debug.Assert(_execType is EXECTYPE.UNPREPARED, "Invalid execType!");
                        BuildExecuteSql(cmdBehavior, commandText: null, _parameters, ref rpc);
                    }

                    rpc.options = TdsEnums.RPC_NOMETADATA;
                    if (returnStream)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            "SqlCommand.RunExecuteReaderTds | Info | " +
                            $"Object Id {ObjectID}, " +
                            $"Activity Id {ActivityCorrelator.Current}, " +
                            $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                            $"Command executed as RPC, " +
                            $"RPC Name '{rpc.rpcName}'");
                    }

                    Debug.Assert(_rpcArrayOf1[0] == rpc);
                    writeTask = _stateObj.Parser.TdsExecuteRPC(
                        this,
                        _rpcArrayOf1,
                        timeout,
                        inSchema,
                        Notification,
                        _stateObj,
                        CommandType is CommandType.StoredProcedure, // @TODO: uhhhhh this shouldn't ever be true?
                        sync: !asyncWrite);
                }
                else
                {
                    // @TODO: Execute as sproc
                    Debug.Assert(CommandType is CommandType.StoredProcedure, "unknown command type!");

                    BuildRPC(inSchema, _parameters, ref rpc);

                    // If we need to augment the command because a user has changed the command
                    // behavior (e.g. FillSchema) then batch sql them over. This is inefficient (3
                    // round trips) but the only way we can get metadata only from a stored proc.
                    optionSettings = GetSetOptionsString(cmdBehavior);

                    if (returnStream)
                    {
                        SqlClientEventSource.Log.TryTraceEvent(
                            "SqlCommand.RunExecuteReaderTds | Info | " +
                            $"Object Id {ObjectID}, " +
                            $"Activity Id {ActivityCorrelator.Current}, " +
                            $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                            $"Command executed as RPC, " +
                            $"RPC Name '{CommandText}'");
                    }

                    // Turn set options ON
                    if (optionSettings is not null)
                    {
                        Task executeTask = _stateObj.Parser.TdsExecuteSQLBatch(
                            optionSettings,
                            timeout,
                            Notification,
                            _stateObj,
                            sync: true);

                        Debug.Assert(executeTask is null, "Shouldn't get a task when doing sync writes");
                        Debug.Assert(_stateObj._syncOverAsync, "Should not attempt pends in a synchronous call");

                        TdsOperationStatus result = _stateObj.Parser.TryRun(
                            RunBehavior.UntilDone,
                            cmdHandler: this,
                            dataStream: null,
                            bulkCopyHandler: null,
                            _stateObj,
                            out bool _);
                        if (result is not TdsOperationStatus.Done)
                        {
                            throw SQL.SynchronousCallMayNotPend();
                        }

                        // And turn OFF when the ds exhausts the stream on Close()
                        optionSettings = GetResetOptionsString(cmdBehavior);
                    }

                    // Execute sproc
                    Debug.Assert(_rpcArrayOf1[0] == rpc);
                    writeTask = _stateObj.Parser.TdsExecuteRPC(
                        this,
                        _rpcArrayOf1,
                        timeout,
                        inSchema,
                        Notification,
                        _stateObj,
                        isCommandProc: CommandType is CommandType.StoredProcedure, // @TODO: This should always be true...
                        sync: !asyncWrite);
                }

                Debug.Assert(writeTask is null || isAsync, "Returned task in sync mode");

                if (isAsync)
                {
                    decrementAsyncCountOnFailure = false;
                    if (writeTask is not null)
                    {
                        task = RunExecuteReaderTdsSetupContinuation(runBehavior, ds, optionSettings, writeTask);
                    }
                    else
                    {
                        CachedAsyncState.SetAsyncReaderState(ds, runBehavior, optionSettings);
                    }
                }
                else
                {
                    // Always execute - even if no reader!
                    FinishExecuteReader(
                        ds,
                        runBehavior,
                        optionSettings,
                        isInternal: false,
                        forDescribeParameterEncryption: false,
                        shouldCacheForAlwaysEncrypted: !describeParameterEncryptionRequest);
                }
            }
            catch (Exception e)
            {
                processFinallyBlock = ADP.IsCatchableExceptionType(e);

                if (decrementAsyncCountOnFailure)
                {
                    if (_activeConnection.InnerConnection is SqlInternalConnectionTds innerConnectionTds)
                    {
                        // It may be closed
                        innerConnectionTds.DecrementAsyncCount();
                    }
                }

                throw;
            }
            finally
            {
                if (processFinallyBlock && !isAsync)
                {
                    // When executing async, we need to keep the _stateObj alive...
                    PutStateObject();
                }
            }

            Debug.Assert(isAsync || _stateObj == null, "non-null state object in RunExecuteReader");
            return ds;
        }

        private Task RunExecuteReaderTdsSetupContinuation(
            RunBehavior runBehavior,
            SqlDataReader ds,
            string optionSettings,
            Task writeTask)
        {
            return AsyncHelper.CreateContinuationTaskWithState(
                taskToContinue: writeTask,
                state1: this,
                state2: Tuple.Create(ds, runBehavior, optionSettings),
                onSuccess: static (this2, parameters) =>
                {
                    // This will throw if the connection is closed.
                    // @TODO: So... can we have something that specifically does that?
                    this2._activeConnection.GetOpenTdsConnection();
                    this2.CachedAsyncState.SetAsyncReaderState(parameters.Item1, parameters.Item2, parameters.Item3);
                },
                onFailure: static (this2, _, exception) =>
                    this2._activeConnection.GetOpenTdsConnection().DecrementAsyncCount());
        }

        // @TODO: This is way too many parameters being shoveled back and forth. We can do better.
        private void RunExecuteReaderTdsSetupReconnectContinuation(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            bool isAsync,
            int timeout,
            bool asyncWrite,
            bool isRetry,
            SqlDataReader ds,
            Task reconnectTask,
            long reconnectionStart,
            TaskCompletionSource<object> completion) // @TODO: I think this can be an untyped TCS.
        {
            CancellationTokenSource timeoutCts = new CancellationTokenSource();
            AsyncHelper.SetTimeoutException(
                completion,
                timeout,
                onFailure: static () => SQL.CR_ReconnectTimeout(),
                timeoutCts.Token);

            // @TODO: With an object to pass around we can use the state-based version
            AsyncHelper.ContinueTask(
                reconnectTask,
                completion,
                onSuccess: () =>
                {
                    if (completion.Task.IsCompleted)
                    {
                        return;
                    }

                    Interlocked.CompareExchange(ref _reconnectionCompletionSource, null, completion);
                    timeoutCts.Cancel();

                    RunExecuteReaderTds(
                        cmdBehavior,
                        runBehavior,
                        returnStream,
                        isAsync,
                        TdsParserStaticMethods.GetRemainingTimeout(timeout, reconnectionStart),
                        out Task subTask,
                        asyncWrite,
                        isRetry,
                        ds);

                    if (subTask is null)
                    {
                        completion.SetResult(null);
                    }
                    else
                    {
                        AsyncHelper.ContinueTaskWithState(
                            taskToContinue: subTask,
                            taskCompletionSource: completion,
                            state: completion,
                            onSuccess: static state => state.SetResult(null));
                    }
                });
        }

        private SqlDataReader RunExecuteReaderTdsWithTransparentParameterEncryption(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            bool isAsync,
            int timeout,
            out Task task,
            bool asyncWrite,
            bool isRetry,
            SqlDataReader ds = null,
            Task describeParameterEncryptionTask = null) // @TODO: This task should likely come from this method otherwise this is just setting up a continuation
        {
            Debug.Assert(!asyncWrite || isAsync, "AsyncWrite should be always accompanied by Async");

            if (ds is null && returnStream)
            {
                ds = new SqlDataReader(command: this, cmdBehavior);
            }

            if (describeParameterEncryptionTask is not null)
            {
                // @TODO: I guess this means async execution? Using tasks as the primary means of determining async vs sync is clunky. It would be better to have separate async vs sync pathways.
                long parameterEncryptionStart = ADP.TimerCurrent();

                // @TODO: This can totally be a non-generic TCS
                // @TODO: This is a prime candidate for proper async-await execution
                TaskCompletionSource<object> completion = new TaskCompletionSource<object>();
                AsyncHelper.ContinueTaskWithState(
                    taskToContinue: describeParameterEncryptionTask,
                    taskCompletionSource: completion,
                    state: this,
                    onSuccess: this2 =>
                    {
                        this2.GenerateEnclavePackage();
                        this2.RunExecuteReaderTds(
                            cmdBehavior,
                            runBehavior,
                            returnStream,
                            isAsync,
                            TdsParserStaticMethods.GetRemainingTimeout(timeout, parameterEncryptionStart),
                            out Task subTask,
                            asyncWrite,
                            isRetry,
                            ds);

                        if (subTask is null)
                        {
                            // @TODO: Why would this ever be the case? We should structure this so that it doesn't need to be checked.
                            completion.SetResult(null);
                        }
                        else
                        {
                            AsyncHelper.ContinueTaskWithState(
                                taskToContinue: subTask,
                                taskCompletionSource: completion,
                                state: completion,
                                onSuccess: static state => state.SetResult(null));
                        }
                    },
                    onFailure: static (this2, exception) =>
                    {
                        this2.CachedAsyncState?.ResetAsyncState();
                        if (exception is not null)
                        {
                            // @TODO: This doesn't do anything, afaik.
                            throw exception;
                        }
                    },
                    onCancellation: static this2 => this2.CachedAsyncState?.ResetAsyncState());

                task = completion.Task;
                return ds;
            }
            else
            {
                // Synchronous execution
                GenerateEnclavePackage();
                return RunExecuteReaderTds(
                    cmdBehavior,
                    runBehavior,
                    returnStream,
                    isAsync,
                    timeout,
                    out task,
                    asyncWrite,
                    isRetry,
                    ds);
            }
        }

        private SqlDataReader RunExecuteReaderWithRetry(
            CommandBehavior cmdBehavior,
            RunBehavior runBehavior,
            bool returnStream,
            [CallerMemberName] string method = "") =>
            RetryLogicProvider.Execute(
                this,
                () => RunExecuteReader(cmdBehavior, runBehavior, returnStream, method));

        private void SetCachedCommandExecuteReaderAsyncContext(ExecuteReaderAsyncCallContext instance)
        {
            if (_activeConnection?.InnerConnection is SqlInternalConnection sqlInternalConnection)
            {
                // @TODO: This should be part of the sql internal connection class.
                Interlocked.CompareExchange(
                    ref sqlInternalConnection.CachedCommandExecuteReaderAsyncContext,
                    instance,
                    null);
            }
        }

        #endregion

        internal sealed class ExecuteReaderAsyncCallContext
            : AAsyncCallContext<SqlCommand, SqlDataReader, CancellationTokenRegistration>
        {
            public SqlCommand Command => _owner;

            public CommandBehavior CommandBehavior { get; set; }

            public Guid OperationId { get; set; }

            public TaskCompletionSource<SqlDataReader> TaskCompletionSource => _source;

            public void Set(
                SqlCommand command,
                TaskCompletionSource<SqlDataReader> source,
                CancellationTokenRegistration disposable,
                CommandBehavior behavior,
                Guid operationId)
            {
                base.Set(command, source, disposable);
                CommandBehavior = behavior;
                OperationId = operationId;
            }

            protected override void AfterCleared(SqlCommand owner)
            {
                owner?.SetCachedCommandExecuteReaderAsyncContext(this);
            }

            protected override void Clear()
            {
                OperationId = Guid.Empty;
                CommandBehavior = CommandBehavior.Default;
            }
        }
    }
}
