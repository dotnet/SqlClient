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
            // NOTE: Both the lookup and the DROP are schema-qualified to [dbo] to match the CREATE
            //   TABLE statements in the derived classes. An unqualified name resolves against the
            //   connection's default schema, so if that is not dbo the guard would return NULL and
            //   silently skip the drop, leaking the table.
            // NOTE: T-SQL cannot parameterize an identifier, so the name is parameterized in the
            //   guard - where it is compared as a string - but must be interpolated into the DROP
            //   itself. The interpolated identifier is bracket-quoted, and the value only ever
            //   comes from the test's own generated name.
            string sql = $"IF (OBJECT_ID(@name) IS NOT NULL) DROP TABLE [dbo].[{Name}];";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.Parameters.AddWithValue("@name", $"[dbo].[{Name}]");
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
