// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using static Microsoft.Data.SqlClient.SqlClientEventSource;


namespace Microsoft.Data.SqlClient.ManualTesting.Tests.EventSourceTest
{
    public class EventSourceListenerGeneral : EventListener
    {
        TextWriter Out = Console.Out;
        public EventLevel Level { get; set; }
        public EventKeywords Keyword { get; set; }

        List<string> events = new List<string>();

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {

            events= eventData.Payload != null ? eventData.Payload.Select(o => o.ToString()).ToList() : null;
        }
    }

    public class SqlClientEventSourceTest
    {
        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp, "Not Implemented")]
        [Fact]
        public void TestSqlClientEventSourceName()
        {
            Assert.Equal("Microsoft.Data.SqlClient.EventSource", SqlClientEventSource.Log.Name);
        }

        [Theory]
        [InlineData(new int[] { 1, 2, 4, 8, 16 })]
        public void TestEventKeywords(int[] values)
        {
            using (EventSourceListenerGeneral listener = new EventSourceListenerGeneral())
            {
                listener.Keyword = (EventKeywords)values[0];
                listener.Level = EventLevel.Informational;
                //Check if Log is disabled by  default
                Assert.True(!Log.IsEnabled());

                listener.EnableEvents(Log, EventLevel.Informational);

                //Check if Enabling Log is able.
                var task = Task.Run(() =>
                {
                    listener.EnableEvents(Log, EventLevel.Informational);
                });
                task.ContinueWith((t) =>
                {
                    Assert.True(Log.IsEnabled());
                });

                //Check if enabling Log for Eventkeyword Trace returns true.
                listener.EnableEvents(Log, EventLevel.Informational, SqlClientEventSourceKeywords.NotificationTrace);

                Assert.True(Log.IsEnabled());
                
            }
        }
    }
}
