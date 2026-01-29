// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// Tests for ExecuteScalar to verify proper exception handling.
    /// Regression tests for GitHub issue #3736: https://github.com/dotnet/SqlClient/issues/3736
    /// </summary>
    public static class SqlCommandExecuteScalarTest
    {
        /// <summary>
        /// ExecuteScalar should propagate conversion errors that occur after the first result.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_ShouldThrowOnConversionError()
        {
            string tableName = DataTestUtility.GetLongName("GH3736");

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                // Arrange
                // Insert valid VARCHAR values - '42-43' is a valid string, not an invalid number
                DataTestUtility.CreateTable(connection, tableName, "(Id INT IDENTITY(1,1) NOT NULL, Val VARCHAR(10) NOT NULL)");
                using (SqlCommand insertCmd = connection.CreateCommand())
                {
                    insertCmd.CommandText =
                        $"INSERT INTO {tableName} (Val) VALUES ('12345'); " +
                        $"INSERT INTO {tableName} (Val) VALUES ('42-43');";
                    insertCmd.ExecuteNonQuery();
                }

                // Act
                // The WHERE clause compares VARCHAR to INT (no quotes around 12345), causing SQL Server
                // to convert each Val to INT. '12345' converts fine, but '42-43' fails with error 245.
                using SqlCommand cmd = new($"SELECT Id FROM {tableName} WHERE Val = 12345", connection);
                SqlException ex = Assert.Throws<SqlException>(() => cmd.ExecuteScalar());

                // Assert
                Assert.Equal(245, ex.Number);
                Assert.Contains("Conversion failed", ex.Message);
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        /// <summary>
        /// ExecuteScalar should throw on conversion error so transaction can be properly rolled back.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_TransactionShouldRollbackOnError()
        {
            string sourceTable = DataTestUtility.GetLongName("GH3736_Src");
            string targetTable = DataTestUtility.GetLongName("GH3736_Tgt");

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            try
            {
                // Arrange
                // sourceTable.Val is VARCHAR - both '12345' and '42-43' are valid strings
                DataTestUtility.CreateTable(connection, sourceTable, "(Id INT IDENTITY(1,1) NOT NULL, Val VARCHAR(10) NOT NULL)");
                DataTestUtility.CreateTable(connection, targetTable, "(Id INT IDENTITY(1,1) NOT NULL, Val1 INT NOT NULL, Val2 INT NOT NULL)");
                using (SqlCommand insertCmd = connection.CreateCommand())
                {
                    insertCmd.CommandText =
                        $"INSERT INTO {sourceTable} (Val) VALUES ('12345'); " +
                        $"INSERT INTO {sourceTable} (Val) VALUES ('42-43');";
                    insertCmd.ExecuteNonQuery();
                }

                // Act
                // The conversion error occurs in cmd2's WHERE clause (Val = 12345 without quotes),
                // not during the INSERT statements above.
                SqlException ex = Assert.Throws<SqlException>(() =>
                {
                    using SqlTransaction transaction = connection.BeginTransaction();
                    using (SqlCommand cmd1 = new($"INSERT INTO {targetTable} (Val1, Val2) VALUES (42, 43)", connection, transaction))
                    {
                        cmd1.ExecuteNonQuery();
                    }
                    using (SqlCommand cmd2 = new($"SELECT Id FROM {sourceTable} WHERE Val = 12345", connection, transaction))
                    {
                        cmd2.ExecuteScalar();
                    }
                    using (SqlCommand cmd3 = new($"INSERT INTO {targetTable} (Val1, Val2) VALUES (100, 200)", connection, transaction))
                    {
                        cmd3.ExecuteNonQuery();
                    }
                    transaction.Commit();
                });

                // Assert
                Assert.Equal(245, ex.Number);
                using (SqlCommand verifyCmd = new($"SELECT COUNT(*) FROM {targetTable}", connection))
                {
                    int count = (int)verifyCmd.ExecuteScalar();
                    Assert.Equal(0, count);
                }
            }
            finally
            {
                DataTestUtility.DropTable(connection, sourceTable);
                DataTestUtility.DropTable(connection, targetTable);
            }
        }

        /// <summary>
        /// ExecuteScalar should work correctly when there is no error.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_ShouldWorkCorrectlyWithoutError()
        {
            // Arrange
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand cmd = new("SELECT 42", connection);

            // Act
            object result = cmd.ExecuteScalar();

            // Assert
            Assert.Equal(42, result);
        }

        /// <summary>
        /// ExecuteScalar should return null when there are no rows.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void ExecuteScalar_ShouldReturnNullWhenNoRows()
        {
            // Arrange
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();
            using SqlCommand cmd = new("SELECT 1 WHERE 1 = 0", connection);

            // Act
            object result = cmd.ExecuteScalar();

            // Assert
            Assert.Null(result);
        }

        /// <summary>
        /// ExecuteScalarAsync should propagate conversion errors that occur after the first result.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteScalarAsync_ShouldThrowOnConversionError()
        {
            string tableName = DataTestUtility.GetLongName("GH3736_Async");

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            try
            {
                // Arrange
                DataTestUtility.CreateTable(connection, tableName, "(Id INT IDENTITY(1,1) NOT NULL, Val VARCHAR(10) NOT NULL)");
                using (SqlCommand insertCmd = connection.CreateCommand())
                {
                    insertCmd.CommandText =
                        $"INSERT INTO {tableName} (Val) VALUES ('12345'); " +
                        $"INSERT INTO {tableName} (Val) VALUES ('42-43');";
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // Act
                using SqlCommand cmd = new($"SELECT Id FROM {tableName} WHERE Val = 12345", connection);
                SqlException ex = await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync());

                // Assert
                Assert.Equal(245, ex.Number);
                Assert.Contains("Conversion failed", ex.Message);
            }
            finally
            {
                DataTestUtility.DropTable(connection, tableName);
            }
        }

        /// <summary>
        /// ExecuteScalarAsync should throw on conversion error so transaction can be properly rolled back.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteScalarAsync_TransactionShouldRollbackOnError()
        {
            string sourceTable = DataTestUtility.GetLongName("GH3736_AsyncSrc");
            string targetTable = DataTestUtility.GetLongName("GH3736_AsyncTgt");

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();

            try
            {
                // Arrange
                // sourceTable.Val is VARCHAR - both '12345' and '42-43' are valid strings
                DataTestUtility.CreateTable(connection, sourceTable, "(Id INT IDENTITY(1,1) NOT NULL, Val VARCHAR(10) NOT NULL)");
                DataTestUtility.CreateTable(connection, targetTable, "(Id INT IDENTITY(1,1) NOT NULL, Val1 INT NOT NULL, Val2 INT NOT NULL)");
                using (SqlCommand insertCmd = connection.CreateCommand())
                {
                    insertCmd.CommandText =
                        $"INSERT INTO {sourceTable} (Val) VALUES ('12345'); " +
                        $"INSERT INTO {sourceTable} (Val) VALUES ('42-43');";
                    await insertCmd.ExecuteNonQueryAsync();
                }

                // Act
                // The conversion error occurs in cmd2's WHERE clause (Val = 12345 without quotes),
                // not during the INSERT statements above.
                SqlException ex = await Assert.ThrowsAsync<SqlException>(async () =>
                {
                    using SqlTransaction transaction = connection.BeginTransaction();
                    using (SqlCommand cmd1 = new($"INSERT INTO {targetTable} (Val1, Val2) VALUES (42, 43)", connection, transaction))
                    {
                        await cmd1.ExecuteNonQueryAsync();
                    }
                    using (SqlCommand cmd2 = new($"SELECT Id FROM {sourceTable} WHERE Val = 12345", connection, transaction))
                    {
                        await cmd2.ExecuteScalarAsync();
                    }
                    using (SqlCommand cmd3 = new($"INSERT INTO {targetTable} (Val1, Val2) VALUES (100, 200)", connection, transaction))
                    {
                        await cmd3.ExecuteNonQueryAsync();
                    }
                    transaction.Commit();
                });

                // Assert
                Assert.Equal(245, ex.Number);
                using (SqlCommand verifyCmd = new($"SELECT COUNT(*) FROM {targetTable}", connection))
                {
                    int count = (int)await verifyCmd.ExecuteScalarAsync();
                    Assert.Equal(0, count);
                }
            }
            finally
            {
                DataTestUtility.DropTable(connection, sourceTable);
                DataTestUtility.DropTable(connection, targetTable);
            }
        }

        /// <summary>
        /// ExecuteScalarAsync should work correctly when there is no error.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteScalarAsync_ShouldWorkCorrectlyWithoutError()
        {
            // Arrange
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using SqlCommand cmd = new("SELECT 42", connection);

            // Act
            object result = await cmd.ExecuteScalarAsync();

            // Assert
            Assert.Equal(42, result);
        }

        /// <summary>
        /// ExecuteScalarAsync should return null when there are no rows.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static async Task ExecuteScalarAsync_ShouldReturnNullWhenNoRows()
        {
            // Arrange
            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            await connection.OpenAsync();
            using SqlCommand cmd = new("SELECT 1 WHERE 1 = 0", connection);

            // Act
            object result = await cmd.ExecuteScalarAsync();

            // Assert
            Assert.Null(result);
        }
    }
}
