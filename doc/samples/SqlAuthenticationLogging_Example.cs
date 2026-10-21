// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// TODO: Remove this guard once a published Microsoft.Data.SqlClient.Extensions.Azure package
// includes the authentication logging API. The samples project builds against published packages.
#if false
// <Snippet_AuthenticationLogging>
using System;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlAuthenticationLoggingExample
{
    public static class Sample
    {
        public static async Task RunAsync(bool enableCallback = false)
        {
            string connectionString = Environment.GetEnvironmentVariable("SQLCLIENT_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set SQLCLIENT_CONNECTION_STRING to an Entra ID connection string.");
            }

            using var listener = new SqlClientListener();
            if (enableCallback)
            {
                var provider = new ActiveDirectoryAuthenticationProvider(
                    new ActiveDirectoryAuthenticationProviderOptions
                    {
                        AuthenticationLoggingCallback = WriteAuthenticationLog,
                        EnableMsalPiiLogging = false
                    });
                var method = new SqlConnectionStringBuilder(connectionString).Authentication;
                if (!provider.IsSupported(method))
                {
                    throw new InvalidOperationException("Use a built-in Entra ID authentication method.");
                }
                SqlAuthenticationProvider.SetProvider(method, provider);
            }

            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }

        /// <summary>Runs synchronously; async sinks should use an application-owned bounded queue instead of async void.</summary>
        private static void WriteAuthenticationLog(SqlAuthenticationLogEntry entry)
        {
            if (!entry.ContainsPii && entry.Level <= EventLevel.Informational)
            {
                Console.WriteLine(
                    $"[{entry.Level}] [{entry.Source}] [{entry.ConnectionId}] {entry.Message}");
            }
        }

        private sealed class SqlClientListener : EventListener
        {
            /// <summary>Collects SqlClient Trace events, including the new authentication diagnostics.</summary>
            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
                {
                    EnableEvents(eventSource, EventLevel.Verbose, (EventKeywords)2);
                }
            }

            /// <summary>Writes collected payloads to the sample's application-controlled output.</summary>
            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.Payload != null)
                {
                    Console.WriteLine($"[{eventData.Level}] {string.Join(", ", eventData.Payload)}");
                }
            }
        }
    }
}
// </Snippet_AuthenticationLogging>
#endif
