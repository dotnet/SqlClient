// <Snippet1>
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;

// This listener class will listen for events from the SqlClientEventSource class.
// SqlClientEventSource is an implementation of the EventSource class which gives 
// it the ability to create events.
public class EventCounterListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // Only enable events from SqlClientEventSource.
        if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource"))
        {
            var options = new Dictionary<string, string>();
            // define time interval 1 second
            // without defining this parameter event counters will not enabled
            options.Add("EventCounterIntervalSec", "1");
            // enable for the None keyword
            EnableEvents(eventSource, EventLevel.Informational, EventKeywords.None, options);
        }
    }

    // This callback runs whenever an event is written by SqlClientEventSource.
    // Event data is accessed through the EventWrittenEventArgs parameter.
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.Payload.FirstOrDefault(p => p is IDictionary<string, object> x && x.ContainsKey("Name")) is IDictionary<string, object> counters)
        {
            if (counters.TryGetValue("DisplayName", out object name) && name is string cntName
                && counters.TryGetValue("Mean", out object value) && value is double cntValue)
            {
                // print event counter's name and mean value
                Console.WriteLine($"{cntName}\t\t{cntValue}");
            }
        }
    }
}

class Program
{
    static void Main(string[] args)
    {
        // Create a new event listener
        using (var listener = new EventCounterListener())
        {
            string connectionString = "Data Source=localhost; Integrated Security=true";

            for (int i = 0; i < 50; i++)
            {
                // Open a connection
                SqlConnection cnn = new SqlConnection(connectionString);
                cnn.Open();
                // wait for sampling interval happens
                System.Threading.Thread.Sleep(500);
            }
        }
    }
}
// </Snippet1>
