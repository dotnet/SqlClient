// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public abstract class Table : DbObject
    {
        protected Table(string name) : base(name)
        {
        }

        public override void Drop(SqlConnection sqlConnection)
        {
            // NOTE: The drop is guarded so that cleanup is idempotent. An unguarded DROP throws when
            //   the object was never created (for example when setup failed part way through), which
            //   would abort the enclosing drop loop and leak every remaining object.
            string sql = $"IF (OBJECT_ID(@name) IS NOT NULL) DROP TABLE [{Name}];";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@name", $"[{Name}]");
                command.ExecuteNonQuery();
            }
        }

        public static void DeleteData(string tableName, SqlConnection sqlConnection)
        {
            string sql = $"DELETE FROM [{tableName}];";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
