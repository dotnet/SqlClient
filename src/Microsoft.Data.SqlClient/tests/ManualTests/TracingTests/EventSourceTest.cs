// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public class EventSourceTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventSourceTestAll()
        {
            using DataTestUtility.MDSEventListener TraceListener = new();
            using (SqlConnection connection = new(DataTestUtility.TCPConnectionString))
            {
                connection.Open();
                using SqlCommand command = new("SELECT @@VERSION", connection);
                using SqlDataReader reader = command.ExecuteReader();
                reader.FlushResultSet();
            }

            // Need to investigate better way of validating traces in sequential runs, 
            // For now we're collecting all traces to improve code coverage.

            Assert.All(TraceListener.IDs, item => { Assert.Contains(item, Enumerable.Range(1, 21)); });
        }
    }
}
