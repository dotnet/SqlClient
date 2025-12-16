// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted
{
    public sealed class SqlDataAdapterBatchUpdateTests : IClassFixture<SQLSetupStrategyCertStoreProvider>, IDisposable
    {
        private readonly SQLSetupStrategy _fixture;
        private readonly Dictionary<string, string> tableNames = new();

        public SqlDataAdapterBatchUpdateTests(SQLSetupStrategyCertStoreProvider context)
        {
            _fixture = context;

            // Provide table names to mirror repo patterns.
            // If your fixture already exposes specific names for BuyerSeller and procs, wire them here.
            // Otherwise use literal names as below.
            tableNames["BuyerSeller"] = "BuyerSeller";
            tableNames["ProcInsertBuyerSeller"] = "InsertBuyerSeller";
            tableNames["ProcUpdateBuyerSeller"] = "UpdateBuyerSeller";
        }

        // ---------- TESTS ----------

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public async Task AdapterUpdate_BatchSizeGreaterThanOne_Succeeds(string connectionString)
        {
            // Arrange
            // Ensure baseline rows exist
            EnsureBuyerSellerObjectsExist(connectionString);
            TruncateTables("BuyerSeller", connectionString);
            PopulateTable("BuyerSeller", new (int id, string s1, string s2)[] {
                (1, "123-45-6789", "987-65-4321"),
                (2, "234-56-7890", "876-54-3210"),
                (3, "345-67-8901", "765-43-2109"),
                (4, "456-78-9012", "654-32-1098"),
            }, connectionString);

            using var conn = new SqlConnection(GetOpenConnectionString(connectionString, encryptionEnabled: true));
            await conn.OpenAsync();

            using var adapter = CreateAdapter(conn, updateBatchSize: 10); // failure repro: > 1
            var dataTable = BuildBuyerSellerDataTable();
            LoadCurrentRowsIntoDataTable(dataTable, conn);

            // Mutate values for update
            MutateForUpdate(dataTable);

            // Act - With batch updates (UpdateBatchSize > 1), this previously threw NullReferenceException due to null systemParams in batch RPC mode
            var updated = await Task.Run(() => adapter.Update(dataTable));

            // Assert
            Assert.Equal(dataTable.Rows.Count, updated);

        }

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public async Task AdapterUpdate_BatchSizeOne_Succeeds(string connectionString)
        {
            // Arrange
            EnsureBuyerSellerObjectsExist(connectionString);
            TruncateTables("BuyerSeller", connectionString);
            PopulateTable("BuyerSeller", new (int id, string s1, string s2)[] {
                (1, "123-45-6789", "987-65-4321"),
                (2, "234-56-7890", "876-54-3210"),
                (3, "345-67-8901", "765-43-2109"),
                (4, "456-78-9012", "654-32-1098"),
            }, connectionString);

            using var conn = new SqlConnection(GetOpenConnectionString(connectionString, encryptionEnabled: true));
            await conn.OpenAsync();

            using var adapter = CreateAdapter(conn, updateBatchSize: 1); // success path
            var dataTable = BuildBuyerSellerDataTable();
            LoadCurrentRowsIntoDataTable(dataTable, conn);

            MutateForUpdate(dataTable);

            // Act (should not throw)
            var updatedRows = await Task.Run(() => adapter.Update(dataTable));

            // Assert
            Assert.Equal(dataTable.Rows.Count, updatedRows);

        }

        // ---------- HELPERS ----------

        private SqlDataAdapter CreateAdapter(SqlConnection connection, int updateBatchSize)
        {
            // Insert
            var insertCmd = new SqlCommand(tableNames["ProcInsertBuyerSeller"], connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            insertCmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@BuyerSellerID", SqlDbType.Int)   { SourceColumn = "BuyerSellerID" },
                new SqlParameter("@SSN1",          SqlDbType.VarChar, 255) { SourceColumn = "SSN1" },
                new SqlParameter("@SSN2",          SqlDbType.VarChar, 255) { SourceColumn = "SSN2" },
            });
            insertCmd.UpdatedRowSource = UpdateRowSource.None;

            // Update
            var updateCmd = new SqlCommand(tableNames["ProcUpdateBuyerSeller"], connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            updateCmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@BuyerSellerID", SqlDbType.Int)   { SourceColumn = "BuyerSellerID" },
                new SqlParameter("@SSN1",          SqlDbType.VarChar, 255) { SourceColumn = "SSN1" },
                new SqlParameter("@SSN2",          SqlDbType.VarChar, 255) { SourceColumn = "SSN2" },
            });
            updateCmd.UpdatedRowSource = UpdateRowSource.None;

            return new SqlDataAdapter
            {
                InsertCommand = insertCmd,
                UpdateCommand = updateCmd,
                UpdateBatchSize = updateBatchSize
            };
        }

        private DataTable BuildBuyerSellerDataTable()
        {
            var dt = new DataTable(tableNames["BuyerSeller"]);
            dt.Columns.AddRange(new[]
            {
                new DataColumn("BuyerSellerID", typeof(int)),
                new DataColumn("SSN1", typeof(string)),
                new DataColumn("SSN2", typeof(string)),
            });
            dt.PrimaryKey = new[] { dt.Columns["BuyerSellerID"] };
            return dt;
        }

        private void LoadCurrentRowsIntoDataTable(DataTable dt, SqlConnection conn)
        {
            using var cmd = new SqlCommand($"SELECT BuyerSellerID, SSN1, SSN2 FROM [dbo].[{tableNames["BuyerSeller"]}] ORDER BY BuyerSellerID", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dt.Rows.Add(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
            }
        }

        private void MutateForUpdate(DataTable dt)
        {
            int i = 0;
            var fixedTime = new DateTime(2000, 01, 01, 12, 34, 56);
            string timeStr = fixedTime.ToString("HHmm");
            foreach (DataRow row in dt.Rows)
            {
                i++;
                row["SSN1"] = $"{i:000}-11-{timeStr}";
                row["SSN2"] = $"{i:000}-22-{timeStr}";
            }
        }

        internal void TruncateTables(string tableName, string connectionString)
        {
            using var connection = new SqlConnection(GetOpenConnectionString(connectionString, encryptionEnabled: true));
            connection.Open();
            SilentRunCommand($@"TRUNCATE TABLE [dbo].[{tableNames[tableName]}]", connection);
        }

        internal void ExecuteQuery(SqlConnection connection, string commandText)
        {
            // Mirror AE-enabled command execution style used in repo tests
            using var cmd = new SqlCommand(
                commandText,
                connection: connection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled);
            cmd.ExecuteNonQuery();
        }

        internal void PopulateTable(string tableName, (int id, string s1, string s2)[] rows, string connectionString)
        {
            using var connection = new SqlConnection(GetOpenConnectionString(connectionString, encryptionEnabled: true));
            connection.Open();

            foreach (var (id, s1, s2) in rows)
            {
                using var cmd = new SqlCommand(
                    $@"INSERT INTO [dbo].[{tableNames[tableName]}] (BuyerSellerID, SSN1, SSN2) VALUES (@id, @s1, @s2)",
                    connection,
                    null,
                    SqlCommandColumnEncryptionSetting.Enabled);

                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
                cmd.Parameters.Add(new SqlParameter("@s1", SqlDbType.VarChar, 255) { Value = s1 });
                cmd.Parameters.Add(new SqlParameter("@s2", SqlDbType.VarChar, 255) { Value = s2 });

                cmd.ExecuteNonQuery();
            }
        }

        public string GetOpenConnectionString(string baseConnectionString, bool encryptionEnabled)
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                // TrustServerCertificate can be set based on environment; mirror repo’s AE toggling idiom
                ColumnEncryptionSetting = encryptionEnabled
                    ? SqlConnectionColumnEncryptionSetting.Enabled
                    : SqlConnectionColumnEncryptionSetting.Disabled
            };
            return builder.ToString();
        }

        internal void SilentRunCommand(string commandText, SqlConnection connection)
        {
            try
            { ExecuteQuery(connection, commandText); }
            catch (SqlException ex)
            {
                // Only swallow "object does not exist" (error 208), log others
                bool onlyObjectNotExist = true;
                foreach (SqlError err in ex.Errors)
                {
                    if (err.Number != 208)
                    {
                        onlyObjectNotExist = false;
                        break;
                    }
                }
                if (!onlyObjectNotExist)
                {
                    Console.WriteLine($"SilentRunCommand: Unexpected SqlException during cleanup: {ex}");
                }
                // Swallow all exceptions, but log unexpected ones
            }
        }

        public void Dispose()
        {
            foreach (string connectionString in DataTestUtility.AEConnStringsSetup)
            {
                using var connection = new SqlConnection(GetOpenConnectionString(connectionString, encryptionEnabled: true));
                connection.Open();
                SilentRunCommand($"DELETE FROM [dbo].[{tableNames["BuyerSeller"]}]", connection);
            }
        }
        private void EnsureBuyerSellerObjectsExist(string connectionString)
        {
            using var connection = new SqlConnection(GetOpenConnectionString(connectionString, encryptionEnabled: true));
            connection.Open();

            // Create table if not exists
            SilentRunCommand(@"
        IF OBJECT_ID('dbo.BuyerSeller', 'U') IS NULL
        CREATE TABLE [dbo].[BuyerSeller] (
            [BuyerSellerID] INT PRIMARY KEY,
            [SSN1] VARCHAR(255),
            [SSN2] VARCHAR(255)
        )", connection);

            // Create Insert proc if not exists
            SilentRunCommand(@"
        IF OBJECT_ID('dbo.InsertBuyerSeller', 'P') IS NULL
        EXEC('CREATE PROCEDURE [dbo].[InsertBuyerSeller]
            @BuyerSellerID INT,
            @SSN1 VARCHAR(255),
            @SSN2 VARCHAR(255)
        AS
        INSERT INTO [dbo].[BuyerSeller] (BuyerSellerID, SSN1, SSN2)
        VALUES (@BuyerSellerID, @SSN1, @SSN2)')
    ", connection);

            // Create Update proc if not exists
            SilentRunCommand(@"
        IF OBJECT_ID('dbo.UpdateBuyerSeller', 'P') IS NULL
        EXEC('CREATE PROCEDURE [dbo].[UpdateBuyerSeller]
            @BuyerSellerID INT,
            @SSN1 VARCHAR(255),
            @SSN2 VARCHAR(255)
        AS
        UPDATE [dbo].[BuyerSeller]
        SET SSN1 = @SSN1, SSN2 = @SSN2
        WHERE BuyerSellerID = @BuyerSellerID')
    ", connection);

        }
    }
}
