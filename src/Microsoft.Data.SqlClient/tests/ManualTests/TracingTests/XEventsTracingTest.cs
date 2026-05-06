// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;
using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    // XEvent sessions may become orphaned on the Azure SQL Server, which leads to poor performance
    // (query timeouts, deadlocks, etc) over time.  This class is instantiated once per test run and
    // drops these orphaned sessions as part of every run to help mitigate this issue.
    public class XEventCleaner
    {
        public XEventCleaner()
        {
            if (DataTestUtility.AreConnStringsSetup() &&
                DataTestUtility.IsNotAzureSynapse() &&
                DataTestUtility.IsNotManagedInstance())
            {
                using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
                connection.Open();

                // Identify orphaned event sessions and generate DROP commands.
                using SqlCommand command = new(
                    """
                    SELECT Sessions.name
                    FROM sys.database_event_sessions Sessions
                    LEFT JOIN sys.dm_xe_database_sessions Active ON Sessions.name = Active.name
                    WHERE Active.name IS NULL;
                    """,
                    connection);

                HashSet<string> orphanedSessions = new();
                try
                {
                    using SqlDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        string sessionName = reader.GetString(0);
                        orphanedSessions.Add(sessionName);
                    }
                }
                catch (SqlException)
                {
                    // Ignore exceptions - the next test run will attempt the cleanup.
                }

                if (orphanedSessions.Count == 0)
                {
                    return;
                }

                Console.WriteLine($"Identified {orphanedSessions.Count} orphaned XEvent sessions:");

                // Drop them one at a time.
                foreach (string sessionName in orphanedSessions)
                {
                    using SqlCommand dropCommand = new(
                        $"DROP EVENT SESSION [{sessionName}] ON DATABASE;",
                        connection);

                    try
                    {
                        dropCommand.ExecuteNonQuery();
                    }
                    catch (SqlException)
                    {
                        // Ignore exceptions, as the session may have been cleaned up by another
                        // test run at the same time.
                    }

                    Console.WriteLine($"  Dropped orphaned XEvent session: {sessionName}");
                }
            }
        }
    }

    // This empty class is required by xUnit to tie the cleaner to the test classes.
    [CollectionDefinition("XEventCleaner")]
    public class XEventCleanerCollection : ICollectionFixture<XEventCleaner>
    {
    }

    [Collection("XEventCleaner")]
    public class XEventsTracingTest
    {
        private readonly string _testName;

        public XEventsTracingTest(ITestOutputHelper outputHelper)
        {
            _testName = DataTestUtility.CurrentTestName(outputHelper);
        }

        [Trait("Category", "flaky")]
        [ConditionalTheory(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        [InlineData("SELECT @@VERSION", System.Data.CommandType.Text, "sql_statement_starting")]
        [InlineData("sp_help", System.Data.CommandType.StoredProcedure, "rpc_starting")]
        public void XEventActivityIDConsistentWithTracing(string query, System.Data.CommandType commandType, string xEvent)
        {
            // This test validates that the activity ID recorded in the client-side trace is passed through to the server,
            // where it can be recorded in an XEvent session. This is documented at:
            // https://learn.microsoft.com/en-us/sql/relational-databases/native-client/features/accessing-diagnostic-information-in-the-extended-events-log

            using SqlConnection activityConnection = new(DataTestUtility.TCPConnectionString);
            activityConnection.Open();

            Guid connectionId = activityConnection.ClientConnectionId;
            HashSet<string> ids;

            using SqlConnection xEventManagementConnection = new(DataTestUtility.TCPConnectionString);
            xEventManagementConnection.Open();

            using XEventScope xEventSession = new(
                _testName,
                xEventManagementConnection,
                $@"ADD EVENT SQL_STATEMENT_STARTING (ACTION (client_connection_id) WHERE (client_connection_id='{connectionId}')),
                    ADD EVENT RPC_STARTING (ACTION (client_connection_id) WHERE (client_connection_id='{connectionId}'))",
                "ADD TARGET ring_buffer");

            using (DataTestUtility.MDSEventListener TraceListener = new())
            {
                using SqlCommand command = new(query, activityConnection) { CommandType = commandType };
                using SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    // Flush data
                }

                ids = TraceListener.ActivityIDs;
            }

            XmlDocument eventList = xEventSession.GetEvents();
            // Get the associated activity ID from the XEvent session. We expect to see the same ID in the trace as well.
            string activityId = GetCommandActivityId(query, xEvent, connectionId, eventList);

            Assert.Contains(activityId, ids);
        }

        private static string GetCommandActivityId(string commandText, string eventName, Guid connectionId, XmlDocument xEvents)
        {
            XPathNavigator? xPathRoot = xEvents.CreateNavigator();
            Assert.NotNull(xPathRoot);

            // The transferred activity ID is attached to the "attach_activity_id_xfer" action within
            // the "sql_statement_starting" and the "rpc_starting" events.
            XPathNodeIterator statementStartingQuery = xPathRoot.Select(
                $"/RingBufferTarget/event[@name='{eventName}'"
                + $" and action[@name='client_connection_id']/value='{connectionId.ToString().ToUpper()}'"
                + $" and (data[@name='statement']='{commandText}' or data[@name='object_name']='{commandText}')]");

            Assert.Equal(1, statementStartingQuery.Count);
            Assert.True(statementStartingQuery.MoveNext());

            XPathNavigator? current = statementStartingQuery.Current;
            Assert.NotNull(current);
            XPathNavigator? activityIdElement = current.SelectSingleNode("action[@name='attach_activity_id_xfer']/value");

            Assert.NotNull(activityIdElement);
            Assert.NotNull(activityIdElement.Value);

            return activityIdElement.Value;
        }
    }
}
