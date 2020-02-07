// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/SqlClientEventSource/*'/>
    [EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
    public class SqlClientEventSource : EventSource
    {
        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/Log/*'  />
        public static readonly SqlClientEventSource Log = new SqlClientEventSource();
        private static long s_nextScopeId = 0;
        private static long s_nextNotificationScopeId = 0;

        private const int TraceEventId = 1;
        private const int EnterScopeId = 2;
        private const int ExitScopeId = 3;
        private const int TraceBinId = 4;
        private const int CorrelationTraceId = 5;
        private const int NotificationsScopeEnterId = 6;
        private const int NotificationsTraceId = 7;
        private const int PoolerScopeEnterId = 8;
        private const int PoolerTraceId = 9;

        /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/Keywords/*'/>
        public class Keywords
        {
            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/Trace/*'/>
            public const EventKeywords Trace = (EventKeywords)1;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/Scope/*' />
            public const EventKeywords Scope = (EventKeywords)2;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/NotificationTrace/*' />
            public const EventKeywords NotificationTrace = (EventKeywords)4;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/Pooling/*' />
            public const EventKeywords Pooling = (EventKeywords)8;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/Correlation/*' />
            public const EventKeywords Correlation = (EventKeywords)16;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/NotificationScope/*' />
            public const EventKeywords NotificationScope = (EventKeywords)32;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/PoolerScope/*' />
            public const EventKeywords PoolerScope = (EventKeywords)64;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/PoolerTrace/*' />
            public const EventKeywords PoolerTrace = (EventKeywords)128;

            /// <include file='../../../../../../../doc/snippets/Microsoft.Data.SqlClient/SqlClientEventSource.xml' path='docs/members[@name="SqlClientEventSource"]/members[@name="Keywords"]/Advanced/*' />
            public const EventKeywords Advanced = (EventKeywords)512;
        }

        [NonEvent]
        internal bool IsTraceEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Trace);

        [NonEvent]
        internal bool IsScopeEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Scope);

        [NonEvent]
        internal bool IsPoolerScopeEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.PoolerScope);

        [NonEvent]
        internal bool IsCorrelationEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Correlation);

        [NonEvent]
        internal bool IsNotificationScopeEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.NotificationScope);

        [NonEvent]
        internal bool IsPoolingEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Pooling);

        [NonEvent]
        internal bool IsNotificationTraceEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.NotificationTrace);

        [NonEvent]
        internal bool IsPoolerTraceEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.PoolerTrace);

        [NonEvent]
        internal bool IsAdvanceTraceOn() => SqlClientEventSource.Log.IsEnabled(EventLevel.LogAlways, Keywords.Advanced);

        [Event(TraceEventId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void Trace(string message)
        {
            WriteEvent(TraceEventId, message);
        }

        [Event(EnterScopeId, Level = EventLevel.Verbose, Keywords = Keywords.Scope)]
        internal long ScopeEnter(string message)
        {
            long scopeId = 0;
            if (SqlClientEventSource.Log.IsEnabled())
            {
                StringBuilder MsgstrBldr = new StringBuilder(message);
                scopeId = Interlocked.Increment(ref s_nextScopeId);
                WriteEvent(EnterScopeId, MsgstrBldr.Append($" Scope ID ='[{ scopeId}]'"));
            }
            return scopeId;
        }

        [Event(ExitScopeId, Level = EventLevel.Verbose, Keywords = Keywords.Scope)]
        internal void ScopeLeave(long scopeId)
        {
            if (SqlClientEventSource.Log.IsEnabled())
            {
                WriteEvent(ExitScopeId, scopeId);
            }
        }

        [Event(TraceBinId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void TraceBin(string message, byte[] whereabout, int length)
        {
            if (SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Trace))
            {
                WriteEvent(TraceBinId, message, whereabout, length);
            }
        }

        [Event(CorrelationTraceId, Level = EventLevel.Informational, Keywords = Keywords.Correlation, Opcode = EventOpcode.Start)]
        internal void CorrelationTrace(string message)
        {
            WriteEvent(CorrelationTraceId, message);
        }

        [Event(NotificationsScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.NotificationScope)]
        internal long NotificationsScopeEnter(string message)
        {
            long scopeId = 0;
            if (SqlClientEventSource.Log.IsEnabled())
            {
                StringBuilder MsgstrBldr = new StringBuilder(message);
                scopeId = Interlocked.Increment(ref s_nextNotificationScopeId);
                WriteEvent(NotificationsScopeEnterId, MsgstrBldr.Append($", Scope ID ='[{ scopeId}]'"));
            }
            return scopeId;
        }

        [Event(PoolerScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.PoolerScope)]
        internal long PoolerScopeEnter(string message)
        {
            long scopeId = 0;
            if (SqlClientEventSource.Log.IsEnabled())
            {
                StringBuilder MsgstrBldr = new StringBuilder(message);
                WriteEvent(PoolerScopeEnterId, MsgstrBldr.Append($", Scope ID ='[{ scopeId}]'"));
            }
            return scopeId;
        }

        [Event(NotificationsTraceId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void NotificationsTrace(string message)
        {
            WriteEvent(NotificationsTraceId, message);
        }

        [Event(PoolerTraceId, Level = EventLevel.Informational, Keywords = Keywords.PoolerTrace)]
        internal void PoolerTrace(string message)
        {
            WriteEvent(PoolerTraceId, message);
        }
    }
}
