// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.Data.SqlClient
{
    public partial class SqlBulkCopy
    {
        // Newly added member variables for Async modification, m = member variable to bcp.
        private int _savedBatchSize = 0; // Save the batchsize so that changes are not affected unexpectedly.
        private bool _hasMoreRowToCopy = false;

        // we can remove this variable as it is not being used anymore.
        private bool _isAsyncBulkCopy = false;
        private bool _isBulkCopyingInProgress = false;
        private SqlInternalConnectionTds.SyncAsyncLock _parserLock = null;

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataRowParameter"]/*'/>
        public async Task WriteToServerAsync(DataRow[] rows) => await WriteToServerAsync(rows, CancellationToken.None);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataRowAndCancellationTokenParameters"]/*'/>
        public async Task<Task> WriteToServerAsync(DataRow[] rows, CancellationToken cancellationToken)
        {
            Task resultTask = null;

            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (_isBulkCopyingInProgress)
            {
                throw SQL.BulkLoadPendingOperation();
            }

            SqlStatistics statistics = Statistics;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);

                if (rows.Length == 0)
                {
                    return cancellationToken.IsCancellationRequested ?
                            Task.FromCanceled(cancellationToken) :
                            Task.CompletedTask;
                }

                DataTable table = rows[0].Table;
                Debug.Assert(null != table, "How can we have rows without a table?");
                _rowStateToSkip = DataRowState.Deleted; // Don't allow deleted rows
                _rowSource = rows;
                _dataTableSource = table;
                _SqlDataReaderRowSource = null;
                _rowSourceType = ValueSourceType.RowArray;
                _rowEnumerator = rows.GetEnumerator();
                _isAsyncBulkCopy = true;
                resultTask = WriteRowSourceToServerAsync(table.Columns.Count, cancellationToken); // It returns Task since _isAsyncBulkCopy = true;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
            await Task.WhenAll(resultTask);
            return resultTask;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DbDataReaderParameter"]/*'/>
        public async Task WriteToServerAsync(DbDataReader reader) => await WriteToServerAsync(reader, CancellationToken.None);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DbDataReaderAndCancellationTokenParameters"]/*'/>
        public Task WriteToServerAsync(DbDataReader reader, CancellationToken cancellationToken)
        {
            Task resultTask = null;
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (_isBulkCopyingInProgress)
            {
                throw SQL.BulkLoadPendingOperation();
            }

            SqlStatistics statistics = Statistics;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                _rowSource = reader;
                _SqlDataReaderRowSource = reader as SqlDataReader;
                _DbDataReaderRowSource = reader;
                _dataTableSource = null;
                _rowSourceType = ValueSourceType.DbDataReader;
                _isAsyncBulkCopy = true;
                resultTask = WriteRowSourceToServerAsync(reader.FieldCount, cancellationToken); // It returns Task since _isAsyncBulkCopy = true;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
            return resultTask;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="IDataReaderParameter"]/*'/>
        public async Task WriteToServerAsync(IDataReader reader) => await WriteToServerAsync(reader, CancellationToken.None);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="IDataReaderAndCancellationTokenParameters"]/*'/>
        public Task WriteToServerAsync(IDataReader reader, CancellationToken cancellationToken)
        {
            Task resultTask = null;

            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            if (_isBulkCopyingInProgress)
            {
                throw SQL.BulkLoadPendingOperation();
            }

            SqlStatistics statistics = Statistics;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                _rowSource = reader;
                _SqlDataReaderRowSource = _rowSource as SqlDataReader;
                _DbDataReaderRowSource = _rowSource as DbDataReader;
                _dataTableSource = null;
                _rowSourceType = ValueSourceType.IDataReader;
                _isAsyncBulkCopy = true;
                resultTask = WriteRowSourceToServerAsync(reader.FieldCount, cancellationToken); // It returns Task since _isAsyncBulkCopy = true;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
            return resultTask;
        }

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableParameter"]/*'/>
        public async Task WriteToServerAsync(DataTable table) => await WriteToServerAsync(table, 0, CancellationToken.None);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableAndCancellationTokenParameters"]/*'/>
        public async Task WriteToServerAsync(DataTable table, CancellationToken cancellationToken) => await WriteToServerAsync(table, 0, cancellationToken);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableAndDataRowStateParameter"]/*'/>
        public async Task WriteToServerAsync(DataTable table, DataRowState rowState) => await WriteToServerAsync(table, rowState, CancellationToken.None);

        /// <include file='../../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlBulkCopy.xml' path='docs/members[@name="SqlBulkCopy"]/WriteToServerAsync[@name="DataTableAndDataRowStateAndCancellationTokenParameters"]/*'/>
        public Task WriteToServerAsync(DataTable table, DataRowState rowState, CancellationToken cancellationToken)
        {
            Task resultTask = null;

            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (_isBulkCopyingInProgress)
            {
                throw SQL.BulkLoadPendingOperation();
            }

            SqlStatistics statistics = Statistics;
            try
            {
                statistics = SqlStatistics.StartTimer(Statistics);
                _rowStateToSkip = ((rowState == 0) || (rowState == DataRowState.Deleted)) ? DataRowState.Deleted : ~rowState | DataRowState.Deleted;
                _rowSource = table;
                _SqlDataReaderRowSource = null;
                _dataTableSource = table;
                _rowSourceType = ValueSourceType.DataTable;
                _rowEnumerator = table.Rows.GetEnumerator();
                _isAsyncBulkCopy = true;
                resultTask = WriteRowSourceToServerAsync(table.Columns.Count, cancellationToken); // It returns Task since _isAsyncBulkCopy = true;
            }
            finally
            {
                SqlStatistics.StopTimer(statistics);
            }
            return resultTask;
        }


        private Task WriteRowSourceToServerAsync(int columnCount, CancellationToken ctoken)
        {
            Task reconnectTask = _connection._currentReconnectionTask;

            //We need to keep this for now, as reconnectTask async calls returns task and sync calls will return null.
            if (reconnectTask != null && !reconnectTask.IsCompleted)
            {
                TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
                reconnectTask.ContinueWith((t) =>
                {
                    Task writeTask = WriteRowSourceToServerAsync(columnCount, ctoken);
                    if (writeTask == null)
                    {
                        tcs.SetResult(null);
                    }
                    else
                    {
                        AsyncHelper.ContinueTaskWithState(writeTask, tcs,
                            state: tcs,
                            onSuccess: state => ((TaskCompletionSource<object>)state).SetResult(null)
                        );
                    }
                }, ctoken); // We do not need to propagate exception, etc, from reconnect task, we just need to wait for it to finish.
                return tcs.Task;
            }

            bool finishedSynchronously = true;
            _isBulkCopyingInProgress = true;

            CreateOrValidateConnection(nameof(WriteToServer));
            SqlInternalConnectionTds internalConnection = _connection.GetOpenTdsConnection();

            Debug.Assert(_parserLock == null, "Previous parser lock not cleaned");
            _parserLock = internalConnection._parserLock;
            _parserLock.Wait(canReleaseFromAnyThread: _isAsyncBulkCopy);

            try
            {
                WriteRowSourceToServerCommon(columnCount); // This is common in both sync and async
                Task resultTask = WriteToServerInternalAsync(ctoken); // resultTask is null for sync, but Task for async.

                finishedSynchronously = false;
                return resultTask.ContinueWith((t) =>
                {
                    try
                    {
                        AbortTransaction(); // If there is one, on success transactions will be committed.
                    }
                    finally
                    {
                        _isBulkCopyingInProgress = false;
                        if (_parser != null)
                        {
                            _parser._asyncWrite = false;
                        }
                        if (_parserLock != null)
                        {
                            _parserLock.Release();
                            _parserLock = null;
                        }
                    }
                    return t;
                }, TaskScheduler.Default).Unwrap();
            }
            catch (System.OutOfMemoryException e)
            {
                _connection.Abort(e);
                throw;
            }
            catch (System.StackOverflowException e)
            {
                _connection.Abort(e);
                throw;
            }
            catch (System.Threading.ThreadAbortException e)
            {
                _connection.Abort(e);
                throw;
            }
            finally
            {
                _columnMappings.ReadOnly = false;
                if (finishedSynchronously)
                {
                    try
                    {
                        AbortTransaction(); // If there is one, on success transactions will be committed.
                    }
                    finally
                    {
                        _isBulkCopyingInProgress = false;
                        if (_parser != null)
                        {
                            _parser._asyncWrite = false;
                        }
                        if (_parserLock != null)
                        {
                            _parserLock.Release();
                            _parserLock = null;
                        }
                    }
                }
            }
        }

        // This returns Task for Async
        private Task WriteToServerInternalAsync(CancellationToken ctoken)
        {
            TaskCompletionSource<object> source = null;
            Task<object> resultTask = null;

            source = new TaskCompletionSource<object>(); // Creating the completion source/Task that we pass to application
            resultTask = source.Task;

            resultTask = RegisterForConnectionCloseNotification(resultTask);

            if (_destinationTableName == null)
            {
                if (source != null)
                {
                    source.SetException(SQL.BulkLoadMissingDestinationTable()); // No table to copy
                }
                else
                {
                    throw SQL.BulkLoadMissingDestinationTable();
                }
                return resultTask;
            }

            try
            {
                Task readTask = ReadFromRowSourceAsync(ctoken); // readTask == reading task. This is the first read call. "more" is valid only if readTask == null;

                Debug.Assert(_isAsyncBulkCopy, "Read must not return a Task in the Sync mode");
                AsyncHelper.ContinueTask(readTask, source,
                    () =>
                    {
                        if (!_hasMoreRowToCopy)
                        {
                            source.SetResult(null); // No rows to copy!
                        }
                        else
                        {
                            WriteToServerInternalRestAsync(ctoken, source); // Passing the same completion which will be completed by the Callee.
                        }
                    }
                );
                return resultTask;
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                }
                else
                {
                    throw;
                }
            }
            return resultTask;
        }

        private async Task<bool> ReadFromRowSourceAsync(CancellationToken cts)
        {
            if (_DbDataReaderRowSource != null)
            {
                // This will call ReadAsync for DbDataReader (for SqlDataReader it will be truly async read; for non-SqlDataReader it may block.)
                return await _DbDataReaderRowSource.ReadAsync(cts).ContinueWith((t) =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                    {
                        _hasMoreRowToCopy = t.Result;
                    }
                    return t;
                }, TaskScheduler.Default).Unwrap();
            }
            else
            { // This will call Read for DataRows, DataTable and IDataReader (this includes all IDataReader except DbDataReader)
              // Release lock to prevent possible deadlocks
                SqlInternalConnectionTds internalConnection = _connection.GetOpenTdsConnection();
                bool semaphoreLock = internalConnection._parserLock.CanBeReleasedFromAnyThread;
                internalConnection._parserLock.Release();

                _hasMoreRowToCopy = false;
                try
                {
                    _hasMoreRowToCopy = ReadFromRowSource(); // Synchronous calls for DataRows and DataTable won't block. For IDataReader, it may block.
                }
                catch (Exception ex)
                {
                    return await Task.FromException<bool>(ex);
                }
                finally
                {
                    internalConnection._parserLock.Wait(canReleaseFromAnyThread: semaphoreLock);
                }
                return false;
            }
        }

        // Rest of the WriteToServerInternalAsync method.
        // It carries on the source from its caller WriteToServerInternal.
        // source is null in case of Sync bcp. But valid in case of Async bcp.
        // It calls the WriteToServerInternalRestContinuedAsync as a continuation of the initial query task.
        private void WriteToServerInternalRestAsync(CancellationToken cts, TaskCompletionSource<object> source)
        {
            Debug.Assert(_hasMoreRowToCopy, "first time it is true, otherwise this method would not have been called.");
            _hasMoreRowToCopy = true;
            Task<BulkCopySimpleResultSet> internalResultsTask = null;
            BulkCopySimpleResultSet internalResults = new BulkCopySimpleResultSet();
            SqlInternalConnectionTds internalConnection = _connection.GetOpenTdsConnection();
            try
            {
                _parser = _connection.Parser;
                _parser._asyncWrite = true; // Very important!

                Task reconnectTask;
                try
                {
                    reconnectTask = _connection.ValidateAndReconnect(
                        () =>
                        {
                            if (_parserLock != null)
                            {
                                _parserLock.Release();
                                _parserLock = null;
                            }
                        }, BulkCopyTimeout);
                }
                catch (SqlException ex)
                {
                    throw SQL.BulkLoadInvalidDestinationTable(_destinationTableName, ex);
                }

                if (reconnectTask != null)
                {

                    CancellationTokenRegistration regReconnectCancel = new CancellationTokenRegistration();
                    TaskCompletionSource<object> cancellableReconnectTS = new TaskCompletionSource<object>();
                    if (cts.CanBeCanceled)
                    {
                        regReconnectCancel = cts.Register(s => ((TaskCompletionSource<object>)s).TrySetCanceled(), cancellableReconnectTS);
                    }
                    AsyncHelper.ContinueTaskWithState(reconnectTask, cancellableReconnectTS,
                        state: cancellableReconnectTS,
                        onSuccess: (state) => { ((TaskCompletionSource<object>)state).SetResult(null); }
                    );
                    // No need to cancel timer since SqlBulkCopy creates specific task source for reconnection.
                    AsyncHelper.SetTimeoutException(cancellableReconnectTS, BulkCopyTimeout,
                            () => { return SQL.BulkLoadInvalidDestinationTable(_destinationTableName, SQL.CR_ReconnectTimeout()); }, CancellationToken.None);
                    AsyncHelper.ContinueTask(cancellableReconnectTS.Task, source,
                        onSuccess: () =>
                        {
                            regReconnectCancel.Dispose();
                            if (_parserLock != null)
                            {
                                _parserLock.Release();
                                _parserLock = null;
                            }
                            _parserLock = _connection.GetOpenTdsConnection()._parserLock;
                            _parserLock.Wait(canReleaseFromAnyThread: true);
                            WriteToServerInternalRestAsync(cts, source);
                        },
                        onFailure: (e) => { regReconnectCancel.Dispose(); },
                        onCancellation: () => { regReconnectCancel.Dispose(); },
                        exceptionConverter: (ex) => SQL.BulkLoadInvalidDestinationTable(_destinationTableName, ex));
                    return;

                }

                _connection.AddWeakReference(this, SqlReferenceCollection.BulkCopyTag);


                internalConnection.ThreadHasParserLockForClose = true;    // In case of error, let the connection know that we already have the parser lock.

                try
                {
                    _stateObj = _parser.GetSession(this);
                    _stateObj._bulkCopyOpperationInProgress = true;
                    _stateObj.StartSession(this);
                }
                finally
                {
                    internalConnection.ThreadHasParserLockForClose = false;
                }

                try
                {
                    internalResultsTask = CreateAndExecuteInitialQueryAsync(); // Task/Null
                }
                catch (SqlException ex)
                {
                    throw SQL.BulkLoadInvalidDestinationTable(_destinationTableName, ex);
                }

                AsyncHelper.ContinueTask(internalResultsTask, source, () => WriteToServerInternalRestContinuedAsync(internalResultsTask.Result, cts, source));
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                }
                else
                {
                    throw;
                }
            }
        }

        // Creates and then executes initial query to get information about the targettable
        // When __isAsyncBulkCopy == false (i.e. it is Sync copy): out result contains the resulset. Returns null.
        // When __isAsyncBulkCopy == true (i.e. it is Async copy): This still uses the _parser.Run method synchronously and return Task<BulkCopySimpleResultSet>.
        // We need to have a _parser.RunAsync to make it real async.
        private async Task<BulkCopySimpleResultSet> CreateAndExecuteInitialQueryAsync()
        {
            string TDSCommand = CreateInitialQuery();
            SqlClientEventSource.Log.TryTraceEvent("<sc.SqlBulkCopy.CreateAndExecuteInitialQueryAsync|INFO> Initial Query: '{0}'", TDSCommand);
            //SqlClientEventSource.Log.TryCorrelationTraceEvent("<sc.SqlBulkCopy.CreateAndExecuteInitialQueryAsync|Info|Correlation> ObjectID {0}, ActivityID {1}", ObjectID, Common.ActivityCorrelator.Current);
            Task executeTask = _parser.TdsExecuteSQLBatch(TDSCommand, this.BulkCopyTimeout, null, _stateObj, sync: !_isAsyncBulkCopy, callerHasConnectionLock: true);

            Debug.Assert(_isAsyncBulkCopy, "Execution pended when not doing async bulk copy");
            return await executeTask.ContinueWith(t =>
              {
                  Debug.Assert(!t.IsCanceled, "Execution task was canceled");
                  if (t.IsFaulted)
                  {
                      throw t.Exception.InnerException;
                  }
                  else
                  {
                      var internalResult = new BulkCopySimpleResultSet();
                      RunParserReliably(internalResult);
                      return internalResult;
                  }
              }, TaskScheduler.Default);
        }

        // Copies all the rows in a batch.
        // Maintains state machine with state variable: rowSoFar.
        // Returned Task could be null in two cases: (1) _isAsyncBulkCopy == false, or (2) _isAsyncBulkCopy == true but all async writes finished synchronously.
        private async Task<Task> CopyRowsAsync(int rowsSoFar, int totalRows, CancellationToken cts, TaskCompletionSource<object> source = null)
        {
            Task resultTask = null;
            Task task = null;
            int i;
            try
            {
                // totalRows is batchsize which is 0 by default. In that case, we keep copying till the end (until _hasMoreRowToCopy == false).
                for (i = rowsSoFar; (totalRows <= 0 || i < totalRows) && _hasMoreRowToCopy == true; i++)
                {

                    resultTask = CheckForCancellation(cts);
                    if (resultTask != null)
                    {
                        return resultTask; // Task got cancelled!
                    }

                    _stateObj.WriteByte(TdsEnums.SQLROW);

                    task = CopyColumnsAsync(0); // Copy 1 row
                    await Task.WhenAll(task);

                    //This was left as of Async calls task apparently could run synn based on the comment in the CopycolumnAsync.
                    if (task == null)
                    {   // Task is done.
                        CheckAndRaiseNotification(); // Check notification logic after copying the row

                        // Now we will read the next row.
                        Task readTask = ReadFromRowSourceAsync(cts); // Read the next row. Caution: more is only valid if the task returns null. Otherwise, we wait for Task.Result
                        if (readTask != null)
                        {
                            if (source == null)
                            {
                                source = new TaskCompletionSource<object>();
                            }
                            resultTask = source.Task;

                            AsyncHelper.ContinueTask(readTask, source, async () => await CopyRowsAsync(i + 1, totalRows, cts, source));
                            return resultTask; // Associated task will be completed when all rows are copied to server/exception/cancelled.
                        }
                    }
                    else
                    {   // task != null, so add continuation for it.
                        source = source ?? new TaskCompletionSource<object>();
                        resultTask = source.Task;

                        AsyncHelper.ContinueTask(task, source, onSuccess: async () =>
                        {
                            CheckAndRaiseNotification(); // Check for notification now as the current row copy is done at this moment.

                            Task readTask = ReadFromRowSourceAsync(cts);
                            if (readTask == null)
                            {
                                await CopyRowsAsync(i + 1, totalRows, cts, source);
                            }
                            else
                            {
                                AsyncHelper.ContinueTask(readTask, source, onSuccess: async () => await CopyRowsAsync(i + 1, totalRows, cts, source));
                            }
                        }
                       );
                        return resultTask;
                    }
                }

                if (source != null)
                {
                    source.TrySetResult(null); // This is set only on the last call of async copy. But may not be set if everything runs synchronously.
                }
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                }
                else
                {
                    throw;
                }
            }
            return resultTask;
        }


        // Runs a loop to copy all columns of a single row.
        // Maintains a state by remembering #columns copied so far (int col).
        // Returned Task could be null in two cases: (1) _isAsyncBulkCopy == false, (2) _isAsyncBulkCopy == true but all async writes finished synchronously.
        private Task CopyColumnsAsync(int col, TaskCompletionSource<object> source = null)
        {
            Task resultTask = null, task = null;
            int i;
            try
            {
                for (i = col; i < _sortedColumnMappings.Count; i++)
                {
                    task = ReadWriteColumnValueAsync(i); //First reads and then writes one cell value. Task 'task' is completed when reading task and writing task both are complete.
                    if (task != null)
                        break; //task != null means we have a pending read/write Task.
                }
                if (task != null)
                {
                    if (source == null)
                    {
                        source = new TaskCompletionSource<object>();
                        resultTask = source.Task;
                    }
                    CopyColumnsAsyncSetupContinuation(source, task, i);
                    return resultTask; //associated task will be completed when all columns (i.e. the entire row) is written
                }
                if (source != null)
                {
                    source.SetResult(null);
                }
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                }
                else
                {
                    throw;
                }
            }
            return resultTask;
        }

        // Reads a cell and then writes it.
        // Read may block at this moment since there is no getValueAsync or DownStream async at this moment.
        // When _isAsyncBulkCopy == true: Write will return Task (when async method runs asynchronously) or Null (when async call actually ran synchronously) for performance.
        // When _isAsyncBulkCopy == false: Writes are purely sync. This method return null at the end.
        // This is redundant in both SqlBulk and SqlBulk.Async, but needs to be fixed when real async/await is implemented.
        private Task ReadWriteColumnValueAsync(int col)
        {
            bool isSqlType;
            bool isDataFeed;
            bool isNull;
            object value = GetValueFromSourceRow(col, out isSqlType, out isDataFeed, out isNull); //this will return Task/null in future: as rTask

            _SqlMetaData metadata = _sortedColumnMappings[col]._metadata;
            if (!isDataFeed)
            {
                value = ConvertValue(value, metadata, isNull, ref isSqlType, out isDataFeed);

                // If column encryption is requested via connection string option, perform encryption here
                if (!isNull && // if value is not NULL
                    metadata.isEncrypted)
                { // If we are transparently encrypting
                    Debug.Assert(_parser.ShouldEncryptValuesForBulkCopy());
                    value = _parser.EncryptColumnValue(value, metadata, metadata.column, _stateObj, isDataFeed, isSqlType);
                    isSqlType = false; // Its not a sql type anymore
                }
            }

            //write part
            Task writeTask = null;
            if (metadata.type != SqlDbType.Variant)
            {
                //this is the most common path
                writeTask = _parser.WriteBulkCopyValue(value, metadata, _stateObj, isSqlType, isDataFeed, isNull); //returns Task/Null
            }
            else
            {
                // Target type shouldn't be encrypted
                Debug.Assert(!metadata.isEncrypted, "Can't encrypt SQL Variant type");
                SqlBuffer.StorageType variantInternalType = SqlBuffer.StorageType.Empty;
                if ((_SqlDataReaderRowSource != null) && (_connection.IsKatmaiOrNewer))
                {
                    variantInternalType = _SqlDataReaderRowSource.GetVariantInternalStorageType(_sortedColumnMappings[col]._sourceColumnOrdinal);
                }

                if (variantInternalType == SqlBuffer.StorageType.DateTime2)
                {
                    _parser.WriteSqlVariantDateTime2(((DateTime)value), _stateObj);
                }
                else if (variantInternalType == SqlBuffer.StorageType.Date)
                {
                    _parser.WriteSqlVariantDate(((DateTime)value), _stateObj);
                }
                else
                {
                    writeTask = _parser.WriteSqlVariantDataRowValue(value, _stateObj); //returns Task/Null
                }
            }

            return writeTask;
        }

        // This is in its own method to avoid always allocating the lambda in CopyColumnsAsync
        private void CopyColumnsAsyncSetupContinuation(TaskCompletionSource<object> source, Task task, int i)
        {
            AsyncHelper.ContinueTask(task, source, () =>
            {
                if (i + 1 < _sortedColumnMappings.Count)
                {
                    CopyColumns(i + 1, source); //continue from the next column
                }
                else
                {
                    source.SetResult(null);
                }
            }
           );
        }

        // Copies all the batches in a loop. One iteration for one batch.
        // state variable is essentially not needed. (however, _hasMoreRowToCopy might be thought as a state variable)
        // Returned Task could be null in two cases: (1) _isAsyncBulkCopy == false, or (2) _isAsyncBulkCopy == true but all async writes finished synchronously.
        private Task CopyBatchesAsync(BulkCopySimpleResultSet internalResults, string updateBulkCommandText, CancellationToken cts, TaskCompletionSource<object> source = null)
        {
            Debug.Assert(source == null || !source.Task.IsCompleted, "Called into CopyBatchesAsync with a completed task!");
            try
            {
                while (_hasMoreRowToCopy)
                {
                    //pre->before every batch: Transaction, BulkCmd and metadata are done.
                    SqlInternalConnectionTds internalConnection = _connection.GetOpenTdsConnection();

                    if (IsCopyOption(SqlBulkCopyOptions.UseInternalTransaction))
                    { //internal transaction is started prior to each batch if the Option is set.
                        internalConnection.ThreadHasParserLockForClose = true;     // In case of error, tell the connection we already have the parser lock
                        try
                        {
                            _internalTransaction = _connection.BeginTransaction();
                        }
                        finally
                        {
                            internalConnection.ThreadHasParserLockForClose = false;
                        }
                    }

                    Task commandTask = SubmitUpdateBulkCommand(updateBulkCommandText);

                    if (commandTask == null)
                    {
                        Task continuedTask = CopyBatchesAsyncContinued(internalResults, updateBulkCommandText, cts, source);
                        if (continuedTask != null)
                        {
                            // Continuation will take care of re-calling CopyBatchesAsync
                            return continuedTask;
                        }
                    }
                    else
                    {
                        Debug.Assert(_isAsyncBulkCopy, "Task should not pend while doing sync bulk copy");
                        if (source == null)
                        {
                            source = new TaskCompletionSource<object>();
                        }

                        AsyncHelper.ContinueTask(commandTask, source,
                            () =>
                            {
                                Task continuedTask = CopyBatchesAsyncContinued(internalResults, updateBulkCommandText, cts, source);
                                if (continuedTask == null)
                                {
                                    // Continuation finished sync, recall into CopyBatchesAsync to continue
                                    CopyBatchesAsync(internalResults, updateBulkCommandText, cts, source);
                                }
                            }
                        );
                        return source.Task;
                    }
                }
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                    return source.Task;
                }
                else
                {
                    throw;
                }
            }

            // If we are here, then we finished everything
            if (source != null)
            {
                source.SetResult(null);
                return source.Task;
            }
            else
            {
                return null;
            }
        }

        // Writes the MetaData and a single batch.
        // If this returns true, then the caller is responsible for starting the next stage.
        private Task CopyBatchesAsyncContinued(BulkCopySimpleResultSet internalResults, string updateBulkCommandText, CancellationToken cts, TaskCompletionSource<object> source)
        {
            Debug.Assert(source == null || !source.Task.IsCompleted, "Called into CopyBatchesAsync with a completed task!");
            try
            {
                WriteMetaData(internalResults);

                // Load encryption keys now (if needed)
                _parser.LoadColumnEncryptionKeys(
                    internalResults[MetaDataResultId].MetaData,
                    _connection.DataSource);

                Task task = CopyRowsAsync(0, _savedBatchSize, cts); // This is copying 1 batch of rows and setting _hasMoreRowToCopy = true/false.

                // post->after every batch
                if (task != null)
                {
                    Debug.Assert(_isAsyncBulkCopy, "Task should not pend while doing sync bulk copy");
                    if (source == null)
                    {   // First time only
                        source = new TaskCompletionSource<object>();
                    }
                    AsyncHelper.ContinueTask(task, source,
                        onSuccess: () =>
                        {
                            Task continuedTask = CopyBatchesAsyncContinuedOnSuccess(internalResults, updateBulkCommandText, cts, source);
                            if (continuedTask == null)
                            {
                                // Continuation finished sync, recall into CopyBatchesAsync to continue
                                CopyBatchesAsync(internalResults, updateBulkCommandText, cts, source);
                            }
                        },
                        onFailure: (_) => CopyBatchesAsyncContinuedOnError(cleanupParser: false),
                        onCancellation: () => CopyBatchesAsyncContinuedOnError(cleanupParser: true)
                    );

                    return source.Task;
                }
                else
                {
                    return CopyBatchesAsyncContinuedOnSuccess(internalResults, updateBulkCommandText, cts, source);
                }
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                    return source.Task;
                }
                else
                {
                    throw;
                }
            }
        }

        // Takes care of finishing a single batch (write done, run parser, commit transaction).
        // If this returns true, then the caller is responsible for starting the next stage.
        private Task CopyBatchesAsyncContinuedOnSuccess(BulkCopySimpleResultSet internalResults, string updateBulkCommandText, CancellationToken cts, TaskCompletionSource<object> source)
        {
            Debug.Assert(source == null || !source.Task.IsCompleted, "Called into CopyBatchesAsync with a completed task!");
            try
            {
                Task writeTask = _parser.WriteBulkCopyDone(_stateObj);

                if (writeTask == null)
                {
                    RunParser();
                    CommitTransaction();

                    return null;
                }
                else
                {
                    Debug.Assert(_isAsyncBulkCopy, "Task should not pend while doing sync bulk copy");
                    if (source == null)
                    {
                        source = new TaskCompletionSource<object>();
                    }

                    AsyncHelper.ContinueTask(writeTask, source,
                        onSuccess: () =>
                        {
                            try
                            {
                                RunParser();
                                CommitTransaction();
                            }
                            catch (Exception)
                            {
                                CopyBatchesAsyncContinuedOnError(cleanupParser: false);
                                throw;
                            }

                            // Always call back into CopyBatchesAsync
                            CopyBatchesAsync(internalResults, updateBulkCommandText, cts, source);
                        },
                        onFailure: (_) => CopyBatchesAsyncContinuedOnError(cleanupParser: false)
                    );
                    return source.Task;
                }
            }
            catch (Exception ex)
            {
                if (source != null)
                {
                    source.TrySetException(ex);
                    return source.Task;
                }
                else
                {
                    throw;
                }
            }
        }

        // Takes care of cleaning up the parser, stateObj and transaction when CopyBatchesAsync fails.
        private void CopyBatchesAsyncContinuedOnError(bool cleanupParser)
        {
            SqlInternalConnectionTds internalConnection = _connection.GetOpenTdsConnection();
            try
            {
                if ((cleanupParser) && (_parser != null) && (_stateObj != null))
                {
                    _parser._asyncWrite = false;
                    Task task = _parser.WriteBulkCopyDone(_stateObj);
                    Debug.Assert(task == null, "Write should not pend when error occurs");
                    RunParser();
                }

                if (_stateObj != null)
                {
                    CleanUpStateObject();
                }
            }
            catch (OutOfMemoryException)
            {
                internalConnection.DoomThisConnection();
                throw;
            }
            catch (StackOverflowException)
            {
                internalConnection.DoomThisConnection();
                throw;
            }
            catch (ThreadAbortException)
            {
                internalConnection.DoomThisConnection();
                throw;
            }

            AbortTransaction();
        }

        // The continuation part of WriteToServerInternalRest. Executes when the initial query task is completed. (see, WriteToServerInternalRest).
        // It carries on the source which is passed from the WriteToServerInternalRest and performs SetResult when the entire copy is done.
        // The carried on source may be null in case of Sync copy. So no need to SetResult at that time.
        // It launches the copy operation.
        private void WriteToServerInternalRestContinuedAsync(BulkCopySimpleResultSet internalResults, CancellationToken cts, TaskCompletionSource<object> source)
        {
            Task task = null;
            string updateBulkCommandText = null;

            try
            {
                updateBulkCommandText = AnalyzeTargetAndCreateUpdateBulkCommand(internalResults);

                if (_sortedColumnMappings.Count != 0)
                {
                    _stateObj.SniContext = SniContext.Snix_SendRows;
                    _savedBatchSize = _batchSize; // For safety. If someone changes the batchsize during copy we still be using _savedBatchSize.
                    _rowsUntilNotification = _notifyAfter;
                    _rowsCopied = 0;

                    _currentRowMetadata = new SourceColumnMetadata[_sortedColumnMappings.Count];
                    for (int i = 0; i < _currentRowMetadata.Length; i++)
                    {
                        _currentRowMetadata[i] = GetColumnMetadata(i);
                    }

                    task = CopyBatchesAsync(internalResults, updateBulkCommandText, cts); // Launch the BulkCopy
                }

                if (task != null)
                {
                    if (source == null)
                    {
                        source = new TaskCompletionSource<object>();
                    }
                    AsyncHelper.ContinueTask(task, source,
                        () =>
                        {
                            // Bulk copy task is completed at this moment.
                            if (task.IsCanceled)
                            {
                                _localColumnMappings = null;
                                try
                                {
                                    CleanUpStateObject();
                                }
                                finally
                                {
                                    source.SetCanceled();
                                }
                            }
                            else if (task.Exception != null)
                            {
                                source.SetException(task.Exception.InnerException);
                            }
                            else
                            {
                                _localColumnMappings = null;
                                try
                                {
                                    CleanUpStateObject(isCancelRequested: false);
                                }
                                finally
                                {
                                    if (source != null)
                                    {
                                        if (cts.IsCancellationRequested)
                                        {   // We may get cancellation req even after the entire copy.
                                            source.SetCanceled();
                                        }
                                        else
                                        {
                                            source.SetResult(null);
                                        }
                                    }
                                }
                            }
                        }
                    );
                    return;
                }
                else
                {
                    _localColumnMappings = null;

                    try
                    {
                        CleanUpStateObject(isCancelRequested: false);
                    }
                    catch (Exception cleanupEx)
                    {
                        Debug.Fail($"Unexpected exception during {nameof(CleanUpStateObject)} (ignored)", cleanupEx.ToString());
                    }

                    if (source != null)
                    {
                        source.SetResult(null);
                    }
                }
            }
            catch (Exception ex)
            {
                _localColumnMappings = null;

                try
                {
                    CleanUpStateObject();
                }
                catch (Exception cleanupEx)
                {
                    Debug.Fail($"Unexpected exception during {nameof(CleanUpStateObject)} (ignored)", cleanupEx.ToString());
                }

                if (source != null)
                {
                    source.TrySetException(ex);
                }
                else
                {
                    throw;
                }
            }
        }
    }
}
