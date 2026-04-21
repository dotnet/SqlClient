using System;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace SqlDataAdapter_TableMappings
{
    class Program
    {
        static void Main()
        {
            string s = GetConnectionString();
            SqlConnection c = new SqlConnection(s);
            CustomerTableMapping(c);
            BizTableMapping(c);
            GetCustomersOrders(c);
            Console.ReadLine();
        }

        static DataSet CustomerTableMapping(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet1>
                // Assumes that connection is a valid SqlConnection object.
                DataSet custDataSet = new DataSet();

                SqlDataAdapter custAdapter = new SqlDataAdapter(
                    "SELECT * FROM dbo.Customers", connection);

                DataTableMapping mapping =
                    custAdapter.TableMappings.Add("Table", "NorthwindCustomers");
                mapping.ColumnMappings.Add("CompanyName", "Company");
                mapping.ColumnMappings.Add("ContactName", "Contact");
                mapping.ColumnMappings.Add("PostalCode", "ZIPCode");

                custAdapter.Fill(custDataSet);
                // </Snippet1>

                return custDataSet;
            }
        }

        static DataSet BizTableMapping(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet2>
                // Assumes that connection is a valid SqlConnection object.
                DataSet custDataSet = new DataSet();

                SqlDataAdapter custAdapter = new SqlDataAdapter(
                    "SELECT * FROM dbo.Customers", connection);

                // The DataTableMapping is implemented ITableMapping.
                ITableMapping mapping =
                    custAdapter.TableMappings.Add("Table", "BizTalkSchema");
                mapping.ColumnMappings.Add("CustomerID", "ClientID");
                mapping.ColumnMappings.Add("CompanyName", "ClientName");
                mapping.ColumnMappings.Add("ContactName", "Contact");
                mapping.ColumnMappings.Add("PostalCode", "ZIP");

                custAdapter.Fill(custDataSet);
                // </Snippet2>

                return custDataSet;
            }
        }

        static DataSet GetCustomersOrders(SqlConnection connection)
        {
            using (connection)
            {
                // <Snippet3>
                // Assumes that connection is a valid SqlConnection object.
                string queryString =
                "SELECT * FROM dbo.Customers; SELECT * FROM dbo.Orders;";
                SqlDataAdapter adapter = new SqlDataAdapter(queryString, connection);

                DataSet customersDataSet = new DataSet();

                adapter.TableMappings.Add("Customers1", "Orders");
                adapter.Fill(customersDataSet, "Customers");
                // </Snippet3>

                return customersDataSet;
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
