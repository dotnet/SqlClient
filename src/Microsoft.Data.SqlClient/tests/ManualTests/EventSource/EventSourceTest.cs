// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Data.SqlClient.SqlClientEventSource;


namespace Microsoft.Data.SqlClient.ManualTesting.Tests.EventSourceTest
{
    public class EventSourceListenerGeneral : EventListener
    {
        public EventLevel Level { get; set; }
        public EventKeywords Keyword { get; set; }

        public List<object> events = new List<object>();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            events = eventData.Payload.ToList();
        }
    }
    [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "Not Implemented")]

    public class SqlClientEventSourceTest
    {
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp,"Not Implemented")]
        [Fact]
        public void TestSqlClientEventSourceName()
        {
            Assert.Equal("Microsoft.Data.SqlClient.EventSource", SqlClientEventSource.Log.Name);
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "Not Implemented")]
        [Theory]
        [InlineData(new int[] { 1, 2, 4, 8, 16 })]
        public async void TestEventKeywords(int[] values)
        {
            using (EventSourceListenerGeneral listener = new EventSourceListenerGeneral())
            {
                listener.Keyword = (EventKeywords)values[0];
                listener.Level = EventLevel.Informational;
                bool status = false;

                //Events should be disabled by default
                Assert.False(Log.IsEnabled());
            }
        }
    }
}
