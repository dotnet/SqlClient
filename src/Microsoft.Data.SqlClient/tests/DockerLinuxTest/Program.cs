using System;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.DockerLnuxTest
{
    class Program
    {
        static string server = "microsoft.sqlserver";
        static string user = "sa";

        // Provide password as set in docker-compose.yml
        static string pwd = "P@ssw0rd!123"; 

        static void Main(string[] args)
        {
            using (SqlConnection sqlConnection = new SqlConnection($"Server={server}; UID={user}; PWD={pwd}"))
            {
                sqlConnection.Open();

                Console.WriteLine($"Connected to SQL Server v{sqlConnection.ServerVersion} from {Environment.OSVersion.VersionString}");
                // Write your code here to debug inside Docker Linux containers.
            }
        }
    }
}

