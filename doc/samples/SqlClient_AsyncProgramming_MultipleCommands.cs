namespace SqlClient_AsyncProgramming_MultipleCommands;

using System;
// <Snippet1>
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class Class1
{
    static void Main()
    {
        Task task = MultipleCommands();
        task.Wait();
    }

    static async Task MultipleCommands()
    {
        // By default, MARS is disabled when connecting to a MARS-enabled.
        // It must be enabled in the connection string.
        string connectionString = GetConnectionString();

        int vendorID;
        SqlDataReader productReader = null;
        string vendorSQL =
            "SELECT BusinessEntityID, Name FROM Purchasing.Vendor";
        string productSQL =
            "SELECT Production.Product.Name FROM Production.Product " +
            "INNER JOIN Purchasing.ProductVendor " +
            "ON Production.Product.ProductID = " +
            "Purchasing.ProductVendor.ProductID " +
            "WHERE Purchasing.ProductVendor.BusinessEntityID = @VendorId";

        using (SqlConnection awConnection =
            new SqlConnection(connectionString))
        {
            SqlCommand vendorCmd = new SqlCommand(vendorSQL, awConnection);
            SqlCommand productCmd =
                new SqlCommand(productSQL, awConnection);

            productCmd.Parameters.Add("@VendorId", SqlDbType.Int);

            await awConnection.OpenAsync();
            using (SqlDataReader vendorReader = await vendorCmd.ExecuteReaderAsync())
            {
                while (await vendorReader.ReadAsync())
                {
                    Console.WriteLine(vendorReader["Name"]);

                    vendorID = (int)vendorReader["BusinessEntityID"];

                    productCmd.Parameters["@VendorId"].Value = vendorID;
                    // The following line of code requires a MARS-enabled connection.
                    productReader = await productCmd.ExecuteReaderAsync();
                    using (productReader)
                    {
                        while (await productReader.ReadAsync())
                        {
                            Console.WriteLine("  " +
                                productReader["Name"].ToString());
                        }
                    }
                }
            }
        }
    }

    private static string GetConnectionString()
    {
        // To avoid storing the connection string in your code, you can retrieve it from a configuration file.
        return "Data Source=(local);Integrated Security=SSPI;Initial Catalog=AdventureWorks;MultipleActiveResultSets=True";
    }
}
// </Snippet1>
