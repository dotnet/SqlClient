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

        private static readonly SqlDiagnosticListener s_diagnosticListener = new SqlDiagnosticListener();
        private bool _parentOperationStarted = false;

        internal static readonly Action<object> s_cancelIgnoreFailure = CancelIgnoreFailureCallback;

        private _SqlRPC[] _rpcArrayOf1 = null;                // Used for RPC executes
        private _SqlRPC _rpcForEncryption = null;                // Used for sp_describe_parameter_encryption RPC executes

        // cut down on object creation and cache all these
        // cached metadata
        private _SqlMetaDataSet _cachedMetaData;

        // Last TaskCompletionSource for reconnect task - use for cancellation only
        private TaskCompletionSource<object> _reconnectionCompletionSource = null;

#if DEBUG
        internal static int DebugForceAsyncWriteDelay { get; set; }
#endif

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
        
        private List<_SqlRPC> _RPCList;
        private int _currentlyExecutingBatch;

        /// <summary>
        /// A flag to indicate if EndExecute was already initiated by the Begin call.
        /// </summary>
        private volatile bool _internalEndExecuteInitiated;

        /// <summary>
        /// A flag to indicate whether we postponed caching the query metadata for this command.
        /// </summary>
        internal bool CachingQueryMetadataPostponed { get; set; }

        private bool IsProviderRetriable => SqlConfigurableRetryFactory.IsRetriable(RetryLogicProvider);

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

        // @TODO: Why not *return* it?
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
