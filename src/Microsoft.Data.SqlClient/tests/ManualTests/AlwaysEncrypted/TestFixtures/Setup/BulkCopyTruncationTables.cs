// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.TestFixtures.Setup
{
    public class BulkCopyTruncationTables : Table
    {
        private const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";
        private ColumnEncryptionKey columnEncryptionKey;

        public BulkCopyTruncationTables(string tableName, ColumnEncryptionKey columnEncryptionKey1) : base(tableName)
        {
            this.columnEncryptionKey = columnEncryptionKey1;
        }

        public override void Create(SqlConnection sqlConnection)
        {
            string encryptionInfo = Name.Contains("Target") ? $@" ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{ columnEncryptionKey.Name}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}')" : "";
            string c2ColumnType = string.Empty;

            if (Name.StartsWith("AE-TabIntSource-") || Name.StartsWith("AE-TabIntSourceDirect-") || Name.StartsWith("AE-TabIntTargetDirect-"))
            {
                c2ColumnType = "int";

                if (Name.StartsWith("AE-TabIntSourceDirect-"))
                {
                    c2ColumnType = $"int ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{ columnEncryptionKey.Name}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}')";
                }
            }

            if (Name.StartsWith("AE-TabTinyIntTarget-"))
            {
                c2ColumnType = "tinyint";
            }

            if (Name.StartsWith("AE-TabDatetime2Source-"))
            {
                c2ColumnType = "datetime2(6)";
            }

            if (Name.StartsWith("AE-TabDatetime2Target-"))
            {
                c2ColumnType = "datetime2(2)";
            }

            if (Name.StartsWith("AE-TabDecimalSource-"))
            {
                c2ColumnType = "decimal(10,4)";
            }

            if (Name.StartsWith("AE-TabDecimalTarget-"))
            {
                c2ColumnType = "decimal(5,2)";
            }

            if (Name.StartsWith("AE-TabVarCharSmallSource-") || Name.StartsWith("AE-TabNVarCharSmallSource-"))
            {
                c2ColumnType = "varchar(10)";
            }

            if (Name.StartsWith("AE-TabVarCharTarget-"))
            {
                c2ColumnType = "varchar(2) COLLATE  Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabVarCharMaxSource-") || Name.StartsWith("AE-TabSmallCharMaxTarget-"))
            {
                c2ColumnType = "varchar(max) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabVarCharMaxTarget-"))
            {
                c2ColumnType = "varchar(7000)";
            }

            if (Name.StartsWith("AE-TabNVarCharSmallTarget-"))
            {
                c2ColumnType = "nvarchar(2) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabNVarCharMaxSource-"))
            {
                c2ColumnType = "nvarchar(max) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabNVarCharTarget-"))
            {
                c2ColumnType = "nvarchar(4000) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabVarBinaryMaxSource-") || Name.StartsWith("AE-TabSmallBinaryMaxTarget-"))
            {
                c2ColumnType = "varbinary(max)";
            }

            if (Name.StartsWith("AE-TabVarBinaryTarget-"))
            {
                c2ColumnType = "varbinary(3000)";
            }

            if (Name.StartsWith("AE-TabBinaryMaxSource-"))
            {
                c2ColumnType = "binary(7000)";
            }

            if (Name.StartsWith("AE-TabBinaryTarget-") || Name.StartsWith("AE-TabSmallBinarySource-"))
            {
                c2ColumnType = "binary(3000)";
            }

            if (Name.StartsWith("AE-TabSmallBinaryTarget-"))
            {
                c2ColumnType = "binary(8000)";
            }

            if (Name.StartsWith("AE-TabSmallCharSource-"))
            {
                c2ColumnType = "char(8000) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabSmallCharTarget-"))
            {
                c2ColumnType = "char(3000) COLLATE Latin1_General_BIN2";
            }

            string sql = $@"CREATE TABLE [dbo].[{Name}] ([c1] [int]  PRIMARY KEY, [c2] {c2ColumnType} {encryptionInfo});";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
