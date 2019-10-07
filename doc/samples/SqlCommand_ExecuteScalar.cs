// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

public class Sample
{

    public void CreateSqlCommand(
        string queryString, SqlConnection connection)
    {
        SqlCommand command = new
            SqlCommand(queryString, connection);
        command.Connection.Open();
        command.ExecuteScalar();
        connection.Close();
    }
    // </Snippet1>

}
