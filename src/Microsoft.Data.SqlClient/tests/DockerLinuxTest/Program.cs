// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.SqlClient;

namespace Microsoft.Data.SqlClient.DockerLinuxTest
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

