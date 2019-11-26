using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using Xunit;
using T3 = Microsoft.Data.SqlClient.ManualTesting.Tests.T3Helpers;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class T3SqlBulkCopyTest
    {
        /**
         * Testing several constructors, some for success, some for failure.
         */
        [CheckConnStrSetupFact]
        public static void TestConstructorsAndValidations()
        {
            using (var connection1 = new SqlConnection(T3.ConnectionString))
            using (var connection2 = new SqlConnection(T3.ConnectionString))
            {
                connection1.Open();
                connection2.Open();

                using (SqlTransaction transaction1 = connection1.BeginTransaction())
                using (SqlTransaction transaction2 = connection2.BeginTransaction())
                {
                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection1))
                    { }

                    // this is a new constructor with no transaction
                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.Default))
                    { }

                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.Default, transaction1))
                    { }

                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.Default, null))
                    { }

                    // This fails because conflicting connection1 and transaction2.
                    Assert.Throws<InvalidOperationException>(delegate
                    {
                        using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.Default, transaction2))
                        { }
                    });

                    // This works because by the time user calls WritetoServer, the trasaction1 might be completed.
                    // If it is not, validation is done late and it fails on the WritetoServer call.
                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.UseInternalTransaction))
                    { }

                    // This fails because conflicting UseInternalTransaction and transaction1.
                    // This we validate on the spot as user shows clear erroneous intent to bind this to transaction1 too.
                    Assert.Throws<ArgumentException>(delegate
                    {
                        using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.UseInternalTransaction, transaction1))
                        { }
                    });

                    // This fails because conflicting UseInternalTransaction and transaction2.
                    // This we validate on the spot as user shows clear erroneous intent to bind this to transaction1 too.
                    Assert.Throws<ArgumentException>(delegate
                    {
                        using (SqlBulkCopy sbc = new SqlBulkCopy(connection1, SqlBulkCopyOptions.UseInternalTransaction, transaction2))
                        { }
                    });
                }
            }
        }

        /**
         * Ensure ExternalTransaction succeedes without UseInternalTransaction.
         */
        [CheckConnStrSetupFact]
        public static void BulkCopyHasTransactionWhenNotAssigned()
        {
            const string table = "#BulkCopyHasTransactionWhenNotAssigned";

            using (var connection = new SqlConnection(T3.ConnectionString))
            using (DataTable dt = T3.BulkValues(table, 100))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                using (SqlBulkCopy sbc = new SqlBulkCopy(connection)) // <-- no transaction mentioned
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    // this one is rolledback
                    using (SqlTransaction transaction1 = connection.BeginTransaction())
                    {
                        Assert.Equal(0, T3.CountTable(connection, table));
                        sbc.WriteToServer(dt); // <-- works
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                        transaction1.Rollback();
                        Assert.Equal(0, T3.CountTable(connection, table));
                    }

                    // this one is committed
                    using (SqlTransaction transaction2 = connection.BeginTransaction())
                    {
                        Assert.Equal(0, T3.CountTable(connection, table));
                        sbc.WriteToServer(dt); // <-- works
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                        transaction2.Commit();
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                    }

                    // and this runs outside transactions
                    sbc.WriteToServer(dt); // <-- works
                    Assert.Equal(dt.Rows.Count * 2, T3.CountTable(connection, table));
                }
            }
        }

        /**
         * Ensure SqlBulkCopyOptions.UseInternalTransaction and BeginTransaction combo fails.
         * Ensure SqlBulkCopyOptions.UseInternalTransaction and 'begin transaction' combo fails.
         */
        [CheckConnStrSetupFact]
        public static void InternalAndExternalTransactionFails()
        {
            const string table = "#InternalAndExternalTransactionFails";

            using (DataTable dt = T3.BulkValues(table, 100))
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                // test against api transaction
                using (SqlBulkCopy sbc = new SqlBulkCopy(connection, SqlBulkCopyOptions.UseInternalTransaction))
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        // ensure the 'Parallel Transactions not supported' Exception kicks in
                        Assert.Throws<InvalidOperationException>(delegate
                        {
                            sbc.WriteToServer(dt); // <-- this fails
                        });
                        Assert.Equal(0, T3.CountTable(connection, table));

                        transaction.Rollback();
                    }

                    // now internal transaction will work as expected
                    sbc.WriteToServer(dt); // <--
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                }

                T3.PurgeTable(connection, table);

                // test against tsql transaction
                using (SqlBulkCopy sbc = new SqlBulkCopy(connection, SqlBulkCopyOptions.UseInternalTransaction))
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    T3.BeginTransaction(connection);

                    // ensure the 'Parallel Transactions not supported' Exception kicks in
                    Assert.Throws<InvalidOperationException>(() => sbc.WriteToServer(dt)); // <--
                    Assert.Equal(0, T3.CountTable(connection, table));

                    T3.RollbackTransaction(connection);

                    // now it must work as we closed the pre-existing transaction
                    sbc.WriteToServer(dt); // <--
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                }
            }
        }

        /**
         * Ensure UseInternalTransaction succeedes if not already in a Transaction.
         */
        [CheckConnStrSetupFact]
        public static void InternalTransactionWorks()
        {
            const string table = "#InternalTransactionWorks";

            using (DataTable dt = T3.BulkValues(table, 100))
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                using (SqlBulkCopy sbc = new SqlBulkCopy(connection, SqlBulkCopyOptions.UseInternalTransaction))
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    sbc.WriteToServer(dt); // <--

                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                }
            }
        }

        /**
         * Ensure UseInternalTransaction succeedes if not already in a Transaction.
         */
        [CheckConnStrSetupFact]
        public static void NoTransactionWorks()
        {
            const string table = "#NoTransactionWorks";

            using (DataTable dt = T3.BulkValues(table, 100))
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                using (SqlBulkCopy sbc = new SqlBulkCopy(connection))
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    sbc.WriteToServer(dt); // <--

                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                }
            }
        }

        /**
         * Ensure External Api/TSql Transaction succeedes without UseInternalTransaction.
         */
        [CheckConnStrSetupFact]
        public static void ExternalTransactionWorks()
        {
            const string table = "#ExternalTransactionWorks";

            using (DataTable dt = T3.BulkValues(table, 100))
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                // first we use api transactions
                using (SqlBulkCopy sbc = new SqlBulkCopy(connection)) // <-- no internal
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    // ensure it all works well with a commit transaction
                    using (SqlTransaction transaction1 = connection.BeginTransaction())
                    {
                        sbc.WriteToServer(dt);
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                        transaction1.Commit();
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                    }

                    T3.PurgeTable(connection, table);

                    // ensure it all works well with a rollback transaction
                    using (SqlTransaction transaction2 = connection.BeginTransaction())
                    {
                        sbc.WriteToServer(dt);
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                        transaction2.Rollback();
                        Assert.Equal(0, T3.CountTable(connection, table));
                    }
                }

                T3.PurgeTable(connection, table);

                // second we use tsql transaction
                using (SqlBulkCopy sbc = new SqlBulkCopy(connection)) // <--
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    // ensure it all works well with a commit transaction
                    T3.BeginTransaction(connection);
                    sbc.WriteToServer(dt);
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                    T3.CommitTransaction(connection);
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));

                    T3.PurgeTable(connection, table);

                    // ensure it all works well with a rollback transaction
                    T3.BeginTransaction(connection);
                    sbc.WriteToServer(dt);
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                    T3.RollbackTransaction(connection);
                    Assert.Equal(0, T3.CountTable(connection, table));
                }
            }
        }

        /**
         * Ensure SqlBulkCopy WriteToServer() fails if UseInternalTransaction is present and connection is already in a transaction.
         * 
         * We test both BeginTransaction and 'begin transaction'.
         * 
         * SqlBulkCopy() constructor will pass as we don't validate there.
         * User might complete the existing transaction before WriteToServer() is called.
         * So if still in transaction, we get 'Parallel Transaction Not Allowed' to occur naturally in WriteToServer().
         */
        [CheckConnStrSetupFact]
        public static void TransactionWithInternalFails()
        {
            const string table = "#TransactionWithInternalFails";

            using (DataTable dt = T3.BulkValues(table, 100))
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                // first use TSql Transaction
                T3.BeginTransaction(connection);
                using (SqlBulkCopy sbc = new SqlBulkCopy(connection, SqlBulkCopyOptions.UseInternalTransaction)) // <--
                {
                    T3.BulkPrepare(sbc, dt, 0);
                    
                    Assert.Throws<InvalidOperationException>(() => sbc.WriteToServer(dt));
                    Assert.Equal(0, T3.CountTable(connection, table));
                }
                T3.RollbackTransaction(connection);

                // second use Api Transaction
                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection, SqlBulkCopyOptions.UseInternalTransaction)) // <--
                    {
                        T3.BulkPrepare(sbc, dt, 0);

                        Assert.Throws<InvalidOperationException>(() => sbc.WriteToServer(dt));
                        Assert.Equal(0, T3.CountTable(connection, table));
                    }
                }
            }
        }

        /**
         * Ensure SqlBulkCopy constructor fails if UseInternalTransaction is present and connection is already in a Api transaction.
         */
        [CheckConnStrSetupFact]
        public static void TransactionWithoutInternalWorks()
        {
            const string table = "#ApiTransactionCommitWithoutInternalWorks";

            using (DataTable dt = T3.BulkValues(table, 100))
            using (var connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                T3.DropTable(connection, table);
                T3.CreateTable(connection, table, true);

                using (SqlTransaction transaction = connection.BeginTransaction())
                {
                    using (SqlBulkCopy sbc = new SqlBulkCopy(connection))
                    {
                        T3.BulkPrepare(sbc, dt, 0);

                        sbc.WriteToServer(dt);
                        Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                    }
                    transaction.Commit();
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                }

                T3.PurgeTable(connection, table);

                T3.BeginTransaction(connection);
                using (SqlBulkCopy sbc = new SqlBulkCopy(connection))
                {
                    T3.BulkPrepare(sbc, dt, 0);

                    sbc.WriteToServer(dt);
                    Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                }
                Assert.Equal(dt.Rows.Count, T3.CountTable(connection, table));
                T3.CommitTransaction(connection);
            }
        }
    }
}
