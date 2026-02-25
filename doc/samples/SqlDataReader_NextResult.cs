using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Data.SqlClient;
using System.Data;

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
                SqlCommand command = new SqlCommand(
                  "SELECT CategoryID, CategoryName FROM dbo.Categories;" +
                  "SELECT EmployeeID, LastName FROM dbo.Employees",
                  connection);
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                // Check if the DataReader has any row.
                while (reader.HasRows)
                {
                    Console.WriteLine("\t{0}\t{1}", reader.GetName(0),
                        reader.GetName(1));

                    // Obtain a row from the query result.
                    while (reader.Read())
                    {
                        Console.WriteLine("\t{0}\t{1}", reader.GetInt32(0),
                            reader.GetString(1));
                    }

                    // Hop to the next result-set.
                    reader.NextResult();
                }
                // Always call the Close method when you have finished using the DataReader object.
                reader.Close();
            }
        }

        // </Snippet1>
        static private string GetConnectionString()
        {
            // To avoid storing the connection string in your code,
            // you can retrieve it from a configuration file.
            return "Data Source=(local);Initial Catalog=Northwind;"
                + "Integrated Security=SSPI";
        }
    }
}
