using System;
using System.Data;
// <Snippet1>
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string s = GetConnectionString();

        OpenSqlConnection(s);
        Console.ReadLine();
    }

    private static void OpenSqlConnection(string connectionString)
    {
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            Console.WriteLine("ServerVersion: {0}", connection.ServerVersion);
            Console.WriteLine("WorkstationId: {0}", connection.WorkstationId);
        }
    }

    static private string GetConnectionString()
    {
        // To avoid storing the connection string in your code, 
        // you can retrieve it from a configuration file, using the 
        // System.Configuration.ConfigurationSettings.AppSettings property 
        return "Data Source=(local);Initial Catalog=AdventureWorks;"
            + "Integrated Security=SSPI;";
    }
}
// </Snippet1>