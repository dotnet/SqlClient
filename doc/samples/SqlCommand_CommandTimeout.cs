// <Snippet1>
using System;
using Microsoft.Data.SqlClient;

public class A
{
    public static void Main()
    {
        string connectionString = "<Your-connection-string-here>";
        // Wait for 5 second delay in the command
        string queryString = "waitfor delay '00:00:05'";
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            SqlCommand command = new SqlCommand(queryString, connection);
            // Setting command timeout to 1 second
            command.CommandTimeout = 1;
            try
            {
                command.ExecuteNonQuery();
            }
            catch (SqlException e)
            {
                Console.WriteLine("Got expected SqlException due to command timeout ");
                Console.WriteLine(e);
            }
        }
    }
}
// </Snippet1>
