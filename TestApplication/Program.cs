using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SqlClientX;

namespace TestApplication
{
    internal class Program
    {
        
        static void Main(string[] args)
        {
            string connectionString = $"Server=tcp:127.0.0.1;" +
            $"Min Pool Size=120;Max Pool Size = 200;User Id=sa; pwd={Environment.GetEnvironmentVariable("SQL_PWD")}; " +
            "Connection Timeout=30;TrustServerCertificate=True;Timeout=0;Encrypt=False;Database=master;Pooling=False;" +
            "Application Name=TestAppX"; // pooled
            Console.WriteLine("1 for X else default MDS");
            char testX = Console.ReadKey().KeyChar;
            if (testX == '1')
                SimpleConnectionTestX(connectionString);
            else
                SimpleConnectionTest(connectionString);
        }

        private static void SimpleConnectionTest(string connectionString)
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT @@SPID, 1, @@VERSION";
                Console.WriteLine("Executing command");

                using (SqlDataReader reader = command.ExecuteReader())
                {
                    do
                    {
                        while (reader.Read())
                        {
                            // Process each row
                            // REad in reverse to cached the data in the reader buffers.
                            for (int i = reader.FieldCount -1; i >= 0; i--)
                            {
                                Console.WriteLine(reader.GetValue(i));
                            }
                        }
                    } while (reader.NextResult()); // Move to the next result set
                }
            }
        }

        private static void SimpleConnectionTestX(string connectionString)
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

            SqlConnectionX connection = new SqlConnectionX(connectionString);
            connection.Open();

            using (SqlCommandX command = connection.CreateCommand())
            {
                command.CommandText = "Select @@VERSION, cast(2 as varchar), cast(@@SPID as varchar) ";
                Console.WriteLine("Executing command");

                using (SqlDataReaderX reader = command.ExecuteReader())
                {
                    do
                    {
                        while (reader.Read())
                        {
                            // Process each row
                            // REad in reverse to cached the data in the reader buffers.
                            for (int i = reader.FieldCount - 1; i >= 0; i--)
                            {
                                Console.WriteLine(reader.GetValue(i));
                            }
                        }
                    } while (reader.NextResult()); // Move to the next result set
                }
            }
        }
    }
}
