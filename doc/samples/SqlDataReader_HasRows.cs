using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlDataReader_HasRows
{
    class Program
    {
        static void Main()
        {
            string s = GetConnectionString();
            SqlConnection c = new SqlConnection(s);
            HasRows(c);
            Console.ReadLine();
        }

        // <Snippet1>
        static void HasRows(SqlConnection connection)
        {
            using (connection)
            {
                SqlCommand command = new SqlCommand(
                  "SELECT CategoryID, CategoryName FROM Categories;",
                  connection);
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                // Check if the DataReader has any row.
                if (reader.HasRows)
                {
                    // Obtain a row from the query result.
                    while (reader.Read())
                    {
                        Console.WriteLine("{0}\t{1}", reader.GetInt32(0),
                            reader.GetString(1));
                    }
                }
                else
                {
                    Console.WriteLine("No rows found.");
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
