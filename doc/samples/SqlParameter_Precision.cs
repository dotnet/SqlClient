namespace SqlParameter_Precision;

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

// <Snippet2>
class ZeroParameterExample
{
    private static void AddZeroSqlParameter(SqlCommand command)
    {
        SqlParameter parameter = new SqlParameter("@Factor", SqlDbType.Decimal)
        {
            Value = 0m,
            Precision = 3,
            Scale = 3
        };

        command.Parameters.Add(parameter);
    }
}
// </Snippet2>
