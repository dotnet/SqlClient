// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Server;

#if NETFRAMEWORK
using System.Security.Permissions;
#endif

namespace Microsoft.Data.SqlClient
{
    public sealed partial class SqlCommand
    {
        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="default"]/*'/>
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public IAsyncResult BeginExecuteXmlReader() =>
            BeginExecuteXmlReader(callback: null, stateObject: null);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/BeginExecuteXmlReader[@name="AsyncCallbackAndstateObject"]/*'/>
        #if NETFRAMEWORK
        [HostProtection(ExternalThreading = true)]
        #endif
        public IAsyncResult BeginExecuteXmlReader(AsyncCallback callback, object stateObject)
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif
            
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.BeginExecuteXmlReader | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            return BeginExecuteXmlReaderInternal(
                CommandBehavior.SequentialAccess,
                callback,
                stateObject,
                timeout: 0,
                isRetry: false,
                asyncWrite: false);
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/EndExecuteXmlReader[@name="IAsyncResult"]/*'/>
        public XmlReader EndExecuteXmlReader(IAsyncResult asyncResult)
        {
            try
            {
                return EndExecuteXmlReaderInternal(asyncResult);
            }
            finally
            {
                SqlClientEventSource.Log.TryCorrelationTraceEvent(
                    "SqlCommand.EndExecuteXmlReader | API | Correlation | " +
                    $"Object Id {ObjectID}, " +
                    $"Activity Id {ActivityCorrelator.Current}, " +
                    $"Client Connection Id {_activeConnection.ClientConnectionId}, " +
                    $"Command Text '{CommandText}'");
            }
        }
        
        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReader/*'/>
        public XmlReader ExecuteXmlReader()
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

            using var eventScope = TryEventScope.Create($"SqlCommand.ExecuteXmlReader | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteXmlReader | API | Correlation | " +
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

                // Use the reader to consume metadata
                SqlDataReader reader = IsProviderRetriable
                    ? RunExecuteReaderWithRetry(CommandBehavior.SequentialAccess, RunBehavior.ReturnImmediately, returnStream: true)
                    : RunExecuteReader(CommandBehavior.SequentialAccess, RunBehavior.ReturnImmediately, returnStream: true);
                success = true;

                return CompleteXmlReader(reader, isAsync: false);
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

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReaderAsync[@name="default"]/*'/>
        public Task<XmlReader> ExecuteXmlReaderAsync() =>
            ExecuteXmlReaderAsync(CancellationToken.None);

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteXmlReaderAsync[@name="CancellationToken"]/*'/>
        public Task<XmlReader> ExecuteXmlReaderAsync(CancellationToken cancellationToken) =>
            IsProviderRetriable
                ? InternalExecuteXmlReaderWithRetryAsync(cancellationToken)
                : InternalExecuteXmlReaderAsync(cancellationToken);
        
        #endregion
        
        #region Private Methods
        
        private static XmlReader CompleteXmlReader(SqlDataReader dataReader, bool isAsync)
        {
            XmlReader xmlReader = null;

            SmiExtendedMetaData[] metaData = dataReader.GetInternalSmiMetaData();
            bool isXmlCapable = metaData?.Length == 1 &&
                                metaData[0].SqlDbType is SqlDbType.NVarChar or SqlDbType.NText or SqlDbType.Xml;

            if (isXmlCapable)
            {
                try
                {
                    SqlStream sqlStream = new SqlStream(
                        dataReader,
                        addByteOrderMark: true,
                        processAllRows: metaData[0].SqlDbType is not SqlDbType.Xml);
                    xmlReader = sqlStream.ToXmlReader(isAsync);
                }
                catch (Exception e)
                {
                    if (ADP.IsCatchableExceptionType(e))
                    {
                        dataReader.Close();
                    }

                    throw;
                }
            }

            if (xmlReader is null)
            {
                dataReader.Close();
                throw SQL.NonXmlResult();
            }

            return xmlReader;
        }

        private IAsyncResult BeginExecuteXmlReaderAsync(AsyncCallback callback, object stateObject)
        {
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.BeginExecuteXmlReaderAsync | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");
            
            return BeginExecuteXmlReaderInternal(
                CommandBehavior.SequentialAccess,
                callback,
                stateObject,
                CommandTimeout,
                isRetry: false,
                asyncWrite: true);
        }

        private IAsyncResult BeginExecuteXmlReaderInternal(
            CommandBehavior behavior,
            AsyncCallback callback,
            object stateObject,
            int timeout,
            bool isRetry,
            bool asyncWrite)
        {
            TaskCompletionSource<object> globalCompletion = new TaskCompletionSource<object>(stateObject);
            TaskCompletionSource<object> localCompletion = new TaskCompletionSource<object>(stateObject);

            if (!isRetry)
            {
                // Reset _pendingCancel upon entry into any Execute - used to synchronize state
                // between entry into Execute* API and the thread obtaining the stateObject.
                _pendingCancel = false;
                
                // Special case - done outside of try/catches to prevent putting a stateObj back
                // into pool when we should not.
                ValidateAsyncCommand();
            }

            SqlStatistics statistics = null;
            try
            {
                if (!isRetry)
                {
                    statistics = SqlStatistics.StartTimer(Statistics);
                    WriteBeginExecuteEvent();
                }

                bool usedCache; // @TODO: Is this used *usefully* anywhere?
                Task writeTask;
                try
                {
                    // RunExecuteReader already has reliability block, but if it fails, it
                    // will not put stateObj back into pool.
                    RunExecuteReader(
                        behavior,
                        RunBehavior.ReturnImmediately,
                        returnStream: true,
                        localCompletion,
                        timeout,
                        out writeTask,
                        out usedCache,
                        asyncWrite,
                        isRetry);

                    // @TODO: NonQuery pathway has the continueTaskWithState block inside this try. One or the other seems wrong 
                }
                catch (Exception e)
                {
                    // @TODO: Invert
                    if (!ADP.IsCatchableOrSecurityExceptionType(e))
                    {
                        // If not catchable - the connection has already been caught and doomed in
                        // RunExecuteReader.
                        throw;
                    }

                    // For async, RunExecuteReader will never put the stateObj back into the pool,
                    // so, do so now.
                    ReliablePutStateObject();
                    throw;
                }

                if (writeTask is not null)
                {
                    AsyncHelper.ContinueTaskWithState(
                        task: writeTask,
                        completion: localCompletion,
                        state: Tuple.Create(this, localCompletion),
                        onSuccess: static state =>
                        {
                            var parameters = (Tuple<SqlCommand, TaskCompletionSource<object>>)state;
                            parameters.Item1.BeginExecuteXmlReaderInternalReadStage(parameters.Item2);
                        });
                }
                else
                {
                    BeginExecuteXmlReaderInternalReadStage(localCompletion);
                }

                // When we use query caching for parameter encryption we need to retry on specific
                // errors. In these cases finalize the call internally and trigger a retry when needed.
                // @TODO: store this method call in a variable, it's faaaaar too big to be used in an if statement
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
                            return command.BeginExecuteXmlReaderInternal(behavior, callback, stateObject, timeout, isRetry, asyncWrite);
                        },
                        endMethod: nameof(EndExecuteXmlReader)))
                {
                    // @TODO: globalCompletion isn't being used past here! Why are we doing this??
                    globalCompletion = localCompletion;
                }

                // Add callback after work is done to avoid overlapping Begin/End methods
                if (callback is not null)
                {
                    localCompletion.Task.ContinueWith(
                        static (task, state) => ((AsyncCallback)state)(task),
                        state: callback);
                }

                return localCompletion.Task;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
        }

        private void BeginExecuteXmlReaderInternalReadStage(TaskCompletionSource<object> completion)
        {
            // Read SNI does not have catches for async exceptions, handle here.

            try
            {
                // Must finish caching information before ReadSni which can activate the callback
                // before returning.
                CachedAsyncState.SetActiveConnectionAndResult(
                    completion,
                    nameof(EndExecuteXmlReader),
                    _activeConnection);
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

        // @TODO: This is basically identical to non-query (and probably the reader) versions. Can be refactored to be generic.
        private void CleanupAfterExecuteXmlReaderAsync(
            Task<XmlReader> task,
            TaskCompletionSource<XmlReader> source,
            Guid operationId)
        {
            if (task.IsFaulted)
            {
                Exception e = task.Exception?.InnerException;
                
                #if NET
                s_diagnosticListener.WriteCommandError(operationId, this, _transaction, e);
                #endif
                
                source.SetException(e);
            }
            else if (task.IsCanceled)
            {
                #if NET
                s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                #endif
                
                source.SetCanceled();
            }
            else
            {
                #if NET
                s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                #endif
                
                source.SetResult(task.Result);
            }
        }

        private XmlReader EndExecuteXmlReaderAsync(IAsyncResult asyncResult)
        {
            Debug.Assert(!_internalEndExecuteInitiated || _stateObj is null);
            
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.EndExecuteXmlReaderAsync | API | Correlation | " +
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
            
            // Locking _stateObj prevents races with close/cancel.
            // If we have already initiated the End call internally, we have already done that, so
            // no point doing it again.
            if (!_internalEndExecuteInitiated)
            {
                lock (_stateObj)
                {
                    return EndExecuteXmlReaderInternal(asyncResult);
                }
            }

            return EndExecuteXmlReaderInternal(asyncResult);
        }
        
        private XmlReader EndExecuteXmlReaderInternal(IAsyncResult asyncResult)
        {
            bool success = false;
            int? sqlExceptionNumber = null;

            try
            {
                SqlDataReader dataReader = InternalEndExecuteReader(
                    asyncResult,
                    isInternal: false,
                    endMethod: nameof(EndExecuteXmlReader));
                XmlReader result = CompleteXmlReader(dataReader, isAsync: true);

                success = true;
                return result;
            }
            catch (Exception e)
            {
                if (e is SqlException sqlException)
                {
                    sqlExceptionNumber = sqlException.Number;
                }

                CachedAsyncState?.ResetAsyncState();

                if (ADP.IsCatchableExceptionType(e))
                {
                    ReliablePutStateObject();
                }

                throw;
            }
            finally
            {
                WriteEndExecuteEvent(success, sqlExceptionNumber, synchronous: false);
            }
        }
        
        private Task<XmlReader> InternalExecuteXmlReaderAsync(CancellationToken cancellationToken)
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif
            
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.InternalExecuteXmlReaderAsync | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");
            
            #if NET
            Guid operationId = s_diagnosticListener.WriteCommandBefore(this, _transaction);
            #else
            Guid operationId = Guid.Empty;
            #endif
            
            // Connection can be used as state in RegisterForConnectionCloseNotification continuation
            // to avoid an allocation so use it as the state value if possible but it can be changed if
            // you need it for a more important piece of data that justifies the tuple allocation later
            TaskCompletionSource<XmlReader> source = new TaskCompletionSource<XmlReader>(_activeConnection);
            
            CancellationTokenRegistration registration = new CancellationTokenRegistration();
            if (cancellationToken.CanBeCanceled)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    source.SetCanceled();
                    return source.Task;
                }

                registration = cancellationToken.Register(callback: s_cancelIgnoreFailure, state: this);
            }

            // @TODO: This can be cleaned up to lines if InnerConnection is always SqlInternalConnection 
            ExecuteXmlReaderAsyncCallContext context = null;
            if (_activeConnection?.InnerConnection is SqlInternalConnection sqlInternalConnection)
            {
                context = Interlocked.Exchange(
                    ref sqlInternalConnection.CachedCommandExecuteXmlReaderAsyncContext,
                    null);
            }

            context ??= new ExecuteXmlReaderAsyncCallContext();
            context.Set(this, source, registration, operationId);

            Task<XmlReader> returnedTask = source.Task;
            try
            {
                returnedTask = RegisterForConnectionCloseNotification(returnedTask);

                // @TODO: Replace with native async implementation, make Begin/End implementation rely on async implementation
                Task<XmlReader>.Factory.FromAsync(
                    beginMethod: static (callback, stateObject) =>
                    {
                        // @TODO: With C# 10/net6 add [StackTraceHidden]
                        return ((ExecuteXmlReaderAsyncCallContext)stateObject).Command.BeginExecuteXmlReaderAsync(
                            callback,
                            stateObject);
                    },
                    endMethod: static asyncResult =>
                    {
                        // @TODO: With C# 10/net6 add [StackTraceHidden]
                        return ((ExecuteXmlReaderAsyncCallContext)asyncResult.AsyncState).Command.EndExecuteXmlReaderAsync(
                            asyncResult);
                    },
                    state: context
                ).ContinueWith(static task =>
                {
                    // @TODO: With C#/net6 add [StackTraceHidden]
                    ExecuteXmlReaderAsyncCallContext context = (ExecuteXmlReaderAsyncCallContext)task.AsyncState;
                    SqlCommand command = context.Command;
                    Guid operationId = context.OperationId;
                    TaskCompletionSource<XmlReader> source = context.TaskCompletionSource;

                    context.Dispose();
                        
                    command.CleanupAfterExecuteXmlReaderAsync(task, source, operationId);
                },
                scheduler: TaskScheduler.Default);
            }
            catch (Exception e) 
            {
                #if NET
                s_diagnosticListener.WriteCommandError(operationId, this, _transaction, e);
                #endif
                
                source.SetException(e);
            }

            return returnedTask;
        }
        
        private Task<XmlReader> InternalExecuteXmlReaderWithRetryAsync(CancellationToken cancellationToken) =>
            RetryLogicProvider.ExecuteAsync(
                sender: this,
                () => InternalExecuteXmlReaderAsync(cancellationToken),
                cancellationToken);

        private void SetCachedCommandExecuteXmlReaderContext(ExecuteXmlReaderAsyncCallContext instance)
        {
            if (_activeConnection?.InnerConnection is SqlInternalConnection sqlInternalConnection)
            {
                // @TODO: Move this compare exchange into the SqlInternalConnection class (or better yet, do away with this context)
                Interlocked.CompareExchange(
                    ref sqlInternalConnection.CachedCommandExecuteXmlReaderAsyncContext,
                    instance,
                    comparand: null);
            }
        }
        
        #endregion

        internal sealed class ExecuteXmlReaderAsyncCallContext
            : AAsyncCallContext<SqlCommand, XmlReader, CancellationTokenRegistration>
        {
            public SqlCommand Command => _owner;
            
            public Guid OperationId { get; set; }

            public TaskCompletionSource<XmlReader> TaskCompletionSource => _source;

            public void Set(
                SqlCommand command,
                TaskCompletionSource<XmlReader> source,
                CancellationTokenRegistration disposable,
                Guid operationId)
            {
                base.Set(command,source, disposable);
                OperationId = operationId;
            }

            protected override void AfterCleared(SqlCommand owner)
            {
                owner?.SetCachedCommandExecuteXmlReaderContext(this);
            }

            protected override void Clear()
            {
                OperationId = Guid.Empty;
            }
        }
    }
}
