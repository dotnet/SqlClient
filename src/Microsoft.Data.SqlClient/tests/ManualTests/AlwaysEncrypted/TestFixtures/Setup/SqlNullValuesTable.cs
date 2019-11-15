// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public class SqlNullValuesTable : Table
    {
        private const string ColumnEncryptionAlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA_256";
        private ColumnEncryptionKey columnEncryptionKey;

        public SqlNullValuesTable(string tableName, ColumnEncryptionKey columnEncryptionKey) : base(tableName)
        {
            this.columnEncryptionKey = columnEncryptionKey;
        }

        public override void Create(SqlConnection sqlConnection)
        {
            string encryptionType = "RANDOMIZED"; // RANDOMIZED
            string sql =
                $@"CREATE TABLE [dbo].[{Name}]
                (
                    [c1] [int] ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = [{columnEncryptionKey.Name}], ENCRYPTION_TYPE = {encryptionType}, ALGORITHM = '{ColumnEncryptionAlgorithmName}'),
                    [c2] [int] IDENTITY(1,1)
                )";
            
            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
