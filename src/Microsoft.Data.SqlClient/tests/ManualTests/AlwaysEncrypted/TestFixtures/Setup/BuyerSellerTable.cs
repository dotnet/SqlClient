// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public class BuyerSellerTable : Table
    {
        private const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";
        private readonly ColumnEncryptionKey _columnEncryptionKey1;
        private readonly ColumnEncryptionKey _columnEncryptionKey2;

        // ✅ ADD: Unique stored procedure names based on table name
        public string InsertProcedureName => $"InsertBuyerSeller_{Name}";
        public string UpdateProcedureName => $"UpdateBuyerSeller_{Name}";

        public BuyerSellerTable(string tableName, ColumnEncryptionKey columnEncryptionKey1, ColumnEncryptionKey columnEncryptionKey2)
            : base(tableName)
        {
            _columnEncryptionKey1 = columnEncryptionKey1;
            _columnEncryptionKey2 = columnEncryptionKey2;
        }

        public override void Create(SqlConnection sqlConnection)
        {
            // Create the table with encrypted columns
            string createTableSql = $@"
                CREATE TABLE [dbo].[{Name}]
                (
                    [BuyerSellerID] [int] NOT NULL PRIMARY KEY,
                    [SSN1] [varchar](255) COLLATE Latin1_General_BIN2 ENCRYPTED WITH (
                        COLUMN_ENCRYPTION_KEY = [{_columnEncryptionKey1.Name}], 
                        ENCRYPTION_TYPE = DETERMINISTIC, 
                        ALGORITHM = '{ColumnEncryptionAlgorithmName}'
                    ),
                    [SSN2] [varchar](255) COLLATE Latin1_General_BIN2 ENCRYPTED WITH (
                        COLUMN_ENCRYPTION_KEY = [{_columnEncryptionKey2.Name}], 
                        ENCRYPTION_TYPE = DETERMINISTIC, 
                        ALGORITHM = '{ColumnEncryptionAlgorithmName}'
                    )
                )";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = createTableSql;
                command.ExecuteNonQuery();
            }

            // ✅ CHANGED: Use unique SP names
            string createInsertProcSql = $@"
                CREATE PROCEDURE [dbo].[{InsertProcedureName}]
                    @BuyerSellerID int,
                    @SSN1 varchar(255),
                    @SSN2 varchar(255)
                AS
                BEGIN
                    INSERT INTO [dbo].[{Name}] (BuyerSellerID, SSN1, SSN2)
                    VALUES (@BuyerSellerID, @SSN1, @SSN2)
                END";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = createInsertProcSql;
                command.ExecuteNonQuery();
            }

            // ✅ CHANGED: Use unique SP names
            string createUpdateProcSql = $@"
                CREATE PROCEDURE [dbo].[{UpdateProcedureName}]
                    @BuyerSellerID int,
                    @SSN1 varchar(255),
                    @SSN2 varchar(255)
                AS
                BEGIN
                    UPDATE [dbo].[{Name}]
                    SET SSN1 = @SSN1, SSN2 = @SSN2
                    WHERE BuyerSellerID = @BuyerSellerID
                END";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = createUpdateProcSql;
                command.ExecuteNonQuery();
            }
        }

        public override void Drop(SqlConnection sqlConnection)
        {
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = $"IF OBJECT_ID('[dbo].[{InsertProcedureName}]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[{InsertProcedureName}]";
                command.ExecuteNonQuery();
            }

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = $"IF OBJECT_ID('[dbo].[{UpdateProcedureName}]', 'P') IS NOT NULL DROP PROCEDURE [dbo].[{UpdateProcedureName}]";
                command.ExecuteNonQuery();
            }

            // Drop table
            base.Drop(sqlConnection);
        }
    }
}
