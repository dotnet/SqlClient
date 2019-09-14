using System;
using System.Data;
using Microsoft.Data.SqlClient;

public class Sample
{
    // <Snippet1>
    public void AddSqlParameter(SqlCommand command)
    {
        command.Parameters.Add(new SqlParameter("Description", "Beverages"));
    }
    // </Snippet1>
}
