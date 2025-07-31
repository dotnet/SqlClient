// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
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
                            $"Client Connection Id {_activeConnection.ClientConnectionId}, " +
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

                        // And turn OF Fwhen the ds exhausts the stream on Close()
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
