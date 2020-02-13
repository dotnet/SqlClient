using System;
using System.Data;
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient;

// <Snippet1>
class Program
{
    static void Main()
    {
        static void Main(string[] args)
        {
            using (var listener = new SampleEventListener())
            {
                //This Enables events for Keywords.Tracing
                //We also can select EventKeywords.All to get all possible available events.
                listener.EnableEvents(SqlClientEventSource.Log, EventLevel.Informational, SqlClientEventSource.Keywords.Trace);
                using (SqlConnection connection = new SqlConnection("Data Source=tcp:localhost;Database=Northwind;Integrated Security=SSIP;"))
                {
                    connection.Open();
                    using (SqlCommand command = new SqlCommand("SELECT * From [Customers]", connection))
                    {
                        command.ExecuteNonQuery();
                    }
                }
            }
        }
    }
}

public class SampleEventListener : EventListener
{
    static TextWriter Out = Console.Out;

    /// <summary>
    /// We override this method to get a callback on every event.
    /// </summary>
    /// <param name="eventData"></param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // report all event information
        Out.Write("  Event {0} ", eventData.EventName);

        // Events can have formatting strings 'the Message property on the 'Event' attribute.  
        // If the event has a formatted message, print that, otherwise print out argument values.  
        if (eventData.Message != null)
            Out.WriteLine(eventData.Message, eventData.Payload.ToArray());
        else
        {
            string[] sargs = eventData.Payload != null ? eventData.Payload.Select(o => o.ToString()).ToArray() : null;
            Out.WriteLine("({0}).", sargs != null ? string.Join(", ", sargs) : "");
        }
    }
}
// </Snippet1>
