using System;
using Microsoft.Data.SqlClient;
using System.Data;

namespace SqlDataAdapter_FillDataSet
{
    class Program
    {
        static void Main()
        {
            string s = GetConnectionString();
            SqlConnection c = new SqlConnection(s);
            GetCustomers(c);
            PrintCustomersOrders(c, c);
            CustomerFillSchema1(c);
            CustomerFillSchema2(c);
            Console.ReadLine();
        }

        static DataSet GetCustomers(SqlConnection connection)
        {
            using (connection)
            {
            // <Snippet1>
                // Assumes that connection is a valid SqlConnection object.
                string queryString =
                "SELECT CustomerID, CompanyName FROM dbo.Customers";
                SqlDataAdapter adapter = new SqlDataAdapter(queryString, connection);
                
                DataSet customers = new DataSet();
                adapter.Fill(customers, "Customers");
            //  </Snippet1>
                return customers;
            }
        }

        static void PrintCustomersOrders(SqlConnection customerConnection, SqlConnection orderConnection)
        {
            using (customerConnection)
            using (orderConnection)
            {
                // <Snippet2>
                // Assumes that customerConnection and orderConnection are valid SqlConnection objects.
                SqlDataAdapter custAdapter = new SqlDataAdapter(
                "SELECT * FROM dbo.Customers", customerConnection);
                SqlDataAdapter ordAdapter = new SqlDataAdapter(
                "SELECT * FROM Orders", orderConnection);

                DataSet customerOrders = new DataSet();

                custAdapter.Fill(customerOrders, "Customers");
                ordAdapter.Fill(customerOrders, "Orders");

                DataRelation relation = customerOrders.Relations.Add("CustOrders",
                customerOrders.Tables["Customers"].Columns["CustomerID"],
                customerOrders.Tables["Orders"].Columns["CustomerID"]);

                foreach (DataRow pRow in customerOrders.Tables["Customers"].Rows)
                {
                    Console.WriteLine(pRow["CustomerID"]);
                    foreach (DataRow cRow in pRow.GetChildRows(relation))
                        Console.WriteLine("\t" + cRow["OrderID"]);
                }
                // </Snippet2>
            }
        }

        static DataSet CustomerFillSchema1(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet3>
                // Assumes that connection is a valid SqlConnection object.
                DataSet custDataSet = new DataSet();

                SqlDataAdapter custAdapter = new SqlDataAdapter(
                    "SELECT * FROM dbo.Customers", connection);

                custAdapter.FillSchema(custDataSet, SchemaType.Source, "Customers");
                custAdapter.Fill(custDataSet, "Customers");
                // </Snippet3>

                return custDataSet;
            }
        }

        static DataSet CustomerFillSchema2(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet4>
                // Assumes that connection is a valid SqlConnection object.
                DataSet custDataSet = new DataSet();

                SqlDataAdapter custAdapter = new SqlDataAdapter(
                    "SELECT * FROM dbo.Customers", connection);

                custAdapter.MissingSchemaAction = MissingSchemaAction.AddWithKey;
                custAdapter.Fill(custDataSet, "Customers");
                // </Snippet4>

                return custDataSet;
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
