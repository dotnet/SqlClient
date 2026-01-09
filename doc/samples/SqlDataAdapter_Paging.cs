using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlDataAdapter_Paging
{
    class Program
    {
        static void Main()
        {
            string s = GetConnectionString();
            SqlConnection c = new SqlConnection(s);
            GetOrders_Fill(c);
            GetOrders_Select(c);
            Console.ReadLine();
        }

        static DataSet GetOrders_Fill(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet1>
                int currentIndex = 0;
                int pageSize = 5;

                string orderSQL = "SELECT * FROM Orders ORDER BY OrderID";
                // Assumes that connection is a valid SqlConnection object.  
                SqlDataAdapter adapter = new SqlDataAdapter(orderSQL, connection);

                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet, currentIndex, pageSize, "Orders");
                // </Snippet1>

                // Retrieve the next page.
                // <Snippet3>
                currentIndex += pageSize;

                // Assumes that dataset and adapter are valid objects.
                dataSet.Tables["Orders"].Rows.Clear();
                adapter.Fill(dataSet, currentIndex, pageSize, "Orders");
                // </Snippet3>

                return dataSet;
            }
        }
        
        static DataSet GetOrders_Select(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet2>
                int pageSize = 5;

                string orderSQL = "SELECT TOP " + pageSize +
                  " * FROM Orders ORDER BY OrderID";
                
                // Assumes that connection is a valid SqlConnection object.  
                SqlDataAdapter adapter = new SqlDataAdapter(orderSQL, connection);

                DataSet dataSet = new DataSet();
                adapter.Fill(dataSet, "Orders");
                // </Snippet2>
                
                // <Snippet4>
                string lastRecord = 
                    dataSet.Tables["Orders"].Rows[pageSize - 1]["OrderID"].ToString();
                // </Snippet4>

                // <Snippet5>
                orderSQL = "SELECT TOP " + pageSize + 
                    " * FROM Orders WHERE OrderID > " + lastRecord + " ORDER BY OrderID";
                adapter.SelectCommand.CommandText = orderSQL;

                dataSet.Tables["Orders"].Rows.Clear();

                adapter.Fill(dataSet, "Orders");
                // </Snippet5>

                return dataSet;
            }
        }

        static private string GetConnectionString()
        {
            // To avoid storing the connection string in your code,
            // you can retrieve it from a configuration file.
            return "Data Source=(local);Initial Catalog=Northwind;"
                + "Integrated Security=SSPI";
        }
    }
}
