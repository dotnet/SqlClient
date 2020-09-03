// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    /// <summary>
    /// This unit test is just valid for .NetCore 3.0 and above
    /// </summary>
    public class EventCounterTest
    {
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public void EventCounterTestAll()
        {
            var stringBuilder = new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString)
            {
                Pooling = true,
                MaxPoolSize = 20
            };

            using (var TraceListener = new TraceEventCounterListener())
            {
                OpenConnections(stringBuilder.ConnectionString);
                stringBuilder.Pooling = false;
                OpenConnections(stringBuilder.ConnectionString);

                Thread.Sleep(3000); // wait to complete sampling!
                Assert.All(TraceListener.EventCounters, item => Assert.True(item.Value > 0));
            }
        }

        private void OpenConnections(string cnnString)
        {
            List<Task> tasks = new List<Task>();

            Enumerable.Range(1, 100).ToList().ForEach(i =>
            {
                SqlConnection cnn = new SqlConnection(cnnString);
                cnn.Open();
                int x = i;
                tasks.Add(Task.Run(() => { Thread.Sleep(x); cnn.Close(); }));
            });
            Task.WhenAll(tasks).Wait();
        }
    }

    public class TraceEventCounterListener : EventListener
    {
        private const string Name = "Name";

        public Dictionary<string, long> EventCounters { get; private set; }

        public TraceEventCounterListener()
        {
            EventCounters = new Dictionary<string, long>
                {
                    { "active-hard-connections", 0 },
                    { "hard-connects", 0 },
                    { "hard-disconnects", 0 },
                    { "active-soft-connects", 0 },
                    { "soft-connects", 0 },
                    { "soft-disconnects", 0 },
                    { "number-of-non-pooled-connections", 0 },
                    { "number-of-pooled-connections", 0 },
                    { "number-of-active-connection-pool-groups", 0 },
                    { "number-of-inactive-connection-pool-groups", 0 },
                    { "number-of-active-connection-pools", 0 },
                    { "number-of-inactive-connection-pools", 0 },
                    { "number-of-active-connections", 0 },
                    { "number-of-free-connections", 0 },
                    { "number-of-stasis-connections", 0 },
                    { "number-of-reclaimed-connections", 0 }
                };
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
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

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            object counter = null;
            eventData.Payload.FirstOrDefault(p => p is IDictionary<string, object> x && x.TryGetValue(Name, out counter));
            if (counter is string cntName && EventCounters.ContainsKey(cntName))
            {
                EventCounters[cntName] += 1;
            }
        }
    }
}
