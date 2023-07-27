// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
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
            string encryptionInfo = Name.Contains("Target") ? $@" ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{columnEncryptionKey.Name}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}')" : "";
            string c2ColumnType = string.Empty;

            if (Name.StartsWith("AE-TabIntSource-", StringComparison.Ordinal) || Name.StartsWith("AE-TabIntSourceDirect-", StringComparison.Ordinal) || Name.StartsWith("AE-TabIntTargetDirect-", StringComparison.Ordinal))
            {
                c2ColumnType = "int";

                if (Name.StartsWith("AE-TabIntSourceDirect-", StringComparison.Ordinal))
                {
                    c2ColumnType = $"int ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{columnEncryptionKey.Name}], ENCRYPTION_TYPE = RANDOMIZED, ALGORITHM = '{ColumnEncryptionAlgorithmName}')";
                }
            }

            if (Name.StartsWith("AE-TabTinyIntTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "tinyint";
            }

            if (Name.StartsWith("AE-TabDatetime2Source-", StringComparison.Ordinal))
            {
                c2ColumnType = "datetime2(6)";
            }

            if (Name.StartsWith("AE-TabDatetime2Target-", StringComparison.Ordinal))
            {
                c2ColumnType = "datetime2(2)";
            }

            if (Name.StartsWith("AE-TabDecimalSource-", StringComparison.Ordinal))
            {
                c2ColumnType = "decimal(10,4)";
            }

            if (Name.StartsWith("AE-TabDecimalTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "decimal(5,2)";
            }

            if (Name.StartsWith("AE-TabVarCharSmallSource-", StringComparison.Ordinal) || Name.StartsWith("AE-TabNVarCharSmallSource-", StringComparison.Ordinal))
            {
                c2ColumnType = "varchar(10)";
            }

            if (Name.StartsWith("AE-TabVarCharTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "varchar(2) COLLATE  Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabVarCharMaxSource-", StringComparison.Ordinal) || Name.StartsWith("AE-TabSmallCharMaxTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "varchar(max) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabVarCharMaxTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "varchar(7000)";
            }

            if (Name.StartsWith("AE-TabNVarCharSmallTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "nvarchar(2) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabNVarCharMaxSource-", StringComparison.Ordinal))
            {
                c2ColumnType = "nvarchar(max) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabNVarCharTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "nvarchar(4000) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabVarBinaryMaxSource-", StringComparison.Ordinal) || Name.StartsWith("AE-TabSmallBinaryMaxTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "varbinary(max)";
            }

            if (Name.StartsWith("AE-TabVarBinaryTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "varbinary(3000)";
            }

            if (Name.StartsWith("AE-TabBinaryMaxSource-", StringComparison.Ordinal))
            {
                c2ColumnType = "binary(7000)";
            }

            if (Name.StartsWith("AE-TabBinaryTarget-", StringComparison.Ordinal) || Name.StartsWith("AE-TabSmallBinarySource-", StringComparison.Ordinal))
            {
                c2ColumnType = "binary(3000)";
            }

            if (Name.StartsWith("AE-TabSmallBinaryTarget-", StringComparison.Ordinal))
            {
                c2ColumnType = "binary(8000)";
            }

            if (Name.StartsWith("AE-TabSmallCharSource-", StringComparison.Ordinal))
            {
                c2ColumnType = "char(8000) COLLATE Latin1_General_BIN2";
            }

            if (Name.StartsWith("AE-TabSmallCharTarget-", StringComparison.Ordinal))
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
