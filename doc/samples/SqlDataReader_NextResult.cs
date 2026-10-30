using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlDataReader_NextResult
{
    class Program
    {
        static void Main()
        {
            string s = GetConnectionString();
            SqlConnection c = new SqlConnection(s);
            RetrieveMultipleResults(c);
            Console.ReadLine();
        }

        // <Snippet1>
        static void RetrieveMultipleResults(SqlConnection connection)
        {
            using (connection)
            {
                using SqlCommand command = new SqlCommand(
                  "SELECT CategoryID, CategoryName FROM dbo.Categories;" +
                  "SELECT EmployeeID, LastName FROM dbo.Employees",
                  connection);
                connection.Open();

                ReadAllResults(command);
            }
        }

        static void ReadAllResults(SqlCommand command)
        {
            using SqlDataReader reader = command.ExecuteReader(CommandBehavior.Default);
            do
            {
                while (reader.Read())
                {
                    for (int column = 0; column < reader.FieldCount; column++)
                    {
                        Console.WriteLine(reader.GetValue(column));
                    }
                }
            }
            // Advance even if the current result is empty or no further rows are expected.
            // A trailing server error can throw here instead of during Read.
            while (reader.NextResult());
        }
        // </Snippet1>

        // <Snippet2>
        static async Task ReadAllResultsAsync(SqlCommand command)
        {
            using SqlDataReader reader =
                await command.ExecuteReaderAsync(CommandBehavior.Default);
            do
            {
                while (await reader.ReadAsync())
                {
                    for (int column = 0; column < reader.FieldCount; column++)
                    {
                        Console.WriteLine(reader.GetValue(column));
                    }
                }
            }
            while (await reader.NextResultAsync());
        }
        // </Snippet2>
        static private string GetConnectionString()
        {
            // To avoid storing the connection string in your code,
            // you can retrieve it from a configuration file.
            return "Data Source=(local);Initial Catalog=Northwind;"
                + "Integrated Security=SSPI";
        }
    }
}
