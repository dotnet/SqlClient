// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Diagnostics;

// NOTE: The current Microsoft.VSDesigner editor attributes are implemented for System.Data.SqlClient, and are not publicly available.
// New attributes that are designed to work with Microsoft.Data.SqlClient and are publicly documented should be included in future.
namespace Microsoft.Data.SqlClient
{
    // TODO: Add designer attribute when Microsoft.VSDesigner.Data.VS.SqlCommandDesigner uses Microsoft.Data.SqlClient
    public sealed partial class SqlCommand : DbCommand, ICloneable
    {
        private const int MaxRPCNameLength = 1046;

        /// <summary>
        /// Indicates if the column encryption setting was set at-least once in the batch rpc mode, when using AddBatchCommand.
        /// </summary>
        private bool _wasBatchModeColumnEncryptionSettingSetOnce;

#if DEBUG
        /// <summary>
        /// Force the client to sleep during sp_describe_parameter_encryption in the function TryFetchInputParameterEncryptionInfo.
        /// </summary>
        private static bool _sleepDuringTryFetchInputParameterEncryptionInfo = false;

        /// <summary>
        /// Force the client to sleep during sp_describe_parameter_encryption in the function RunExecuteReaderTds.
        /// </summary>
        private static bool _sleepDuringRunExecuteReaderTdsForSpDescribeParameterEncryption = false;

        /// <summary>
        /// Force the client to sleep during sp_describe_parameter_encryption after ReadDescribeEncryptionParameterResults.
        /// </summary>
        private static bool _sleepAfterReadDescribeEncryptionParameterResults = false;

        /// <summary>
        /// Internal flag for testing purposes that forces all queries to internally end async calls.
        /// </summary>
        private static bool _forceInternalEndQuery = false;

        /// <summary>
        /// Internal flag for testing purposes that forces one RetryableEnclaveQueryExecutionException during GenerateEnclavePackage
        /// </summary>
        private static bool _forceRetryableEnclaveQueryExecutionExceptionDuringGenerateEnclavePackage = false;
#endif

        private static readonly SqlDiagnosticListener s_diagnosticListener = new SqlDiagnosticListener();
        private bool _parentOperationStarted = false;

        internal static readonly Action<object> s_cancelIgnoreFailure = CancelIgnoreFailureCallback;

        private _SqlRPC[] _rpcArrayOf1 = null;                // Used for RPC executes
        private _SqlRPC _rpcForEncryption = null;                // Used for sp_describe_parameter_encryption RPC executes

        // cut down on object creation and cache all these
        // cached metadata
        private _SqlMetaDataSet _cachedMetaData;

        // @TODO: Make properties
        internal ConcurrentDictionary<int, SqlTceCipherInfoEntry> keysToBeSentToEnclave;
        internal bool requiresEnclaveComputations = false;

        private bool ShouldCacheEncryptionMetadata
        {
            get
            {
                return !requiresEnclaveComputations || _activeConnection.Parser.AreEnclaveRetriesSupported;
            }
        }

        internal EnclavePackage enclavePackage = null;
        private SqlEnclaveAttestationParameters enclaveAttestationParameters = null;
        private byte[] customData = null;
        private int customDataLength = 0;

        // Last TaskCompletionSource for reconnect task - use for cancellation only
        private TaskCompletionSource<object> _reconnectionCompletionSource = null;

#if DEBUG
        internal static int DebugForceAsyncWriteDelay { get; set; }
#endif

        internal bool ShouldUseEnclaveBasedWorkflow =>
            (!string.IsNullOrWhiteSpace(_activeConnection.EnclaveAttestationUrl) || Connection.AttestationProtocol == SqlConnectionAttestationProtocol.None) &&
                  IsColumnEncryptionEnabled;

        /// <summary>
        /// Per-command custom providers. It can be provided by the user and can be set more than once. 
        /// </summary> 
        private IReadOnlyDictionary<string, SqlColumnEncryptionKeyStoreProvider> _customColumnEncryptionKeyStoreProviders;

        internal bool HasColumnEncryptionKeyStoreProvidersRegistered =>
            _customColumnEncryptionKeyStoreProviders is not null && _customColumnEncryptionKeyStoreProviders.Count > 0;

        // Cached info for async executions
        private sealed class AsyncState
        {
            // @TODO: Autoproperties
            private int _cachedAsyncCloseCount = -1;    // value of the connection's CloseCount property when the asyncResult was set; tracks when connections are closed after an async operation
            private TaskCompletionSource<object> _cachedAsyncResult = null;
            private SqlConnection _cachedAsyncConnection = null;  // Used to validate that the connection hasn't changed when end the connection;
            private SqlDataReader _cachedAsyncReader = null;
            private RunBehavior _cachedRunBehavior = RunBehavior.ReturnImmediately;
            private string _cachedSetOptions = null;
            private string _cachedEndMethod = null;

            internal AsyncState()
            {
            }

            internal SqlDataReader CachedAsyncReader
            {
                get { return _cachedAsyncReader; }
            }
            internal RunBehavior CachedRunBehavior
            {
                get { return _cachedRunBehavior; }
            }
            internal string CachedSetOptions
            {
                get { return _cachedSetOptions; }
            }
            internal bool PendingAsyncOperation
            {
                get { return _cachedAsyncResult != null; }
            }
            internal string EndMethodName
            {
                get { return _cachedEndMethod; }
            }

            internal bool IsActiveConnectionValid(SqlConnection activeConnection)
            {
                return (_cachedAsyncConnection == activeConnection && _cachedAsyncCloseCount == activeConnection.CloseCount);
            }

            internal void ResetAsyncState()
            {
                SqlClientEventSource.Log.TryTraceEvent("CachedAsyncState.ResetAsyncState | API | ObjectId {0}, Client Connection Id {1}, AsyncCommandInProgress={2}",
                                                       _cachedAsyncConnection?.ObjectID, _cachedAsyncConnection?.ClientConnectionId, _cachedAsyncConnection?.AsyncCommandInProgress);
                _cachedAsyncCloseCount = -1;
                _cachedAsyncResult = null;
                if (_cachedAsyncConnection != null)
                {
                    _cachedAsyncConnection.AsyncCommandInProgress = false;
                    _cachedAsyncConnection = null;
                }
                _cachedAsyncReader = null;
                _cachedRunBehavior = RunBehavior.ReturnImmediately;
                _cachedSetOptions = null;
                _cachedEndMethod = null;
            }

            internal void SetActiveConnectionAndResult(TaskCompletionSource<object> completion, string endMethod, SqlConnection activeConnection)
            {
                Debug.Assert(activeConnection != null, "Unexpected null connection argument on SetActiveConnectionAndResult!");
                TdsParser parser = activeConnection?.Parser;
                SqlClientEventSource.Log.TryTraceEvent("SqlCommand.SetActiveConnectionAndResult | API | ObjectId {0}, Client Connection Id {1}, MARS={2}", activeConnection?.ObjectID, activeConnection?.ClientConnectionId, parser?.MARSOn);
                if ((parser == null) || (parser.State == TdsParserState.Closed) || (parser.State == TdsParserState.Broken))
                {
                    throw ADP.ClosedConnectionError();
                }

                _cachedAsyncCloseCount = activeConnection.CloseCount;
                _cachedAsyncResult = completion;
                if (!parser.MARSOn)
                {
                    if (activeConnection.AsyncCommandInProgress)
                    {
                        throw SQL.MARSUnsupportedOnConnection();
                    }
                }
                _cachedAsyncConnection = activeConnection;

                // Should only be needed for non-MARS, but set anyways.
                _cachedAsyncConnection.AsyncCommandInProgress = true;
                _cachedEndMethod = endMethod;
            }

            internal void SetAsyncReaderState(SqlDataReader ds, RunBehavior runBehavior, string optionSettings)
            {
                _cachedAsyncReader = ds;
                _cachedRunBehavior = runBehavior;
                _cachedSetOptions = optionSettings;
            }
        }

        private AsyncState _cachedAsyncState = null;

        // @TODO: This is never null, so we can remove the null checks from usages of it.
        private AsyncState CachedAsyncState
        {
            get
            {
                _cachedAsyncState ??= new AsyncState();
                return _cachedAsyncState;
            }
        }

        // number of rows affected by sp_describe_parameter_encryption.
        // The below line is used only for debug asserts and not exposed publicly or impacts functionality otherwise.
        private int _rowsAffectedBySpDescribeParameterEncryption = -1;
        
        private List<_SqlRPC> _RPCList;
        private _SqlRPC[] _sqlRPCParameterEncryptionReqArray;
        private int _currentlyExecutingBatch;

        /// <summary>
        /// This variable is used to keep track of which RPC batch's results are being read when reading the results of
        /// describe parameter encryption RPC requests in BatchRPCMode.
        /// </summary>
        private int _currentlyExecutingDescribeParameterEncryptionRPC;

        /// <summary>
        /// A flag to indicate if we have in-progress describe parameter encryption RPC requests.
        /// Reset to false when completed.
        /// </summary>
        internal bool IsDescribeParameterEncryptionRPCCurrentlyInProgress { get; private set; }

        /// <summary>
        /// A flag to indicate if EndExecute was already initiated by the Begin call.
        /// </summary>
        private volatile bool _internalEndExecuteInitiated;

        /// <summary>
        /// A flag to indicate whether we postponed caching the query metadata for this command.
        /// </summary>
        internal bool CachingQueryMetadataPostponed { get; set; }

        private SqlCommand(SqlCommand from) : this()
        {
            CommandText = from.CommandText;
            CommandTimeout = from.CommandTimeout;
            CommandType = from.CommandType;
            Connection = from.Connection;
            DesignTimeVisible = from.DesignTimeVisible;
            Transaction = from.Transaction;
            UpdatedRowSource = from.UpdatedRowSource;
            _columnEncryptionSetting = from.ColumnEncryptionSetting;

            SqlParameterCollection parameters = Parameters;
            foreach (object parameter in from.Parameters)
            {
                parameters.Add((parameter is ICloneable) ? (parameter as ICloneable).Clone() : parameter);
            }
        }

        private bool IsProviderRetriable => SqlConfigurableRetryFactory.IsRetriable(RetryLogicProvider);

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ResetCommandTimeout/*'/>
        public void ResetCommandTimeout()
        {
            if (ADP.DefaultCommandTimeout != CommandTimeout)
            {
                PropertyChanging();
                _commandTimeout = DefaultCommandTimeout;
            }
        }

        internal void OnStatementCompleted(int recordCount)
        {
            if (0 <= recordCount)
            {
                StatementCompletedEventHandler handler = _statementCompletedEventHandler;
                if (handler != null)
                {
                    try
                    {
                        SqlClientEventSource.Log.TryTraceEvent("SqlCommand.OnStatementCompleted | Info | ObjectId {0}, Record Count {1}, Client Connection Id {2}", ObjectID, recordCount, Connection?.ClientConnectionId);
                        handler(this, new StatementCompletedEventArgs(recordCount));
                    }
                    catch (Exception e)
                    {
                        if (!ADP.IsCatchableOrSecurityExceptionType(e))
                        {
                            throw;
                        }
                    }
                }
            }
        }

        // Cancel is supposed to be multi-thread safe.
        // It doesn't make sense to verify the connection exists or that it is open during cancel
        // because immediately after checking the connection can be closed or removed via another thread.
        //
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Cancel/*'/>
        public override void Cancel()
        {
            using (TryEventScope.Create("SqlCommand.Cancel | API | Object Id {0}", ObjectID))
            {
                SqlClientEventSource.Log.TryCorrelationTraceEvent("SqlCommand.Cancel | API | Correlation | Object Id {0}, Activity Id {1}, Client Connection Id {2}, Command Text '{3}'", ObjectID, ActivityCorrelator.Current, Connection?.ClientConnectionId, CommandText);

                SqlStatistics statistics = null;
                try
                {
                    statistics = SqlStatistics.StartTimer(Statistics);

                    // If we are in reconnect phase simply cancel the waiting task
                    var reconnectCompletionSource = _reconnectionCompletionSource;
                    if (reconnectCompletionSource != null)
                    {
                        if (reconnectCompletionSource.TrySetCanceled())
                        {
                            return;
                        }
                    }

                    // the pending data flag means that we are awaiting a response or are in the middle of processing a response
                    // if we have no pending data, then there is nothing to cancel
                    // if we have pending data, but it is not a result of this command, then we don't cancel either.  Note that
                    // this model is implementable because we only allow one active command at any one time.  This code
                    // will have to change we allow multiple outstanding batches
                    if (_activeConnection == null)
                    {
                        return;
                    }
                    SqlInternalConnectionTds connection = (_activeConnection.InnerConnection as SqlInternalConnectionTds);
                    if (connection == null)
                    {  // Fail with out locking
                        return;
                    }

                    // The lock here is to protect against the command.cancel / connection.close race condition
                    // The SqlInternalConnectionTds is set to OpenBusy during close, once this happens the cast below will fail and
                    // the command will no longer be cancelable.  It might be desirable to be able to cancel the close operation, but this is
                    // outside of the scope of Whidbey RTM.  See (SqlConnection::Close) for other lock.
                    lock (connection)
                    {
                        if (connection != (_activeConnection.InnerConnection as SqlInternalConnectionTds))
                        {
                            // make sure the connection held on the active connection is what we have stored in our temp connection variable, if not between getting "connection" and taking the lock, the connection has been closed
                            return;
                        }

                        TdsParser parser = connection.Parser;
                        if (parser == null)
                        {
                            return;
                        }

                        if (!_pendingCancel)
                        { 
                            // Do nothing if already pending.
                          // Before attempting actual cancel, set the _pendingCancel flag to false.
                          // This denotes to other thread before obtaining stateObject from the
                          // session pool that there is another thread wishing to cancel.
                          // The period in question is between entering the ExecuteAPI and obtaining
                          // a stateObject.
                            _pendingCancel = true;

                            TdsParserStateObject stateObj = _stateObj;
                            if (stateObj != null)
                            {
                                stateObj.Cancel(this);
                            }
                            else
                            {
                                SqlDataReader reader = connection.FindLiveReader(this);
                                if (reader != null)
                                {
                                    reader.Cancel(this);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    SqlStatistics.StopTimer(statistics);
                }
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateParameter/*'/>
        new public SqlParameter CreateParameter()
        {
            return new SqlParameter();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/CreateDbParameter/*'/>
        protected override DbParameter CreateDbParameter()
        {
            return CreateParameter();
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Dispose/*'/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // release managed objects
                _cachedMetaData = null;

                // reset async cache information to allow a second async execute
                CachedAsyncState?.ResetAsyncState();
            }
            // release unmanaged objects
            base.Dispose(disposing);
        }

        private void VerifyEndExecuteState(Task completionTask, string endMethod, bool fullCheckForColumnEncryption = false)
        {
            Debug.Assert(completionTask != null);
            SqlClientEventSource.Log.TryTraceEvent("SqlCommand.VerifyEndExecuteState | API | ObjectId {0}, Client Connection Id {1}, MARS={2}, AsyncCommandInProgress={3}",
                                                    _activeConnection?.ObjectID, _activeConnection?.ClientConnectionId,
                                                    _activeConnection?.Parser?.MARSOn, _activeConnection?.AsyncCommandInProgress);

            if (completionTask.IsCanceled)
            {
                if (_stateObj != null)
                {
                    _stateObj.Parser.State = TdsParserState.Broken; // We failed to respond to attention, we have to quit!
                    _stateObj.Parser.Connection.BreakConnection();
                    _stateObj.Parser.ThrowExceptionAndWarning(_stateObj, this);
                }
                else
                {
                    Debug.Assert(_reconnectionCompletionSource == null || _reconnectionCompletionSource.Task.IsCanceled, "ReconnectCompletionSource should be null or cancelled");
                    throw SQL.CR_ReconnectionCancelled();
                }
            }
            else if (completionTask.IsFaulted)
            {
                throw completionTask.Exception.InnerException;
            }

            // If transparent parameter encryption was attempted, then we need to skip other checks like those on EndMethodName
            // since we want to wait for async results before checking those fields.
            if (IsColumnEncryptionEnabled && !fullCheckForColumnEncryption)
            {
                if (_activeConnection.State != ConnectionState.Open)
                {
                    // If the connection is not 'valid' then it was closed while we were executing
                    throw ADP.ClosedConnectionError();
                }

                return;
            }

            if (CachedAsyncState.EndMethodName == null)
            {
                throw ADP.MethodCalledTwice(endMethod);
            }
            if (endMethod != CachedAsyncState.EndMethodName)
            {
                throw ADP.MismatchedAsyncResult(CachedAsyncState.EndMethodName, endMethod);
            }
            if ((_activeConnection.State != ConnectionState.Open) || (!CachedAsyncState.IsActiveConnectionValid(_activeConnection)))
            {
                // If the connection is not 'valid' then it was closed while we were executing
                throw ADP.ClosedConnectionError();
            }
        }

        private void WaitForAsyncResults(IAsyncResult asyncResult, bool isInternal)
        {
            Task completionTask = (Task)asyncResult;
            if (!asyncResult.IsCompleted)
            {
                asyncResult.AsyncWaitHandle.WaitOne();
            }

            if (_stateObj != null)
            {
                _stateObj._networkPacketTaskSource = null;
            }

            // If this is an internal command we will decrement the count when the End method is actually called by the user.
            // If we are using Column Encryption and the previous task failed, the async count should have already been fixed up.
            // There is a generic issue in how we handle the async count because:
            // a) BeginExecute might or might not clean it up on failure.
            // b) In EndExecute, we check the task state before waiting and throw if it's failed, whereas if we wait we will always adjust the count.
            if (!isInternal && (!IsColumnEncryptionEnabled || !completionTask.IsFaulted))
            {
                _activeConnection.GetOpenTdsConnection().DecrementAsyncCount();
            }
        }

        private void ThrowIfReconnectionHasBeenCanceled()
        {
            if (_stateObj == null)
            {
                var reconnectionCompletionSource = _reconnectionCompletionSource;
                if (reconnectionCompletionSource != null && reconnectionCompletionSource.Task != null && reconnectionCompletionSource.Task.IsCanceled)
                {
                    throw SQL.CR_ReconnectionCancelled();
                }
            }
        }

        private bool TriggerInternalEndAndRetryIfNecessary(
            CommandBehavior behavior,
            object stateObject,
            int timeout,
            bool usedCache,
            bool isRetry,
            bool asyncWrite,
            TaskCompletionSource<object> globalCompletion,
            TaskCompletionSource<object> localCompletion,
            Func<SqlCommand, IAsyncResult, bool, string, object> endFunc,
            Func<SqlCommand, CommandBehavior, AsyncCallback, object, int, bool, bool, IAsyncResult> retryFunc,
            string endMethod)
        {
            // We shouldn't be using the cache if we are in retry.
            Debug.Assert(!usedCache || !isRetry);

            // If column encryption is enabled and we used the cache, we want to catch any potential exceptions that were caused by the query cache and retry if the error indicates that we should.
            // So, try to read the result of the query before completing the overall task and trigger a retry if appropriate.
            if ((IsColumnEncryptionEnabled && !isRetry && (usedCache || ShouldUseEnclaveBasedWorkflow))
#if DEBUG
                || _forceInternalEndQuery
#endif
                )
            {
                long firstAttemptStart = ADP.TimerCurrent();

                CreateLocalCompletionTask(
                    behavior,
                    stateObject,
                    timeout,
                    usedCache,
                    asyncWrite,
                    globalCompletion,
                    localCompletion,
                    endFunc,
                    retryFunc,
                    endMethod,
                    firstAttemptStart);

                return true;
            }
            else
            {
                return false;
            }
        }

        private void CreateLocalCompletionTask(
            CommandBehavior behavior,
            object stateObject,
            int timeout,
            bool usedCache,
            bool asyncWrite,
            TaskCompletionSource<object> globalCompletion,
            TaskCompletionSource<object> localCompletion,
            Func<SqlCommand, IAsyncResult, bool, string, object> endFunc,
            Func<SqlCommand, CommandBehavior, AsyncCallback, object, int, bool, bool, IAsyncResult> retryFunc,
            string endMethod,
            long firstAttemptStart
        )
        {
            localCompletion.Task.ContinueWith(tsk =>
            {
                if (tsk.IsFaulted)
                {
                    globalCompletion.TrySetException(tsk.Exception.InnerException);
                }
                else if (tsk.IsCanceled)
                {
                    globalCompletion.TrySetCanceled();
                }
                else
                {
                    try
                    {
                        // Mark that we initiated the internal EndExecute. This should always be false until we set it here.
                        Debug.Assert(!_internalEndExecuteInitiated);
                        _internalEndExecuteInitiated = true;

                        // lock on _stateObj prevents races with close/cancel.
                        lock (_stateObj)
                        {
                            endFunc(this, tsk, /*isInternal:*/ true, endMethod);
                        }

                        globalCompletion.TrySetResult(tsk.Result);
                    }
                    catch (Exception e)
                    {
                        // Put the state object back to the cache.
                        // Do not reset the async state, since this is managed by the user Begin/End and not internally.
                        if (ADP.IsCatchableExceptionType(e))
                        {
                            ReliablePutStateObject();
                        }

                        bool shouldRetry = e is EnclaveDelegate.RetryableEnclaveQueryExecutionException;

                        // Check if we have an error indicating that we can retry.
                        if (e is SqlException)
                        {
                            SqlException sqlEx = e as SqlException;

                            for (int i = 0; i < sqlEx.Errors.Count; i++)
                            {
                                if ((usedCache && (sqlEx.Errors[i].Number == TdsEnums.TCE_CONVERSION_ERROR_CLIENT_RETRY)) ||
                                    (ShouldUseEnclaveBasedWorkflow &&
                                     (sqlEx.Errors[i].Number == TdsEnums.TCE_ENCLAVE_INVALID_SESSION_HANDLE)))
                                {
                                    shouldRetry = true;
                                    break;
                                }
                            }
                        }

                        if (!shouldRetry)
                        {
                            // If we cannot retry, Reset the async state to make sure we leave a clean state.
                            if (CachedAsyncState != null)
                            {
                                CachedAsyncState.ResetAsyncState();
                            }

                            try
                            {
                                _activeConnection.GetOpenTdsConnection().DecrementAsyncCount();

                                globalCompletion.TrySetException(e);
                            }
                            catch (Exception e2)
                            {
                                globalCompletion.TrySetException(e2);
                            }
                        }
                        else
                        {
                            // Remove the entry from the cache since it was inconsistent.
                            SqlQueryMetadataCache.GetInstance().InvalidateCacheEntry(this);

                            InvalidateEnclaveSession();

                            try
                            {
                                // Kick off the retry.
                                _internalEndExecuteInitiated = false;
                                Task<object> retryTask = (Task<object>)retryFunc(
                                    this,
                                    behavior,
                                    null,
                                    stateObject,
                                    TdsParserStaticMethods.GetRemainingTimeout(timeout, firstAttemptStart),
                                    /*isRetry:*/ true,
                                    asyncWrite);

                                retryTask.ContinueWith(
                                    static (Task<object> retryTask, object state) =>
                                    {
                                        TaskCompletionSource<object> completion = (TaskCompletionSource<object>)state;
                                        if (retryTask.IsFaulted)
                                        {
                                            completion.TrySetException(retryTask.Exception.InnerException);
                                        }
                                        else if (retryTask.IsCanceled)
                                        {
                                            completion.TrySetCanceled();
                                        }
                                        else
                                        {
                                            completion.TrySetResult(retryTask.Result);
                                        }
                                    }, 
                                    state: globalCompletion,
                                    TaskScheduler.Default
                                );
                            }
                            catch (Exception e2)
                            {
                                globalCompletion.TrySetException(e2);
                            }
                        }
                    }
                }
            }, TaskScheduler.Default);
        }

        private void InvalidateEnclaveSession()
        {
            if (ShouldUseEnclaveBasedWorkflow && this.enclavePackage != null)
            {
                EnclaveDelegate.Instance.InvalidateEnclaveSession(
                    this._activeConnection.AttestationProtocol,
                    this._activeConnection.Parser.EnclaveType,
                    GetEnclaveSessionParameters(),
                    this.enclavePackage.EnclaveSession);
            }
        }

        private EnclaveSessionParameters GetEnclaveSessionParameters()
        {
            return new EnclaveSessionParameters(
                this._activeConnection.DataSource,
                this._activeConnection.EnclaveAttestationUrl,
                this._activeConnection.Database);
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/RegisterColumnEncryptionKeyStoreProvidersOnCommand/*' />
        public void RegisterColumnEncryptionKeyStoreProvidersOnCommand(IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders)
        {
            ValidateCustomProviders(customProviders);

            // Create a temporary dictionary and then add items from the provided dictionary.
            // Dictionary constructor does shallow copying by simply copying the provider name and provider reference pairs
            // in the provided customerProviders dictionary.
            Dictionary<string, SqlColumnEncryptionKeyStoreProvider> customColumnEncryptionKeyStoreProviders =
                new(customProviders, StringComparer.OrdinalIgnoreCase);

            _customColumnEncryptionKeyStoreProviders = customColumnEncryptionKeyStoreProviders;
        }

        private void ValidateCustomProviders(IDictionary<string, SqlColumnEncryptionKeyStoreProvider> customProviders)
        {
            // Throw when the provided dictionary is null.
            if (customProviders is null)
            {
                throw SQL.NullCustomKeyStoreProviderDictionary();
            }

            // Validate that custom provider list doesn't contain any of system provider list
            foreach (string key in customProviders.Keys)
            {
                // Validate the provider name
                //
                // Check for null or empty
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw SQL.EmptyProviderName();
                }

                // Check if the name starts with MSSQL_, since this is reserved namespace for system providers.
                if (key.StartsWith(ADP.ColumnEncryptionSystemProviderNamePrefix, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw SQL.InvalidCustomKeyStoreProviderName(key, ADP.ColumnEncryptionSystemProviderNamePrefix);
                }

                // Validate the provider value
                if (customProviders[key] is null)
                {
                    throw SQL.NullProviderValue(key);
                }
            }
        }

        /// <summary>
        /// This function walks through the registered custom column encryption key store providers and returns an object if found.
        /// </summary>
        /// <param name="providerName">Provider Name to be searched in custom provider dictionary.</param>
        /// <param name="columnKeyStoreProvider">If the provider is found, initializes the corresponding SqlColumnEncryptionKeyStoreProvider instance.</param>
        /// <returns>true if the provider is found, else returns false</returns>
        internal bool TryGetColumnEncryptionKeyStoreProvider(string providerName, out SqlColumnEncryptionKeyStoreProvider columnKeyStoreProvider)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(providerName), "Provider name is invalid");
            return _customColumnEncryptionKeyStoreProviders.TryGetValue(providerName, out columnKeyStoreProvider);
        }

        /// <summary>
        /// This function returns a list of the names of the custom providers currently registered.
        /// </summary>
        /// <returns>Combined list of provider names</returns>
        internal List<string> GetColumnEncryptionCustomKeyStoreProvidersNames()
        {
            if (_customColumnEncryptionKeyStoreProviders.Count > 0)
            {
                return new List<string>(_customColumnEncryptionKeyStoreProviders.Keys);
            }
            return new List<string>(0);
        }

        // If the user part is quoted, remove first and last brackets and then unquote any right square
        // brackets in the procedure.  This is a very simple parser that performs no validation.  As
        // with the function below, ideally we should have support from the server for this.
        private static string UnquoteProcedurePart(string part)
        {
            if (part != null && (2 <= part.Length))
            {
                if ('[' == part[0] && ']' == part[part.Length - 1])
                {
                    part = part.Substring(1, part.Length - 2); // strip outer '[' & ']'
                    part = part.Replace("]]", "]"); // undo quoted "]" from "]]" to "]"
                }
            }
            return part;
        }

        // User value in this format: [server].[database].[schema].[sp_foo];1
        // This function should only be passed "[sp_foo];1".
        // This function uses a pretty simple parser that doesn't do any validation.
        // Ideally, we would have support from the server rather than us having to do this.
        private static string UnquoteProcedureName(string name, out object groupNumber)
        {
            groupNumber = null; // Out param - initialize value to no value.
            string sproc = name;

            if (sproc != null)
            {
                if (char.IsDigit(sproc[sproc.Length - 1]))
                {
                    // If last char is a digit, parse.
                    int semicolon = sproc.LastIndexOf(';');
                    if (semicolon != -1)
                    {
                        // If we found a semicolon, obtain the integer.
                        string part = sproc.Substring(semicolon + 1);
                        int number = 0;
                        if (int.TryParse(part, out number))
                        {
                            // No checking, just fail if this doesn't work.
                            groupNumber = number;
                            sproc = sproc.Substring(0, semicolon);
                        }
                    }
                }
                sproc = UnquoteProcedurePart(sproc);
            }
            return sproc;
        }

        // Index into indirection arrays for columns of interest to DeriveParameters
        private enum ProcParamsColIndex
        {
            ParameterName = 0,
            ParameterType,
            DataType, // obsolete in 2008, use ManagedDataType instead
            ManagedDataType, // new in 2008
            CharacterMaximumLength,
            NumericPrecision,
            NumericScale,
            TypeCatalogName,
            TypeSchemaName,
            TypeName,
            XmlSchemaCollectionCatalogName,
            XmlSchemaCollectionSchemaName,
            XmlSchemaCollectionName,
            UdtTypeName, // obsolete in 2008.  Holds the actual typename if UDT, since TypeName didn't back then.
            DateTimeScale // new in 2008
        };

        // 2005- column ordinals (this array indexed by ProcParamsColIndex
        internal static readonly string[] PreSql2008ProcParamsNames = new string[] {
            "PARAMETER_NAME",           // ParameterName,
            "PARAMETER_TYPE",           // ParameterType,
            "DATA_TYPE",                // DataType
            null,                       // ManagedDataType,     introduced in 2008
            "CHARACTER_MAXIMUM_LENGTH", // CharacterMaximumLength,
            "NUMERIC_PRECISION",        // NumericPrecision,
            "NUMERIC_SCALE",            // NumericScale,
            "UDT_CATALOG",              // TypeCatalogName,
            "UDT_SCHEMA",               // TypeSchemaName,
            "TYPE_NAME",                // TypeName,
            "XML_CATALOGNAME",          // XmlSchemaCollectionCatalogName,
            "XML_SCHEMANAME",           // XmlSchemaCollectionSchemaName,
            "XML_SCHEMACOLLECTIONNAME", // XmlSchemaCollectionName
            "UDT_NAME",                 // UdtTypeName
            null,                       // Scale for datetime types with scale, introduced in 2008
        };

        // 2008+ column ordinals (this array indexed by ProcParamsColIndex
        internal static readonly string[] Sql2008ProcParamsNames = new string[] {
            "PARAMETER_NAME",           // ParameterName,
            "PARAMETER_TYPE",           // ParameterType,
            null,                       // DataType, removed from 2008+
            "MANAGED_DATA_TYPE",        // ManagedDataType,
            "CHARACTER_MAXIMUM_LENGTH", // CharacterMaximumLength,
            "NUMERIC_PRECISION",        // NumericPrecision,
            "NUMERIC_SCALE",            // NumericScale,
            "TYPE_CATALOG_NAME",        // TypeCatalogName,
            "TYPE_SCHEMA_NAME",         // TypeSchemaName,
            "TYPE_NAME",                // TypeName,
            "XML_CATALOGNAME",          // XmlSchemaCollectionCatalogName,
            "XML_SCHEMANAME",           // XmlSchemaCollectionSchemaName,
            "XML_SCHEMACOLLECTIONNAME", // XmlSchemaCollectionName
            null,                       // UdtTypeName, removed from 2008+
            "SS_DATETIME_PRECISION",    // Scale for datetime types with scale
        };

        internal void DeriveParameters()
        {
            switch (CommandType)
            {
                case CommandType.Text:
                    throw ADP.DeriveParametersNotSupported(this);
                case CommandType.StoredProcedure:
                    break;
                case CommandType.TableDirect:
                    // CommandType.TableDirect - do nothing, parameters are not supported
                    throw ADP.DeriveParametersNotSupported(this);
                default:
                    throw ADP.InvalidCommandType(CommandType);
            }

            // validate that we have a valid connection
            ValidateCommand(isAsync: false);

            // Use common parser for SqlClient and OleDb - parse into 4 parts - Server, Catalog, Schema, ProcedureName
            string[] parsedSProc = MultipartIdentifier.ParseMultipartIdentifier(CommandText, "[\"", "]\"", Strings.SQL_SqlCommandCommandText, false);
            if (string.IsNullOrEmpty(parsedSProc[3]))
            {
                throw ADP.NoStoredProcedureExists(CommandText);
            }

            Debug.Assert(parsedSProc.Length == 4, "Invalid array length result from SqlCommandBuilder.ParseProcedureName");

            SqlCommand paramsCmd = null;
            StringBuilder cmdText = new StringBuilder();

            // Build call for sp_procedure_params_rowset built of unquoted values from user:
            // [user server, if provided].[user catalog, else current database].[sys if 2005, else blank].[sp_procedure_params_rowset]

            // Server - pass only if user provided.
            if (!string.IsNullOrEmpty(parsedSProc[0]))
            {
                SqlCommandSet.BuildStoredProcedureName(cmdText, parsedSProc[0]);
                cmdText.Append(".");
            }

            // Catalog - pass user provided, otherwise use current database.
            if (string.IsNullOrEmpty(parsedSProc[1]))
            {
                parsedSProc[1] = Connection.Database;
            }
            SqlCommandSet.BuildStoredProcedureName(cmdText, parsedSProc[1]);
            cmdText.Append(".");

            // Schema - only if 2005, and then only pass sys.  Also - pass managed version of sproc
            // for 2005, else older sproc.
            string[] colNames;
            bool useManagedDataType;
            if (Connection.Is2008OrNewer)
            {
                // Procedure - [sp_procedure_params_managed]
                cmdText.Append("[sys].[").Append(TdsEnums.SP_PARAMS_MGD10).Append("]");

                colNames = Sql2008ProcParamsNames;
                useManagedDataType = true;
            }
            else
            {
                // Procedure - [sp_procedure_params_managed]
                cmdText.Append("[sys].[").Append(TdsEnums.SP_PARAMS_MANAGED).Append("]");

                colNames = PreSql2008ProcParamsNames;
                useManagedDataType = false;
            }


            paramsCmd = new SqlCommand(cmdText.ToString(), Connection, Transaction)
            {
                CommandType = CommandType.StoredProcedure
            };

            object groupNumber;

            // Prepare parameters for sp_procedure_params_rowset:
            // 1) procedure name - unquote user value
            // 2) group number - parsed at the time we unquoted procedure name
            // 3) procedure schema - unquote user value

            paramsCmd.Parameters.Add(new SqlParameter("@procedure_name", SqlDbType.NVarChar, 255));
            paramsCmd.Parameters[0].Value = UnquoteProcedureName(parsedSProc[3], out groupNumber); // ProcedureName is 4rd element in parsed array

            if (groupNumber != null)
            {
                SqlParameter param = paramsCmd.Parameters.Add(new SqlParameter("@group_number", SqlDbType.Int));
                param.Value = groupNumber;
            }

            if (!string.IsNullOrEmpty(parsedSProc[2]))
            {
                // SchemaName is 3rd element in parsed array
                SqlParameter param = paramsCmd.Parameters.Add(new SqlParameter("@procedure_schema", SqlDbType.NVarChar, 255));
                param.Value = UnquoteProcedurePart(parsedSProc[2]);
            }

            SqlDataReader r = null;

            List<SqlParameter> parameters = new List<SqlParameter>();
            bool processFinallyBlock = true;

            try
            {
                r = paramsCmd.ExecuteReader();

                SqlParameter p = null;

                while (r.Read())
                {
                    // each row corresponds to a parameter of the stored proc.  Fill in all the info
                    p = new SqlParameter()
                    {
                        ParameterName = (string)r[colNames[(int)ProcParamsColIndex.ParameterName]]
                    };

                    // type
                    if (useManagedDataType)
                    {
                        p.SqlDbType = (SqlDbType)(short)r[colNames[(int)ProcParamsColIndex.ManagedDataType]];

                        // 2005 didn't have as accurate of information as we're getting for 2008, so re-map a couple of
                        //  types for backward compatability.
                        switch (p.SqlDbType)
                        {
                            case SqlDbType.Image:
                            case SqlDbType.Timestamp:
                                p.SqlDbType = SqlDbType.VarBinary;
                                break;

                            case SqlDbType.NText:
                                p.SqlDbType = SqlDbType.NVarChar;
                                break;

                            case SqlDbType.Text:
                                p.SqlDbType = SqlDbType.VarChar;
                                break;

                            default:
                                break;
                        }
                    }
                    else
                    {
                        p.SqlDbType = MetaType.GetSqlDbTypeFromOleDbType((short)r[colNames[(int)ProcParamsColIndex.DataType]],
                            ADP.IsNull(r[colNames[(int)ProcParamsColIndex.TypeName]]) ? "" :
                                (string)r[colNames[(int)ProcParamsColIndex.TypeName]]);
                    }

                    // size
                    object a = r[colNames[(int)ProcParamsColIndex.CharacterMaximumLength]];
                    if (a is int)
                    {
                        int size = (int)a;

                        // Map MAX sizes correctly.  The 2008 server-side proc sends 0 for these instead of -1.
                        //  Should be fixed on the 2008 side, but would likely hold up the RI, and is safer to fix here.
                        //  If we can get the server-side fixed before shipping 2008, we can remove this mapping.
                        if (0 == size &&
                                (p.SqlDbType == SqlDbType.NVarChar ||
                                 p.SqlDbType == SqlDbType.VarBinary ||
                                 p.SqlDbType == SqlDbType.VarChar))
                        {
                            size = -1;
                        }
                        p.Size = size;
                    }

                    // direction
                    p.Direction = ParameterDirectionFromOleDbDirection((short)r[colNames[(int)ProcParamsColIndex.ParameterType]]);

                    if (p.SqlDbType == SqlDbType.Decimal)
                    {
                        p.ScaleInternal = (byte)((short)r[colNames[(int)ProcParamsColIndex.NumericScale]] & 0xff);
                        p.PrecisionInternal = (byte)((short)r[colNames[(int)ProcParamsColIndex.NumericPrecision]] & 0xff);
                    }

                    // type name for Udt
                    if (SqlDbType.Udt == p.SqlDbType)
                    {
                        string udtTypeName;
                        if (useManagedDataType)
                        {
                            udtTypeName = (string)r[colNames[(int)ProcParamsColIndex.TypeName]];
                        }
                        else
                        {
                            udtTypeName = (string)r[colNames[(int)ProcParamsColIndex.UdtTypeName]];
                        }

                        //read the type name
                        p.UdtTypeName = r[colNames[(int)ProcParamsColIndex.TypeCatalogName]] + "." +
                            r[colNames[(int)ProcParamsColIndex.TypeSchemaName]] + "." +
                            udtTypeName;
                    }

                    // type name for Structured types (same as for Udt's except assign p.TypeName instead of p.UdtTypeName
                    if (SqlDbType.Structured == p.SqlDbType)
                    {
                        Debug.Assert(_activeConnection.Is2008OrNewer, "Invalid datatype token received from pre-2008 server");

                        //read the type name
                        p.TypeName = r[colNames[(int)ProcParamsColIndex.TypeCatalogName]] + "." +
                            r[colNames[(int)ProcParamsColIndex.TypeSchemaName]] + "." +
                            r[colNames[(int)ProcParamsColIndex.TypeName]];

                        // the constructed type name above is incorrectly formatted, it should be a 2 part name not 3
                        // for compatibility we can't change this because the bug has existed for a long time and been 
                        // worked around by users, so identify that it is present and catch it later in the execution
                        // process once users can no longer interact with with the parameter type name
                        p.IsDerivedParameterTypeName = true;
                    }

                    // XmlSchema name for Xml types
                    if (SqlDbType.Xml == p.SqlDbType)
                    {
                        object value;

                        value = r[colNames[(int)ProcParamsColIndex.XmlSchemaCollectionCatalogName]];
                        p.XmlSchemaCollectionDatabase = ADP.IsNull(value) ? string.Empty : (string)value;

                        value = r[colNames[(int)ProcParamsColIndex.XmlSchemaCollectionSchemaName]];
                        p.XmlSchemaCollectionOwningSchema = ADP.IsNull(value) ? string.Empty : (string)value;

                        value = r[colNames[(int)ProcParamsColIndex.XmlSchemaCollectionName]];
                        p.XmlSchemaCollectionName = ADP.IsNull(value) ? string.Empty : (string)value;
                    }

                    if (MetaType._IsVarTime(p.SqlDbType))
                    {
                        object value = r[colNames[(int)ProcParamsColIndex.DateTimeScale]];
                        if (value is int)
                        {
                            p.ScaleInternal = (byte)(((int)value) & 0xff);
                        }
                    }

                    parameters.Add(p);
                }
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
                    r?.Close();

                    // always unhook the user's connection
                    paramsCmd.Connection = null;
                }
            }

            if (parameters.Count == 0)
            {
                throw ADP.NoStoredProcedureExists(this.CommandText);
            }

            Parameters.Clear();

            foreach (SqlParameter temp in parameters)
            {
                _parameters.Add(temp);
            }
        }

        private ParameterDirection ParameterDirectionFromOleDbDirection(short oledbDirection)
        {
            Debug.Assert(oledbDirection >= 1 && oledbDirection <= 4, "invalid parameter direction from params_rowset!");

            switch (oledbDirection)
            {
                case 2:
                    return ParameterDirection.InputOutput;
                case 3:
                    return ParameterDirection.Output;
                case 4:
                    return ParameterDirection.ReturnValue;
                default:
                    return ParameterDirection.Input;
            }

        }

        // get cached metadata
        internal _SqlMetaDataSet MetaData
        {
            get
            {
                return _cachedMetaData;
            }
        }

        // Check to see if notifications auto enlistment is turned on. Enlist if so.
        private void CheckNotificationStateAndAutoEnlist()
        {
            // Auto-enlist not supported in Core

            // If we have a notification with a dependency, setup the notification options at this time.

            // If user passes options, then we will always have option data at the time the SqlDependency
            // ctor is called.  But, if we are using default queue, then we do not have this data until
            // Start().  Due to this, we always delay setting options until execute.

            // There is a variance in order between Start(), SqlDependency(), and Execute.  This is the
            // best way to solve that problem.
            if (Notification != null)
            {
                if (_sqlDep != null)
                {
                    if (_sqlDep.Options == null)
                    {
                        // If null, SqlDependency was not created with options, so we need to obtain default options now.
                        // GetDefaultOptions can and will throw under certain conditions.

                        // In order to match to the appropriate start - we need 3 pieces of info:
                        // 1) server 2) user identity (SQL Auth or Int Sec) 3) database

                        SqlDependency.IdentityUserNamePair identityUserName = null;

                        // Obtain identity from connection.
                        SqlInternalConnectionTds internalConnection = _activeConnection.InnerConnection as SqlInternalConnectionTds;
                        if (internalConnection.Identity != null)
                        {
                            identityUserName = new SqlDependency.IdentityUserNamePair(internalConnection.Identity, null);
                        }
                        else
                        {
                            identityUserName = new SqlDependency.IdentityUserNamePair(null, internalConnection.ConnectionOptions.UserID);
                        }

                        Notification.Options = SqlDependency.GetDefaultComposedOptions(_activeConnection.DataSource,
                                                             InternalTdsConnection.ServerProvidedFailoverPartner,
                                                             identityUserName, _activeConnection.Database);
                    }

                    // Set UserData on notifications, as well as adding to the appdomain dispatcher.  The value is
                    // computed by an algorithm on the dependency - fixed and will always produce the same value
                    // given identical commandtext + parameter values.
                    Notification.UserData = _sqlDep.ComputeHashAndAddToDispatcher(this);
                    // Maintain server list for SqlDependency.
                    _sqlDep.AddToServerList(_activeConnection.DataSource);
                }
            }
        }

        /// <summary>
        /// Resets the encryption related state of the command object and each of the parameters.
        /// BatchRPC doesn't need special handling to cleanup the state of each RPC object and its parameters since a new RPC object and
        /// parameters are generated on every execution.
        /// </summary>
        private void ResetEncryptionState()
        {
            // First reset the command level state.
            ClearDescribeParameterEncryptionRequests();

            // Reset the state for internal End execution.
            _internalEndExecuteInitiated = false;

            // Reset the state for the cache.
            CachingQueryMetadataPostponed = false;

            // Reset the state of each of the parameters.
            if (_parameters != null)
            {
                for (int i = 0; i < _parameters.Count; i++)
                {
                    _parameters[i].CipherMetadata = null;
                    _parameters[i].HasReceivedMetadata = false;
                }
            }

            keysToBeSentToEnclave?.Clear();
            enclavePackage = null;
            requiresEnclaveComputations = false;
            enclaveAttestationParameters = null;
            customData = null;
            customDataLength = 0;
        }

        /// <summary>
        /// Steps to be executed in the Prepare Transparent Encryption finally block.
        /// </summary>
        private void PrepareTransparentEncryptionFinallyBlock(bool closeDataReader,
            bool clearDataStructures,
            bool decrementAsyncCount,
            bool wasDescribeParameterEncryptionNeeded,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            SqlDataReader describeParameterEncryptionDataReader)
        {
            if (clearDataStructures)
            {
                // Clear some state variables in SqlCommand that reflect in-progress describe parameter encryption requests.
                ClearDescribeParameterEncryptionRequests();

                if (describeParameterEncryptionRpcOriginalRpcMap != null)
                {
                    describeParameterEncryptionRpcOriginalRpcMap = null;
                }
            }

            // Decrement the async count.
            if (decrementAsyncCount)
            {
                SqlInternalConnectionTds internalConnectionTds = _activeConnection.GetOpenTdsConnection();
                if (internalConnectionTds != null)
                {
                    internalConnectionTds.DecrementAsyncCount();
                }
            }

            if (closeDataReader)
            {
                // Close the data reader to reset the _stateObj
                if (describeParameterEncryptionDataReader != null)
                {
                    describeParameterEncryptionDataReader.Close();
                }
            }
        }

        /// <summary>
        /// Executes the reader after checking to see if we need to encrypt input parameters and then encrypting it if required.
        /// TryFetchInputParameterEncryptionInfo() -> ReadDescribeEncryptionParameterResults()-> EncryptInputParameters() ->RunExecuteReaderTds()
        /// </summary>
        /// <param name="isAsync"></param>
        /// <param name="timeout"></param>
        /// <param name="completion"></param>
        /// <param name="returnTask"></param>
        /// <param name="asyncWrite"></param>
        /// <param name="usedCache"></param>
        /// <param name="isRetry"></param>
        /// <returns></returns>
        private void PrepareForTransparentEncryption(
            bool isAsync,
            int timeout,
            TaskCompletionSource<object> completion,
            out Task returnTask,
            bool asyncWrite,
            out bool usedCache,
            bool isRetry)
        {
            // Fetch reader with input params
            Task fetchInputParameterEncryptionInfoTask = null;
            bool describeParameterEncryptionNeeded = false;
            SqlDataReader describeParameterEncryptionDataReader = null;
            returnTask = null;
            usedCache = false;

            Debug.Assert(_activeConnection != null, "_activeConnection should not be null in PrepareForTransparentEncryption.");
            Debug.Assert(_activeConnection.Parser != null, "_activeConnection.Parser should not be null in PrepareForTransparentEncryption.");
            Debug.Assert(_activeConnection.Parser.IsColumnEncryptionSupported,
                "_activeConnection.Parser.IsColumnEncryptionSupported should be true in PrepareForTransparentEncryption.");
            Debug.Assert(_columnEncryptionSetting == SqlCommandColumnEncryptionSetting.Enabled
                        || (_columnEncryptionSetting == SqlCommandColumnEncryptionSetting.UseConnectionSetting && _activeConnection.IsColumnEncryptionSettingEnabled),
                        "ColumnEncryption setting should be enabled for input parameter encryption.");
            Debug.Assert(isAsync == (completion != null), "completion should can be null if and only if mode is async.");

            // If we are not in Batch RPC and not already retrying, attempt to fetch the cipher MD for each parameter from the cache.
            // If this succeeds then return immediately, otherwise just fall back to the full crypto MD discovery.
            if (!_batchRPCMode && !isRetry && (this._parameters != null && this._parameters.Count > 0) && SqlQueryMetadataCache.GetInstance().GetQueryMetadataIfExists(this))
            {
                usedCache = true;
                return;
            }

            // A flag to indicate if finallyblock needs to execute.
            bool processFinallyBlock = true;

            // A flag to indicate if we need to decrement async count on the connection in finally block.
            bool decrementAsyncCountInFinallyBlock = false;

            // Flag to indicate if exception is caught during the execution, to govern clean up.
            bool exceptionCaught = false;

            // Used in _batchRPCMode to maintain a map of describe parameter encryption RPC requests (Keys) and their corresponding original RPC requests (Values).
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap = null;


            try
            {
                try
                {
                    // Fetch the encryption information that applies to any of the input parameters.
                    describeParameterEncryptionDataReader = TryFetchInputParameterEncryptionInfo(timeout,
                                                                                                 isAsync,
                                                                                                 asyncWrite,
                                                                                                 out describeParameterEncryptionNeeded,
                                                                                                 out fetchInputParameterEncryptionInfoTask,
                                                                                                 out describeParameterEncryptionRpcOriginalRpcMap,
                                                                                                 isRetry);

                    Debug.Assert(describeParameterEncryptionNeeded || describeParameterEncryptionDataReader == null,
                        "describeParameterEncryptionDataReader should be null if we don't need to request describe parameter encryption request.");

                    Debug.Assert(fetchInputParameterEncryptionInfoTask == null || isAsync,
                        "Task returned by TryFetchInputParameterEncryptionInfo, when in sync mode, in PrepareForTransparentEncryption.");

                    Debug.Assert((describeParameterEncryptionRpcOriginalRpcMap != null) == _batchRPCMode,
                        "describeParameterEncryptionRpcOriginalRpcMap can be non-null if and only if it is in _batchRPCMode.");

                    // If we didn't have parameters, we can fall back to regular code path, by simply returning.
                    if (!describeParameterEncryptionNeeded)
                    {
                        Debug.Assert(fetchInputParameterEncryptionInfoTask == null,
                            "fetchInputParameterEncryptionInfoTask should not be set if describe parameter encryption is not needed.");

                        Debug.Assert(describeParameterEncryptionDataReader == null,
                            "SqlDataReader created for describe parameter encryption params when it is not needed.");

                        return;
                    }

                    // If we are in async execution, we need to decrement our async count on exception.
                    decrementAsyncCountInFinallyBlock = isAsync;

                    Debug.Assert(describeParameterEncryptionDataReader != null,
                        "describeParameterEncryptionDataReader should not be null, as it is required to get results of describe parameter encryption.");

                    // Fire up another task to read the results of describe parameter encryption
                    if (fetchInputParameterEncryptionInfoTask != null)
                    {
                        // Mark that we should not process the finally block since we have async execution pending.
                        // Note that this should be done outside the task's continuation delegate.
                        processFinallyBlock = false;
                        describeParameterEncryptionDataReader = GetParameterEncryptionDataReader(
                            out returnTask,
                            fetchInputParameterEncryptionInfoTask,
                            describeParameterEncryptionDataReader,
                            describeParameterEncryptionRpcOriginalRpcMap,
                            describeParameterEncryptionNeeded,
                            isRetry);

                        decrementAsyncCountInFinallyBlock = false;
                    }
                    else
                    {
                        // If it was async, ending the reader is still pending.
                        if (isAsync)
                        {
                            // Mark that we should not process the finally block since we have async execution pending.
                            // Note that this should be done outside the task's continuation delegate.
                            processFinallyBlock = false;
                            describeParameterEncryptionDataReader = GetParameterEncryptionDataReaderAsync(
                                out returnTask,
                                describeParameterEncryptionDataReader,
                                describeParameterEncryptionRpcOriginalRpcMap,
                                describeParameterEncryptionNeeded,
                                isRetry);

                            decrementAsyncCountInFinallyBlock = false;
                        }
                        else
                        {
                            // For synchronous execution, read the results of describe parameter encryption here.
                            ReadDescribeEncryptionParameterResults(
                                describeParameterEncryptionDataReader,
                                describeParameterEncryptionRpcOriginalRpcMap,
                                isRetry);
                        }

#if DEBUG
                        // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                        if (_sleepAfterReadDescribeEncryptionParameterResults)
                        {
                            Thread.Sleep(10000);
                        }
#endif
                    }
                }
                catch (Exception e)
                {
                    processFinallyBlock = ADP.IsCatchableExceptionType(e);
                    exceptionCaught = true;
                    throw;
                }
                finally
                {
                    // Free up the state only for synchronous execution. For asynchronous execution, free only if there was an exception.
                    PrepareTransparentEncryptionFinallyBlock(closeDataReader: (processFinallyBlock && !isAsync) || exceptionCaught,
                                           decrementAsyncCount: decrementAsyncCountInFinallyBlock && exceptionCaught,
                                           clearDataStructures: (processFinallyBlock && !isAsync) || exceptionCaught,
                                           wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                                           describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                                           describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                }
            }
            catch (Exception e)
            {
                if (CachedAsyncState != null)
                {
                    CachedAsyncState.ResetAsyncState();
                }

                if (ADP.IsCatchableExceptionType(e))
                {
                    ReliablePutStateObject();
                }

                throw;
            }
        }

        private SqlDataReader GetParameterEncryptionDataReader(out Task returnTask, Task fetchInputParameterEncryptionInfoTask,
            SqlDataReader describeParameterEncryptionDataReader,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap, bool describeParameterEncryptionNeeded, bool isRetry)
        {
            returnTask = AsyncHelper.CreateContinuationTaskWithState(fetchInputParameterEncryptionInfoTask, this,
                (object state) =>
                {
                    SqlCommand command = (SqlCommand)state;
                    bool processFinallyBlockAsync = true;
                    bool decrementAsyncCountInFinallyBlockAsync = true;

                    try
                    {
                        // Check for any exceptions on network write, before reading.
                        command.CheckThrowSNIException();

                        // If it is async, then TryFetchInputParameterEncryptionInfo-> RunExecuteReaderTds would have incremented the async count.
                        // Decrement it when we are about to complete async execute reader.
                        SqlInternalConnectionTds internalConnectionTds = command._activeConnection.GetOpenTdsConnection();
                        if (internalConnectionTds != null)
                        {
                            internalConnectionTds.DecrementAsyncCount();
                            decrementAsyncCountInFinallyBlockAsync = false;
                        }

                        // Complete executereader.
                        describeParameterEncryptionDataReader = command.CompleteAsyncExecuteReader(isInternal: false, forDescribeParameterEncryption: true);
                        Debug.Assert(command._stateObj == null, "non-null state object in PrepareForTransparentEncryption.");

                        // Read the results of describe parameter encryption.
                        command.ReadDescribeEncryptionParameterResults(describeParameterEncryptionDataReader, describeParameterEncryptionRpcOriginalRpcMap, isRetry);

#if DEBUG
                        // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                        if (_sleepAfterReadDescribeEncryptionParameterResults)
                        {
                            Thread.Sleep(10000);
                        }
#endif
                    }
                    catch (Exception e)
                    {
                        processFinallyBlockAsync = ADP.IsCatchableExceptionType(e);
                        throw;
                    }
                    finally
                    {
                        command.PrepareTransparentEncryptionFinallyBlock(closeDataReader: processFinallyBlockAsync,
                            decrementAsyncCount: decrementAsyncCountInFinallyBlockAsync,
                            clearDataStructures: processFinallyBlockAsync,
                            wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                            describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                            describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                    }
                },
                onFailure: static (Exception exception, object state) =>
                {
                    SqlCommand command = (SqlCommand)state;
                    if (command.CachedAsyncState != null)
                    {
                        command.CachedAsyncState.ResetAsyncState();
                    }

                    if (exception != null)
                    {
                        throw exception;
                    }
                }
            );

            return describeParameterEncryptionDataReader;
        }

        private SqlDataReader GetParameterEncryptionDataReaderAsync(out Task returnTask,
            SqlDataReader describeParameterEncryptionDataReader,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap, bool describeParameterEncryptionNeeded, bool isRetry)
        {
            returnTask = Task.Run(() =>
            {
                bool processFinallyBlockAsync = true;
                bool decrementAsyncCountInFinallyBlockAsync = true;

                try
                {
                    // Check for any exceptions on network write, before reading.
                    CheckThrowSNIException();

                    // If it is async, then TryFetchInputParameterEncryptionInfo-> RunExecuteReaderTds would have incremented the async count.
                    // Decrement it when we are about to complete async execute reader.
                    SqlInternalConnectionTds internalConnectionTds = _activeConnection.GetOpenTdsConnection();
                    if (internalConnectionTds != null)
                    {
                        internalConnectionTds.DecrementAsyncCount();
                        decrementAsyncCountInFinallyBlockAsync = false;
                    }

                    // Complete executereader.
                    describeParameterEncryptionDataReader = CompleteAsyncExecuteReader(isInternal: false, forDescribeParameterEncryption: true);
                    Debug.Assert(_stateObj == null, "non-null state object in PrepareForTransparentEncryption.");

                    // Read the results of describe parameter encryption.
                    ReadDescribeEncryptionParameterResults(describeParameterEncryptionDataReader,
                        describeParameterEncryptionRpcOriginalRpcMap, isRetry);
#if DEBUG
                    // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                    if (_sleepAfterReadDescribeEncryptionParameterResults)
                    {
                        Thread.Sleep(10000);
                    }
#endif
                }
                catch (Exception e)
                {
                    processFinallyBlockAsync = ADP.IsCatchableExceptionType(e);
                    throw;
                }
                finally
                {
                    PrepareTransparentEncryptionFinallyBlock(closeDataReader: processFinallyBlockAsync,
                        decrementAsyncCount: decrementAsyncCountInFinallyBlockAsync,
                        clearDataStructures: processFinallyBlockAsync,
                        wasDescribeParameterEncryptionNeeded: describeParameterEncryptionNeeded,
                        describeParameterEncryptionRpcOriginalRpcMap: describeParameterEncryptionRpcOriginalRpcMap,
                        describeParameterEncryptionDataReader: describeParameterEncryptionDataReader);
                }
            });
            return describeParameterEncryptionDataReader;
        }

        /// <summary>
        /// Executes an RPC to fetch param encryption info from SQL Engine. If this method is not done writing
        ///  the request to wire, it'll set the "task" parameter which can be used to create continuations.
        /// </summary>
        /// <param name="timeout"></param>
        /// <param name="isAsync"></param>
        /// <param name="asyncWrite"></param>
        /// <param name="inputParameterEncryptionNeeded"></param>
        /// <param name="task"></param>
        /// <param name="describeParameterEncryptionRpcOriginalRpcMap"></param>
        /// <param name="isRetry">Indicates if this is a retry from a failed call.</param>
        /// <returns></returns>
        private SqlDataReader TryFetchInputParameterEncryptionInfo(
            int timeout,
            bool isAsync,
            bool asyncWrite,
            out bool inputParameterEncryptionNeeded,
            out Task task,
            out ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool isRetry)
        {
            inputParameterEncryptionNeeded = false;
            task = null;
            describeParameterEncryptionRpcOriginalRpcMap = null;
            byte[] serializedAttestationParameters = null;

            if (ShouldUseEnclaveBasedWorkflow)
            {
                SqlConnectionAttestationProtocol attestationProtocol = this._activeConnection.AttestationProtocol;
                string enclaveType = this._activeConnection.Parser.EnclaveType;

                EnclaveSessionParameters enclaveSessionParameters = GetEnclaveSessionParameters();

                SqlEnclaveSession sqlEnclaveSession = null;
                EnclaveDelegate.Instance.GetEnclaveSession(attestationProtocol, enclaveType, enclaveSessionParameters, true, isRetry, out sqlEnclaveSession, out customData, out customDataLength);
                if (sqlEnclaveSession == null)
                {
                    enclaveAttestationParameters = EnclaveDelegate.Instance.GetAttestationParameters(attestationProtocol, enclaveType, enclaveSessionParameters.AttestationUrl, customData, customDataLength);
                    serializedAttestationParameters = EnclaveDelegate.Instance.GetSerializedAttestationParameters(enclaveAttestationParameters, enclaveType);
                }
            }

            if (_batchRPCMode)
            {
                // Count the rpc requests that need to be transparently encrypted
                // We simply look for any parameters in a request and add the request to be queried for parameter encryption
                Dictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcDictionary = new Dictionary<_SqlRPC, _SqlRPC>();

                for (int i = 0; i < _RPCList.Count; i++)
                {
                    // In BatchRPCMode, the actual T-SQL query is in the first parameter and not present as the rpcName, as is the case with non-BatchRPCMode.
                    // So input parameters start at parameters[1]. parameters[0] is the actual T-SQL Statement. rpcName is sp_executesql.
                    if (_RPCList[i].systemParams.Length > 1)
                    {
                        _RPCList[i].needsFetchParameterEncryptionMetadata = true;

                        // Since we are going to need multiple RPC objects, allocate a new one here for each command in the batch.
                        _SqlRPC rpcDescribeParameterEncryptionRequest = new _SqlRPC();

                        // Prepare the describe parameter encryption request.
                        PrepareDescribeParameterEncryptionRequest(_RPCList[i], ref rpcDescribeParameterEncryptionRequest, i == 0 ? serializedAttestationParameters : null);
                        Debug.Assert(rpcDescribeParameterEncryptionRequest != null, "rpcDescribeParameterEncryptionRequest should not be null, after call to PrepareDescribeParameterEncryptionRequest.");

                        Debug.Assert(!describeParameterEncryptionRpcOriginalRpcDictionary.ContainsKey(rpcDescribeParameterEncryptionRequest),
                            "There should not already be a key referring to the current rpcDescribeParameterEncryptionRequest, in the dictionary describeParameterEncryptionRpcOriginalRpcDictionary.");

                        // Add the describe parameter encryption RPC request as the key and its corresponding original rpc request to the dictionary.
                        describeParameterEncryptionRpcOriginalRpcDictionary.Add(rpcDescribeParameterEncryptionRequest, _RPCList[i]);
                    }
                }

                describeParameterEncryptionRpcOriginalRpcMap = new ReadOnlyDictionary<_SqlRPC, _SqlRPC>(describeParameterEncryptionRpcOriginalRpcDictionary);

                if (describeParameterEncryptionRpcOriginalRpcMap.Count == 0)
                {
                    // If no parameters are present, nothing to do, simply return.
                    return null;
                }
                else
                {
                    inputParameterEncryptionNeeded = true;
                }

                _sqlRPCParameterEncryptionReqArray = new _SqlRPC[describeParameterEncryptionRpcOriginalRpcMap.Count];
                describeParameterEncryptionRpcOriginalRpcMap.Keys.CopyTo(_sqlRPCParameterEncryptionReqArray, 0);

                Debug.Assert(_sqlRPCParameterEncryptionReqArray.Length > 0, "There should be at-least 1 describe parameter encryption rpc request.");
                Debug.Assert(_sqlRPCParameterEncryptionReqArray.Length <= _RPCList.Count,
                                "The number of decribe parameter encryption RPC requests is more than the number of original RPC requests.");
            }
            //Always Encrypted generally operates only on parameterized queries. However enclave based Always encrypted also supports unparameterized queries
            else if (ShouldUseEnclaveBasedWorkflow || (0 != GetParameterCount(_parameters)))
            {
                // Fetch params for a single batch
                inputParameterEncryptionNeeded = true;
                _sqlRPCParameterEncryptionReqArray = new _SqlRPC[1];

                _SqlRPC rpc = null;
                GetRPCObject(0, GetParameterCount(_parameters), ref rpc);
                Debug.Assert(rpc != null, "GetRPCObject should not return rpc as null.");

                rpc.rpcName = CommandText;
                rpc.userParams = _parameters;

                // Prepare the RPC request for describe parameter encryption procedure.
                PrepareDescribeParameterEncryptionRequest(rpc, ref _sqlRPCParameterEncryptionReqArray[0], serializedAttestationParameters);
                Debug.Assert(_sqlRPCParameterEncryptionReqArray[0] != null, "_sqlRPCParameterEncryptionReqArray[0] should not be null, after call to PrepareDescribeParameterEncryptionRequest.");
            }

            if (inputParameterEncryptionNeeded)
            {
                // Set the flag that indicates that parameter encryption requests are currently in-progress.
                IsDescribeParameterEncryptionRPCCurrentlyInProgress = true;

#if DEBUG
                // Failpoint to force the thread to halt to simulate cancellation of SqlCommand.
                if (_sleepDuringTryFetchInputParameterEncryptionInfo)
                {
                    Thread.Sleep(10000);
                }
#endif

                // Execute the RPC.
                return RunExecuteReaderTds(
                    CommandBehavior.Default,
                    runBehavior: RunBehavior.ReturnImmediately,
                    returnStream: true,
                    isAsync: isAsync,
                    timeout: timeout,
                    task: out task,
                    asyncWrite: asyncWrite,
                    isRetry: false,
                    ds: null,
                    describeParameterEncryptionRequest: true);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Constructs a SqlParameter with a given string value
        /// </summary>
        /// <param name="queryText"></param>
        /// <returns></returns>
        private SqlParameter GetSqlParameterWithQueryText(string queryText)
        {
            SqlParameter sqlParam = new SqlParameter(null, ((queryText.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT) ? SqlDbType.NVarChar : SqlDbType.NText, queryText.Length);
            sqlParam.Value = queryText;

            return sqlParam;
        }

        /// <summary>
        /// Constructs the sp_describe_parameter_encryption request with the values from the original RPC call.	
        /// Prototype for &lt;sp_describe_parameter_encryption&gt; is 	
        /// exec sp_describe_parameter_encryption @tsql=N'[SQL Statement]', @params=N'@p1 varbinary(256)'
        /// </summary>
        /// <param name="originalRpcRequest"></param>
        /// <param name="describeParameterEncryptionRequest"></param>
        /// <param name="attestationParameters"></param>
        private void PrepareDescribeParameterEncryptionRequest(_SqlRPC originalRpcRequest, ref _SqlRPC describeParameterEncryptionRequest, byte[] attestationParameters = null)
        {
            Debug.Assert(originalRpcRequest != null);

            // Construct the RPC request for sp_describe_parameter_encryption
            // sp_describe_parameter_encryption always has 2 parameters (stmt, paramlist).
            // sp_describe_parameter_encryption can have an optional 3rd parameter (attestationParameters), used to identify and execute attestation protocol
            GetRPCObject(attestationParameters == null ? 2 : 3, 0, ref describeParameterEncryptionRequest, forSpDescribeParameterEncryption: true);
            describeParameterEncryptionRequest.rpcName = "sp_describe_parameter_encryption";

            // Prepare @tsql parameter
            string text;

            // In _batchRPCMode, The actual T-SQL query is in the first parameter and not present as the rpcName, as is the case with non-_batchRPCMode.
            if (_batchRPCMode)
            {
                Debug.Assert(originalRpcRequest.systemParamCount > 0,
                    "originalRpcRequest didn't have at-least 1 parameter in BatchRPCMode, in PrepareDescribeParameterEncryptionRequest.");
                text = (string)originalRpcRequest.systemParams[0].Value;
                //@tsql
                SqlParameter tsqlParam = describeParameterEncryptionRequest.systemParams[0];
                tsqlParam.SqlDbType = ((text.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT) ? SqlDbType.NVarChar : SqlDbType.NText;
                tsqlParam.Value = text;
                tsqlParam.Size = text.Length;
                tsqlParam.Direction = ParameterDirection.Input;
            }
            else
            {
                text = originalRpcRequest.rpcName;
                if (CommandType == CommandType.StoredProcedure)
                {
                    // For stored procedures, we need to prepare @tsql in the following format
                    // N'EXEC sp_name @param1=@param1, @param1=@param2, ..., @paramN=@paramN'
                    describeParameterEncryptionRequest.systemParams[0] = BuildStoredProcedureStatementForColumnEncryption(text, originalRpcRequest.userParams);
                }
                else
                {
                    //@tsql
                    SqlParameter tsqlParam = describeParameterEncryptionRequest.systemParams[0];
                    tsqlParam.SqlDbType = ((text.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT) ? SqlDbType.NVarChar : SqlDbType.NText;
                    tsqlParam.Value = text;
                    tsqlParam.Size = text.Length;
                    tsqlParam.Direction = ParameterDirection.Input;
                }
            }

            Debug.Assert(text != null, "@tsql parameter is null in PrepareDescribeParameterEncryptionRequest.");
            string parameterList = null;

            // In BatchRPCMode, the input parameters start at parameters[1]. parameters[0] is the T-SQL statement. rpcName is sp_executesql.
            // And it is already in the format expected out of BuildParamList, which is not the case with Non-BatchRPCMode.
            if (_batchRPCMode)
            {
                // systemParamCount == 2 when user parameters are supplied to BuildExecuteSql
                if (originalRpcRequest.systemParamCount > 1)
                {
                    parameterList = (string)originalRpcRequest.systemParams[1].Value;
                }
            }
            else
            {
                // Prepare @params parameter
                // Need to create new parameters as we cannot have the same parameter being part of two SqlCommand objects
                SqlParameterCollection tempCollection = new SqlParameterCollection();

                if (originalRpcRequest.userParams != null)
                {
                    for (int i = 0; i < originalRpcRequest.userParams.Count; i++)
                    {
                        SqlParameter param = originalRpcRequest.userParams[i];
                        SqlParameter paramCopy = new SqlParameter(
                            param.ParameterName,
                            param.SqlDbType,
                            param.Size,
                            param.Direction,
                            param.Precision,
                            param.Scale,
                            param.SourceColumn,
                            param.SourceVersion,
                            param.SourceColumnNullMapping,
                            param.Value,
                            param.XmlSchemaCollectionDatabase,
                            param.XmlSchemaCollectionOwningSchema,
                            param.XmlSchemaCollectionName
                        );
                        paramCopy.CompareInfo = param.CompareInfo;
                        paramCopy.TypeName = param.TypeName;
                        paramCopy.UdtTypeName = param.UdtTypeName;
                        paramCopy.IsNullable = param.IsNullable;
                        paramCopy.LocaleId = param.LocaleId;
                        paramCopy.Offset = param.Offset;

                        tempCollection.Add(paramCopy);
                    }
                }

                Debug.Assert(_stateObj == null, "_stateObj should be null at this time, in PrepareDescribeParameterEncryptionRequest.");
                Debug.Assert(_activeConnection != null, "_activeConnection should not be null at this time, in PrepareDescribeParameterEncryptionRequest.");
                TdsParser tdsParser = null;

                if (_activeConnection.Parser != null)
                {
                    tdsParser = _activeConnection.Parser;
                    if ((tdsParser == null) || (tdsParser.State == TdsParserState.Broken) || (tdsParser.State == TdsParserState.Closed))
                    {
                        // Connection's parser is null as well, therefore we must be closed
                        throw ADP.ClosedConnectionError();
                    }
                }

                parameterList = BuildParamList(tdsParser, tempCollection, includeReturnValue: true);
            }

            SqlParameter paramsParam = describeParameterEncryptionRequest.systemParams[1];
            paramsParam.SqlDbType = ((parameterList.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT) ? SqlDbType.NVarChar : SqlDbType.NText;
            paramsParam.Size = parameterList.Length;
            paramsParam.Value = parameterList;
            paramsParam.Direction = ParameterDirection.Input;

            if (attestationParameters != null)
            {
                SqlParameter attestationParametersParam = describeParameterEncryptionRequest.systemParams[2];
                attestationParametersParam.SqlDbType = SqlDbType.VarBinary;
                attestationParametersParam.Size = attestationParameters.Length;
                attestationParametersParam.Value = attestationParameters;
                attestationParametersParam.Direction = ParameterDirection.Input;
            }
        }

        /// <summary>
        /// Read the output of sp_describe_parameter_encryption
        /// </summary>
        /// <param name="ds">Resultset from calling to sp_describe_parameter_encryption</param>
        /// <param name="describeParameterEncryptionRpcOriginalRpcMap"> Readonly dictionary with the map of parameter encryption rpc requests with the corresponding original rpc requests.</param>
        /// <param name="isRetry">Indicates if this is a retry from a failed call.</param>
        private void ReadDescribeEncryptionParameterResults(
            SqlDataReader ds,
            ReadOnlyDictionary<_SqlRPC, _SqlRPC> describeParameterEncryptionRpcOriginalRpcMap,
            bool isRetry)
        {
            _SqlRPC rpc = null;
            int currentOrdinal = -1;
            SqlTceCipherInfoEntry cipherInfoEntry;
            Dictionary<int, SqlTceCipherInfoEntry> columnEncryptionKeyTable = new Dictionary<int, SqlTceCipherInfoEntry>();

            Debug.Assert((describeParameterEncryptionRpcOriginalRpcMap != null) == _batchRPCMode,
                "describeParameterEncryptionRpcOriginalRpcMap should be non-null if and only if it is _batchRPCMode.");

            // Indicates the current result set we are reading, used in BatchRPCMode, where we can have more than 1 result set.
            int resultSetSequenceNumber = 0;

#if DEBUG
            // Keep track of the number of rows in the result sets.
            int rowsAffected = 0;
#endif

            // A flag that used in BatchRPCMode, to assert the result of lookup in to the dictionary maintaining the map of describe parameter encryption requests
            // and the corresponding original rpc requests.
            bool lookupDictionaryResult;

            do
            {
                if (_batchRPCMode)
                {
                    // If we got more RPC results from the server than what was requested.
                    if (resultSetSequenceNumber >= _sqlRPCParameterEncryptionReqArray.Length)
                    {
                        Debug.Assert(false, "Server sent back more results than what was expected for describe parameter encryption requests in _batchRPCMode.");
                        // Ignore the rest of the results from the server, if for whatever reason it sends back more than what we expect.
                        break;
                    }
                }

                bool enclaveMetadataExists = true;

                // First read the column encryption key list
                while (ds.Read())
                {

#if DEBUG
                    rowsAffected++;
#endif

                    // Column Encryption Key Ordinal.
                    currentOrdinal = ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyOrdinal);
                    Debug.Assert(currentOrdinal >= 0, "currentOrdinal cannot be negative.");

                    // Try to see if there was already an entry for the current ordinal.
                    if (!columnEncryptionKeyTable.TryGetValue(currentOrdinal, out cipherInfoEntry))
                    {
                        // If an entry for this ordinal was not found, create an entry in the columnEncryptionKeyTable for this ordinal.
                        cipherInfoEntry = new SqlTceCipherInfoEntry(currentOrdinal);
                        columnEncryptionKeyTable.Add(currentOrdinal, cipherInfoEntry);
                    }

                    Debug.Assert(!cipherInfoEntry.Equals(default(SqlTceCipherInfoEntry)), "cipherInfoEntry should not be un-initialized.");

                    // Read the CEK.
                    byte[] encryptedKey = null;
                    int encryptedKeyLength = (int)ds.GetBytes((int)DescribeParameterEncryptionResultSet1.EncryptedKey, 0, encryptedKey, 0, 0);
                    encryptedKey = new byte[encryptedKeyLength];
                    ds.GetBytes((int)DescribeParameterEncryptionResultSet1.EncryptedKey, 0, encryptedKey, 0, encryptedKeyLength);

                    // Read the metadata version of the key.
                    // It should always be 8 bytes.
                    byte[] keyMdVersion = new byte[8];
                    ds.GetBytes((int)DescribeParameterEncryptionResultSet1.KeyMdVersion, 0, keyMdVersion, 0, keyMdVersion.Length);

                    // Validate the provider name
                    string providerName = ds.GetString((int)DescribeParameterEncryptionResultSet1.ProviderName);

                    string keyPath = ds.GetString((int)DescribeParameterEncryptionResultSet1.KeyPath);
                    cipherInfoEntry.Add(encryptedKey: encryptedKey,
                                        databaseId: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.DbId),
                                        cekId: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyId),
                                        cekVersion: ds.GetInt32((int)DescribeParameterEncryptionResultSet1.KeyVersion),
                                        cekMdVersion: keyMdVersion,
                                        keyPath: keyPath,
                                        keyStoreName: providerName,
                                        algorithmName: ds.GetString((int)DescribeParameterEncryptionResultSet1.KeyEncryptionAlgorithm));

                    bool isRequestedByEnclave = false;

                    // Servers supporting enclave computations should always
                    // return a boolean indicating whether the key is required by enclave or not.
                    if (this._activeConnection.Parser.TceVersionSupported >= TdsEnums.MIN_TCE_VERSION_WITH_ENCLAVE_SUPPORT)
                    {
                        isRequestedByEnclave =
                            ds.GetBoolean((int)DescribeParameterEncryptionResultSet1.IsRequestedByEnclave);
                    }
                    else
                    {
                        enclaveMetadataExists = false;
                    }

                    if (isRequestedByEnclave)
                    {
                        if (string.IsNullOrWhiteSpace(this.Connection.EnclaveAttestationUrl) && Connection.AttestationProtocol != SqlConnectionAttestationProtocol.None)
                        {
                            throw SQL.NoAttestationUrlSpecifiedForEnclaveBasedQuerySpDescribe(this._activeConnection.Parser.EnclaveType);
                        }

                        byte[] keySignature = null;

                        if (!ds.IsDBNull((int)DescribeParameterEncryptionResultSet1.KeySignature))
                        {
                            int keySignatureLength = (int)ds.GetBytes((int)DescribeParameterEncryptionResultSet1.KeySignature, 0, keySignature, 0, 0);
                            keySignature = new byte[keySignatureLength];
                            ds.GetBytes((int)DescribeParameterEncryptionResultSet1.KeySignature, 0, keySignature, 0, keySignatureLength);
                        }

                        SqlSecurityUtility.VerifyColumnMasterKeySignature(providerName, keyPath, isRequestedByEnclave, keySignature, _activeConnection, this);

                        int requestedKey = currentOrdinal;
                        SqlTceCipherInfoEntry cipherInfo;

                        // Lookup the key, failing which throw an exception
                        if (!columnEncryptionKeyTable.TryGetValue(requestedKey, out cipherInfo))
                        {
                            throw SQL.InvalidEncryptionKeyOrdinalEnclaveMetadata(requestedKey, columnEncryptionKeyTable.Count);
                        }

                        if (keysToBeSentToEnclave == null)
                        {
                            keysToBeSentToEnclave = new ConcurrentDictionary<int, SqlTceCipherInfoEntry>();
                            keysToBeSentToEnclave.TryAdd(currentOrdinal, cipherInfo);
                        }
                        else if (!keysToBeSentToEnclave.ContainsKey(currentOrdinal))
                        {
                            keysToBeSentToEnclave.TryAdd(currentOrdinal, cipherInfo);
                        }

                        requiresEnclaveComputations = true;
                    }
                }

                if (!enclaveMetadataExists && !ds.NextResult())
                {
                    throw SQL.UnexpectedDescribeParamFormatParameterMetadata();
                }

                // Find the RPC command that generated this tce request
                if (_batchRPCMode)
                {
                    Debug.Assert(_sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber] != null, "_sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber] should not be null.");

                    // Lookup in the dictionary to get the original rpc request corresponding to the describe parameter encryption request
                    // pointed to by _sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber]
                    rpc = null;
                    lookupDictionaryResult = describeParameterEncryptionRpcOriginalRpcMap.TryGetValue(_sqlRPCParameterEncryptionReqArray[resultSetSequenceNumber++], out rpc);

                    Debug.Assert(lookupDictionaryResult,
                        "Describe Parameter Encryption RPC request key must be present in the dictionary describeParameterEncryptionRpcOriginalRpcMap");
                    Debug.Assert(rpc != null,
                        "Describe Parameter Encryption RPC request's corresponding original rpc request must not be null in the dictionary describeParameterEncryptionRpcOriginalRpcMap");
                }
                else
                {
                    rpc = _rpcArrayOf1[0];
                }

                Debug.Assert(rpc != null, "rpc should not be null here.");

                int userParamCount = rpc.userParams?.Count ?? 0;
                int receivedMetadataCount = 0;
                if (!enclaveMetadataExists || ds.NextResult())
                {
                    // Iterate over the parameter names to read the encryption type info
                    while (ds.Read())
                    {
#if DEBUG
                        rowsAffected++;
#endif
                        Debug.Assert(rpc != null, "Describe Parameter Encryption requested for non-tce spec proc");
                        string parameterName = ds.GetString((int)DescribeParameterEncryptionResultSet2.ParameterName);

                        // When the RPC object gets reused, the parameter array has more parameters that the valid params for the command.
                        // Null is used to indicate the end of the valid part of the array. Refer to GetRPCObject().
                        for (int index = 0; index < userParamCount; index++)
                        {
                            SqlParameter sqlParameter = rpc.userParams[index];
                            Debug.Assert(sqlParameter != null, "sqlParameter should not be null.");

                            if (SqlParameter.ParameterNamesEqual(sqlParameter.ParameterName, parameterName, StringComparison.Ordinal))
                            {
                                Debug.Assert(sqlParameter.CipherMetadata == null, "param.CipherMetadata should be null.");
                                sqlParameter.HasReceivedMetadata = true;
                                receivedMetadataCount += 1;
                                // Found the param, setup the encryption info.
                                byte columnEncryptionType = ds.GetByte((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionType);
                                if ((byte)SqlClientEncryptionType.PlainText != columnEncryptionType)
                                {
                                    byte cipherAlgorithmId = ds.GetByte((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionAlgorithm);
                                    int columnEncryptionKeyOrdinal = ds.GetInt32((int)DescribeParameterEncryptionResultSet2.ColumnEncryptionKeyOrdinal);
                                    byte columnNormalizationRuleVersion = ds.GetByte((int)DescribeParameterEncryptionResultSet2.NormalizationRuleVersion);

                                    // Lookup the key, failing which throw an exception
                                    if (!columnEncryptionKeyTable.TryGetValue(columnEncryptionKeyOrdinal, out cipherInfoEntry))
                                    {
                                        throw SQL.InvalidEncryptionKeyOrdinalParameterMetadata(columnEncryptionKeyOrdinal, columnEncryptionKeyTable.Count);
                                    }

                                    sqlParameter.CipherMetadata = new SqlCipherMetadata(sqlTceCipherInfoEntry: cipherInfoEntry,
                                                                                        ordinal: unchecked((ushort)-1),
                                                                                        cipherAlgorithmId: cipherAlgorithmId,
                                                                                        cipherAlgorithmName: null,
                                                                                        encryptionType: columnEncryptionType,
                                                                                        normalizationRuleVersion: columnNormalizationRuleVersion);

                                    // Decrypt the symmetric key.(This will also validate and throw if needed).
                                    Debug.Assert(_activeConnection != null, @"_activeConnection should not be null");
                                    SqlSecurityUtility.DecryptSymmetricKey(sqlParameter.CipherMetadata, _activeConnection, this);

                                    // This is effective only for BatchRPCMode even though we set it for non-BatchRPCMode also,
                                    // since for non-BatchRPCMode mode, paramoptions gets thrown away and reconstructed in BuildExecuteSql.
                                    int options = (int)(rpc.userParamMap[index] >> 32);
                                    options |= TdsEnums.RPC_PARAM_ENCRYPTED;
                                    rpc.userParamMap[index] = ((((long)options) << 32) | (long)index);
                                }

                                break;
                            }
                        }
                    }
                }

                // When the RPC object gets reused, the parameter array has more parameters that the valid params for the command.
                // Null is used to indicate the end of the valid part of the array. Refer to GetRPCObject().
                if (receivedMetadataCount != userParamCount)
                {
                    for (int index = 0; index < userParamCount; index++)
                    {
                        SqlParameter sqlParameter = rpc.userParams[index];
                        if (!sqlParameter.HasReceivedMetadata && sqlParameter.Direction != ParameterDirection.ReturnValue)
                        {
                            // Encryption MD wasn't sent by the server - we expect the metadata to be sent for all the parameters
                            // that were sent in the original sp_describe_parameter_encryption but not necessarily for return values,
                            // since there might be multiple return values but server will only send for one of them.
                            // For parameters that don't need encryption, the encryption type is set to plaintext.
                            throw SQL.ParamEncryptionMetadataMissing(sqlParameter.ParameterName, rpc.GetCommandTextOrRpcName());
                        }
                    }
                }

#if DEBUG
                Debug.Assert((rowsAffected == 0) || (rowsAffected == RowsAffectedByDescribeParameterEncryption),
                            "number of rows received (if received) for describe parameter encryption should be equal to rows affected by describe parameter encryption.");
#endif


                if (ShouldUseEnclaveBasedWorkflow && (enclaveAttestationParameters != null) && requiresEnclaveComputations)
                {
                    if (!ds.NextResult())
                    {
                        throw SQL.UnexpectedDescribeParamFormatAttestationInfo(this._activeConnection.Parser.EnclaveType);
                    }

                    bool attestationInfoRead = false;

                    while (ds.Read())
                    {
                        if (attestationInfoRead)
                        {
                            throw SQL.MultipleRowsReturnedForAttestationInfo();
                        }

                        int attestationInfoLength = (int)ds.GetBytes((int)DescribeParameterEncryptionResultSet3.AttestationInfo, 0, null, 0, 0);
                        byte[] attestationInfo = new byte[attestationInfoLength];
                        ds.GetBytes((int)DescribeParameterEncryptionResultSet3.AttestationInfo, 0, attestationInfo, 0, attestationInfoLength);

                        SqlConnectionAttestationProtocol attestationProtocol = this._activeConnection.AttestationProtocol;
                        string enclaveType = this._activeConnection.Parser.EnclaveType;

                        EnclaveDelegate.Instance.CreateEnclaveSession(
                            attestationProtocol,
                            enclaveType,
                            GetEnclaveSessionParameters(),
                            attestationInfo,
                            enclaveAttestationParameters,
                            customData,
                            customDataLength,
                            isRetry);
                        enclaveAttestationParameters = null;
                        attestationInfoRead = true;
                    }

                    if (!attestationInfoRead)
                    {
                        throw SQL.AttestationInfoNotReturnedFromSqlServer(this._activeConnection.Parser.EnclaveType, this._activeConnection.EnclaveAttestationUrl);
                    }
                }

                // The server has responded with encryption related information for this rpc request. So clear the needsFetchParameterEncryptionMetadata flag.
                rpc.needsFetchParameterEncryptionMetadata = false;
            } while (ds.NextResult());

            // Verify that we received response for each rpc call needs tce
            if (_batchRPCMode)
            {
                for (int i = 0; i < _RPCList.Count; i++)
                {
                    if (_RPCList[i].needsFetchParameterEncryptionMetadata)
                    {
                        throw SQL.ProcEncryptionMetadataMissing(_RPCList[i].rpcName);
                    }
                }
            }

            // If we are not in Batch RPC mode, update the query cache with the encryption MD.
            if (!_batchRPCMode && ShouldCacheEncryptionMetadata && (_parameters is not null && _parameters.Count > 0))
            {
                SqlQueryMetadataCache.GetInstance().AddQueryMetadata(this, ignoreQueriesWithReturnValueParams: true);
            }
        }

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/Clone/*'/>
        public SqlCommand Clone()
        {
            SqlCommand clone = new SqlCommand(this);
            SqlClientEventSource.Log.TryTraceEvent("SqlCommand.Clone | API | Object Id {0}, Clone Object Id {1}, Client Connection Id {2}", ObjectID, clone.ObjectID, Connection?.ClientConnectionId);
            return clone;
        }

        object ICloneable.Clone() =>
            Clone();

        private Task<T> RegisterForConnectionCloseNotification<T>(Task<T> outerTask)
        {
            SqlConnection connection = _activeConnection;
            if (connection == null)
            {
                // No connection
                throw ADP.ClosedConnectionError();
            }

            return connection.RegisterForConnectionCloseNotification(outerTask, this, SqlReferenceCollection.CommandTag);
        }

        // validates that a command has commandText and a non-busy open connection
        // throws exception for error case, returns false if the commandText is empty
        private void ValidateCommand(bool isAsync, [CallerMemberName] string method = "")
        {
            if (_activeConnection == null)
            {
                throw ADP.ConnectionRequired(method);
            }

            // Ensure that the connection is open and that the Parser is in the correct state
            SqlInternalConnectionTds tdsConnection = _activeConnection.InnerConnection as SqlInternalConnectionTds;

            // Ensure that if column encryption override was used then server supports its
            if (((SqlCommandColumnEncryptionSetting.UseConnectionSetting == ColumnEncryptionSetting && _activeConnection.IsColumnEncryptionSettingEnabled)
                 || (ColumnEncryptionSetting == SqlCommandColumnEncryptionSetting.Enabled || ColumnEncryptionSetting == SqlCommandColumnEncryptionSetting.ResultSetOnly))
                && tdsConnection != null
                && tdsConnection.Parser != null
                && !tdsConnection.Parser.IsColumnEncryptionSupported)
            {
                throw SQL.TceNotSupported();
            }

            if (tdsConnection != null)
            {
                var parser = tdsConnection.Parser;
                if ((parser == null) || (parser.State == TdsParserState.Closed))
                {
                    throw ADP.OpenConnectionRequired(method, ConnectionState.Closed);
                }
                else if (parser.State != TdsParserState.OpenLoggedIn)
                {
                    throw ADP.OpenConnectionRequired(method, ConnectionState.Broken);
                }
            }
            else if (_activeConnection.State == ConnectionState.Closed)
            {
                throw ADP.OpenConnectionRequired(method, ConnectionState.Closed);
            }
            else if (_activeConnection.State == ConnectionState.Broken)
            {
                throw ADP.OpenConnectionRequired(method, ConnectionState.Broken);
            }

            ValidateAsyncCommand();

            // close any non MARS dead readers, if applicable, and then throw if still busy.
            // Throw if we have a live reader on this command
            _activeConnection.ValidateConnectionForExecute(method, this);
            // Check to see if the currently set transaction has completed.  If so,
            // null out our local reference.
            if (_transaction != null && _transaction.Connection == null)
            {
                _transaction = null;
            }

            // throw if the connection is in a transaction but there is no
            // locally assigned transaction object
            if (_activeConnection.HasLocalTransactionFromAPI && _transaction == null)
            {
                throw ADP.TransactionRequired(method);
            }

            // if we have a transaction, check to ensure that the active
            // connection property matches the connection associated with
            // the transaction
            if (_transaction != null && _activeConnection != _transaction.Connection)
            {
                throw ADP.TransactionConnectionMismatch();
            }

            if (string.IsNullOrEmpty(this.CommandText))
            {
                throw ADP.CommandTextRequired(method);
            }
        }

        private void ValidateAsyncCommand()
        {
            if (CachedAsyncState != null && CachedAsyncState.PendingAsyncOperation)
            {
                // Enforce only one pending async execute at a time.
                if (CachedAsyncState.IsActiveConnectionValid(_activeConnection))
                {
                    throw SQL.PendingBeginXXXExists();
                }
                else
                {
                    _stateObj = null; // Session was re-claimed by session pool upon connection close.
                    CachedAsyncState.ResetAsyncState();
                }
            }
        }

        private void GetStateObject(TdsParser parser = null)
        {
            Debug.Assert(_stateObj == null, "StateObject not null on GetStateObject");
            Debug.Assert(_activeConnection != null, "no active connection?");

            if (_pendingCancel)
            {
                _pendingCancel = false; // Not really needed, but we'll reset anyways.

                // If a pendingCancel exists on the object, we must have had a Cancel() call
                // between the point that we entered an Execute* API and the point in Execute* that
                // we proceeded to call this function and obtain a stateObject.  In that case,
                // we now throw a cancelled error.
                throw SQL.OperationCancelled();
            }

            if (parser == null)
            {
                parser = _activeConnection.Parser;
                if ((parser == null) || (parser.State == TdsParserState.Broken) || (parser.State == TdsParserState.Closed))
                {
                    // Connection's parser is null as well, therefore we must be closed
                    throw ADP.ClosedConnectionError();
                }
            }

            TdsParserStateObject stateObj = parser.GetSession(this);
            stateObj.StartSession(this);

            _stateObj = stateObj;

            if (_pendingCancel)
            {
                _pendingCancel = false; // Not really needed, but we'll reset anyways.

                // If a pendingCancel exists on the object, we must have had a Cancel() call
                // between the point that we entered this function and the point where we obtained
                // and actually assigned the stateObject to the local member.  It is possible
                // that the flag is set as well as a call to stateObj.Cancel - though that would
                // be a no-op.  So - throw.
                throw SQL.OperationCancelled();
            }
        }

        private void ReliablePutStateObject()
        {
            PutStateObject();
        }

        private void PutStateObject()
        {
            TdsParserStateObject stateObj = _stateObj;
            _stateObj = null;

            if (stateObj != null)
            {
                stateObj.CloseSession();
            }
        }

        internal void OnDoneDescribeParameterEncryptionProc(TdsParserStateObject stateObj)
        {
            // called per rpc batch complete
            if (_batchRPCMode)
            {
                OnDone(stateObj, _currentlyExecutingDescribeParameterEncryptionRPC, _sqlRPCParameterEncryptionReqArray, _rowsAffected);
                _currentlyExecutingDescribeParameterEncryptionRPC++;
            }
        }

        internal void OnDoneProc(TdsParserStateObject stateObject)
        {
            // called per rpc batch complete
            if (_batchRPCMode)
            {
                OnDone(stateObject, _currentlyExecutingBatch, _RPCList, _rowsAffected);
                _currentlyExecutingBatch++;
                Debug.Assert(_RPCList.Count >= _currentlyExecutingBatch, "OnDoneProc: Too many DONEPROC events");
            }
        }

        private static void OnDone(TdsParserStateObject stateObj, int index, IList<_SqlRPC> rpcList, int rowsAffected)
        {
            _SqlRPC current = rpcList[index];
            _SqlRPC previous = (index > 0) ? rpcList[index - 1] : null;

            // track the records affected for the just completed rpc batch
            // _rowsAffected is cumulative for ExecuteNonQuery across all rpc batches
            current.cumulativeRecordsAffected = rowsAffected;

            current.recordsAffected =
                (((previous != null) && (0 <= rowsAffected))
                    ? (rowsAffected - Math.Max(previous.cumulativeRecordsAffected, 0))
                    : rowsAffected);

            if (current.batchCommand != null)
            {
                current.batchCommand.SetRecordAffected(current.recordsAffected.GetValueOrDefault());
            }

            // track the error collection (not available from TdsParser after ExecuteNonQuery)
            // and the which errors are associated with the just completed rpc batch
            current.errorsIndexStart = previous?.errorsIndexEnd ?? 0;
            current.errorsIndexEnd = stateObj.ErrorCount;
            current.errors = stateObj._errors;

            // track the warning collection (not available from TdsParser after ExecuteNonQuery)
            // and the which warnings are associated with the just completed rpc batch
            current.warningsIndexStart = previous?.warningsIndexEnd ?? 0;
            current.warningsIndexEnd = stateObj.WarningCount;
            current.warnings = stateObj._warnings;
        }

        internal void OnReturnStatus(int status)
        {
            // Don't set the return status if this is the status for sp_describe_parameter_encryption.
            if (_inPrepare || IsDescribeParameterEncryptionRPCCurrentlyInProgress)
            {
                return;
            }

            SqlParameterCollection parameters = _parameters;
            if (_batchRPCMode)
            {
                if (_RPCList.Count > _currentlyExecutingBatch)
                {
                    parameters = _RPCList[_currentlyExecutingBatch].userParams;
                }
                else
                {
                    Debug.Fail("OnReturnStatus: SqlCommand got too many DONEPROC events");
                    parameters = null;
                }
            }
            // see if a return value is bound
            int count = GetParameterCount(parameters);
            for (int i = 0; i < count; i++)
            {
                SqlParameter parameter = parameters[i];
                if (parameter.Direction == ParameterDirection.ReturnValue)
                {
                    object v = parameter.Value;

                    // if the user bound a sqlint32 (the only valid one for status, use it)
                    if (v != null && (v.GetType() == typeof(SqlInt32)))
                    {
                        parameter.Value = new SqlInt32(status); // value type
                    }
                    else
                    {
                        parameter.Value = status;
                    }

                    // If we are not in Batch RPC mode, update the query cache with the encryption MD.
                    // We can do this now that we have distinguished between ReturnValue and ReturnStatus.
                    // Read comment in AddQueryMetadata() for more details.
                    if (!_batchRPCMode && CachingQueryMetadataPostponed &&
                        ShouldCacheEncryptionMetadata && (_parameters is not null && _parameters.Count > 0))
                    {
                        SqlQueryMetadataCache.GetInstance().AddQueryMetadata(this, ignoreQueriesWithReturnValueParams: false);
                    }

                    break;
                }
            }
        }

        //
        // Move the return value to the corresponding output parameter.
        // Return parameters are sent in the order in which they were defined in the procedure.
        // If named, match the parameter name, otherwise fill in based on ordinal position.
        // If the parameter is not bound, then ignore the return value.
        //
        internal void OnReturnValue(SqlReturnValue rec, TdsParserStateObject stateObj)
        {
            if (_inPrepare)
            {
                if (!rec.value.IsNull)
                {
                    _prepareHandle = rec.value.Int32;
                }
                _inPrepare = false;
                return;
            }

            SqlParameterCollection parameters = GetCurrentParameterCollection();
            int count = GetParameterCount(parameters);

            SqlParameter thisParam = GetParameterForOutputValueExtraction(parameters, rec.parameter, count);

            if (thisParam != null)
            {
                // If the parameter's direction is InputOutput, Output or ReturnValue and it needs to be transparently encrypted/decrypted
                // then simply decrypt, deserialize and set the value.
                if (rec.cipherMD != null &&
                    thisParam.CipherMetadata != null &&
                    (thisParam.Direction == ParameterDirection.Output ||
                    thisParam.Direction == ParameterDirection.InputOutput ||
                    thisParam.Direction == ParameterDirection.ReturnValue))
                {
                    if (rec.tdsType != TdsEnums.SQLBIGVARBINARY)
                    {
                        throw SQL.InvalidDataTypeForEncryptedParameter(thisParam.GetPrefixedParameterName(), rec.tdsType, TdsEnums.SQLBIGVARBINARY);
                    }

                    // Decrypt the ciphertext
                    TdsParser parser = _activeConnection.Parser;
                    if ((parser == null) || (parser.State == TdsParserState.Closed) || (parser.State == TdsParserState.Broken))
                    {
                        throw ADP.ClosedConnectionError();
                    }

                    if (!rec.value.IsNull)
                    {
                        try
                        {
                            Debug.Assert(_activeConnection != null, @"_activeConnection should not be null");

                            // Get the key information from the parameter and decrypt the value.
                            rec.cipherMD.EncryptionInfo = thisParam.CipherMetadata.EncryptionInfo;
                            byte[] unencryptedBytes = SqlSecurityUtility.DecryptWithKey(rec.value.ByteArray, rec.cipherMD, _activeConnection, this);

                            if (unencryptedBytes != null)
                            {
                                // Denormalize the value and convert it to the parameter type.
                                SqlBuffer buffer = new SqlBuffer();
                                parser.DeserializeUnencryptedValue(buffer, unencryptedBytes, rec, stateObj, rec.NormalizationRuleVersion);
                                thisParam.SetSqlBuffer(buffer);
                            }
                        }
                        catch (Exception e)
                        {
                            throw SQL.ParamDecryptionFailed(thisParam.GetPrefixedParameterName(), null, e);
                        }
                    }
                    else
                    {
                        // Create a new SqlBuffer and set it to null
                        // Note: We can't reuse the SqlBuffer in "rec" below since it's already been set (to varbinary)
                        // in previous call to TryProcessReturnValue().
                        // Note 2: We will be coming down this code path only if the Command Setting is set to use TCE.
                        // We pass the command setting as TCE enabled in the below call for this reason.
                        SqlBuffer buff = new SqlBuffer();
                        TdsParser.GetNullSqlValue(buff, rec, SqlCommandColumnEncryptionSetting.Enabled, parser.Connection);
                        thisParam.SetSqlBuffer(buff);
                    }
                }
                else
                {
                    // copy over data

                    // if the value user has supplied a SqlType class, then just copy over the SqlType, otherwise convert
                    // to the com type
                    object val = thisParam.Value;

                    //set the UDT value as typed object rather than bytes
                    if (SqlDbType.Udt == thisParam.SqlDbType)
                    {
                        object data = null;
                        try
                        {
                            Connection.CheckGetExtendedUDTInfo(rec, true);

                            //extract the byte array from the param value
                            if (rec.value.IsNull)
                            {
                                data = DBNull.Value;
                            }
                            else
                            {
                                data = rec.value.ByteArray; //should work for both sql and non-sql values
                            }

                            //call the connection to instantiate the UDT object
                            thisParam.Value = Connection.GetUdtValue(data, rec, false);
                        }
                        catch (FileNotFoundException e)
                        {
                            // Assign Assembly.Load failure in case where assembly not on client.
                            // This allows execution to complete and failure on SqlParameter.Value.
                            thisParam.SetUdtLoadError(e);
                        }
                        catch (FileLoadException e)
                        {
                            // Assign Assembly.Load failure in case where assembly cannot be loaded on client.
                            // This allows execution to complete and failure on SqlParameter.Value.
                            thisParam.SetUdtLoadError(e);
                        }

                        return;
                    }
                    else
                    {
                        thisParam.SetSqlBuffer(rec.value);
                    }

                    MetaType mt = MetaType.GetMetaTypeFromSqlDbType(rec.type, false);

                    if (rec.type == SqlDbType.Decimal)
                    {
                        thisParam.ScaleInternal = rec.scale;
                        thisParam.PrecisionInternal = rec.precision;
                    }
                    else if (mt.IsVarTime)
                    {
                        thisParam.ScaleInternal = rec.scale;
                    }
                    else if (rec.type == SqlDbType.Xml)
                    {
                        SqlCachedBuffer cachedBuffer = (thisParam.Value as SqlCachedBuffer);
                        if (cachedBuffer != null)
                        {
                            thisParam.Value = cachedBuffer.ToString();
                        }
                    }

                    if (rec.collation != null)
                    {
                        Debug.Assert(mt.IsCharType, "Invalid collation structure for non-char type");
                        thisParam.Collation = rec.collation;
                    }
                }
            }

            return;
        }

        private SqlParameterCollection GetCurrentParameterCollection()
        {
            if (_batchRPCMode)
            {
                if (_RPCList.Count > _currentlyExecutingBatch)
                {
                    return _RPCList[_currentlyExecutingBatch].userParams;
                }
                else
                {
                    Debug.Fail("OnReturnValue: SqlCommand got too many DONEPROC events");
                    return null;
                }
            }
            else
            {
                return _parameters;
            }
        }

        private SqlParameter GetParameterForOutputValueExtraction(SqlParameterCollection parameters,
                        string paramName, int paramCount)
        {
            SqlParameter thisParam = null;
            bool foundParam = false;

            if (paramName == null)
            {
                // rec.parameter should only be null for a return value from a function
                for (int i = 0; i < paramCount; i++)
                {
                    thisParam = parameters[i];
                    // searching for ReturnValue
                    if (thisParam.Direction == ParameterDirection.ReturnValue)
                    {
                        foundParam = true;
                        break; // found it
                    }
                }
            }
            else
            {
                for (int i = 0; i < paramCount; i++)
                {
                    thisParam = parameters[i];
                    // searching for Output or InputOutput or ReturnValue with matching name
                    if (
                        thisParam.Direction != ParameterDirection.Input &&
                        thisParam.Direction != ParameterDirection.ReturnValue &&
                        SqlParameter.ParameterNamesEqual(paramName, thisParam.ParameterName, StringComparison.Ordinal)
                    )
                    {
                        foundParam = true;
                        break; // found it
                    }
                }
            }

            if (foundParam)
            {
                return thisParam;
            }
            else
            {
                return null;
            }
        }

        private void GetRPCObject(int systemParamCount, int userParamCount, ref _SqlRPC rpc, bool forSpDescribeParameterEncryption = false)
        {
            // Designed to minimize necessary allocations
            if (rpc == null)
            {
                if (!forSpDescribeParameterEncryption)
                {
                    if (_rpcArrayOf1 == null)
                    {
                        _rpcArrayOf1 = new _SqlRPC[1];
                        _rpcArrayOf1[0] = new _SqlRPC();
                    }

                    rpc = _rpcArrayOf1[0];
                }
                else
                {
                    if (_rpcForEncryption == null)
                    {
                        _rpcForEncryption = new _SqlRPC();
                    }

                    rpc = _rpcForEncryption;
                }
            }

            rpc.ProcID = 0;
            rpc.rpcName = null;
            rpc.options = 0;
            rpc.systemParamCount = systemParamCount;
            rpc.needsFetchParameterEncryptionMetadata = false;

            int currentCount = rpc.systemParams?.Length ?? 0;

            // Make sure there is enough space in the parameters and paramoptions arrays
            if (currentCount < systemParamCount)
            {
                Array.Resize(ref rpc.systemParams, systemParamCount);
                Array.Resize(ref rpc.systemParamOptions, systemParamCount);
                for (int index = currentCount; index < systemParamCount; index++)
                {
                    rpc.systemParams[index] = new SqlParameter();
                }
            }

            for (int ii = 0; ii < systemParamCount; ii++)
            {
                rpc.systemParamOptions[ii] = 0;
            }

            if ((rpc.userParamMap?.Length ?? 0) < userParamCount)
            {
                Array.Resize(ref rpc.userParamMap, userParamCount);
            }
        }

        private void SetUpRPCParameters(_SqlRPC rpc, bool inSchema, SqlParameterCollection parameters)
        {
            int paramCount = GetParameterCount(parameters);
            int userParamCount = 0;

            for (int index = 0; index < paramCount; index++)
            {
                SqlParameter parameter = parameters[index];
                parameter.Validate(index, CommandType.StoredProcedure == CommandType);

                // func will change type to that with a 4 byte length if the type has a two
                // byte length and a parameter length > than that expressible in 2 bytes
                if ((!parameter.ValidateTypeLengths().IsPlp) && (parameter.Direction != ParameterDirection.Output))
                {
                    parameter.FixStreamDataForNonPLP();
                }

                if (ShouldSendParameter(parameter))
                {
                    byte options = 0;

                    // set output bit
                    if (parameter.Direction == ParameterDirection.InputOutput || parameter.Direction == ParameterDirection.Output)
                    {
                        options = TdsEnums.RPC_PARAM_BYREF;
                    }

                    // Set the encryped bit, if the parameter is to be encrypted.
                    if (parameter.CipherMetadata != null)
                    {
                        options |= TdsEnums.RPC_PARAM_ENCRYPTED;
                    }

                    // set default value bit
                    if (parameter.Direction != ParameterDirection.Output)
                    {
                        // remember that Convert.IsEmpty is null, DBNull.Value is a database null!

                        // Don't assume a default value exists for parameters in the case when
                        // the user is simply requesting schema.
                        // TVPs use DEFAULT and do not allow NULL, even for schema only.
                        if (parameter.Value == null && (!inSchema || SqlDbType.Structured == parameter.SqlDbType))
                        {
                            options |= TdsEnums.RPC_PARAM_DEFAULT;
                        }

                        // detect incorrectly derived type names unchanged by the caller and fix them
                        if (parameter.IsDerivedParameterTypeName)
                        {
                            string[] parts = MultipartIdentifier.ParseMultipartIdentifier(parameter.TypeName, "[\"", "]\"", Strings.SQL_TDSParserTableName, false);
                            if (parts != null && parts.Length == 4) // will always return int[4] right justified
                            {
                                if (
                                    parts[3] != null && // name must not be null
                                    parts[2] != null && // schema must not be null
                                    parts[1] != null // server should not be null or we don't need to remove it
                                )
                                {
                                    parameter.TypeName = QuoteIdentifier(parts.AsSpan(2, 2));
                                }
                            }
                        }
                    }

                    rpc.userParamMap[userParamCount] = ((((long)options) << 32) | (long)index);
                    userParamCount += 1;

                    // Must set parameter option bit for LOB_COOKIE if unfilled LazyMat blob
                }
            }

            rpc.userParamCount = userParamCount;
            rpc.userParams = parameters;
        }

        //
        // returns true if the parameter is not a return value
        // and it's value is not DBNull (for a nullable parameter)
        //
        private static bool ShouldSendParameter(SqlParameter p, bool includeReturnValue = false)
        {
            switch (p.Direction)
            {
                case ParameterDirection.ReturnValue:
                    // return value parameters are not sent, except for the parameter list of sp_describe_parameter_encryption
                    return includeReturnValue;
                case ParameterDirection.Output:
                case ParameterDirection.InputOutput:
                case ParameterDirection.Input:
                    // InputOutput/Output parameters are aways sent
                    return true;
                default:
                    Debug.Fail("Invalid ParameterDirection!");
                    return false;
            }
        }

        private static int CountSendableParameters(SqlParameterCollection parameters)
        {
            int cParams = 0;

            if (parameters != null)
            {
                int count = parameters.Count;
                for (int i = 0; i < count; i++)
                {
                    if (ShouldSendParameter(parameters[i]))
                    {
                        cParams++;
                    }
                }
            }
            return cParams;
        }

        // Returns total number of parameters
        private static int GetParameterCount(SqlParameterCollection parameters)
        {
            return parameters != null ? parameters.Count : 0;
        }

        /// <summary>
        /// This function constructs a string parameter containing the exec statement in the following format
        /// N'EXEC sp_name @param1=@param1, @param1=@param2, ..., @paramN=@paramN'
        /// TODO: Need to handle return values.
        /// </summary>
        /// <param name="storedProcedureName">Stored procedure name</param>
        /// <param name="parameters">SqlParameter list</param>
        /// <returns>A string SqlParameter containing the constructed sql statement value</returns>
        private SqlParameter BuildStoredProcedureStatementForColumnEncryption(string storedProcedureName, SqlParameterCollection parameters)
        {
            Debug.Assert(CommandType == CommandType.StoredProcedure, "BuildStoredProcedureStatementForColumnEncryption() should only be called for stored procedures");
            Debug.Assert(!string.IsNullOrWhiteSpace(storedProcedureName), "storedProcedureName cannot be null or empty in BuildStoredProcedureStatementForColumnEncryption");

            StringBuilder execStatement = new StringBuilder();
            execStatement.Append(@"EXEC ");

            if (parameters is null)
            {
                execStatement.Append(ParseAndQuoteIdentifier(storedProcedureName, false));
                return new SqlParameter(
                    null,
                    ((execStatement.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT) ? SqlDbType.NVarChar : SqlDbType.NText,
                    execStatement.Length)
                {
                    Value = execStatement.ToString()
                };
            }

            // Find the return value parameter (if any).
            SqlParameter returnValueParameter = null;
            foreach (SqlParameter param in parameters)
            {
                if (param.Direction == ParameterDirection.ReturnValue)
                {
                    returnValueParameter = param;
                    break;
                }
            }

            // If there is a return value parameter we need to assign the result to it.
            // EXEC @returnValue = moduleName [parameters]
            if (returnValueParameter != null)
            {
                SqlParameter.AppendPrefixedParameterName(execStatement, returnValueParameter.ParameterName);
                execStatement.Append('=');
            }

            execStatement.Append(ParseAndQuoteIdentifier(storedProcedureName, false));

            // Build parameter list in the format
            // @param1=@param1, @param1=@param2, ..., @paramn=@paramn

            // Append the first parameter
            int index = 0;
            int count = parameters.Count;
            SqlParameter parameter;
            if (count > 0)
            {
                // Skip the return value parameters.
                while (index < parameters.Count && parameters[index].Direction == ParameterDirection.ReturnValue)
                {
                    index++;
                }

                if (index < count)
                {
                    parameter = parameters[index];
                    // Possibility of a SQL Injection issue through parameter names and how to construct valid identifier for parameters.
                    // Since the parameters comes from application itself, there should not be a security vulnerability.
                    // Also since the query is not executed, but only analyzed there is no possibility for elevation of privilege, but only for
                    // incorrect results which would only affect the user that attempts the injection.
                    execStatement.Append(' ');
                    SqlParameter.AppendPrefixedParameterName(execStatement, parameter.ParameterName);
                    execStatement.Append('=');
                    SqlParameter.AppendPrefixedParameterName(execStatement, parameter.ParameterName);

                    // InputOutput and Output parameters need to be marked as such.
                    if (parameter.Direction == ParameterDirection.Output ||
                        parameter.Direction == ParameterDirection.InputOutput)
                    {
                        execStatement.AppendFormat(@" OUTPUT");
                    }
                }
            }

            // Move to the next parameter.
            index++;

            // Append the rest of parameters
            for (; index < count; index++)
            {
                parameter = parameters[index];
                if (parameter.Direction != ParameterDirection.ReturnValue)
                {
                    execStatement.Append(", ");
                    SqlParameter.AppendPrefixedParameterName(execStatement, parameter.ParameterName);
                    execStatement.Append('=');
                    SqlParameter.AppendPrefixedParameterName(execStatement, parameter.ParameterName);

                    // InputOutput and Output parameters need to be marked as such.
                    if (
                        parameter.Direction == ParameterDirection.Output ||
                        parameter.Direction == ParameterDirection.InputOutput
                    )
                    {
                        execStatement.AppendFormat(@" OUTPUT");
                    }
                }
            }

            // Construct @tsql SqlParameter to be returned
            SqlParameter tsqlParameter = new SqlParameter(null, ((execStatement.Length << 1) <= TdsEnums.TYPE_SIZE_LIMIT) ? SqlDbType.NVarChar : SqlDbType.NText, execStatement.Length);
            tsqlParameter.Value = execStatement.ToString();

            return tsqlParameter;
        }

        // paramList parameter for sp_executesql, sp_prepare, and sp_prepexec
        internal string BuildParamList(TdsParser parser, SqlParameterCollection parameters, bool includeReturnValue = false)
        {
            StringBuilder paramList = new StringBuilder();
            bool fAddSeparator = false;

            int count = parameters.Count;
            for (int i = 0; i < count; i++)
            {
                SqlParameter sqlParam = parameters[i];
                sqlParam.Validate(i, CommandType.StoredProcedure == CommandType);
                // skip ReturnValue parameters; we never send them to the server
                if (!ShouldSendParameter(sqlParam, includeReturnValue))
                {
                    continue;
                }

                // add our separator for the ith parameter
                if (fAddSeparator)
                {
                    paramList.Append(',');
                }

                SqlParameter.AppendPrefixedParameterName(paramList, sqlParam.ParameterName);

                MetaType mt = sqlParam.InternalMetaType;

                //for UDTs, get the actual type name. Get only the typename, omit catalog and schema names.
                //in TSQL you should only specify the unqualified type name

                // paragraph above doesn't seem to be correct. Server won't find the type
                // if we don't provide a fully qualified name
                paramList.Append(" ");
                if (mt.SqlDbType == SqlDbType.Udt)
                {
                    string fullTypeName = sqlParam.UdtTypeName;
                    if (string.IsNullOrEmpty(fullTypeName))
                    {
                        throw SQL.MustSetUdtTypeNameForUdtParams();
                    }

                    paramList.Append(ParseAndQuoteIdentifier(fullTypeName, true /* is UdtTypeName */));
                }
                else if (mt.SqlDbType == SqlDbType.Structured)
                {
                    string typeName = sqlParam.TypeName;
                    if (string.IsNullOrEmpty(typeName))
                    {
                        throw SQL.MustSetTypeNameForParam(mt.TypeName, sqlParam.GetPrefixedParameterName());
                    }
                    paramList.Append(ParseAndQuoteIdentifier(typeName, false /* is not UdtTypeName*/));

                    // TVPs currently are the only Structured type and must be read only, so add that keyword
                    paramList.Append(" READONLY");
                }
                else
                {
                    // func will change type to that with a 4 byte length if the type has a two
                    // byte length and a parameter length > than that expressible in 2 bytes
                    mt = sqlParam.ValidateTypeLengths();
                    if ((!mt.IsPlp) && (sqlParam.Direction != ParameterDirection.Output))
                    {
                        sqlParam.FixStreamDataForNonPLP();
                    }
                    paramList.Append(mt.TypeName);
                }

                fAddSeparator = true;

                if (mt.SqlDbType == SqlDbType.Decimal)
                {
                    byte precision = sqlParam.GetActualPrecision();
                    byte scale = sqlParam.GetActualScale();

                    paramList.Append('(');

                    if (0 == precision)
                    {
                        precision = TdsEnums.DEFAULT_NUMERIC_PRECISION;
                    }

                    paramList.Append(precision);
                    paramList.Append(',');
                    paramList.Append(scale);
                    paramList.Append(')');
                }
                else if (mt.IsVarTime)
                {
                    byte scale = sqlParam.GetActualScale();

                    paramList.Append('(');
                    paramList.Append(scale);
                    paramList.Append(')');
                }
                else if (mt.SqlDbType == SqlDbTypeExtensions.Vector)
                {
                    // The validate function for SqlParameters would
                    // have already thrown InvalidCastException if an incompatible
                    // value is specified for SqlDbType Vector.
                    var sqlVectorProps = (ISqlVector)sqlParam.Value;
                    paramList.Append('(');
                    paramList.Append(sqlVectorProps.Length);
                    paramList.Append(')');
                }
                else if (!mt.IsFixed && !mt.IsLong && mt.SqlDbType != SqlDbType.Timestamp && mt.SqlDbType != SqlDbType.Udt && SqlDbType.Structured != mt.SqlDbType)
                {
                    int size = sqlParam.Size;

                    paramList.Append('(');

                    // if using non unicode types, obtain the actual byte length from the parser, with it's associated code page
                    if (mt.IsAnsiType)
                    {
                        object val = sqlParam.GetCoercedValue();
                        string s = null;

                        // deal with the sql types
                        if (val != null && (DBNull.Value != val))
                        {
                            s = (val as string);
                            if (s == null)
                            {
                                SqlString sval = val is SqlString ? (SqlString)val : SqlString.Null;
                                if (!sval.IsNull)
                                {
                                    s = sval.Value;
                                }
                            }
                        }

                        if (s != null)
                        {
                            int actualBytes = parser.GetEncodingCharLength(s, sqlParam.GetActualSize(), sqlParam.Offset, null);
                            // if actual number of bytes is greater than the user given number of chars, use actual bytes
                            if (actualBytes > size)
                            {
                                size = actualBytes;
                            }
                        }
                    }

                    // If the user specifies a 0-sized parameter for a variable len field
                    // pass over max size (8000 bytes or 4000 characters for wide types)
                    if (0 == size)
                    {
                        size = mt.IsSizeInCharacters ? (TdsEnums.MAXSIZE >> 1) : TdsEnums.MAXSIZE;
                    }

                    paramList.Append(size);
                    paramList.Append(')');
                }
                else if (mt.IsPlp && (mt.SqlDbType != SqlDbType.Xml) && (mt.SqlDbType != SqlDbType.Udt) && (mt.SqlDbType != SqlDbTypeExtensions.Json))
                {
                    paramList.Append("(max) ");
                }

                // set the output bit for Output or InputOutput parameters
                if (sqlParam.Direction != ParameterDirection.Input)
                    paramList.Append(" " + TdsEnums.PARAM_OUTPUT);
            }

            return paramList.ToString();
        }

        // Adds quotes to each part of a SQL identifier that may be multi-part, while leaving
        //  the result as a single composite name.
        private static string ParseAndQuoteIdentifier(string identifier, bool isUdtTypeName)
        {
            string[] strings = SqlParameter.ParseTypeName(identifier, isUdtTypeName);
            return QuoteIdentifier(strings);
        }

        private static string QuoteIdentifier(ReadOnlySpan<string> strings)
        {
            StringBuilder bld = new StringBuilder();

            // Stitching back together is a little tricky. Assume we want to build a full multi-part name
            //  with all parts except trimming separators for leading empty names (null or empty strings,
            //  but not whitespace). Separators in the middle should be added, even if the name part is 
            //  null/empty, to maintain proper location of the parts.
            for (int i = 0; i < strings.Length; i++)
            {
                if (0 < bld.Length)
                {
                    bld.Append('.');
                }
                if (strings[i] != null && 0 != strings[i].Length)
                {
                    ADP.AppendQuotedString(bld, "[", "]", strings[i]);
                }
            }

            return bld.ToString();
        }

        // returns set option text to turn on format only and key info on and off
        //  When we are executing as a text command, then we never need
        // to turn off the options since they command text is executed in the scope of sp_executesql.
        // For a stored proc command, however, we must send over batch sql and then turn off
        // the set options after we read the data.  See the code in Command.Execute()
        private string GetSetOptionsString(CommandBehavior behavior)
        {
            string s = null;

            if ((System.Data.CommandBehavior.SchemaOnly == (behavior & CommandBehavior.SchemaOnly)) ||
               (System.Data.CommandBehavior.KeyInfo == (behavior & CommandBehavior.KeyInfo)))
            {
                // SET FMTONLY ON will cause the server to ignore other SET OPTIONS, so turn
                // it off before we ask for browse mode metadata
                s = TdsEnums.FMTONLY_OFF;

                if (System.Data.CommandBehavior.KeyInfo == (behavior & CommandBehavior.KeyInfo))
                {
                    s = s + TdsEnums.BROWSE_ON;
                }

                if (System.Data.CommandBehavior.SchemaOnly == (behavior & CommandBehavior.SchemaOnly))
                {
                    s = s + TdsEnums.FMTONLY_ON;
                }
            }

            return s;
        }

        private string GetResetOptionsString(CommandBehavior behavior)
        {
            string s = null;

            // SET FMTONLY ON OFF
            if (System.Data.CommandBehavior.SchemaOnly == (behavior & CommandBehavior.SchemaOnly))
            {
                s = s + TdsEnums.FMTONLY_OFF;
            }

            // SET NO_BROWSETABLE OFF
            if (System.Data.CommandBehavior.KeyInfo == (behavior & CommandBehavior.KeyInfo))
            {
                s = s + TdsEnums.BROWSE_OFF;
            }

            return s;
        }

        private string GetCommandText(CommandBehavior behavior)
        {
            // build the batch string we send over, since we execute within a stored proc (sp_executesql), the SET options never need to be
            // turned off since they are scoped to the sproc
            Debug.Assert(System.Data.CommandType.Text == this.CommandType, "invalid call to GetCommandText for stored proc!");
            return GetSetOptionsString(behavior) + this.CommandText;
        }

        internal void CheckThrowSNIException()
        {
            var stateObj = _stateObj;
            if (stateObj != null)
            {
                stateObj.CheckThrowSNIException();
            }
        }

        // We're being notified that the underlying connection has closed
        internal void OnConnectionClosed()
        {
            var stateObj = _stateObj;
            if (stateObj != null)
            {
                stateObj.OnConnectionClosed();
            }
        }

        /// <summary>
        /// Get or add to the number of records affected by SpDescribeParameterEncryption.
        /// The below line is used only for debug asserts and not exposed publicly or impacts functionality otherwise.
        /// </summary>
        internal int RowsAffectedByDescribeParameterEncryption
        {
            get
            {
                return _rowsAffectedBySpDescribeParameterEncryption;
            }
            set
            {
                if (-1 == _rowsAffectedBySpDescribeParameterEncryption)
                {
                    _rowsAffectedBySpDescribeParameterEncryption = value;
                }
                else if (0 < value)
                {
                    _rowsAffectedBySpDescribeParameterEncryption += value;
                }
            }
        }

        /// <summary>
        /// Clear the state in sqlcommand related to describe parameter encryption RPC requests.
        /// </summary>
        private void ClearDescribeParameterEncryptionRequests()
        {
            _sqlRPCParameterEncryptionReqArray = null;
            _currentlyExecutingDescribeParameterEncryptionRPC = 0;
            IsDescribeParameterEncryptionRPCCurrentlyInProgress = false;
            _rowsAffectedBySpDescribeParameterEncryption = -1;
        }

        internal void ClearBatchCommand()
        {
            _RPCList?.Clear();
            _currentlyExecutingBatch = 0;
        }

        internal void SetBatchRPCMode(bool value, int commandCount = 1)
        {
            _batchRPCMode = value;
            ClearBatchCommand();
            if (_batchRPCMode)
            {
                if (_RPCList == null)
                {
                    _RPCList = new List<_SqlRPC>(commandCount);
                }
                else
                {
                    _RPCList.Capacity = commandCount;
                }
            }
        }

        internal void SetBatchRPCModeReadyToExecute()
        {
            Debug.Assert(_batchRPCMode, "Command is not in batch RPC Mode");
            Debug.Assert(_RPCList != null, "No batch commands specified");

            _currentlyExecutingBatch = 0;
        }

        /// <summary>
        /// Set the column encryption setting to the new one.
        /// Do not allow conflicting column encryption settings.
        /// </summary>
        private void SetColumnEncryptionSetting(SqlCommandColumnEncryptionSetting newColumnEncryptionSetting)
        {
            if (!_wasBatchModeColumnEncryptionSettingSetOnce)
            {
                _columnEncryptionSetting = newColumnEncryptionSetting;
                _wasBatchModeColumnEncryptionSettingSetOnce = true;
            }
            else if (_columnEncryptionSetting != newColumnEncryptionSetting)
            {
                throw SQL.BatchedUpdateColumnEncryptionSettingMismatch();
            }
        }

        internal void AddBatchCommand(SqlBatchCommand batchCommand)
        {
            Debug.Assert(_batchRPCMode, "Command is not in batch RPC Mode");
            Debug.Assert(_RPCList != null);

            _SqlRPC rpc = new _SqlRPC
            {
                batchCommand = batchCommand
            };
            string commandText = batchCommand.CommandText;
            CommandType cmdType = batchCommand.CommandType;

            CommandText = commandText;
            CommandType = cmdType;

            // Set the column encryption setting.
            SetColumnEncryptionSetting(batchCommand.ColumnEncryptionSetting);

            GetStateObject();
            if (cmdType == CommandType.StoredProcedure)
            {
                BuildRPC(false, batchCommand.Parameters, ref rpc);
            }
            else
            {
                // All batch sql statements must be executed inside sp_executesql, including those without parameters
                BuildExecuteSql(CommandBehavior.Default, commandText, batchCommand.Parameters, ref rpc);
            }

            _RPCList.Add(rpc);

            ReliablePutStateObject();
        }

        internal int? GetRecordsAffected(int commandIndex)
        {
            Debug.Assert(_batchRPCMode, "Command is not in batch RPC Mode");
            Debug.Assert(_RPCList != null, "batch command have been cleared");
            return _RPCList[commandIndex].recordsAffected;
        }

        internal SqlBatchCommand GetCurrentBatchCommand()
        {
            if (_batchRPCMode)
            {
                return _RPCList[_currentlyExecutingBatch].batchCommand;
            }
            else
            {
                return _rpcArrayOf1?[0].batchCommand;
            }
        }

        internal SqlBatchCommand GetBatchCommand(int index)
        {
            return _RPCList[index].batchCommand;
        }

        internal int GetCurrentBatchIndex()
        {
            return _batchRPCMode ? _currentlyExecutingBatch : -1;
        }

        internal SqlException GetErrors(int commandIndex)
        {
            SqlException result = null;
            _SqlRPC rpc = _RPCList[commandIndex];
            int length = (rpc.errorsIndexEnd - rpc.errorsIndexStart);
            if (0 < length)
            {
                SqlErrorCollection errors = new SqlErrorCollection();
                for (int i = rpc.errorsIndexStart; i < rpc.errorsIndexEnd; ++i)
                {
                    errors.Add(rpc.errors[i]);
                }
                for (int i = rpc.warningsIndexStart; i < rpc.warningsIndexEnd; ++i)
                {
                    errors.Add(rpc.warnings[i]);
                }
                result = SqlException.CreateException(errors, Connection.ServerVersion, Connection.ClientConnectionId, innerException: null, batchCommand: null);
            }
            return result;
        }

        private static void CancelIgnoreFailureCallback(object state)
        {
            SqlCommand command = (SqlCommand)state;
            command.CancelIgnoreFailure();
        }

        private void CancelIgnoreFailure()
        {
            // This method is used to route CancellationTokens to the Cancel method.
            // Cancellation is a suggestion, and exceptions should be ignored
            // rather than allowed to be unhandled, as there is no way to route
            // them to the caller.  It would be expected that the error will be
            // observed anyway from the regular method.  An example is cancelling
            // an operation on a closed connection.
            try
            {
                Cancel();
            }
            catch (Exception)
            {
            }
        }

        private void NotifyDependency()
        {
            if (_sqlDep != null)
            {
                _sqlDep.StartTimer(Notification);
            }
        }

        private void WriteBeginExecuteEvent()
        {
            SqlClientEventSource.Log.TryBeginExecuteEvent(ObjectID, Connection?.DataSource, Connection?.Database, CommandText, Connection?.ClientConnectionId);
        }

        /// <summary>
        /// Writes and end execute event in Event Source.
        /// </summary>
        /// <param name="success">True if SQL command finished successfully, otherwise false.</param>
        /// <param name="sqlExceptionNumber">Gets a number that identifies the type of error.</param>
        /// <param name="synchronous">True if SQL command was executed synchronously, otherwise false.</param>
        private void WriteEndExecuteEvent(bool success, int? sqlExceptionNumber, bool synchronous)
        {
            if (SqlClientEventSource.Log.IsExecutionTraceEnabled())
            {
                // SqlEventSource.WriteEvent(int, int, int, int) is faster than provided overload SqlEventSource.WriteEvent(int, object[]).
                // that's why trying to fit several booleans in one integer value

                // success state is stored the first bit in compositeState 0x01
                int successFlag = success ? 1 : 0;

                // isSqlException is stored in the 2nd bit in compositeState 0x100
                int isSqlExceptionFlag = sqlExceptionNumber.HasValue ? 2 : 0;

                // synchronous state is stored in the second bit in compositeState 0x10
                int synchronousFlag = synchronous ? 4 : 0;

                int compositeState = successFlag | isSqlExceptionFlag | synchronousFlag;

                SqlClientEventSource.Log.TryEndExecuteEvent(ObjectID, compositeState, sqlExceptionNumber.GetValueOrDefault(), Connection?.ClientConnectionId);
            }
        }
    }
}
