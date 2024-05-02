using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Data.SqlClient;

namespace TestApplication
{
    internal class Program
    {

        static string connectionString = "Server=tcp:127.0.0.1;" +
            "Min Pool Size=120;Max Pool Size = 200;User Id=sa; pwd=; " +
            "Connection Timeout=30;TrustServerCertificate=True;Timeout=0;Encrypt=False;Database=master"; // pooled

        static void Main(string[] args)
        {
            //TestConnections().Wait();
            SimpleConnectionTest();

            //Thread.Sleep(10000);
        }

        private static void SimpleConnectionTest()
        {
            //AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.UseManagedNetworkingOnWindows", true);

            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT @@VERSION";
                Console.WriteLine("Executing command");

                object result = command.ExecuteScalar();
                Console.WriteLine(result);
            }
        }

        static async Task TestConnections()
        {

            Console.WriteLine("WARM UP");
            await MeasureSingleConnectionAndReuse(connectionString);

            ClearPools();
            await MeasureSingleConnectionAndReuse(connectionString);

            ClearPools();
            await MeasureSingleConnectionAndReuse(connectionString);

            Console.WriteLine("\n\nCONCURRENT POOLED CONNECTIONS");
            ClearPools();
            MeasureParallelConnections(connectionString);

            Console.WriteLine("\n\nCONCURRENT NON-POOLED CONNECTIONS");
            ClearPools();
            MeasureParallelConnections(connectionString + "Pooling=false;");


            Console.WriteLine("\nTesting finished");
            Console.ReadLine();
        }

        private static void ClearPools()
        {
            SqlConnection.ClearAllPools();
            Console.WriteLine("ALL POOLS CLEARED");
        }
        static ConcurrentDictionary<Guid, object> _connectionIDs = new ConcurrentDictionary<Guid, object>();
        private static void MeasureParallelConnections(string connectionString)
        {
            Console.WriteLine("Start delay      OpenAsync time     Connection ID                        ReusedFromPool");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int numOpens = 100;
            Task[] tasks = new Task[numOpens];
            Stopwatch start = new Stopwatch();
            start.Start();
            for (int i = 0; i < numOpens; i++)
            {
                tasks[i] = MeasureSingleConnection(i, start, connectionString);
            }
            Task.WaitAll(tasks);
            Console.WriteLine($"{sw.Elapsed} {numOpens} connections opened in paralel");
        }

        private static async Task MeasureSingleConnection(int index, Stopwatch start, string connectionString)
        {
            TimeSpan startDelay = start.Elapsed;
            Stopwatch sw = new Stopwatch();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                sw.Start();
                await connection.OpenAsync();
                Console.WriteLine($"{startDelay} {sw.Elapsed} {index} {connection.ClientConnectionId} {IsReuse(connection)}");
                await Task.Delay(4000);
            }
        }

        private static async Task MeasureSingleConnectionAndReuse(string connectionString)
        {
            Stopwatch sw = new Stopwatch();
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                sw.Start();
                await connection.OpenAsync();
                Console.WriteLine($"{sw.Elapsed} {connection.ClientConnectionId} {IsReuse(connection)} Single open time ");
            }
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                sw.Restart();
                await connection.OpenAsync();
                Console.WriteLine($"{sw.Elapsed} {connection.ClientConnectionId} {IsReuse(connection)} Single open time with one previously opened connection");
            }
        }

        private static bool IsReuse(SqlConnection connection)
        {
            return !_connectionIDs.TryAdd(connection.ClientConnectionId, null);
        }
    }

}
