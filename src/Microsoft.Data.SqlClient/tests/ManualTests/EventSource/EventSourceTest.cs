// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class EventSourceTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventSourceTestAll()
        {
            using (var TraceListener = new TraceEventListener())
            {
                using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("SELECT * From [Customers]", connection))
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            // Flush data
                        }
                    }
                }

                // Need to investigate better way of collecting traces in sequential runs, 
                // For now we're collecting all traces to improve code coverage.
#if NETCOREAPP
                Assert.Equal(226, TraceListener.IDs.Count);
#elif NETFX
                Assert.Equal(250, TraceListener.IDs.Count);
#elif UNIX
                Assert.Equal(226, TraceListener.IDs.Count);
#endif
                Assert.All(TraceListener.IDs, item => { Assert.Contains(item, Enumerable.Range(1, 21)); });
            }
        }
    }

    public class TraceEventListener : EventListener
    {
        public List<int> IDs = new List<int>();

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource"))
            {
                // Collect all traces for better code coverage
                EnableEvents(eventSource, EventLevel.Informational, 0);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            IDs.Add(eventData.EventId);
        }
    }
}
