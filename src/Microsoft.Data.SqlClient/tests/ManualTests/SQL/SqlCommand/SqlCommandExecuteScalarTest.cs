// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for ExecuteScalar to verify proper exception handling.
    /// </summary>
    public static class SqlCommandExecuteScalarTest
    {

        private static string GenerateTableName(string prefix) =>
            $"##{prefix}_{Guid.NewGuid():N}";

        /// <summary>
        /// Regression test for GitHub issue #3736: https://github.com/dotnet/SqlClient/issues/3736
        /// ExecuteScalar should properly propagate conversion errors that occur after the first result.
        /// 
        /// Without the fix, the conversion error is swallowed during reader.Close(), causing:
        /// 1. The transaction to become "zombied" without throwing an exception
        /// 2. Subsequent commands to execute on a dead transaction
        /// 3. Partial commits when the transaction commit fails
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_ShouldThrowOnConversionError_GH3736()
        {
            string tab1Name = GenerateTableName("tab1");
            string tab2Name = GenerateTableName("tab2");

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            // Setup: Create test tables (temp tables auto-cleanup)
            using (SqlCommand setupCmd = connection.CreateCommand())
            {
                setupCmd.CommandText = $@"
                    CREATE TABLE {tab1Name} (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [Val] VARCHAR(10) NOT NULL
                    );

                    CREATE TABLE {tab2Name} (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [Val1] INT NOT NULL,
                        [Val2] INT NOT NULL
                    );

                    INSERT INTO {tab1Name} (Val) VALUES ('12345');
                    INSERT INTO {tab1Name} (Val) VALUES ('42-43');"; // This will cause conversion error
                setupCmd.ExecuteNonQuery();
            }

            // Test: Execute a query that will cause a conversion error after returning a valid row
            // The query "SELECT Id FROM tab1 WHERE Val = 12345" will:
            // 1. Return row with Id=1 (Val='12345' converts to 12345)
            // 2. Fail on row with Id=2 (Val='42-43' cannot convert to int)
            // 
            // Before the fix: ExecuteScalar returns 1 without throwing an exception
            // After the fix: ExecuteScalar throws SqlException with the conversion error
            using SqlCommand cmd = new($"SELECT Id FROM {tab1Name} WHERE Val = 12345", connection);
            
            SqlException ex = Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
            
            // Error 245: Conversion failed when converting the varchar value '42-43' to data type int.
            Assert.Equal(245, ex.Number);
            Assert.Contains("Conversion failed", ex.Message);
        }

        /// <summary>
        /// Regression test for GitHub issue #3736:
        /// Verifies the transaction scenario from the original issue report.
        /// 
        /// In the original bug, a transaction would be zombied without notification, causing:
        /// - INSERT before the error to be rolled back (correct)
        /// - INSERT after the error to be committed outside transaction (incorrect)
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_TransactionShouldRollbackOnError_GH3736()
        {
            string tab1Name = GenerateTableName("tab1");
            string tab2Name = GenerateTableName("tab2");

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            // Setup: Create test tables (temp tables auto-cleanup)
            using (SqlCommand setupCmd = connection.CreateCommand())
            {
                setupCmd.CommandText = $@"
                    CREATE TABLE {tab1Name} (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [Val] VARCHAR(10) NOT NULL
                    );

                    CREATE TABLE {tab2Name} (
                        [Id] INT IDENTITY(1,1) NOT NULL,
                        [Val1] INT NOT NULL,
                        [Val2] INT NOT NULL
                    );

                    INSERT INTO {tab1Name} (Val) VALUES ('12345');
                    INSERT INTO {tab1Name} (Val) VALUES ('42-43');";
                setupCmd.ExecuteNonQuery();
            }

            // Test: Execute queries in a transaction where one will cause an error
            bool exceptionThrown = false;
            try
            {
                using SqlTransaction transaction = connection.BeginTransaction();

                // First insert - should be rolled back if transaction fails
                using (SqlCommand cmd1 = new($"INSERT INTO {tab2Name} (Val1, Val2) VALUES (42, 43)", connection, transaction))
                {
                    cmd1.ExecuteNonQuery();
                }

                // This should throw due to conversion error
                using (SqlCommand cmd2 = new($"SELECT Id FROM {tab1Name} WHERE Val = 12345", connection, transaction))
                {
                    cmd2.ExecuteScalar();
                }

                // This should never execute if the fix is working
                using (SqlCommand cmd3 = new($"INSERT INTO {tab2Name} (Val1, Val2) VALUES (100, 200)", connection, transaction))
                {
                    cmd3.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch (SqlException ex) when (ex.Number == 245)
            {
                // Expected: Conversion error should be thrown
                exceptionThrown = true;
            }

            Assert.True(exceptionThrown, "Expected SqlException with conversion error (245) was not thrown");

            // Verify: No rows should be in tab2 (transaction should have been rolled back)
            using (SqlCommand verifyCmd = new($"SELECT COUNT(*) FROM {tab2Name}", connection))
            {
                int count = (int)verifyCmd.ExecuteScalar();
                Assert.Equal(0, count);
            }
        }

        /// <summary>
        /// Verifies that ExecuteScalar works correctly when there is no error.
        /// This is a sanity check to ensure the fix doesn't break normal functionality.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_ShouldWorkCorrectlyWithoutError()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand cmd = new("SELECT 42", connection);
            object result = cmd.ExecuteScalar();

            Assert.Equal(42, result);
        }

        /// <summary>
        /// Verifies that ExecuteScalar returns null when there are no rows.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_ShouldReturnNullWhenNoRows()
        {
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand cmd = new("SELECT 1 WHERE 1 = 0", connection);
            object result = cmd.ExecuteScalar();

            Assert.Null(result);
        }
    }
}
