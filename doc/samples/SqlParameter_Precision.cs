using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

class Program
{
    private static void AddSqlParameter(SqlCommand command)
    {
        SqlParameter parameter = new SqlParameter("@Price", SqlDbType.Decimal);
        parameter.Value = 3.1416;
        parameter.Precision = 8;
        parameter.Scale = 4;

        command.Parameters.Add(parameter);
    }
}
// </Snippet1>