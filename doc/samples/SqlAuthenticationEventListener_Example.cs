// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Requires Microsoft.Data.SqlClient.Extensions.Azure with AuthenticationTrace support.
// Collects authentication diagnostics in process without an external tracing tool.
// <Snippet_AuthenticationEventListener>
using System;
using System.Diagnostics.Tracing;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlAuthenticationEventListenerExample
{
    public static class Sample
    {
        /// <summary>Collects authentication diagnostics in process for the duration of the connection attempt.</summary>
        public static async Task RunAsync()
        {
            string connectionString = Environment.GetEnvironmentVariable("SQLCLIENT_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "Set SQLCLIENT_CONNECTION_STRING to an Entra ID connection string.");
            }

            using var listener = new SqlAuthenticationEventSourceListener();
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
        }

        /// <summary>Collects only AuthenticationTrace events from existing and future SqlClient sources.</summary>
        private sealed class SqlAuthenticationEventSourceListener : EventListener
        {
            private const EventKeywords AuthenticationTrace = (EventKeywords)0x2000;
            private EventSource _source;

            /// <inheritdoc />
            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                if (eventSource.Name == "Microsoft.Data.SqlClient.EventSource")
                {
                    _source = eventSource;
                    EnableEvents(eventSource, EventLevel.Verbose, AuthenticationTrace);
                }
            }

            /// <summary>Writes the message, origin, and connection identifier supplied by authentication events.</summary>
            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                if (eventData.Payload?.Count == 3)
                {
                    Console.WriteLine(
                        $"[{eventData.Level}] [{eventData.Payload[1]}] [{eventData.Payload[2]}] {eventData.Payload[0]}");
                }
                else
                {
                    Console.WriteLine($"[{eventData.Level}] {eventData.EventName}");
                }
            }

            /// <summary>Explicitly disables collection before detaching, including on .NET Framework.</summary>
            public override void Dispose()
            {
                if (_source != null)
                {
                    DisableEvents(_source);
                }
                base.Dispose();
            }
        }
    }
}
// </Snippet_AuthenticationEventListener>
