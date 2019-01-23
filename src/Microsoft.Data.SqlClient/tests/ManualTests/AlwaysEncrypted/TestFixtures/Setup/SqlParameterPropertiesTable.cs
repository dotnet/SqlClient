// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.AlwaysEncrypted.Setup
{
    public class SqlParameterPropertiesTable : Table
    {
        public SqlParameterPropertiesTable(string tableName) : base(tableName)
        {
        }

        public override void Create(SqlConnection sqlConnection)
        {
            string sql =
                $@"CREATE TABLE [dbo].[{Name}]
                (
                    [firstColumn] [nchar](100) NULL,
                    [secondColumn] [decimal](10, 4) NULL,
                    [thirdColumn] [time](5) NULL
                )";

            using (SqlCommand command = sqlConnection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
