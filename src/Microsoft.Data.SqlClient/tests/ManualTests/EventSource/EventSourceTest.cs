// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "Not Implemented")]
    public class EventSourceTest
    {
        List<int> ids = new List<int>();

        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventTraceTests()
        {
            GetIds();
            //Trace EventKeyword is 3.
            //We did not want to break the SqlEventSource for BeginExexute and EndExecute
            // BeginExexute and EndExecute are Enabled when any kind of Event logging is enabled, so we check for those values as well.
            Assert.All(ids, item => { Assert.True(3 == item || 1 == item || item == 2); });
        }

        private void GetIds()
        {
            using (var TraceListener = new TraceEventListener())
            {
                using (SqlConnection connection = new SqlConnection(DataTestUtility.TCPConnectionString))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("SELECT * From [Customers]", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                    ids = TraceListener.IDs;
                }
                ids = TraceListener.IDs;
            }
        }
    }

    [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "Not Implemented")]
    public class TraceEventListener : EventListener
    {
        public List<int> IDs = new List<int>();
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource"))
            {
                EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)1);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            IDs.Add(eventData.EventId);
        }
    }
}
