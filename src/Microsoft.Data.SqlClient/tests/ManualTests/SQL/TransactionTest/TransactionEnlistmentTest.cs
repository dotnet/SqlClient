// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using System.Transactions;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class TransactionEnlistmentTest
    {
        // TODO Synapse: Cannot find data type 'text'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public static void TestAutoEnlistment_TxScopeComplete()
        {
            RunTestSet(TestCase_AutoEnlistment_TxScopeComplete);
        }

        // TODO Synapse: Cannot find data type 'text'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureServer))]
        public static void TestAutoEnlistment_TxScopeNonComplete()
        {
            RunTestSet(TestCase_AutoEnlistment_TxScopeNonComplete);
        }

        // TODO Synapse: Cannot find data type 'text'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestManualEnlistment_Enlist()
        {
            RunTestSet(TestCase_ManualEnlistment_Enlist);
        }

        // TODO Synapse: Cannot find data type 'text'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestManualEnlistment_NonEnlist()
        {
            RunTestSet(TestCase_ManualEnlistment_NonEnlist);
        }

        // TODO Synapse: Cannot find data type 'text'.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void TestManualEnlistment_Enlist_TxScopeComplete()
        {
            RunTestSet(TestCase_ManualEnlistment_Enlist_TxScopeComplete);
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsSupportingDistributedTransactions))]
        public static void TestEnlistmentPrepare_TxScopeComplete()
        {
            Assert.Throws<TransactionAbortedException>(() =>
            {
                using TransactionScope txScope = new(TransactionScopeOption.RequiresNew, new TransactionOptions()
                {
                    IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
                    Timeout = TransactionManager.DefaultTimeout
                }, TransactionScopeAsyncFlowOption.Enabled);

                using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
                connection.Open();
                System.Transactions.Transaction.Current.EnlistDurable(EnlistmentForPrepare.s_id, new EnlistmentForPrepare(), EnlistmentOptions.None);
                txScope.Complete();
                Assert.Fail("Expected exception not thrown.");
            });
        }

        private static void TestCase_AutoEnlistment_TxScopeComplete()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConnectionString);
            builder.Enlist = true;
            ConnectionString = builder.ConnectionString;

            using (TransactionScope txScope = new TransactionScope(TransactionScopeOption.Required, TimeSpan.MaxValue))
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }
                }
                txScope.Complete();
            }

            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.True(result.Rows.Count == 1);
            Assert.True(string.Equals(result.Rows[0][0], InputCol2));
        }

        private static void TestCase_AutoEnlistment_TxScopeNonComplete()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConnectionString);
            builder.Enlist = true;
            ConnectionString = builder.ConnectionString;

            using (TransactionScope txScope = new TransactionScope(TransactionScopeOption.Required, TimeSpan.MaxValue))
            {
                using (SqlConnection connection = new SqlConnection(ConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }
                }
            }

            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.True(result.Rows.Count == 0);
        }

        private static void TestCase_ManualEnlistment_Enlist()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConnectionString);
            builder.Enlist = false;
            ConnectionString = builder.ConnectionString;

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (TransactionScope txScope = new TransactionScope())
                {
                    connection.EnlistTransaction(System.Transactions.Transaction.Current);
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }
                }
            }

            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.True(result.Rows.Count == 0);
        }

        private static void TestCase_ManualEnlistment_NonEnlist()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConnectionString);
            builder.Enlist = false;
            ConnectionString = builder.ConnectionString;

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (TransactionScope txScope = new TransactionScope())
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }
                }
            }

            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.True(result.Rows.Count == 1);
            Assert.True(string.Equals(result.Rows[0][0], InputCol2));
        }

        private static void TestCase_ManualEnlistment_Enlist_TxScopeComplete()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(ConnectionString);
            builder.Enlist = false;
            ConnectionString = builder.ConnectionString;

            using (SqlConnection connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (TransactionScope txScope = new TransactionScope())
                {
                    connection.EnlistTransaction(System.Transactions.Transaction.Current);
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = $"INSERT INTO {TestTableName} VALUES ({InputCol1}, '{InputCol2}')";
                        command.ExecuteNonQuery();
                    }
                    txScope.Complete();
                }
            }

            DataTable result = DataTestUtility.RunQuery(ConnectionString, $"select col2 from {TestTableName} where col1 = {InputCol1}");
            Assert.True(result.Rows.Count == 1);
            Assert.True(string.Equals(result.Rows[0][0], InputCol2));
        }

        class EnlistmentForPrepare : IEnlistmentNotification
        {
            public static readonly Guid s_id = Guid.NewGuid();
            // fail during prepare, this will cause scope.Complete to throw
            public void Prepare(PreparingEnlistment preparingEnlistment) => preparingEnlistment.ForceRollback();
            public void Commit(Enlistment enlistment) => enlistment.Done();
            public void Rollback(Enlistment enlistment) => enlistment.Done();
            public void InDoubt(Enlistment enlistment) => enlistment.Done();
        }

        private static string TestTableName;
        private static string ConnectionString;
        private const int InputCol1 = 1;
        private const string InputCol2 = "One";

        private static void RunTestSet(Action TestCase)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString);
            builder.ConnectTimeout = 5;

            builder.Pooling = true;
            ConnectionString = builder.ConnectionString;

            RunTestFormat(TestCase);

            builder.Pooling = false;
            ConnectionString = builder.ConnectionString;

            RunTestFormat(TestCase);
        }

        private static void RunTestFormat(Action testCase)
        {
            TestTableName = DataTestUtility.GenerateObjectName();
            DataTestUtility.RunNonQuery(ConnectionString, $"create table {TestTableName} (col1 int, col2 text)");
            try
            {
                testCase();
            }
            finally
            {
                DataTestUtility.RunNonQuery(ConnectionString, $"drop table {TestTableName}");
            }
        }
    }
}
