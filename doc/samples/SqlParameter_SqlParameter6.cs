using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

class Program
{
    private static void AddSqlParameter(SqlCommand command)
    {
        SqlParameter parameter = new SqlParameter("@Description",
            SqlDbType.VarChar, 88, "Description");
        parameter.IsNullable = true;
        parameter.Direction = ParameterDirection.Output;

        command.Parameters.Add(parameter);
    }
}
// </Snippet1>