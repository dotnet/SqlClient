using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using T3 = Microsoft.Data.SqlClient.ManualTesting.Tests.T3Helpers;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class T3SqlTransactionTest
    {
        /**
         * This is a test just to make sure you can't overlap multiple BeginTransaction calls.
         */
        [CheckConnStrSetupFact]
        public static void ParallelTransactionsNotAllowed()
        {
            using (SqlConnection connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                using (SqlTransaction transaction1 = connection.BeginTransaction())
                {
                    Assert.Throws<InvalidOperationException>(delegate
                    {
                        using (SqlTransaction transaction2 = connection.BeginTransaction())
                        { }
                    });
                }
            }
        }

        /**
         * Test to see how overlapping TSql and Api transaction work.
         * Also test insert from transaction-unaware command.
         */
        [CheckConnStrSetupFact]
        public static void ValidateOverlapTransactionTSqlApi()
        {
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                var table = "#ValidateOverlapTransactionTSqlApi";
                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, false);

                T3.BeginTransaction(connection);

                using (SqlTransaction tran = connection.BeginTransaction())
                {
                    T3.InsertTable(connection, table, command => Assert.NotNull(command.Transaction));
                    tran.Commit();
                    Assert.Equal(1, (int)T3.CountTable(connection, table));
                }

                T3.RollbackTransaction(connection);
                Assert.Equal(0, (int)T3.CountTable(connection, table));
            }
        }

        /**
         * Test to see how overlapping TSql and Api transaction work.
         * Also test insert from transaction-unaware command.
         */
        [CheckConnStrSetupFact]
        public static void ValidateOverlapTransactionApiTSql()
        {
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                var table = "#ValidateOverlapTransactionApiTSql";
                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, false);

                using (SqlTransaction tran = connection.BeginTransaction())
                {
                    T3.BeginTransaction(connection);
                    T3.InsertTable(connection, table, command => Assert.NotNull(command.Transaction));
                    T3.CommitTransaction(connection);
                    Assert.Equal(1, T3.CountTable(connection, table));

                    tran.Rollback();
                    Assert.Equal(0, T3.CountTable(connection, table));
                }
            }
        }
    }
}
