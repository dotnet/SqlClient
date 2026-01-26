// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class EventSourceTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventSourceTestAll()
        {
            using DataTestUtility.MDSEventListener traceListener = new();

            using SqlConnection connection = new(DataTestUtility.TCPConnectionString);
            connection.Open();

            using SqlCommand command = new("SELECT @@VERSION", connection);
            using SqlDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                // Flush data
            }

            // TODO: Need to investigate better way of validating traces in sequential runs, for now we're collecting all traces to improve code coverage.

            // Assert
            // - Collected trace event IDs are in the range of official trace event IDs
            // @TODO: This is brittle, refactor the SqlClientEventSource code so the event IDs it can throw are accessible here
            HashSet<int> acceptableEventIds = new HashSet<int>(Enumerable.Range(0, 21));
            foreach (int id in traceListener.IDs)
            {
                Assert.Contains(id, acceptableEventIds);
            }
        }
    }
}
