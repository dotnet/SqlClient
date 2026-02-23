namespace SqlClient_AsyncProgramming_MultipleCommandsEx;

using System;
// <Snippet1>
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

class Program
{
    static void Main()
    {
        Task task = ReadingAndUpdatingData();
        task.Wait();
    }

    static async Task ReadingAndUpdatingData()
    {
        // By default, MARS is disabled when connecting to a MARS-enabled host.
        // It must be enabled in the connection string.
        string connectionString = GetConnectionString();

        SqlTransaction updateTx = null;
        SqlCommand vendorCmd = null;
        SqlCommand prodVendCmd = null;
        SqlCommand updateCmd = null;

        SqlDataReader prodVendReader = null;

        int vendorID = 0;
        int productID = 0;
        int minOrderQty = 0;
        int maxOrderQty = 0;
        int onOrderQty = 0;
        int recordsUpdated = 0;
        int totalRecordsUpdated = 0;

        string vendorSQL =
            "SELECT BusinessEntityID, Name FROM Purchasing.Vendor " +
            "WHERE CreditRating = 5";
        string prodVendSQL =
            "SELECT ProductID, MaxOrderQty, MinOrderQty, OnOrderQty " +
            "FROM Purchasing.ProductVendor " +
            "WHERE BusinessEntityID = @VendorID";
        string updateSQL =
            "UPDATE Purchasing.ProductVendor " +
            "SET OnOrderQty = @OrderQty " +
            "WHERE ProductID = @ProductID AND BusinessEntityID = @VendorID";

        using (SqlConnection awConnection =
            new SqlConnection(connectionString))
        {
            await awConnection.OpenAsync();
            updateTx = await Task.Run(() => awConnection.BeginTransaction());

            vendorCmd = new SqlCommand(vendorSQL, awConnection);
            vendorCmd.Transaction = updateTx;

            prodVendCmd = new SqlCommand(prodVendSQL, awConnection);
            prodVendCmd.Transaction = updateTx;
            prodVendCmd.Parameters.Add("@VendorId", SqlDbType.Int);

            updateCmd = new SqlCommand(updateSQL, awConnection);
            updateCmd.Transaction = updateTx;
            updateCmd.Parameters.Add("@OrderQty", SqlDbType.Int);
            updateCmd.Parameters.Add("@ProductID", SqlDbType.Int);
            updateCmd.Parameters.Add("@VendorID", SqlDbType.Int);

            using (SqlDataReader vendorReader = await vendorCmd.ExecuteReaderAsync())
            {
                while (await vendorReader.ReadAsync())
                {
                    Console.WriteLine(vendorReader["Name"]);

                    vendorID = (int)vendorReader["BusinessEntityID"];
                    prodVendCmd.Parameters["@VendorID"].Value = vendorID;
                    prodVendReader = await prodVendCmd.ExecuteReaderAsync();

                    using (prodVendReader)
                    {
                        while (await prodVendReader.ReadAsync())
                        {
                            productID = (int)prodVendReader["ProductID"];

                            if (prodVendReader["OnOrderQty"] == DBNull.Value)
                            {
                                minOrderQty = (int)prodVendReader["MinOrderQty"];
                                onOrderQty = minOrderQty;
                            }
                            else
                            {
                                maxOrderQty = (int)prodVendReader["MaxOrderQty"];
                                onOrderQty = (int)(maxOrderQty / 2);
                            }

                            updateCmd.Parameters["@OrderQty"].Value = onOrderQty;
                            updateCmd.Parameters["@ProductID"].Value = productID;
                            updateCmd.Parameters["@VendorID"].Value = vendorID;

                            recordsUpdated = await updateCmd.ExecuteNonQueryAsync();
                            totalRecordsUpdated += recordsUpdated;
                        }
                    }
                }
            }
            Console.WriteLine("Total Records Updated: ", totalRecordsUpdated.ToString());
            await Task.Run(() => updateTx.Rollback());
            Console.WriteLine("Transaction Rolled Back");
        }
    }

    private static string GetConnectionString()
    {
        // To avoid storing the connection string in your code, you can retrieve it from a configuration file.
        return "Data Source=(local);Integrated Security=SSPI;Initial Catalog=AdventureWorks;MultipleActiveResultSets=True";
    }
}
// </Snippet1>
