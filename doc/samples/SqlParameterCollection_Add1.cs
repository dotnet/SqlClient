using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

public class Sample
{
    public void AddSqlParameter(SqlCommand command)
    {
        SqlParameter param = new SqlParameter(
            "@Description", SqlDbType.NVarChar, 16);
        param.Value = "Beverages";
        command.Parameters.Add(param);
    }
}
// </Snippet1>