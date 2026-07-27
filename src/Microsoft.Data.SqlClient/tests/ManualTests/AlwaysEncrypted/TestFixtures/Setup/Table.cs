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
            string sql = $"DROP TABLE [{Name}];";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
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
