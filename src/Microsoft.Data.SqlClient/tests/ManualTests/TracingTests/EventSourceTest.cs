// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            using (var TraceListener = new DataTestUtility.TraceEventListener())
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

                // Need to investigate better way of validating traces in sequential runs, 
                // For now we're collecting all traces to improve code coverage.

                Assert.All(TraceListener.IDs, item => { Assert.Contains(item, Enumerable.Range(1, 21)); });
            }
        }

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void EventSourceTestEnableDisable()
        {
            EventSource eventSource = null;

            //We have to have some enabled EventSource activities in order to asign the value to local eventSource.
            using (var TraceListener = new DataTestUtility.TraceEventListener())
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
                eventSource = TraceListener.EventSourceProperty;

                //Try to disable events
                TraceListener.DisableEvents(eventSource);

                //Try to see if any event is enabled
                Assert.False(eventSource.IsEnabled());
            }
        }
    }
}
