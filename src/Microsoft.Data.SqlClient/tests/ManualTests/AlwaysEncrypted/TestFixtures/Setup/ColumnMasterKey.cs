// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public abstract class ColumnMasterKey : DbObject
    {
        protected ColumnMasterKey(string name) : base(name)
        {
        }

        protected string KeyStoreProviderName { get; set; }
        protected string CmkSignStr { get; set; }
        public abstract string KeyPath { get; }

        public override void Create(SqlConnection sqlConnection)
        {
            string sql;
            if (DataTestUtility.EnclaveEnabled && !string.IsNullOrEmpty(CmkSignStr))
            {
                sql =
                    $@"CREATE COLUMN MASTER KEY [{Name}]
                     WITH (
                        KEY_STORE_PROVIDER_NAME = N'{KeyStoreProviderName}',
                        KEY_PATH = N'{KeyPath}',
                        ENCLAVE_COMPUTATIONS (SIGNATURE = {CmkSignStr})
                    );";
            }
            else
            {
                sql =
                  $@"CREATE COLUMN MASTER KEY [{Name}]
                    WITH (
                        KEY_STORE_PROVIDER_NAME = N'{KeyStoreProviderName}',
                        KEY_PATH = N'{KeyPath}'
                    );";
            }

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                if (!string.IsNullOrEmpty(sql))
                {
                    command.CommandText = sql;
                    command.CommandTimeout = 60;
                    command.ExecuteNonQuery();
                }
            }
        }

        public override void Drop(SqlConnection sqlConnection)
        {
            // NOTE: The drop is guarded so that cleanup is idempotent. An unguarded DROP throws when
            //   the key was never created (for example when setup failed part way through), which
            //   would abort the enclosing drop loop and leak every remaining object.
            string sql = $"IF EXISTS (SELECT 1 FROM sys.column_master_keys WHERE name = @name) DROP COLUMN MASTER KEY [{Name}];";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.CommandTimeout = 60;
                command.Parameters.AddWithValue("@name", Name);
                command.ExecuteNonQuery();
            }
        }
    }
}
