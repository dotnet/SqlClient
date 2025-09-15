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
    }
}
