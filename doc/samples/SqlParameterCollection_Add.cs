using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

public class Sample
{
    public void AddSqlParameter(SqlCommand command)
    {
        command.Parameters.Add(new SqlParameter("Description", "Beverages"));
    }
}
// </Snippet1>