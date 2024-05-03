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
            "Connection Timeout=30;TrustServerCertificate=True;Timeout=0;Encrypt=False;Database=master"; // pooled

            bool testX = true;
            if (testX)
                SimpleConnectionTestX(connectionString);
            else
                SimpleConnectionTest(connectionString).Wait();
        }

        private static async Task SimpleConnectionTest(string connectionString)
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

            SqlConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT @@VERSION";
                Console.WriteLine("Executing command");

                object result = await command.ExecuteScalarAsync();
                Console.WriteLine(result);
            }
        }

        private static void SimpleConnectionTestX(string connectionString)
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

            SqlConnectionX connection = new SqlConnectionX(connectionString);
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT @@VERSION";
                Console.WriteLine("Executing command");

                object result = command.ExecuteScalar();
                Console.WriteLine(result);
            }
        }
    }
}
