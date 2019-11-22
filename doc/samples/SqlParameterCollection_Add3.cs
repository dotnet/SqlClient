using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

public class Sample
{
    public void AddSqlParameter(SqlCommand command)
    {
        SqlParameter param = command.Parameters.Add(
            "@Description", SqlDbType.NVarChar);
        param.Size = 16;
        param.Value = "Beverages";
    }
}
// </Snippet1>