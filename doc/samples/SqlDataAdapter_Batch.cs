using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlDataAdapter_Batch
{
    class Program
    {
        static void Main()
        {
        }

        // <Snippet1>
        public static void BatchUpdate(DataTable dataTable, Int32 batchSize)
        {
            // Assumes GetConnectionString() returns a valid connection string.
            string connectionString = GetConnectionString();

            // Connect to the AdventureWorks database.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // Create a SqlDataAdapter.  
                SqlDataAdapter adapter = new SqlDataAdapter();

                // Set the UPDATE command and parameters.  
                adapter.UpdateCommand = new SqlCommand(
                    "UPDATE Production.ProductCategory SET "
                    + "Name=@Name WHERE ProductCategoryID=@ProdCatID;",
                    connection);
                adapter.UpdateCommand.Parameters.Add("@Name",
                SqlDbType.NVarChar, 50, "Name");
                adapter.UpdateCommand.Parameters.Add("@ProdCatID",
                SqlDbType.Int, 4, "ProductCategoryID");
                adapter.UpdateCommand.UpdatedRowSource = UpdateRowSource.None;

                // Set the INSERT command and parameter.  
                adapter.InsertCommand = new SqlCommand(
                    "INSERT INTO Production.ProductCategory (Name) VALUES (@Name);",
                    connection);
                adapter.InsertCommand.Parameters.Add("@Name",
                SqlDbType.NVarChar, 50, "Name");
                adapter.InsertCommand.UpdatedRowSource = UpdateRowSource.None;

                // Set the DELETE command and parameter.  
                adapter.DeleteCommand = new SqlCommand(
                    "DELETE FROM Production.ProductCategory "
                    + "WHERE ProductCategoryID=@ProdCatID;", connection);
                adapter.DeleteCommand.Parameters.Add("@ProdCatID",
                SqlDbType.Int, 4, "ProductCategoryID");
                adapter.DeleteCommand.UpdatedRowSource = UpdateRowSource.None;

                // Set the batch size.  
                adapter.UpdateBatchSize = batchSize;

                // Execute the update.  
                adapter.Update(dataTable);
            }
        }
        // </Snippet1>

        static private string GetConnectionString()
        {
            // To avoid storing the connection string in your code,
            // you can retrieve it from a configuration file.
            return "Data Source=(local);Initial Catalog=AdventureWorks;"
                + "Integrated Security=SSPI";
        }
    }
}
