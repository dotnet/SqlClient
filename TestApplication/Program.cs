using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Running;
using Microsoft.Data.SqlClient;
using Microsoft.Data.SqlClient.SqlClientX;

namespace TestApplication
{
    internal class Program
    {
        
        static void Main(string[] args)
        {
            NormalStuff();
            //BenchmarkRunner.Run<Benchmarks>();
        }

        private static async Task AsyncGet()
        {
            string connectionString = $"Server=tcp:127.0.0.1;" +
                        $"Min Pool Size=120;Max Pool Size = 200;User Id=sa; pwd={Environment.GetEnvironmentVariable("SQL_PWD")}; " +
                        "Connection Timeout=30;TrustServerCertificate=True;Timeout=0;Encrypt=False;Database=testdatabase;Pooling=False;" +
                        "Application Name=TestAppX"; // pooled
            using var conn = new SqlConnection(connectionString);
            conn.Open();

            using (var cmd = new SqlCommand("IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TextTable' AND xtype='U') CREATE TABLE [TextTable] ([Text] VARCHAR(MAX))", conn))
                cmd.ExecuteNonQuery();

            using (var cmd = new SqlCommand("INSERT INTO [TextTable] ([Text]) VALUES (@p)", conn))
            {
                cmd.Parameters.AddWithValue("p", new string('x', 1024 * 1024 * 5));
                cmd.ExecuteNonQuery();
            }

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "Select * from AllDataTypesTable";
                    Console.WriteLine("Executing command");

                    using (SqlDataReader reader = await command.ExecuteReaderAsync())
                    {
                        do
                        {
                            while (await reader.ReadAsync())
                            {
                                // Process each row
                                // REad in reverse to cached the data in the reader buffers.
                                for (int i = reader.FieldCount - 1; i >= 0; i--)
                                {
                                    Console.WriteLine(await reader.GetFieldValueAsync<object>(i, CancellationToken.None));
                                }
                            }
                        } while (reader.NextResult()); // Move to the next result set
                    }
                }   
            }

        }

        private static void NormalStuff()
        {
            string connectionString = $"Server=tcp:127.0.0.1;" +
                        $"Min Pool Size=120;Max Pool Size = 200;User Id=sa; pwd={Environment.GetEnvironmentVariable("SQL_PWD")}; " +
                        "Connection Timeout=30;TrustServerCertificate=True;Timeout=0;Encrypt=False;Database=testdatabase;Pooling=False;" +
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
                command.CommandText = "SELECT [Text] FROM [TextTable]";
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
                command.CommandText = "SELECT [Text] FROM [TextTable] ";
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
