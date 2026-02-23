using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlDataAdapter_Events
{
    class Program
    {
        static void Main()
        {
        }

        // <Snippet1>
        static DataSet DataAdapterEventsDemo(SqlConnection connection, DataSet custDS)
        {
            // Assumes that connection is a valid SqlConnection object 
            // and custDS includes the Customers table.
            SqlDataAdapter custAdapter = new SqlDataAdapter(
                "SELECT CustomerID, CompanyName FROM Customers", connection);

            // Add handlers.  
            custAdapter.RowUpdating += new SqlRowUpdatingEventHandler(OnRowUpdating);
            custAdapter.RowUpdated += new SqlRowUpdatedEventHandler(OnRowUpdated);

            // Set DataAdapter command properties, fill DataSet, modify DataSet.  
            custAdapter.Update(custDS, "Customers");

            // Remove handlers.  
            custAdapter.RowUpdating -= new SqlRowUpdatingEventHandler(OnRowUpdating);
            custAdapter.RowUpdated -= new SqlRowUpdatedEventHandler(OnRowUpdated);

            return custDS;
        }

        protected static void OnRowUpdating(object sender, SqlRowUpdatingEventArgs args)
        {
            if (args.StatementType == StatementType.Delete)
            {
                // Saves the removing rows with additional information in a file.
                System.IO.TextWriter tw = System.IO.File.AppendText("Deletes.log");
                tw.WriteLine(
                  "{0}: Customer {1} Deleted.", DateTime.Now,
                   args.Row["CustomerID", DataRowVersion.Original]);
                tw.Close();
            }
        }

        protected static void OnRowUpdated(object sender, SqlRowUpdatedEventArgs args)
        {
            if (args.Status == UpdateStatus.ErrorsOccurred)
            {
                // Adds the error message to the row and skips from it.
                args.Row.RowError = args.Errors.Message;
                args.Status = UpdateStatus.SkipCurrentRow;
            }
        }
        // </Snippet1>

        // <Snippet2>
        static DataSet DataAdapterFillAndError(SqlDataAdapter adapter)
        {
            // Assuemes adapter is a valid SqlDataAdapter object.
            adapter.FillError += new FillErrorEventHandler(FillError);

            DataSet dataSet = new DataSet();
            adapter.Fill(dataSet);
            return dataSet;
        }

        protected static void FillError(object sender, FillErrorEventArgs args)
        {
            if (args.Errors.GetType() == typeof(System.OverflowException))
            {
                // Code to handle precision loss.  
                // Add a row to table using the values from the first two columns.
                DataRow myRow = args.DataTable.Rows.Add(new object[]
                   {args.Values[0], args.Values[1], DBNull.Value});
                //Set the RowError containing the value for the third column.  
                myRow.RowError =
                   "OverflowException Encountered. Value from data source: " +
                   args.Values[2];
                args.Continue = true;
            }
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
