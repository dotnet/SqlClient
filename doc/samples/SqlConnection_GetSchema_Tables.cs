namespace SqlConnection_GetSchema_Tables;

// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Data Source = localhost; Integrated Security = true; Initial Catalog = AdventureWorks";
        
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            DataTable table = connection.GetSchema("Tables");

            // Display the contents of the table.  
            DisplayData(table);
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }
    }

    private static void DisplayData(System.Data.DataTable table)
    {
        foreach (System.Data.DataRow row in table.Rows)
        {
            foreach (System.Data.DataColumn col in table.Columns)
            {
                Console.WriteLine("{0} = {1}", col.ColumnName, row[col]);
            }
            Console.WriteLine("============================");
        }
    }
}
// </Snippet1>
