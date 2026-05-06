using System;
using System.Data;
using Microsoft.Data.SqlClient;

namespace SqlCommandBuilder_Create
{
    class Program
    {
        static void Main()
        {
            string cnnst = "Data Source=(local);Initial Catalog=Northwind;"
                + "Integrated Security=SSPI";
            string queryst = "SELECT CustomerID, CompanyName, ContactName, Phone FROM Customers";
            string newQueryString = "SELECT CustomerID, City, Region FROM Customers";
            string tablen = "Customers";
            DataSet ds = SelectSqlRows(cnnst, queryst, newQueryString, tablen);
        }

        public static DataSet SelectSqlRows(string connectionString,
            string queryString, string newQueryString, string tableName)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                // <Snippet1>
                // Assumes that connection is a valid SqlConnection object  
                // inside of a using block. 
                SqlDataAdapter adapter = new SqlDataAdapter();
                adapter.SelectCommand = new SqlCommand(queryString, connection);
                SqlCommandBuilder builder = new SqlCommandBuilder(adapter);
                builder.QuotePrefix = "[";  
                builder.QuoteSuffix = "]";  
                // </Snippet1>

                // <Snippet2>
                // Generate the update command automatically by SqlCommandBuilder
                Console.WriteLine(builder.GetUpdateCommand().CommandText);
                // </Snippet2>

                connection.Open();

                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet, tableName);

                // <Snippet3>
                // Assumes an open SqlConnection and SqlDataAdapter inside of a using block.
                adapter.SelectCommand.CommandText = newQueryString;
                builder.RefreshSchema();

                dataSet.Tables.Remove(dataSet.Tables[tableName]);
                adapter.Fill(dataSet, tableName);
                // </Snippet3>

                //code to modify data in DataSet here
                builder.GetUpdateCommand();

                //Without the SqlCommandBuilder this line would fail
                adapter.Update(dataSet, tableName);

                return dataSet;
            }
        }
    }
}
