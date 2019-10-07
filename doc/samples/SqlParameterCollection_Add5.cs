using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

public class Sample
{
    public void AddSqlParameter(SqlCommand cmd)
    {
        SqlParameter p1 = cmd.Parameters.Add("@Description", SqlDbType.NVarChar, 16, "Description");
    }
}
// </Snippet1>