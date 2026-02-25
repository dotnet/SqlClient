#if NETFRAMEWORK
// <Snippet1>
using System;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SqlClient_PerformanceCounter
{
    class Program
    {
        PerformanceCounter[] PerfCounters = new PerformanceCounter[10];
        SqlConnection connection = new SqlConnection();

        static void Main()
        {
            Program prog = new Program();
            // Open a connection and create the performance counters.  
            prog.connection.ConnectionString =
               GetIntegratedSecurityConnectionString();
            prog.SetUpPerformanceCounters();
            Console.WriteLine("Available Performance Counters:");

            // Create the connections and display the results.  
            prog.CreateConnections();
            Console.WriteLine("Press Enter to finish.");
            Console.ReadLine();
        }

        private void CreateConnections()
        {
            // List the Performance counters.  
            WritePerformanceCounters();

            // Create 4 connections and display counter information.  
            SqlConnection connection1 = new SqlConnection(
                  GetIntegratedSecurityConnectionString());
            connection1.Open();
            Console.WriteLine("Opened the 1st Connection:");
            WritePerformanceCounters();

            SqlConnection connection2 = new SqlConnection(
                  GetSqlConnectionStringDifferent());
            connection2.Open();
            Console.WriteLine("Opened the 2nd Connection:");
            WritePerformanceCounters();

            SqlConnection connection3 = new SqlConnection(
                  GetSqlConnectionString());
            connection3.Open();
            Console.WriteLine("Opened the 3rd Connection:");
            WritePerformanceCounters();

            SqlConnection connection4 = new SqlConnection(
                  GetSqlConnectionString());
            connection4.Open();
            Console.WriteLine("Opened the 4th Connection:");
            WritePerformanceCounters();

            connection1.Close();
            Console.WriteLine("Closed the 1st Connection:");
            WritePerformanceCounters();

            connection2.Close();
            Console.WriteLine("Closed the 2nd Connection:");
            WritePerformanceCounters();

            connection3.Close();
            Console.WriteLine("Closed the 3rd Connection:");
            WritePerformanceCounters();

            connection4.Close();
            Console.WriteLine("Closed the 4th Connection:");
            WritePerformanceCounters();
        }

        private enum ADO_Net_Performance_Counters
        {
            NumberOfActiveConnectionPools,
            NumberOfReclaimedConnections,
            HardConnectsPerSecond,
            HardDisconnectsPerSecond,
            NumberOfActiveConnectionPoolGroups,
            NumberOfInactiveConnectionPoolGroups,
            NumberOfInactiveConnectionPools,
            NumberOfNonPooledConnections,
            NumberOfPooledConnections,
            NumberOfStasisConnections
            // The following performance counters are more expensive to track.  
            // Enable ConnectionPoolPerformanceCounterDetail in your config file.  
            //     SoftConnectsPerSecond  
            //     SoftDisconnectsPerSecond  
            //     NumberOfActiveConnections  
            //     NumberOfFreeConnections  
        }

        private void SetUpPerformanceCounters()
        {
            connection.Close();
            this.PerfCounters = new PerformanceCounter[10];
            string instanceName = GetInstanceName();
            Type apc = typeof(ADO_Net_Performance_Counters);
            int i = 0;
            foreach (string s in Enum.GetNames(apc))
            {
                this.PerfCounters[i] = new PerformanceCounter();
                this.PerfCounters[i].CategoryName = ".NET Data Provider for SqlServer";
                this.PerfCounters[i].CounterName = s;
                this.PerfCounters[i].InstanceName = instanceName;
                i++;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int GetCurrentProcessId();

        private string GetInstanceName()
        {
            //This works for Winforms apps.  
            string instanceName =
                System.Reflection.Assembly.GetEntryAssembly().GetName().Name;

            // Must replace special characters like (, ), #, /, \\  
            string instanceName2 =
                AppDomain.CurrentDomain.FriendlyName.ToString().Replace('(', '[')
                .Replace(')', ']').Replace('#', '_').Replace('/', '_').Replace('\\', '_');

            // For ASP.NET applications your instanceName will be your CurrentDomain's
            // FriendlyName. Replace the line above that sets the instanceName with this:  
            // instanceName = AppDomain.CurrentDomain.FriendlyName.ToString().Replace('(','[')  
            // .Replace(')',']').Replace('#','_').Replace('/','_').Replace('\\','_');  

            string pid = GetCurrentProcessId().ToString();
            instanceName = instanceName + "[" + pid + "]";
            Console.WriteLine("Instance Name: {0}", instanceName);
            Console.WriteLine("---------------------------");
            return instanceName;
        }

        private void WritePerformanceCounters()
        {
            Console.WriteLine("---------------------------");
            foreach (PerformanceCounter p in this.PerfCounters)
            {
                Console.WriteLine("{0} = {1}", p.CounterName, p.NextValue());
            }
            Console.WriteLine("---------------------------");
        }

        private static string GetIntegratedSecurityConnectionString()
        {
            // To avoid storing the connection string in your code,  
            // you can retrieve it from a configuration file.  
            return @"Data Source=.;Integrated Security=True;" +
              "Initial Catalog=AdventureWorks";
        }
        private static string GetSqlConnectionString()
        {
            // To avoid storing the connection string in your code,  
            // you can retrieve it from a configuration file.  
            return @"Data Source=.;User Id=<myUserID>;Password=<myPassword>;" +
              "Initial Catalog=AdventureWorks";
        }

        private static string GetSqlConnectionStringDifferent()
        {
            // To avoid storing the connection string in your code,  
            // you can retrieve it from a configuration file.  
            return @"Initial Catalog=AdventureWorks;Data Source=.;" +
              "User Id=<myUserID>;Password=<myPassword>;";
        }
    }
}
// </Snippet1>
#endif
