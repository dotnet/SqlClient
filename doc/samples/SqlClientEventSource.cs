// <Snippet1>
using System;
using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient;

// This listener class will listen for events from the SqlClientEventSource class.
// SqlClientEventSource is an implementation of the EventSource class which gives 
// it the ability to create events.
public class SqlClientListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // Only enable events from SqlClientEventSource.
        if (eventSource.Name.Equals("Microsoft.Data.SqlClient.EventSource"))
        {
            // Use EventKeyWord 2 to capture basic application flow events.
            // See the above table for all available keywords.
            EnableEvents(eventSource, EventLevel.Informational, (EventKeywords)2);
        }
    }

    // This callback runs whenever an event is written by SqlClientEventSource.
    // Event data is accessed through the EventWrittenEventArgs parameter.
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        // Print event data.
        Console.WriteLine(eventData.Payload[0]);
    }
}

class Program
{
    public static void Main()
    {
        // Create a new event listener.
        using (SqlClientListener listener = new SqlClientListener())
        {
            string connectionString = "Data Source=localhost; " +
                "Initial Catalog=AdventureWorks; Integrated Security=true";

            // Open a connection to the AdventureWorks database.
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                string sql = "SELECT * FROM Sales.Currency";
                SqlCommand command = new SqlCommand(sql, connection);

                // Perform a data operation on the server.
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Read the data.
                }
                reader.Close();
            }
        }
    }
}
// </Snippet1>
