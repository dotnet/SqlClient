// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient.TestUtilities;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{

    [PlatformSpecific(TestPlatforms.Windows)]
    public class DistributedTransactionTestWindows
    {

#if NET
        private static bool s_DelegatedTransactionCondition => DataTestUtility.AreConnStringsSetup() && DataTestUtility.IsNotAzureServer() && DataTestUtility.IsNotX86Architecture;

        [ConditionalFact(nameof(s_DelegatedTransactionCondition), Timeout = 10000)]
        public async Task Delegated_transaction_deadlock_in_SinglePhaseCommit()
        {
            TransactionManager.ImplicitDistributedTransactions = true;
            using var transaction = new CommittableTransaction();

            // Uncommenting the following makes the deadlock go away as a workaround. If the transaction is promoted before
            // the first SqlClient enlistment, it never goes into the delegated state.
            // _ = TransactionInterop.GetTransmitterPropagationToken(transaction);
            await using var conn = new SqlConnection(DataTestUtility.TCPConnectionString);
            await conn.OpenAsync();
            conn.EnlistTransaction(transaction);

            // Enlisting the transaction in second connection causes the transaction to be promoted.
            // After this, the transaction state will be "delegated" (delegated to SQL Server), and the commit below will
            // trigger a call to SqlDelegatedTransaction.SinglePhaseCommit.
            await using var conn2 = new SqlConnection(DataTestUtility.TCPConnectionString);
            await conn2.OpenAsync();
            conn2.EnlistTransaction(transaction);

            // Possible deadlock
            transaction.Commit();
        }
#endif

        /** Test fails on SQL Managed Instance with error:
        *   Failure: Item not found in collection
        *        Collection: [typeof(System.Transactions.TransactionAbortedException), typeof(System.Transactions.TransactionInDoubtException)]
        *        Not found:  typeof(Microsoft.Data.SqlClient.SqlException)
        **/
        private static bool s_EnlistedTransactionPreservedWhilePooledCondition => DataTestUtility.AreConnStringsSetup() && DataTestUtility.IsNotX86Architecture && DataTestUtility.IsNotManagedInstance();

        [Trait("Category", "flaky")]
        [ConditionalFact(nameof(s_EnlistedTransactionPreservedWhilePooledCondition), Timeout = 10000)]
        public async Task Test_EnlistedTransactionPreservedWhilePooled()
        {
#if NET
            TransactionManager.ImplicitDistributedTransactions = true;
#endif
            await RunTestSet(EnlistedTransactionPreservedWhilePooled);
        }

        private async Task EnlistedTransactionPreservedWhilePooled()
        {
            Exception commandException = null;
            Exception transactionException = null;

            try
            {
                using (TransactionScope txScope = new TransactionScope(TransactionScopeOption.Required, TimeSpan.MaxValue))
                {
                    // Leave first connection open so that the transaction is promoted
                    SqlConnection rootConnection = new SqlConnection(ConnectionString);
                    await rootConnection.OpenAsync();
                    using (SqlCommand command = rootConnection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }

                    // Closing and reopening cycles the connection through the pool.
                    // We want to verify that the transaction state is preserved through this cycle.
                    SqlConnection enlistedConnection = new SqlConnection(ConnectionString);
                    enlistedConnection.Open();
                    enlistedConnection.Close();
                    enlistedConnection.Open();

                    // Forcibly kill the root connection to mimic gateway's behavior when using the proxy connection policy
                    // https://techcommunity.microsoft.com/blog/azuredbsupport/azure-sql-database-idle-sessions-are-killed-after-about-30-minutes-when-proxy-co/3268601
                    // Can also represent a general server-side, process failure
                    KillProcess(rootConnection.ServerProcessId);


                    using (SqlCommand command = enlistedConnection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        try
                        {
                            command.ExecuteNonQuery();
                        }
                        catch (Exception ex)
                        {
                            commandException = ex;
                        }
                    }

                    txScope.Complete();
                }
            }
            catch (Exception ex)
            {
                transactionException = ex;
            }

            // Even if an application swallows the command exception, completing the transaction should indicate that it failed.
            Type[] expectedTransactionExceptions = new[] { typeof(TransactionAbortedException), typeof(TransactionInDoubtException) };
            Assert.Contains(transactionException.GetType(), expectedTransactionExceptions);

            Type[] expectedCommandExceptions = new[] { typeof(SqlException), typeof(InvalidOperationException) };
            Assert.Contains(commandException.GetType(), expectedCommandExceptions);

            if (commandException is SqlException exception)
            {
                // See https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors-8000-to-8999?view=sql-server-ver16
                // The distributed transaction failed
                // See https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors-3000-to-3999?view=sql-server-ver16
                // Error 3971 corresponds to "The server failed to resume the transaction."
                var expectedExceptionCodes = new[] { 3971, 8525 };
                Assert.Contains(exception.Number, expectedExceptionCodes);
            }

            // Verify that nothing made it into the database
            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.Equal(0, result.Rows.Count);
        }

        private void KillProcess(int serverProcessId)
        {
            using (TransactionScope txScope = new TransactionScope(TransactionScopeOption.Suppress))
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"KILL {serverProcessId}";
                        command.ExecuteNonQuery();
                    }
                }
                txScope.Complete();
            }
        }

        private static string TestTableName;
        private static string ConnectionString;
        private const int InputCol1 = 1;
        private const string InputCol2 = "One";

        private static async Task RunTestSet(Func<Task> TestCase)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);

            builder.Pooling = true;
            builder.MaxPoolSize = 5;
            builder.Enlist = true;
            ConnectionString = builder.ConnectionString;

            TestTableName = DataTestUtility.GenerateObjectName();
            DataTestUtility.RunNonQuery(ConnectionString, $"create table {TestTableName} (col1 int, col2 text)");
            try
            {
                await TestCase();
            }
            finally
            {
                DataTestUtility.RunNonQuery(ConnectionString, $"drop table {TestTableName}");
            }
        }
    }
}

