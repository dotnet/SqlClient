using System;
using System.Collections.Generic;
using System.Text;
using System.Transactions;
using Xunit;
using T3 = Microsoft.Data.SqlClient.ManualTesting.Tests.T3Helpers;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class T3SqlCommandTest
    {
        /**
         * Validates the new SqlCommand.Transaction get works properly without a set call.
         * We ensure there is no longer a need to assign transaction to commands.
         */
        [CheckConnStrSetupFact]
        public static void CommandHasTransactionWhenNotAssigned()
        {
            using (SqlConnection connection = new SqlConnection(T3.ConnectionString))
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
            using (SqlConnection connection1 = new SqlConnection(T3.ConnectionString))
            using (SqlConnection connection2 = new SqlConnection(T3.ConnectionString))
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
         * None of the T3 helper methods know the transaction or assign it to the commands.
         */
        [CheckConnStrSetupFact]
        public static void CommandTransactionDataAccessWorks()
        {
            const string table = "#CommandTransactionDataAccessWorks";

            using (SqlConnection connection = new SqlConnection(T3.ConnectionString))
            {
                connection.Open();

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "select @@version;";

                    T3.DropTable(connection, table);
                    T3.CreateTable(connection, table, false);

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        // run several validations against command/connection/transaction
                        Assert.Equal(connection, transaction.Connection);
                        Assert.Equal(connection, command.Connection);
                        Assert.Equal(transaction, command.Transaction);

                        T3.InsertTable(connection, table); // insert 1 row

                        Assert.Equal(1, T3.CountTable(connection, table)); // verify row count = 1

                        transaction.Rollback(); // rollback cleanly

                        Assert.Equal(0, T3.CountTable(connection, table)); // verify row count = 0
                    }

                    T3.PurgeTable(connection, table);

                    Assert.Throws<InvalidProgramException>(new Action(() =>
                    {
                        using (SqlTransaction transaction = connection.BeginTransaction())
                        {
                            // run several validations against command/connection/transaction
                            Assert.Equal(connection, transaction.Connection);
                            Assert.Equal(connection, command.Connection);
                            Assert.Equal(transaction, command.Transaction);

                            T3.InsertTable(connection, table); // insert 1 row

                            Assert.Equal(1, T3.CountTable(connection, table)); // verify row count = 1

                            throw new InvalidProgramException(); // rollback via exception
                        }
                    }));
                    Assert.Equal(0, T3.CountTable(connection, table)); // verify row count = 0

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        // run several validations against command/connection/transaction
                        Assert.Equal(connection, transaction.Connection);
                        Assert.Equal(connection, command.Connection);
                        Assert.Equal(transaction, command.Transaction);

                        T3.InsertTable(connection, table); // insert 1 row

                        Assert.Equal(1, T3.CountTable(connection, table)); // verify row count = 1

                        transaction.Commit(); // commit

                        Assert.Equal(1, T3.CountTable(connection, table)); // verify row count = 0
                    }
                }
            }
        }
    }
}
