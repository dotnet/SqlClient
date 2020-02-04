using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests
{
    public class SqlClientEventSourceTest
    {
        [Fact]
        public void IsTraceEnabled()
        {
            using (var listener = new SampleEventListener())
            {
                listener.EnableEvents(SqlClientEventSource.Log, EventLevel.Informational, SqlClientEventSource.Keywords.Trace);
                using (SqlConnection connection = new SqlConnection("Data Source=tcp:localhost;Database=Northwind;Integrated Security=true;"))
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

                //Check if we can disable the events
                listener.DisableEvents(SqlClientEventSource.Log);
                Assert.False(SqlClientEventSource.Log.IsEnabled());

                //Check if we are able to enable events again
                listener.EnableEvents(SqlClientEventSource.Log, EventLevel.Informational);
                Assert.True(SqlClientEventSource.Log.IsEnabled());
            }
        }
    }

    public class SampleEventListener : EventListener
    {
        public List<string> eventsNames = new List<string>();
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.Message != null)

                eventsNames.Add(eventData.EventName);
            else
            {
                eventsNames.Add(eventData.EventName);
            }
        }
    }
}
