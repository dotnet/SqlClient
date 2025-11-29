namespace SqlConnection_Pooling;

using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        // <Snippet1>
        using (SqlConnection connection = new SqlConnection(
            "Integrated Security=SSPI;Initial Catalog=Northwind"))
                {
                    connection.Open();
                    // Pool A is created.
                }

            using (SqlConnection connection = new SqlConnection(
            "Integrated Security=SSPI;Initial Catalog=pubs"))
                {
                    connection.Open();
                    // Pool B is created because the connection strings differ.  
                }

            using (SqlConnection connection = new SqlConnection(
            "Integrated Security=SSPI;Initial Catalog=Northwind"))
                {  
                    connection.Open();
                    // The connection string matches pool A.  
                }
            // </Snippet1>
    }
}
