using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Transactions;
using Xunit;

/// <remarks>
/// Tests related to issue:
/// https://github.com/dotnet/SqlClient/issues/28
/// SqlCommand 
/// </remarks>
namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class TransactionInferenceTest
    {
        #region Transaction
        /**
         * This is a test just to make sure you can't overlap multiple BeginTransaction calls.
         */
        [CheckConnStrSetupFact]
        public static void ParallelTransactionsNotAllowed()
        {
            using (SqlConnection connection = new SqlConnection(Helpers.ConnectionString))
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
            using (var connection = new SqlConnection(Helpers.ConnectionString))
            {
                connection.Open();

                var table = "#ValidateOverlapTransactionTSqlApi";
                Helpers.DropTable(connection, table);
                Helpers.CreateTable(connection, table, false);

                Helpers.BeginTransaction(connection);

                using (SqlTransaction tran = connection.BeginTransaction())
                {
                    Helpers.InsertTable(connection, table, command => Assert.NotNull(command.Transaction));
                    tran.Commit();
                    Assert.Equal(1, (int)Helpers.CountTable(connection, table));
                }

                Helpers.RollbackTransaction(connection);
                Assert.Equal(0, (int)Helpers.CountTable(connection, table));
            }
        }

        /**
         * Test to see how overlapping TSql and Api transaction work.
         * Also test insert from transaction-unaware command.
         */
        [CheckConnStrSetupFact]
        public static void ValidateOverlapTransactionApiTSql()
        {
            using (var connection = new SqlConnection(Helpers.ConnectionString))
            {
                connection.Open();

                var table = "#ValidateOverlapTransactionApiTSql";
                Helpers.DropTable(connection, table);
                Helpers.CreateTable(connection, table, false);

                using (SqlTransaction tran = connection.BeginTransaction())
                {
                    Helpers.BeginTransaction(connection);
                    Helpers.InsertTable(connection, table, command => Assert.NotNull(command.Transaction));
                    Helpers.CommitTransaction(connection);
                    Assert.Equal(1, Helpers.CountTable(connection, table));

                    tran.Rollback();
                    Assert.Equal(0, Helpers.CountTable(connection, table));
                }
            }
        }
        #endregion

        #region Command
        /**
         * Validates the new SqlCommand.Transaction get works properly without a set call.
         * We ensure there is no longer a need to assign transaction to commands.
         */
        [CheckConnStrSetupFact]
        public static void CommandHasTransactionWhenNotAssigned()
        {
            using (SqlConnection connection = new SqlConnection(Helpers.ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "select @@version;";
                    Assert.Null(command.Transaction); // no transaction at this point
                    command.ExecuteNonQuery();

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        Assert.NotNull(command.Transaction); // we have transaction at this point (never assigned)
                        Assert.Equal(transaction, command.Transaction); // is it the right value?
                        command.ExecuteNonQuery();
                        transaction.Rollback(); // conclude transaction

                        // transaction is concluded so becomes null again
                        Assert.Null(command.Transaction);
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        /**
         * Validates the new SqlCommand.Transaction user input validations.
         * 
         * This focuses on allowing assignment of the right connection's current transaction.
         * And it also makes sure you cannot assign completed transactions.
         * But the assigment does nothing and is never stored, just validated.
         * 
         * We use 2 connections, 2 transactions and 1 command we shuffle around and test.
         */
        [CheckConnStrSetupFact]
        public static void CommandAssignTransactionValidations()
        {
            using (SqlConnection connection1 = new SqlConnection(Helpers.ConnectionString))
            using (SqlConnection connection2 = new SqlConnection(Helpers.ConnectionString))
            {
                connection1.Open();
                connection2.Open();

                using (SqlTransaction transaction1 = connection1.BeginTransaction())
                using (SqlTransaction transaction2 = connection2.BeginTransaction())
                {
                    // belongs to connection1 initially
                    using (SqlCommand command = connection1.CreateCommand())
                    {
                        command.CommandText = "select @@version;";

                        command.Transaction = null; // this works here
                        command.Transaction = transaction1; // this works here
                        Assert.Throws<InvalidOperationException>(() => command.Transaction = transaction2); // this fails

                        transaction1.Rollback(); // conclude transaction1, transaction2 still open

                        command.Transaction = null; // this works too

                        // this now fails as the transaction1 is concluded (zombied)
                        Assert.Throws<InvalidOperationException>(() => command.Transaction = transaction1);

                        // this fails because the transaction2 belongs to another connection
                        Assert.Throws<InvalidOperationException>(() => command.Transaction = transaction2);

                        // we change the connection and now it works
                        command.Connection = connection2;
                        command.Transaction = transaction2;
                        command.Transaction = null;
                        Assert.Throws<InvalidOperationException>(() => command.Transaction = transaction1);

                        Assert.NotNull(command.Transaction);
                        transaction2.Commit(); // conclude transaction2 also
                        Assert.Null(command.Transaction);
                    }
                }
            }
        }

        /**
         * We make sure transactions actually work (with data) without ever assigning the .Transaction on commands.
         * None of the Helpers helper methods know the transaction or assign it to the commands.
         */
        [CheckConnStrSetupFact]
        public static void CommandTransactionDataAccessWorks()
        {
            const string table = "#CommandTransactionDataAccessWorks";

            using (SqlConnection connection = new SqlConnection(Helpers.ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "select @@version;";

                    Helpers.DropTable(connection, table);
                    Helpers.CreateTable(connection, table, false);

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        // run several validations against command/connection/transaction
                        Assert.Equal(connection, transaction.Connection);
                        Assert.Equal(connection, command.Connection);
                        Assert.Equal(transaction, command.Transaction);

                        Helpers.InsertTable(connection, table); // insert 1 row

                        Assert.Equal(1, Helpers.CountTable(connection, table)); // verify row count = 1

                        transaction.Rollback(); // rollback cleanly

                        Assert.Equal(0, Helpers.CountTable(connection, table)); // verify row count = 0
                    }

                    Helpers.PurgeTable(connection, table);

                    Assert.Throws<InvalidProgramException>(new Action(() =>
                    {
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            // run several validations against command/connection/transaction
                            Assert.Equal(connection, transaction.Connection);
                            Assert.Equal(connection, command.Connection);
                            Assert.Equal(transaction, command.Transaction);

                            Helpers.InsertTable(connection, table); // insert 1 row

                            Assert.Equal(1, Helpers.CountTable(connection, table)); // verify row count = 1

                            throw new InvalidProgramException(); // rollback via exception
                        }
                    }));
                    Assert.Equal(0, Helpers.CountTable(connection, table)); // verify row count = 0

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        // run several validations against command/connection/transaction
                        Assert.Equal(connection, transaction.Connection);
                        Assert.Equal(connection, command.Connection);
                        Assert.Equal(transaction, command.Transaction);

                        Helpers.InsertTable(connection, table); // insert 1 row

                        Assert.Equal(1, Helpers.CountTable(connection, table)); // verify row count = 1

                        transaction.Commit(); // commit

                        Assert.Equal(1, Helpers.CountTable(connection, table)); // verify row count = 0
                    }
                }
            }
        }
        #endregion

        #region BulkCopy
        // To be continued.
        #endregion

        #region Helpers
        /// <summary>
        /// Helper methods used in the Test methods to reduce code bloat.
        /// </summary>
        internal static class Helpers
        {
            public static string ConnectionString => CreateConnectionString();

            public static string CreateConnectionString(Action<SqlConnectionStringBuilder> builder = null)
            {
                var csb = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
                {
                    MultipleActiveResultSets = false,
                    Pooling = false,
                };

                builder?.Invoke(csb);
                return csb.ToString();
            }

            public static void DropTable(SqlConnection connection, string name)
            {
                ExecuteNonQuery(connection, $"drop table if exists {name};");
            }

            public static int InsertTable(SqlConnection connection, string name, Action<SqlCommand> callback = null)
            {
                return ExecuteNonQuery(connection, $"insert into {name} default values;", callback);
            }

            public static int InsertTable(SqlConnection connection, string name, long value, Action<SqlCommand> callback = null)
            {
                return ExecuteNonQuery(connection, $"insert into {name} ([Value]) values ({value.ToString(CultureInfo.InvariantCulture)});", callback);
            }

            public static void CreateTable(SqlConnection connection, string name, bool value)
            {
                DropTable(connection, name);
                ExecuteNonQuery(connection, $"create table {name}"
                    + "("
                    + "[ID] bigint identity(1,1) not null"
                    + (value ? ", [Value] bigint not null" : string.Empty)
                    + ", primary key clustered ([ID] asc)"
                    + ");");
            }

            public static int ExecuteNonQuery(SqlConnection connection, string query, Action<SqlCommand> callback = null)
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    callback?.Invoke(command);
                    return command.ExecuteNonQuery();
                }
            }

            public static object ExecuteScalar(SqlConnection connection, string query, Action<SqlCommand> callback = null)
            {
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = query;
                    callback?.Invoke(command);
                    return command.ExecuteScalar();
                }
            }

            /// <summary>
            /// Shorthand to calling: 'select count(*) from {name}';
            /// </summary>
            public static int CountTable(SqlConnection connection, string name, Action<SqlCommand> callback = null)
            {
                return (int)ExecuteScalar(connection, $"select count(*) from {name};", callback);
            }

            /// <summary>
            /// Shorthand to calling: 'truncate table {name}';
            /// </summary>
            public static void PurgeTable(SqlConnection connection, string name, Action<SqlCommand> callback = null)
            {
                ExecuteNonQuery(connection, $"truncate table {name};", callback);
            }

            /// <summary>
            /// Shorthand to calling: 'begin transaction';
            /// </summary>
            public static void BeginTransaction(SqlConnection connection, Action<SqlCommand> callback = null)
            {
                ExecuteNonQuery(connection, $"begin transaction", callback);
            }

            /// <summary>
            /// Shorthand to calling: 'commit transaction';
            /// </summary>
            public static void CommitTransaction(SqlConnection connection, Action<SqlCommand> callback = null)
            {
                ExecuteNonQuery(connection, $"commit transaction;", callback);
            }

            /// <summary>
            /// Shorthand to calling: 'rollback transaction';
            /// </summary>
            public static void RollbackTransaction(SqlConnection connection, Action<SqlCommand> callback = null)
            {
                ExecuteNonQuery(connection, $"rollback transaction;", callback);
            }
        }
        #endregion
    }
}
