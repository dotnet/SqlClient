// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.XPath;
using Microsoft.Data.SqlClient.Tests.Common;
using Microsoft.Data.SqlClient.Tests.Common.Fixtures.DatabaseObjects;
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

    /// <summary>
    /// These tests validate that activity IDs are properly transferred to the server and recorded in XEvent sessions,
    /// even in error scenarios. This is important to ensure that customers can rely on activity IDs being present in
    /// XEvent sessions for troubleshooting and correlation purposes.
    /// </summary>
    [Collection("XEventCleaner")]
    public class XEventsTracingTest
    {
        private const int CustomErrorNumber = 50001;

        /// <summary>
        /// Verifies that the attach_activity_id_xfer in the 'rpc_starting' extended event is consistent with the tracing
        /// context when executing a stored procedure.
        /// </summary>
        /// <remarks>
        /// This test has historically been marked as flaky following occasional failures due to deadlocks on the remote
        /// SQL Server instance. This first became apparent when running 'sp_help' on an Azure SQL instance under load.
        /// This appears to have been resolved by switching to a temporary stored procedure which consists of a simple
        /// 'SELECT 1' statement.
        /// </remarks>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        public void XEventActivityIDConsistentWithTracing_RpcStarting()
        {
            using SqlConnection managementConnection = new(DataTestUtility.TCPConnectionString);
            using StoredProcedure sp = new(managementConnection, nameof(XEventActivityIDConsistentWithTracing_RpcStarting), " AS SELECT 1 AS [Field1];");

            // Our stored procedure name is an escaped SQL Server object name. This will not match the object_name data
            // in the XEvent XML, which records it as an unescaped name.
            string unescapedProcedureName = sp.Name.Substring(1, sp.Name.Length - 2).Replace("]]", "]");

            VerifyXEventActivityIDConsistentWithTracing(unescapedProcedureName, System.Data.CommandType.StoredProcedure, "rpc_starting");
        }

        /// <summary>
        /// Verifies that the attach_activity_id_xfer in the 'sql_statement_starting' extended event is consistent with the tracing
        /// context when executing a SQL statement which is not a stored procedure.
        /// </summary>
        /// <remarks>
        /// This test has historically been marked as flaky following occasional failures due to deadlocks on the remote
        /// SQL Server instance. This first became apparent when running 'SELECT @@VERSION' on an Azure SQL instance under load.
        /// This appears to have been resolved by switching to a simpler 'SELECT 1' statement.
        /// </remarks>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        public void XEventActivityIDConsistentWithTracing_SqlStatementStarting() =>
            VerifyXEventActivityIDConsistentWithTracing("SELECT 1 AS [Field1]", System.Data.CommandType.Text, "sql_statement_starting");

        /// <summary>
        /// Validates that the activity ID is consistently recorded in an XEvent session even when a command generates an error.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse), nameof(DataTestUtility.IsNotManagedInstance))]
        public void XEventActivityIDConsistentWithTracing_ActivityIDTransferOnError() =>
            VerifyXEventActivityIDConsistentWithTracing($"THROW {CustomErrorNumber}, 'Sample message', 0", System.Data.CommandType.Text, "sql_statement_starting");

        private void VerifyXEventActivityIDConsistentWithTracing(string query, System.Data.CommandType commandType, string xEvent,
            [CallerMemberName] string testName = "")
        {
            // This method validates that the activity ID recorded in the client-side trace is passed through to the server,
            // where it can be recorded in an XEvent session. This is documented at:
            // https://learn.microsoft.com/en-us/sql/relational-databases/native-client/features/accessing-diagnostic-information-in-the-extended-events-log

            using SqlConnection activityConnection = new(DataTestUtility.TCPConnectionString);
            activityConnection.Open();

            Guid connectionId = activityConnection.ClientConnectionId;
            HashSet<string> ids;

            using SqlConnection xEventManagementConnection = new(DataTestUtility.TCPConnectionString);
            xEventManagementConnection.Open();

            using XEventScope xEventSession = new(
                testName,
                xEventManagementConnection,
                $@"ADD EVENT SQL_STATEMENT_STARTING (ACTION (client_connection_id) WHERE (client_connection_id='{connectionId}')),
                    ADD EVENT RPC_STARTING (ACTION (client_connection_id) WHERE (client_connection_id='{connectionId}'))",
                "ADD TARGET ring_buffer");

            using (DataTestUtility.MDSEventListener TraceListener = new())
            {
                try
                {
                    using SqlCommand command = new(query, activityConnection) { CommandType = commandType };
                    using SqlDataReader reader = command.ExecuteReader();

                    reader.FlushResultSet();
                }
                // We only want to catch and swallow our custom user error number. Error number 1205 should continue to cause the
                // test to fail, as it indicates a deadlock.
                // @TODO: If deadlocks continue to cause test failures, we should record the xml_deadlock_report extended event to
                // capture more information about the deadlock and output it to the test logs for analysis.
                catch (SqlException sqlEx) when (sqlEx.Number == CustomErrorNumber)
                { }

                ids = TraceListener.ActivityIDs;
            }

            XmlDocument eventList = xEventSession.GetEvents();
            // Get the associated activity ID from the XEvent session. We expect to see the same ID in the trace as well.
            string activityId = GetCommandActivityId(query, xEvent, connectionId, eventList);

            Assert.Contains(activityId, ids);
        }

        private static string GetCommandActivityId(string commandText, string eventName, Guid connectionId, XmlDocument xEvents)
        {
            // We manually build the XPath query and cannot escape quotes, so disallow them from the command text to simplify
            // the test.
            Assert.DoesNotContain("\"", commandText);

            XPathNavigator? xPathRoot = xEvents.CreateNavigator();
            Assert.NotNull(xPathRoot);

            // The transferred activity ID is attached to the "attach_activity_id_xfer" action within
            // the "sql_statement_starting" and the "rpc_starting" events.
            XPathNodeIterator statementStartingQuery = xPathRoot.Select(
                $"/RingBufferTarget/event[@name=\"{eventName}\""
                + $" and action[@name=\"client_connection_id\"]/value=\"{connectionId.ToString().ToUpper()}\""
                + $" and (data[@name=\"statement\"]=\"{commandText}\" or data[@name=\"object_name\"]=\"{commandText}\")]");

            Assert.Equal(1, statementStartingQuery.Count);
            Assert.True(statementStartingQuery.MoveNext());

            XPathNavigator? current = statementStartingQuery.Current;
            Assert.NotNull(current);
            XPathNavigator? activityIdElement = current.SelectSingleNode("action[@name=\"attach_activity_id_xfer\"]/value");

            Assert.NotNull(activityIdElement);
            Assert.NotNull(activityIdElement.Value);

            return activityIdElement.Value;
        }
    }
}
