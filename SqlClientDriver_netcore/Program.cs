using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlClientDriver_netcore
{
    class Program
    {
        static void Main(string[] args)
        {
            string connString = @"Server=testsvr.t-seanlin-28101.onebox.xdb.mscds.com;Database=testdbdemo;User ID=cloudSA;Password=yU8aK5hI2nN4;Trust Server Certificate=true";

            try
            {
                using (SqlConnection conn = new SqlConnection(connString))
                {
                    //retrieve the SQL Server instance version
                    //string query = @"SELECT @@VERSION";
                    string query = @"SELECT * FROM sys.databases";

                    SqlCommand cmd = new SqlCommand(query, conn);

                    //open connections
                    conn.Open();

                    //execute the SQLCommand
                    SqlDataReader dr = cmd.ExecuteReader();

                    //check if there are records
                    if (dr.HasRows)
                    {
                        while (dr.Read())
                        {
                            //display retrieved record (first column only/string value)
                            Console.WriteLine(dr.GetString(0));
                        }
                    }
                    else
                    {
                        Console.WriteLine("No data found.");
                    }
                    dr.Close();
                }
            }
            catch (Exception ex)
            {
                //display error message
                Console.WriteLine("Exception: " + ex.Message);
            }
        }
    }
}
