// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{
    /// <content>
    /// This partial contains the methods related to ExecuteScalar public API.
    /// </content>
    public sealed partial class SqlCommand
    {
        #region Public/Internal Methods

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteScalar/*'/>
        public override object ExecuteScalar()
        {
            #if NETFRAMEWORK
            SqlConnection.ExecutePermission.Demand();
            #endif
            
            // Reset _pendingCancel upon entry into any Execute - used to synchronize state
            // between entry into Execute* API and the thread obtaining the stateObject.
            _pendingCancel = false;
            
            using var diagnosticScope = s_diagnosticListener.CreateCommandScope(this, _transaction);

            using var eventScope = TryEventScope.Create($"SqlCommand.ExecuteScalar | API | Object Id {ObjectID}");
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.ExecuteScalar | API | Correlation | " +
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

                // @TODO: Rename ds to reader
                SqlDataReader ds = IsProviderRetriable
                    ? RunExecuteReaderWithRetry(CommandBehavior.Default, RunBehavior.ReturnImmediately, returnStream: true)
                    : RunExecuteReader(CommandBehavior.Default, RunBehavior.ReturnImmediately, returnStream: true);

                object result = CompleteExecuteScalar(ds, _batchRPCMode);
                success = true;

                return result;
            }
            catch (Exception ex)
            {
                diagnosticScope.SetException(ex);

                if (ex is SqlException sqlException)
                {
                    sqlExceptionNumber = sqlException.Number;
                }

                throw;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
                WriteEndExecuteEvent(success, sqlExceptionNumber, isSynchronous: true);
            }
        }

        /// <include file='../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlCommand.xml' path='docs/members[@name="SqlCommand"]/ExecuteScalarAsync[@name="CancellationToken"]/*'/>
        public override Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            // Do not use retry logic here as ExecuteReaderAsyncInternal handles retry logic
            return ExecuteScalarAsyncInternal(cancellationToken);
        }
        
        #if NET
        internal Task<object> ExecuteScalarBatchAsync(CancellationToken cancellationToken)
        {
            Guid operationId = s_diagnosticListener.WriteCommandBefore(this, _transaction);
            _parentOperationStarted = true;
            
            // @TODO: This code is almost identical to ExecuteScalarAsyncInternal - we can definitely refactor it!
            return ExecuteReaderAsync(cancellationToken).ContinueWith(executeTask =>
            {
                TaskCompletionSource<object> source = new TaskCompletionSource<object>();

                if (executeTask.IsCanceled)
                {
                    source.SetCanceled();
                }
                else if (executeTask.IsFaulted)
                {
                    s_diagnosticListener.WriteCommandError(
                        operationId,
                        this,
                        _transaction,
                        executeTask.Exception.InnerException);
                    source.SetException(executeTask.Exception.InnerException);
                }
                else
                {
                    SqlDataReader reader = executeTask.Result;
                    ExecuteScalarUntilEndAsync(reader, cancellationToken).ContinueWith(readTask =>
                    {
                        try
                        {
                            if (readTask.IsCanceled)
                            {
                                reader.Dispose();
                                source.SetCanceled();
                            }
                            else if (readTask.IsFaulted)
                            {
                                reader.Dispose();
                                s_diagnosticListener.WriteCommandError(
                                    operationId,
                                    this,
                                    _transaction,
                                    readTask.Exception.InnerException);
                                source.SetException(readTask.Exception.InnerException);
                            }
                            else
                            {
                                Exception exception = null;
                                object result = null;
                                try
                                {
                                    result = readTask.Result;
                                }
                                finally
                                {
                                    reader.Dispose();
                                }

                                if (exception is not null)
                                {
                                    s_diagnosticListener.WriteCommandError(operationId, this, _transaction, exception);
                                    source.SetException(exception);
                                }
                                else
                                {
                                    s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                                    source.SetResult(result);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // Exception thrown by dispose
                            source.SetException(e);
                        }
                    },
                    TaskScheduler.Default);
                }

                _parentOperationStarted = false;
                return source.Task;
            },
            TaskScheduler.Default).Unwrap();
        }
        #endif
        
        #endregion
        
        #region Private Methods

        private static object CompleteExecuteScalar(SqlDataReader reader, bool returnLastResult)
        {
            object result = null;
            try
            {
                do
                {
                    if (reader.Read() && reader.FieldCount > 0)
                    {
                        result = reader.GetValue(0);
                    }
                } while (returnLastResult && reader.NextResult());

                // Drain remaining results to ensure all error tokens are processed
                // before returning the result (fix for GH issue #3736).
                while (reader.NextResult())
                { }
            }
            finally
            {
                // Clean off the wire
                reader.Close();
            }

            return result;
        }

        private async Task<object> ExecuteScalarUntilEndAsync(
            SqlDataReader reader,
            CancellationToken cancellationToken)
        {
            // @TODO: This is doing almost the same thing as CompleteExecuteScalar when returnLastResult is true
            object result = null;
            do
            {
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) && reader.FieldCount > 0)
                {
                    // no async untyped value getter, this will work ok as long as the value is in
                    // the current packet
                    result = reader.GetValue(0);
                }
            } while (_batchRPCMode &&
                     !cancellationToken.IsCancellationRequested &&
                     await reader.NextResultAsync(cancellationToken).ConfigureAwait(false));

            return result;
        }
        
        private Task<object> ExecuteScalarAsyncInternal(CancellationToken cancellationToken)
        {
            SqlClientEventSource.Log.TryCorrelationTraceEvent(
                "SqlCommand.InternalExecuteScalarAsync | API | Correlation | " +
                $"Object Id {ObjectID}, " +
                $"Activity Id {ActivityCorrelator.Current}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");
            SqlClientEventSource.Log.TryTraceEvent(
                "SqlCommand.ExecuteScalarAsyncInternal | API " +
                $"Object Id {ObjectID}, " +
                $"Client Connection Id {_activeConnection?.ClientConnectionId}, " +
                $"Command Text '{CommandText}'");

            Guid operationId = s_diagnosticListener.WriteCommandBefore(this, _transaction);
            _parentOperationStarted = true;

            // @TODO: Use continue with state? This would be a good candidate for rewriting async/await
            return ExecuteReaderAsync(cancellationToken).ContinueWith(executeTask =>
            {
                TaskCompletionSource<object> source = new TaskCompletionSource<object>();

                if (executeTask.IsCanceled)
                {
                    source.SetCanceled();
                }
                else if (executeTask.IsFaulted)
                {
                    s_diagnosticListener.WriteCommandError(
                        operationId,
                        this,
                        _transaction,
                        executeTask.Exception.InnerException);
                    
                    source.SetException(executeTask.Exception.InnerException);
                }
                else
                {
                    SqlDataReader reader = executeTask.Result;
                    
                    // @TODO: Use continue with state?
                    reader.ReadAsync(cancellationToken).ContinueWith(async readTask =>
                    {
                        // @TODO: This seems a bit confusing with unnecessary extra dispose calls and try/finally blocks
                        try
                        {
                            
                            if (readTask.IsCanceled)
                            {
                                reader.Dispose();
                                source.SetCanceled();
                            }
                            else if (readTask.IsFaulted)
                            {
                                reader.Dispose();
                                
                                s_diagnosticListener.WriteCommandError(
                                    operationId,
                                    this,
                                    _transaction,
                                    readTask.Exception.InnerException);
                                
                                source.SetException(readTask.Exception.InnerException);
                            }
                            else
                            {
                                Exception exception = null;
                                object result = null;
                                try
                                {
                                    bool more = readTask.Result;
                                    if (more && reader.FieldCount > 0)
                                    {
                                        try
                                        {
                                            result = reader.GetValue(0);
                                        }
                                        catch (Exception e)
                                        {
                                            // @TODO: Yeah, this code doesn't understand how finally blocks work.
                                            exception = e;
                                        }
                                    }

                                    // Drain remaining results to ensure all error tokens are processed
                                    // before returning the result (fix for GH issue #3736).
                                    while (await reader.NextResultAsync(cancellationToken).ConfigureAwait(false))
                                    { }
                                }
                                finally
                                {
                                    reader.Dispose();
                                }

                                if (exception is not null)
                                {
                                    s_diagnosticListener.WriteCommandError(operationId, this, _transaction, exception);
                                    
                                    source.SetException(exception);
                                }
                                else
                                {
                                    s_diagnosticListener.WriteCommandAfter(operationId, this, _transaction);
                                    
                                    source.SetResult(result);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            // Exception thrown by Dispose
                            source.SetException(e);
                        }
                    },
                    TaskScheduler.Default);
                }

                _parentOperationStarted = false;
                
                return source.Task;
            },
            TaskScheduler.Default).Unwrap();
        }
        
        #endregion
    }
}
