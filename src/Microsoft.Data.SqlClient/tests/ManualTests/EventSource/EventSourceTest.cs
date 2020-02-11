// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.EventSource
{
    [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "Not Implemented")]
    public class EventSourceTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void IsTraceEnabled()
        {
            using (var listener = new SampleEventListener())
            {
                using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("SELECT * From [Customers]", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }

                //Check if all the events are from Trace
                foreach (var item in listener.eventsNames)
                {
                    Assert.Contains("Trace", item);
                }
#if net46
                //Check if we can disable the events
                listener.DisableEvents(SqlClientEventSource.Log);

                Assert.False(SqlClientEventSource.Log.IsEnabled());

                //Check if we are able to enable events again
                listener.EnableEvents(SqlClientEventSource.Log, EventLevel.Informational);
                Assert.True(SqlClientEventSource.Log.IsEnabled());
#endif
            }
        }
    }

    public class SampleEventListener : EventListener
    {
        public List<string> eventsNames = new List<string>();
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Message != null)
            {
                eventsNames.Add(eventData.EventName);
            }
            else
            {
                eventsNames.Add(eventData.EventName);
            }
        }
    }
}
