// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.ManualTesting.Tests;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTests.BulkCopy
{
    [Trait("Set", "2")]
    public class WriteToServerTest
    {
        private readonly string _connectionString = null;
        private readonly string _tableName1 = DataTestUtility.GetShortName("Bulk1");
        private readonly string _tableName2 = DataTestUtility.GetShortName("Bulk2");

        public WriteToServerTest()
        {
            _connectionString = (new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString;
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer), nameof(DataTestUtility.IsNotAzureSynapse))]
        public async Task WriteToServerWithDbReaderFollowedByWriteToServerWithDataRowsShouldSucceed()
        {
            try
            {
                SetupTestTables();

                DataRow[] dataRows = WriteToServerTest.CreateDataRows();
                Assert.Equal(4, dataRows.Length); // Verify the number of rows created

                DoBulkCopy(dataRows);
                await DoBulkCopyAsync(dataRows);
            }
            finally
            {
                RemoveTestTables();
            }
        }

        private void SetupTestTables()
        {
            // Create the source table and insert some data
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            DataTestUtility.DropTable(connection, _tableName1);
            DataTestUtility.DropTable(connection, _tableName2);

            using SqlCommand command = connection.CreateCommand();

            Helpers.TryExecute(command, $"create table {_tableName1} (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");
            Helpers.TryExecute(command, $"create table {_tableName2} (Id int identity primary key, FirstName nvarchar(50), LastName nvarchar(50))");

            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('John', 'Doe')");
            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Johnny', 'Smith')");
            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Jenny', 'Doe')");
            Helpers.TryExecute(command, $"insert into {_tableName1} (Firstname, LastName) values ('Jane', 'Smith')");
        }

        private static DataRow[] CreateDataRows()
        {
            DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            table.Columns.Add("FirstName", typeof(string));
            table.Columns.Add("LastName", typeof(string));

            table.Rows.Add(null, "Aaron", "Washington");
            table.Rows.Add(null, "Barry", "Mannilow");
            table.Rows.Add(null, "Charles", "Babage");
            table.Rows.Add(null, "Dean", "Snipes");

            return table.Select();
        }

        private void RemoveTestTables()
        {
            // Simplify the using statement in a small block of code
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            DataTestUtility.DropTable(connection, _tableName1);
            DataTestUtility.DropTable(connection, _tableName2);
        }

        private void DoBulkCopy(DataRow[] dataRows)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"select * from {_tableName1}";

            using IDataReader reader = command.ExecuteReader();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);

            bulkCopy.DestinationTableName = _tableName2;

            BulkCopy(bulkCopy, reader, dataRows);
        }

        private async Task DoBulkCopyAsync(DataRow[] dataRows)
        {
            // Test should be run with MARS enabled
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"select * from {_tableName1}";

            using IDataReader reader = await command.ExecuteReaderAsync();

            using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection);

            bulkCopy.DestinationTableName = _tableName2;

            await BulkCopyAsync(bulkCopy, reader, dataRows);
        }

        private static void BulkCopy(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            bulkCopy.WriteToServer(reader);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
            bulkCopy.WriteToServer(dataRows);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
        }

        private static async Task BulkCopyAsync(SqlBulkCopy bulkCopy, IDataReader reader, DataRow[] dataRows)
        {
            await bulkCopy.WriteToServerAsync(reader);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
            await bulkCopy.WriteToServerAsync(dataRows);
            Assert.Equal(dataRows.Length, bulkCopy.RowsCopied); // Verify the number of rows copied from the reader
        }

        // Regression test: a reader whose ReadAsync returns a *faulted* task carrying
        // an OperationCanceledException must surface WriteToServerAsync as Faulted
        // (not Canceled) and must not commit the rows already written. A Canceled
        // task would select cleanupParser (send BulkCopyDone) and commit partial
        // data; a faulted task aborts the batch.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithFaultedReadShouldFaultAndCommitNoRows()
        {
            string tableName = DataTestUtility.GetShortName("BulkFault");

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);

                // Reader yields two rows, then its third ReadAsync returns a faulted
                // task carrying an OperationCanceledException. The caller token is
                // never canceled, so this is a fault, not a cancellation.
                FaultingDbDataReader reader = new FaultingDbDataReader(
                    rowsBeforeFault: 2,
                    faultFactory: () => Task.FromException<bool>(new OperationCanceledException()));

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, CancellationToken.None);

                // Wait for completion without unwrapping, so we can inspect the status.
                AggregateException agg = Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsFaulted, "Expected a Faulted task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsCanceled, "Task must not be Canceled for a faulted read.");

                // Callers must observe the original OperationCanceledException, not the
                // internal BulkCopyAbortException marker used to preserve Faulted status.
                Exception observed = agg.Flatten().InnerException;
                Assert.IsType<OperationCanceledException>(observed);

                // The faulted read must abort the batch; no rows should have been
                // committed to the destination.
                Assert.Equal(0, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: when the final read reaches EOF synchronously but
        // cancellation was requested in the meantime, WriteToServerAsync must report
        // Canceled rather than success. This exercises the "task == null"
        // (synchronous completion) path, which must still honor cancellation.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithSynchronousEofAfterCancellationShouldReportCanceled()
        {
            string tableName = DataTestUtility.GetShortName("BulkSyncEof");

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);

                CancellationTokenSource cts = new CancellationTokenSource();

                // Reader yields a single row; its final ReadAsync cancels the caller
                // token and then returns a synchronously-completed false (EOF).
                CancelOnEofDbDataReader reader = new CancelOnEofDbDataReader(rowsBeforeEof: 1, cts: cts);

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, cts.Token);

                AggregateException ex = Assert.Throws<AggregateException>(() => writeTask.Wait());
                Assert.Contains(ex.Flatten().InnerExceptions, e => e is OperationCanceledException);

                Assert.True(writeTask.IsCanceled, "Expected a Canceled task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsFaulted, "Task must not be Faulted for a synchronous-EOF cancellation.");
                Assert.Equal(1, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: a reader whose ReadAsync returns a genuinely *canceled*
        // task (TaskStatus.Canceled) must surface WriteToServerAsync as Canceled,
        // even when the caller token is never signaled. The antecedent status must
        // be preserved and must not be converted into a fault.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithCanceledReadShouldReportCanceled()
        {
            string tableName = DataTestUtility.GetShortName("BulkCancel");

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);

                // Pre-canceled token used only to produce a genuinely Canceled read
                // task; it is NOT the caller token passed to WriteToServerAsync.
                CancellationTokenSource readerCts = new CancellationTokenSource();
                readerCts.Cancel();

                FaultingDbDataReader reader = new FaultingDbDataReader(
                    rowsBeforeFault: 2,
                    faultFactory: () => Task.FromCanceled<bool>(readerCts.Token));

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                // Caller token is never canceled.
                Task writeTask = bulkCopy.WriteToServerAsync(reader, CancellationToken.None);

                Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsCanceled, "Expected a Canceled task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsFaulted, "Task must not be Faulted for a genuinely canceled read.");
                Assert.Equal(2, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: a reader whose ReadAsync returns a task faulted with a
        // non-OperationCanceledException must surface WriteToServerAsync as Faulted
        // with the original exception type preserved and must not commit partial
        // data.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithFaultedReadShouldPreserveOriginalException()
        {
            string tableName = DataTestUtility.GetShortName("BulkFaultEx");

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);

                FaultingDbDataReader reader = new FaultingDbDataReader(
                    rowsBeforeFault: 2,
                    faultFactory: () => Task.FromException<bool>(new InvalidOperationException("reader boom")));

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, CancellationToken.None);

                AggregateException agg = Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsFaulted, "Expected a Faulted task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsCanceled, "Task must not be Canceled for a faulted read.");

                // The original exception type and message must be preserved to the caller.
                Exception observed = agg.Flatten().InnerException;
                InvalidOperationException ioe = Assert.IsType<InvalidOperationException>(observed);
                Assert.Equal("reader boom", ioe.Message);

                // No rows should have been committed to the destination.
                Assert.Equal(0, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: a faulted OperationCanceledException from the initial
        // ReadAsync must preserve Faulted status and expose the original exception.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithInitialFaultedReadShouldPreserveOriginalException()
        {
            string tableName = DataTestUtility.GetShortName("BulkInitialFault");

            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);
                ScriptedDbDataReader reader = new ScriptedDbDataReader(
                    (_, _) => Task.FromException<bool>(new OperationCanceledException()));

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, CancellationToken.None);
                AggregateException agg = Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsFaulted, "Expected a Faulted task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsCanceled, "Initial fault must not be reported as Canceled.");
                Assert.IsType<OperationCanceledException>(agg.Flatten().InnerException);
                Assert.Equal(0, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: caller-token cancellation must not turn a reader task
        // that is Faulted with an OperationCanceledException into cancellation.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithCallerCancellationAndFaultedReadShouldRemainFaulted()
        {
            string tableName = DataTestUtility.GetShortName("BulkCallerFault");
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);
                using CancellationTokenSource cts = new CancellationTokenSource();
                ScriptedDbDataReader reader = new ScriptedDbDataReader((readNumber, _) =>
                {
                    if (readNumber == 2)
                    {
                        cts.Cancel();
                        return Task.FromException<bool>(new OperationCanceledException());
                    }

                    return Task.FromResult(true);
                });

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, cts.Token);
                AggregateException agg = Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsFaulted, "Expected a Faulted task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsCanceled, "A faulted read must not become Canceled.");
                Assert.IsType<OperationCanceledException>(agg.Flatten().InnerException);
                Assert.Equal(0, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: a reader that throws an OCE synchronously after the
        // async row loop has started must preserve the original fault semantics.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithSynchronousReaderExceptionShouldPreserveOriginalException()
        {
            string tableName = DataTestUtility.GetShortName("BulkSyncFault");
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);
                ScriptedDbDataReader reader = new ScriptedDbDataReader((readNumber, _) =>
                {
                    if (readNumber >= 2)
                    {
                        throw new OperationCanceledException();
                    }

                    return Task.FromResult(true);
                });

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, CancellationToken.None);
                AggregateException agg = Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsFaulted, "Expected a Faulted task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsCanceled, "A synchronous reader fault must not become Canceled.");
                Assert.IsType<OperationCanceledException>(agg.Flatten().InnerException);
                Assert.Equal(0, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        // Regression test: cancellation observed during the row loop must finalize
        // the partial batch and leave the connection usable.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithMidBatchCancellationShouldCommitRowsReadBeforeCancellation()
        {
            string tableName = DataTestUtility.GetShortName("BulkMidCancel");
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);
                using CancellationTokenSource cts = new CancellationTokenSource();
                ScriptedDbDataReader reader = new ScriptedDbDataReader((readNumber, _) =>
                {
                    if (readNumber == 2)
                    {
                        cts.Cancel();
                    }

                    return Task.FromResult(true);
                });

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                Task writeTask = bulkCopy.WriteToServerAsync(reader, cts.Token);
                Assert.Throws<AggregateException>(() => writeTask.Wait());

                Assert.True(writeTask.IsCanceled, "Expected a Canceled task; actual status: " + writeTask.Status);
                Assert.False(writeTask.IsFaulted, "Mid-batch cancellation must not be Faulted.");
                Assert.Equal(2, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

#if DEBUG
        // The DEBUG-only hook makes the pending-write continuations deterministic,
        // covering CopyColumnsAsyncContinued and the async row loop without relying
        // on network timing.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public void WriteToServerAsyncWithPendingWritesShouldCompleteAndKeepConnectionUsable()
        {
            string tableName = DataTestUtility.GetShortName("BulkPendingWrite");
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                CreateSingleIntColumnTable(connection, tableName);
                ScriptedDbDataReader reader = new ScriptedDbDataReader(
                    (readNumber, _) => Task.FromResult(readNumber < 3));

                using SqlBulkCopy bulkCopy = new SqlBulkCopy(connection)
                {
                    DestinationTableName = tableName,
                };

                PropertyInfo writeHook = typeof(SqlBulkCopy).GetProperty(
                    "SetAlwaysTaskOnWrite",
                    BindingFlags.Static | BindingFlags.NonPublic);
                Assert.NotNull(writeHook);
                bool previous = (bool)writeHook.GetValue(null);
                try
                {
                    writeHook.SetValue(null, true);
                    bulkCopy.WriteToServerAsync(reader, CancellationToken.None).GetAwaiter().GetResult();
                }
                finally
                {
                    writeHook.SetValue(null, previous);
                }

                Assert.Equal(3, GetRowCount(connection, tableName));
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }
#endif

        private static void CreateSingleIntColumnTable(SqlConnection connection, string tableName)
        {
            DataTestUtility.DropTable(connection, tableName);
            using SqlCommand command = connection.CreateCommand();
            Helpers.TryExecute(command, $"create table {tableName} (col1 int)");
        }

        private static int GetRowCount(SqlConnection connection, string tableName)
        {
            using SqlCommand command = connection.CreateCommand();
            command.CommandText = $"select count(*) from {tableName}";
            return (int)command.ExecuteScalar();
        }

        // A single-int-column reader whose ReadAsync behavior is scripted by read number.
        // The delegate may return a completed, faulted, or canceled task, or throw
        // synchronously, allowing the bulk-copy status boundaries to be tested without
        // depending on network timing.
        private sealed class ScriptedDbDataReader : SingleIntColumnDbDataReader
        {
            private readonly Func<int, CancellationToken, Task<bool>> _readFactory;
            private int _rowsRead;

            public ScriptedDbDataReader(Func<int, CancellationToken, Task<bool>> readFactory)
            {
                _readFactory = readFactory;
            }

            public override int GetInt32(int ordinal) => _rowsRead;

            public override object GetValue(int ordinal) => _rowsRead;

            public override Task<bool> ReadAsync(CancellationToken cancellationToken)
            {
                int readNumber = _rowsRead++;
                return _readFactory(readNumber, cancellationToken);
            }
        }

        // A single-int-column reader that returns a configurable number of rows and
        // then faults on the next ReadAsync via the supplied factory.
        private sealed class FaultingDbDataReader : SingleIntColumnDbDataReader
        {
            private readonly int _rowsBeforeFault;
            private readonly Func<Task<bool>> _faultFactory;
            private int _rowsRead;

            public FaultingDbDataReader(int rowsBeforeFault, Func<Task<bool>> faultFactory)
            {
                _rowsBeforeFault = rowsBeforeFault;
                _faultFactory = faultFactory;
            }

            public override int GetInt32(int ordinal) => _rowsRead;

            public override object GetValue(int ordinal) => _rowsRead;

            public override Task<bool> ReadAsync(CancellationToken cancellationToken)
            {
                if (_rowsRead < _rowsBeforeFault)
                {
                    _rowsRead++;
                    return Task.FromResult(true);
                }

                // Return the faulted task verbatim (do not throw synchronously), so
                // the SqlBulkCopy pended path observes a faulted Task<bool> exactly
                // as a real reader would.
                return _faultFactory();
            }
        }

        // A single-int-column reader that returns a configurable number of rows and,
        // on the EOF read, cancels the supplied token and then returns a
        // synchronously-completed false.
        private sealed class CancelOnEofDbDataReader : SingleIntColumnDbDataReader
        {
            private readonly int _rowsBeforeEof;
            private readonly CancellationTokenSource _cts;
            private int _rowsRead;

            public CancelOnEofDbDataReader(int rowsBeforeEof, CancellationTokenSource cts)
            {
                _rowsBeforeEof = rowsBeforeEof;
                _cts = cts;
            }

            public override int GetInt32(int ordinal) => _rowsRead;

            public override object GetValue(int ordinal) => _rowsRead;

            public override Task<bool> ReadAsync(CancellationToken cancellationToken)
            {
                if (_rowsRead < _rowsBeforeEof)
                {
                    _rowsRead++;
                    return Task.FromResult(true);
                }

                // EOF: request cancellation, then report end-of-rows synchronously.
                // Task.FromResult completes synchronously, driving the "read ran
                // synchronously" fast path.
                _cts.Cancel();
                return Task.FromResult(false);
            }
        }

        // Minimal DbDataReader exposing a single Int32 column named "col1". Only the
        // members SqlBulkCopy touches for a one-column copy are implemented.
        private abstract class SingleIntColumnDbDataReader : DbDataReader
        {
            public override int FieldCount => 1;

            public override bool HasRows => true;

            public override int Depth => 0;

            public override bool IsClosed => false;

            public override int RecordsAffected => 0;

            public override object this[int ordinal] => GetValue(ordinal);

            public override object this[string name] => GetValue(GetOrdinal(name));

            public override string GetName(int ordinal) => "col1";

            public override int GetOrdinal(string name) => 0;

            public override Type GetFieldType(int ordinal) => typeof(int);

            public override string GetDataTypeName(int ordinal) => "int";

            public override bool IsDBNull(int ordinal) => false;

            public override bool GetBoolean(int ordinal) => throw new NotSupportedException();

            public override byte GetByte(int ordinal) => throw new NotSupportedException();

            public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length) => throw new NotSupportedException();

            public override char GetChar(int ordinal) => throw new NotSupportedException();

            public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length) => throw new NotSupportedException();

            public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();

            public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();

            public override double GetDouble(int ordinal) => throw new NotSupportedException();

            public override float GetFloat(int ordinal) => throw new NotSupportedException();

            public override Guid GetGuid(int ordinal) => throw new NotSupportedException();

            public override short GetInt16(int ordinal) => throw new NotSupportedException();

            public override long GetInt64(int ordinal) => throw new NotSupportedException();

            public override string GetString(int ordinal) => throw new NotSupportedException();

            public override int GetValues(object[] values)
            {
                values[0] = GetValue(0);
                return 1;
            }

            public override IEnumerator GetEnumerator() => throw new NotSupportedException();

            public override DataTable GetSchemaTable()
            {
                DataTable schema = new DataTable();
                schema.Columns.Add("ColumnName", typeof(string));
                schema.Columns.Add("ColumnOrdinal", typeof(int));
                schema.Columns.Add("DataType", typeof(Type));
                schema.Columns.Add("ColumnSize", typeof(int));
                schema.Columns.Add("AllowDBNull", typeof(bool));
                schema.Rows.Add("col1", 0, typeof(int), 4, true);
                return schema;
            }

            public override bool NextResult() => false;

            // Synchronous Read is not used by async bulk copy.
            public override bool Read() => throw new NotSupportedException("Async bulk copy should call ReadAsync.");
        }
    }
}
