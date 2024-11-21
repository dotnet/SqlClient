using System.Data;
using System;
using System.Transactions;
using Xunit;
using Microsoft.Data.SqlClient.TestUtilities;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SQL.TransactionTest
{
    public class DistributedTransactionTest
    {
        [ConditionalFact(nameof(DataTestUtility.AreConnStringsSetup), Timeout = 10000)]
        public void Test_EnlistedTransactionPreservedWhilePooled()
        {
            RunTestSet(EnlistedTransactionPreservedWhilePooled);
        }

        private void EnlistedTransactionPreservedWhilePooled()
        {
            Exception commandException = null;
            Exception transactionException = null;

            try
            {
                using (TransactionScope txScope = new TransactionScope(TransactionScopeOption.Required, TimeSpan.MaxValue))
                {
                    SqlConnection rootConnection = new SqlConnection(ConnectionString);
                    rootConnection.Open();
                    using (SqlCommand command = rootConnection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }
                    // Leave first connection open so that the transaction is promoted

                    SqlConnection enlistedConnection = new SqlConnection(ConnectionString);
                    // Closing and reopening cycles the connection through the pool.
                    // We want to verify that the transaction state is preserved through this cycle.
                    enlistedConnection.Open();
                    enlistedConnection.Close();
                    enlistedConnection.Open();

                    // Forcibly kill the root connection to mimic gateway's behavior when using the proxy connection policy
                    // https://techcommunity.microsoft.com/blog/azuredbsupport/azure-sql-database-idle-sessions-are-killed-after-about-30-minutes-when-proxy-co/3268601
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
            } catch (Exception ex)
            {
                transactionException = ex;
            }


            Assert.IsType<SqlException>(commandException);
            if (Utils.IsAzureSqlServer(new SqlConnectionStringBuilder((ConnectionString)).DataSource))
            {
                // See https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors-3000-to-3999?view=sql-server-ver16
                // Error 3971 corresponds to "The server failed to resume the transaction."
                Assert.Equal(3971, ((SqlException)commandException).Number);
            } else
            {
                // See https://learn.microsoft.com/en-us/sql/relational-databases/errors-events/database-engine-events-and-errors-8000-to-8999?view=sql-server-ver16
                // The distributed transaction failed
                Assert.Equal(8525, ((SqlException)commandException).Number);
            }

            if (Utils.IsAzureSqlServer(new SqlConnectionStringBuilder((ConnectionString)).DataSource))
            {
                // Even if an application swallows the command exception, completing the transaction should indicate that it failed.
                Assert.IsType<TransactionInDoubtException>(transactionException);
            }
            else
            {
                Assert.IsType<TransactionAbortedException>(transactionException);
            }

            // Verify that nothing made it into the database
            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.True(result.Rows.Count == 0);
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

        private static void RunTestSet(Action TestCase)
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
                TestCase();
            }
            finally
            {
                DataTestUtility.RunNonQuery(ConnectionString, $"drop table {TestTableName}");
            }
        }
    }
}
