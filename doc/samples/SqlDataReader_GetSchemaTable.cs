using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlDataReader_GetSchemaTable
{
    class Program
    {
        static void Main()
        {
            string s = GetConnectionString();
            SqlConnection c = new SqlConnection(s);
            GetSchemaInfo(c);
            Console.ReadLine();
        }
        // <Snippet1>
        static void GetSchemaInfo(SqlConnection connection)
        {
            using (connection)
            {
                SqlCommand command = new SqlCommand(
                  "SELECT CategoryID, CategoryName FROM Categories;",
                  connection);
                connection.Open();

                SqlDataReader reader = command.ExecuteReader();

                // Retrieve schema information about the current result-set.
                DataTable schemaTable = reader.GetSchemaTable();

                foreach (DataRow row in schemaTable.Rows)
                {
                    foreach (DataColumn column in schemaTable.Columns)
                    {
                        Console.WriteLine(String.Format("{0} = {1}",
                           column.ColumnName, row[column]));
                    }
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
