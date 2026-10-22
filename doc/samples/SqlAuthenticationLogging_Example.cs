// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Requires Microsoft.Data.SqlClient.Extensions.Azure with AuthenticationTrace support.
// No application logging setup is needed. Attach before opening the connection:
// dotnet-trace collect --process-id <pid> --providers Microsoft.Data.SqlClient.EventSource:0x2000:5 --rundown false
// Use 0x2002 to include ordinary SqlClient Trace events. PII logging remains disabled.
// <Snippet_AuthenticationLogging>
using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlAuthenticationLoggingExample
{
    public static class Sample
    {
        /// <summary>Opens a connection whose authentication diagnostics can be collected externally.</summary>
        public static async Task RunAsync()
        {
            string connectionString = Environment.GetEnvironmentVariable("SQLCLIENT_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set SQLCLIENT_CONNECTION_STRING to an Entra ID connection string.");
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }
    }
}
// </Snippet_AuthenticationLogging>
