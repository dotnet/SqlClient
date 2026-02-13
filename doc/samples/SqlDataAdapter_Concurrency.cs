namespace SqlDataAdapter_Concurrency;

// <Snippet1>
using System;
using System.Data;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string connectionString = "Data Source = localhost; Integrated Security = true; Initial Catalog = Northwind";

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            // Assumes connection is a valid SqlConnection.  
            SqlDataAdapter adapter = new SqlDataAdapter(
              "SELECT CustomerID, CompanyName FROM Customers ORDER BY CustomerID",
              connection);

            // The Update command checks for optimistic concurrency violations  
            // in the WHERE clause.  
            adapter.UpdateCommand = new SqlCommand("UPDATE Customers Set CustomerID = @CustomerID, CompanyName = @CompanyName " +
               "WHERE CustomerID = @oldCustomerID AND CompanyName = @oldCompanyName", connection);
            adapter.UpdateCommand.Parameters.Add(
              "@CustomerID", SqlDbType.NChar, 5, "CustomerID");
            adapter.UpdateCommand.Parameters.Add(
              "@CompanyName", SqlDbType.NVarChar, 30, "CompanyName");

            // Pass the original values to the WHERE clause parameters.  
            SqlParameter parameter = adapter.UpdateCommand.Parameters.Add(
              "@oldCustomerID", SqlDbType.NChar, 5, "CustomerID");
            parameter.SourceVersion = DataRowVersion.Original;
            parameter = adapter.UpdateCommand.Parameters.Add(
              "@oldCompanyName", SqlDbType.NVarChar, 30, "CompanyName");
            parameter.SourceVersion = DataRowVersion.Original;

            // Add the RowUpdated event handler.  
            adapter.RowUpdated += new SqlRowUpdatedEventHandler(OnRowUpdated);

            DataSet dataSet = new DataSet();
            adapter.Fill(dataSet, "Customers");

            // Modify the DataSet contents.
            adapter.Update(dataSet, "Customers");

            foreach (DataRow dataRow in dataSet.Tables["Customers"].Rows)
            {
                if (dataRow.HasErrors)
                    Console.WriteLine(dataRow[0] + "\n" + dataRow.RowError);
            }
        }
    }

    protected static void OnRowUpdated(object sender, SqlRowUpdatedEventArgs args)
    {
        if (args.RecordsAffected == 0)
        {
            args.Row.RowError = "Optimistic Concurrency Violation Encountered";
            args.Status = UpdateStatus.SkipCurrentRow;
        }
    }
}
// </Snippet1>
