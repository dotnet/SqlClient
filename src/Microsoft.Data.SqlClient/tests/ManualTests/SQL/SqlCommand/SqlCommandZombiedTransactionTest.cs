// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [Trait("Set", "3")]
    public class SqlCommandZombiedTransactionTest
    {
        /// <summary>
        /// After the server aborts a transaction, assigning the command's connection again must not
        /// let the next write auto-commit.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Execute_AfterAssigningSameConnection_DoesNotAutocommit()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "SET XACT_ABORT ON; CREATE TABLE #ZombiedTransaction (Id int NOT NULL);";
            setup.ExecuteNonQuery();

            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand command = new SqlCommand("INSERT INTO #ZombiedTransaction (Id) VALUES (1)", connection, transaction);
            using SqlCommand abort = new SqlCommand("SELECT CAST('n/a' AS int)", connection, transaction);
            Assert.Throws<SqlException>(() => abort.ExecuteScalar());

            command.Connection = connection;

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());
            Assert.Contains("no longer usable", exception.Message);

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #ZombiedTransaction", connection);
            Assert.Equal(0, (int)count.ExecuteScalar());
        }

        /// <summary>
        /// Disposing a live transaction rolls it back. The command that was bound to it must still run.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Execute_AfterDisposingLiveTransaction_Runs()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "CREATE TABLE #DisposedTransaction (Id int NOT NULL);";
            setup.ExecuteNonQuery();

            using SqlCommand command = new SqlCommand("INSERT INTO #DisposedTransaction (Id) VALUES (1)", connection);
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                command.Transaction = transaction;
            }

            Assert.Null(command.Transaction);
            command.Connection = connection;
            command.ExecuteNonQuery();

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #DisposedTransaction", connection);
            Assert.Equal(1, (int)count.ExecuteScalar());
        }

        /// <summary>
        /// Disposing a live transaction rolls it back. An async execute of the bound command must still run.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task Execute_AfterDisposingLiveTransaction_RunsAsync()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "CREATE TABLE #DisposedTransaction (Id int NOT NULL);";
            await setup.ExecuteNonQueryAsync();

            using SqlCommand command = new SqlCommand("INSERT INTO #DisposedTransaction (Id) VALUES (1)", connection);
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                command.Transaction = transaction;
            }

            Assert.Null(command.Transaction);
            command.Connection = connection;
            await command.ExecuteNonQueryAsync();

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #DisposedTransaction", connection);
            Assert.Equal(1, (int)await count.ExecuteScalarAsync());
        }

        /// <summary>
        /// A T-SQL COMMIT finishes the transaction. The command that was bound to it must still run.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Execute_AfterTsqlCommit_Runs()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "CREATE TABLE #CommittedTransaction (Id int NOT NULL);";
            setup.ExecuteNonQuery();

            using SqlCommand command = new SqlCommand("INSERT INTO #CommittedTransaction (Id) VALUES (1)", connection);
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                command.Transaction = transaction;
                using SqlCommand commit = new SqlCommand("COMMIT TRANSACTION", connection, transaction);
                commit.ExecuteNonQuery();
            }

            Assert.Null(command.Transaction);
            command.Connection = connection;
            command.ExecuteNonQuery();

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #CommittedTransaction", connection);
            Assert.Equal(1, (int)count.ExecuteScalar());
        }

        /// <summary>
        /// A T-SQL COMMIT finishes the transaction. An async execute of the bound command must still run.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task Execute_AfterTsqlCommit_RunsAsync()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "CREATE TABLE #CommittedTransaction (Id int NOT NULL);";
            await setup.ExecuteNonQueryAsync();

            using SqlCommand command = new SqlCommand("INSERT INTO #CommittedTransaction (Id) VALUES (1)", connection);
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                command.Transaction = transaction;
                using SqlCommand commit = new SqlCommand("COMMIT TRANSACTION", connection, transaction);
                await commit.ExecuteNonQueryAsync();
            }

            Assert.Null(command.Transaction);
            command.Connection = connection;
            await command.ExecuteNonQueryAsync();

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #CommittedTransaction", connection);
            Assert.Equal(1, (int)await count.ExecuteScalarAsync());
        }

        /// <summary>
        /// A batch must not copy <see cref="SqlCommand.Transaction"/> back onto its command.
        /// That getter returns null after a server abort, and the batch would auto-commit.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Execute_SqlBatchAfterServerAbort_DoesNotAutocommit()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "SET XACT_ABORT ON; CREATE TABLE #BatchAbort (Id int NOT NULL);";
            setup.ExecuteNonQuery();

            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlBatch batch = new SqlBatch(connection, transaction);
            batch.Commands.Add(new SqlBatchCommand("INSERT INTO #BatchAbort (Id) VALUES (1)"));
            using SqlCommand abort = new SqlCommand("SELECT CAST('n/a' AS int)", connection, transaction);
            Assert.Throws<SqlException>(() => abort.ExecuteScalar());

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => batch.ExecuteNonQuery());
            Assert.Contains("no longer usable", exception.Message);

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #BatchAbort", connection);
            Assert.Equal(0, (int)count.ExecuteScalar());
        }

        /// <summary>
        /// Async batch execution keeps the same aborted transaction.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task Execute_SqlBatchAfterServerAbort_DoesNotAutocommitAsync()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "SET XACT_ABORT ON; CREATE TABLE #BatchAbort (Id int NOT NULL);";
            await setup.ExecuteNonQueryAsync();

            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlBatch batch = new SqlBatch(connection, transaction);
            batch.Commands.Add(new SqlBatchCommand("INSERT INTO #BatchAbort (Id) VALUES (1)"));
            using SqlCommand abort = new SqlCommand("SELECT CAST('n/a' AS int)", connection, transaction);
            await Assert.ThrowsAsync<SqlException>(() => abort.ExecuteScalarAsync());

            Exception exception = await Record.ExceptionAsync(() => batch.ExecuteNonQueryAsync());
            InvalidOperationException invalid = Assert.IsType<InvalidOperationException>(exception);
            Assert.Contains("no longer usable", invalid.Message);

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #BatchAbort", connection);
            Assert.Equal(0, (int)await count.ExecuteScalarAsync());
        }

        /// <summary>
        /// Adapter batching must keep a server-aborted transaction. Copying
        /// <see cref="SqlCommand.Transaction"/> gives null, and the batch would auto-commit.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Update_AdapterBatchAfterServerAbort_DoesNotAutocommit()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "SET XACT_ABORT ON; CREATE TABLE #AdapterAbort (Id int NOT NULL);";
            setup.ExecuteNonQuery();

            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand insert = new SqlCommand("INSERT INTO #AdapterAbort (Id) VALUES (@Id)", connection, transaction);
            insert.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int, 0, "Id"));
            insert.UpdatedRowSource = UpdateRowSource.None;
            using SqlCommand abort = new SqlCommand("SELECT CAST('n/a' AS int)", connection, transaction);
            Assert.Throws<SqlException>(() => abort.ExecuteScalar());

            using DataTable table = new DataTable();
            table.Columns.Add("Id", typeof(int));
            // One row uses the original command, which still holds the abort.
            // Two rows go through the batch command, which used to receive null.
            table.Rows.Add(1);
            table.Rows.Add(2);

            using SqlDataAdapter adapter = new SqlDataAdapter
            {
                InsertCommand = insert,
                UpdateBatchSize = 2
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => adapter.Update(table));
            Assert.Contains("no longer usable", exception.Message);

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #AdapterAbort", connection);
            Assert.Equal(0, (int)count.ExecuteScalar());
        }

        /// <summary>
        /// An abandoned reader can still hold the server rollback. The next execute drains it and
        /// must reject the write. Closing the reader here would consume that rollback first.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void Execute_AfterDrainingAbandonedRollback_DoesNotAutocommit()
        {
            using SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand setup = connection.CreateCommand();
            setup.CommandText = "CREATE TABLE #AbandonedRollback (Id int NOT NULL);";
            setup.ExecuteNonQuery();

            using SqlTransaction transaction = connection.BeginTransaction();
            using SqlCommand insert = new SqlCommand("INSERT INTO #AbandonedRollback (Id) VALUES (1)", connection, transaction);
            using SqlCommand read = new SqlCommand("SELECT 1; ROLLBACK;", connection, transaction);
            SqlDataReader reader = read.ExecuteReader();
            try
            {
                Assert.True(reader.Read());
                Assert.Equal(1, reader.GetInt32(0));

                // Drop the reader from the connection without reading the rollback that follows.
                MethodInfo remove = typeof(SqlConnection).GetMethod(
                    "RemoveWeakReference",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(remove);
                remove.Invoke(connection, new object[] { reader });

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => insert.ExecuteNonQuery());
                Assert.Contains("no longer usable", exception.Message);
            }
            finally
            {
                // The execute already drained the rollback this reader was holding.
                try
                {
                    reader.Dispose();
                }
                catch (SqlException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }

            using SqlCommand count = new SqlCommand("SELECT COUNT(*) FROM #AbandonedRollback", connection);
            Assert.Equal(0, (int)count.ExecuteScalar());
        }
    }
}
