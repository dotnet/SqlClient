// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class SqlCommandExecuteScalarTest
    {
        /// <summary>
        /// Validates that ExecuteScalar propagates errors that occur after returning the first result.
        /// This tests the fix for GitHub issue #3736 where conversion errors during WHERE clause
        /// evaluation were being silently swallowed.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteScalar_PropagatesErrorAfterFirstResult_Sync()
        {
            string tableName = DataTestUtility.GetLongName("ES_ErrorProp");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                // Create table with varchar column that will cause conversion error
                using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tableName} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Val VARCHAR(50)
                    );
                    INSERT INTO {tableName} (Val) VALUES ('100'), ('42-43'), ('200');", connection))
                {
                    cmd.ExecuteNonQuery();
                }

                try
                {
                    // This query will return '100' first, then fail on '42-43' conversion
                    using (var cmd = new SqlCommand($"SELECT Id FROM {tableName} WHERE Val = 12345", connection))
                    {
                        Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                    }
                }
                finally
                {
                    // Cleanup
                    using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Validates that ExecuteScalarAsync propagates errors that occur after returning the first result.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task ExecuteScalar_PropagatesErrorAfterFirstResult_Async()
        {
            string tableName = DataTestUtility.GetLongName("ES_ErrorPropAsync");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                // Create table with varchar column that will cause conversion error
                using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tableName} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Val VARCHAR(50)
                    );
                    INSERT INTO {tableName} (Val) VALUES ('100'), ('42-43'), ('200');", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                try
                {
                    // This query will return '100' first, then fail on '42-43' conversion
                    using (var cmd = new SqlCommand($"SELECT Id FROM {tableName} WHERE Val = 12345", connection))
                    {
                        await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync());
                    }
                }
                finally
                {
                    // Cleanup
                    using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Validates that ExecuteScalar properly propagates errors within a transaction,
        /// preventing the "zombie transaction" issue where the transaction appears valid
        /// but has actually been rolled back by the server.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteScalar_PropagatesErrorWithTransaction_Sync()
        {
            string tableName = DataTestUtility.GetLongName("ES_TxnError");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                // Create table with varchar column that will cause conversion error
                using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tableName} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Val VARCHAR(50)
                    );
                    INSERT INTO {tableName} (Val) VALUES ('100'), ('42-43'), ('200');", connection))
                {
                    cmd.ExecuteNonQuery();
                }

                try
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        // This query will return '100' first, then fail on '42-43' conversion
                        using (var cmd = new SqlCommand($"SELECT Id FROM {tableName} WHERE Val = 12345", connection, transaction))
                        {
                            Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                        }

                        // Transaction should still be usable to rollback
                        transaction.Rollback();
                    }
                }
                finally
                {
                    // Cleanup
                    using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Validates that ExecuteScalarAsync properly propagates errors within a transaction.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task ExecuteScalar_PropagatesErrorWithTransaction_Async()
        {
            string tableName = DataTestUtility.GetLongName("ES_TxnErrorAsync");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                // Create table with varchar column that will cause conversion error
                using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tableName} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Val VARCHAR(50)
                    );
                    INSERT INTO {tableName} (Val) VALUES ('100'), ('42-43'), ('200');", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                try
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        // This query will return '100' first, then fail on '42-43' conversion
                        using (var cmd = new SqlCommand($"SELECT Id FROM {tableName} WHERE Val = 12345", connection, transaction))
                        {
                            await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync());
                        }

                        // Transaction should still be usable to rollback
                        transaction.Rollback();
                    }
                }
                finally
                {
                    // Cleanup
                    using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }

        /// <summary>
        /// Sanity test to ensure ExecuteScalar still works correctly for normal queries.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteScalar_ReturnsFirstValue_Sync()
        {
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                using (var cmd = new SqlCommand("SELECT 42", connection))
                {
                    object result = cmd.ExecuteScalar();
                    Assert.Equal(42, result);
                }
            }
        }

        /// <summary>
        /// Sanity test to ensure ExecuteScalarAsync still works correctly for normal queries.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task ExecuteScalar_ReturnsFirstValue_Async()
        {
            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new SqlCommand("SELECT 42", connection))
                {
                    object result = await cmd.ExecuteScalarAsync();
                    Assert.Equal(42, result);
                }
            }
        }

        /// <summary>
        /// Test ExecuteScalar with multiple result sets to ensure all are processed.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void ExecuteScalar_ProcessesMultipleResultSets_Sync()
        {
            string tableName = DataTestUtility.GetLongName("ES_MultiResult");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                connection.Open();

                // Create table with varchar column that will cause conversion error in second result set
                using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tableName} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Val VARCHAR(50)
                    );
                    INSERT INTO {tableName} (Val) VALUES ('100'), ('42-43'), ('200');", connection))
                {
                    cmd.ExecuteNonQuery();
                }

                try
                {
                    // First result set succeeds, second result set has conversion error
                    using (var cmd = new SqlCommand($@"
                        SELECT 1;
                        SELECT Id FROM {tableName} WHERE Val = 12345;", connection))
                    {
                        Assert.Throws<SqlException>(() => cmd.ExecuteScalar());
                    }
                }
                finally
                {
                    // Cleanup
                    using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Test ExecuteScalarAsync with multiple result sets to ensure all are processed.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public async Task ExecuteScalar_ProcessesMultipleResultSets_Async()
        {
            string tableName = DataTestUtility.GetLongName("ES_MultiResultAsync");

            using (var connection = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await connection.OpenAsync();

                // Create table with varchar column that will cause conversion error in second result set
                using (var cmd = new SqlCommand($@"
                    CREATE TABLE {tableName} (
                        Id INT IDENTITY(1,1) PRIMARY KEY,
                        Val VARCHAR(50)
                    );
                    INSERT INTO {tableName} (Val) VALUES ('100'), ('42-43'), ('200');", connection))
                {
                    await cmd.ExecuteNonQueryAsync();
                }

                try
                {
                    // First result set succeeds, second result set has conversion error
                    using (var cmd = new SqlCommand($@"
                        SELECT 1;
                        SELECT Id FROM {tableName} WHERE Val = 12345;", connection))
                    {
                        await Assert.ThrowsAsync<SqlException>(() => cmd.ExecuteScalarAsync());
                    }
                }
                finally
                {
                    // Cleanup
                    using (var cmd = new SqlCommand($"DROP TABLE IF EXISTS {tableName}", connection))
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }
}
