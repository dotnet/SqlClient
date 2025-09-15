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
    }
}
