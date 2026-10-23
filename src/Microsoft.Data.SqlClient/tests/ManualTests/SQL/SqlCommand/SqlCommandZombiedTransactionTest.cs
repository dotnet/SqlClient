// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
    }
}
