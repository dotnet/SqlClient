namespace SqlConnection_Pooling_Use_Statement;

using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connectionString = "Server=localhost;Database=master;Integrated Security=true;";
        // <Snippet1>
        // Assume that connectionString connects to master.  
        using (SqlConnection connection = new SqlConnection(connectionString))
        using (SqlCommand command = new SqlCommand())
        {
            connection.Open();
            command.Connection = connection;
            command.CommandText = "USE DatabaseName";
            command.ExecuteNonQuery();
        }
            // </Snippet1>
    }
}
