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
        private readonly string _tableName;
        private readonly BuyerSellerTable _buyerSellerTable;

        public SqlDataAdapterBatchUpdateTests(SQLSetupStrategyCertStoreProvider context)
        {
            _fixture = context;
            _buyerSellerTable = _fixture.BuyerSellerTable as BuyerSellerTable;
            _tableName = _fixture.BuyerSellerTable.Name;
        }

        // ---------- TESTS ----------

        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.IsTargetReadyForAeWithKeyStore))]
        [ClassData(typeof(AEConnectionStringProvider))]
        public async Task AdapterUpdate_BatchSizeGreaterThanOne_Succeeds(string connectionString)
        {
            // Arrange
            TruncateTable(connectionString);
            int idBase = GetUniqueIdBase();
            PopulateTable(new (int id, string s1, string s2)[] {
                (idBase + 10, "123-45-6789", "987-65-4321"),
                (idBase + 20, "234-56-7890", "876-54-3210"),
                (idBase + 30, "345-67-8901", "765-43-2109"),
                (idBase + 40, "456-78-9012", "654-32-1098"),
            }, connectionString);

            using var conn = new SqlConnection(GetConnectionString(connectionString, encryptionEnabled: true));
            await conn.OpenAsync();

            using var adapter = CreateAdapter(conn, updateBatchSize: 10);
            var dataTable = BuildBuyerSellerDataTable();
            LoadCurrentRowsIntoDataTable(dataTable, conn);

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
            TruncateTable(connectionString);
            int idBase = GetUniqueIdBase();
            PopulateTable(new (int id, string s1, string s2)[] {
                (idBase + 100, "123-45-6789", "987-65-4321"),
                (idBase + 200, "234-56-7890", "876-54-3210"),
                (idBase + 300, "345-67-8901", "765-43-2109"),
                (idBase + 400, "456-78-9012", "654-32-1098"),
            }, connectionString);

            using var conn = new SqlConnection(GetConnectionString(connectionString, encryptionEnabled: true));
            await conn.OpenAsync();

            using var adapter = CreateAdapter(conn, updateBatchSize: 1); // success path
            var dataTable = BuildBuyerSellerDataTable();
            LoadCurrentRowsIntoDataTable(dataTable, conn);

            MutateForUpdate(dataTable);

            // Act
            var updatedRows = await Task.Run(() => adapter.Update(dataTable));

            // Assert
            Assert.Equal(dataTable.Rows.Count, updatedRows);
        }

        // ---------- HELPERS ----------

        private int GetUniqueIdBase() => Math.Abs(Guid.NewGuid().GetHashCode()) % 1000000;

        private SqlDataAdapter CreateAdapter(SqlConnection connection, int updateBatchSize)
        {
            var insertCmd = new SqlCommand(_buyerSellerTable.InsertProcedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            insertCmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@BuyerSellerID", SqlDbType.Int) { SourceColumn = "BuyerSellerID" },
                new SqlParameter("@SSN1", SqlDbType.VarChar, 255) { SourceColumn = "SSN1" },
                new SqlParameter("@SSN2", SqlDbType.VarChar, 255) { SourceColumn = "SSN2" },
            });
            insertCmd.UpdatedRowSource = UpdateRowSource.None;

            var updateCmd = new SqlCommand(_buyerSellerTable.UpdateProcedureName, connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            updateCmd.Parameters.AddRange(new[]
            {
                new SqlParameter("@BuyerSellerID", SqlDbType.Int) { SourceColumn = "BuyerSellerID" },
                new SqlParameter("@SSN1", SqlDbType.VarChar, 255) { SourceColumn = "SSN1" },
                new SqlParameter("@SSN2", SqlDbType.VarChar, 255) { SourceColumn = "SSN2" },
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
            var dt = new DataTable(_tableName);
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
            using var cmd = new SqlCommand($"SELECT BuyerSellerID, SSN1, SSN2 FROM [dbo].[{_tableName}] ORDER BY BuyerSellerID", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                dt.Rows.Add(reader.GetInt32(0), reader.GetString(1), reader.GetString(2));
            }
        }

        private void MutateForUpdate(DataTable dt)
        {
            int i = 0;
            var fixedTime = new DateTime(2023, 01, 01, 12, 34, 56);
            string timeStr = fixedTime.ToString("HHmm");
            foreach (DataRow row in dt.Rows)
            {
                i++;
                row["SSN1"] = $"{i:000}-11-{timeStr}";
                row["SSN2"] = $"{i:000}-22-{timeStr}";
            }
        }

        private void TruncateTable(string connectionString)
        {
            using var connection = new SqlConnection(GetConnectionString(connectionString, encryptionEnabled: true));
            connection.Open();
            ExecuteQuery(connection, $"DELETE FROM [dbo].[{_tableName}]");
        }

        private void ExecuteQuery(SqlConnection connection, string commandText)
        {
            using var cmd = new SqlCommand(
                commandText,
                connection: connection,
                transaction: null,
                columnEncryptionSetting: SqlCommandColumnEncryptionSetting.Enabled);
            cmd.ExecuteNonQuery();
        }

        private void PopulateTable((int id, string s1, string s2)[] rows, string connectionString)
        {
            using var connection = new SqlConnection(GetConnectionString(connectionString, encryptionEnabled: true));
            connection.Open();

            foreach (var (id, s1, s2) in rows)
            {
                using var cmd = new SqlCommand(
                    $"INSERT INTO [dbo].[{_tableName}] (BuyerSellerID, SSN1, SSN2) VALUES (@id, @s1, @s2)",
                    connection,
                    null,
                    SqlCommandColumnEncryptionSetting.Enabled);

                cmd.Parameters.Add(new SqlParameter("@id", SqlDbType.Int) { Value = id });
                cmd.Parameters.Add(new SqlParameter("@s1", SqlDbType.VarChar, 255) { Value = s1 });
                cmd.Parameters.Add(new SqlParameter("@s2", SqlDbType.VarChar, 255) { Value = s2 });

                cmd.ExecuteNonQuery();
            }
        }

        private string GetConnectionString(string baseConnectionString, bool encryptionEnabled)
        {
            var builder = new SqlConnectionStringBuilder(baseConnectionString)
            {
                ColumnEncryptionSetting = encryptionEnabled
                    ? SqlConnectionColumnEncryptionSetting.Enabled
                    : SqlConnectionColumnEncryptionSetting.Disabled
            };
            return builder.ToString();
        }

        private void SilentRunCommand(string commandText, SqlConnection connection)
        {
            try
            {
                ExecuteQuery(connection, commandText);
            }
            catch (SqlException ex)
            {
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
            }
        }

        public void Dispose()
        {
            foreach (string connectionString in DataTestUtility.AEConnStringsSetup)
            {
                using var connection = new SqlConnection(GetConnectionString(connectionString, encryptionEnabled: true));
                connection.Open();
                ExecuteQuery(connection, $"DELETE FROM [dbo].[{_tableName}]");
            }
        }
    }
}
