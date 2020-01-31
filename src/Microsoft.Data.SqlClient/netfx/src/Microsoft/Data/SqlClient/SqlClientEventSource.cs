// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Runtime.CompilerServices;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// 
    /// </summary>
    public class SqlClientEventSourceKeywords
    {
        internal const EventKeywords Trace = (EventKeywords)0x0001;                 // Integer 1
        internal const EventKeywords Scope = (EventKeywords)0x0002;                 // Integer 2
        internal const EventKeywords NotificationTrace = (EventKeywords)0x0004;     // Integer 4
        internal const EventKeywords Pooling = (EventKeywords)0x0008;               // Integer 8
        internal const EventKeywords Correlation = (EventKeywords)0x0010;           // Integer 16
        internal const EventKeywords NotificationScope = (EventKeywords)0x0020;     // Integer 32
        internal const EventKeywords PoolerScope = (EventKeywords)0X0040;           // Integer 64
        internal const EventKeywords StringPrintOut = (EventKeywords)0x0080;        // Integer 128
        internal const EventKeywords PoolerTrace = (EventKeywords)0x0200;           //Integer 512

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static EventKeywords GetAll()
        {
            return Trace | Scope | NotificationTrace | Pooling | Correlation | NotificationScope | PoolerScope | StringPrintOut;
        }
    }

    [EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
    internal class SqlClientEventSource : EventSource
    {
        internal static readonly SqlClientEventSource Log = new SqlClientEventSource();
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
        private const int PutStrId = 10;

        //Any Prropery added to this class should be a power of 2.

        [NonEvent]
        internal bool IsTraceEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.Trace);

        [NonEvent]
        internal bool IsScopeEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.Scope);

        [NonEvent]
        internal bool IsPoolerScopeEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.PoolerScope);

        [NonEvent]
        internal bool IsCorrelationEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.Correlation);

        [NonEvent]
        internal bool IsNotificationScopeEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.NotificationScope);

        [NonEvent]
        internal bool IsPoolingEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.Pooling);

        [NonEvent]
        internal bool IsNotificationTraceEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.NotificationTrace);

        [NonEvent]
        internal bool IsPoolerTraceEnabled() => Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.PoolerTrace);

        [NonEvent]
        internal bool IsAdvanceTraceOn() => Log.IsEnabled(EventLevel.LogAlways, EventKeywords.All);


        [Event(TraceEventId, Level = EventLevel.Informational, Channel = EventChannel.Debug, Keywords = SqlClientEventSourceKeywords.Trace)]
        internal void Trace(string message)
        {
            WriteEvent(TraceEventId, message);
        }

        [Event(EnterScopeId, Level = EventLevel.Verbose, Keywords = SqlClientEventSourceKeywords.Scope)]
        internal long ScopeEnter(string message)
        {
            StringBuilder MsgstrBldr = new StringBuilder(message);
            long scopeId = 0;

            if (Log.IsEnabled())
            {
                scopeId = Interlocked.Increment(ref s_nextScopeId);
                WriteEvent(EnterScopeId, MsgstrBldr.Append($" Scope ID ='[{ scopeId}]'"));
            }
            return scopeId;
        }

        [Event(ExitScopeId, Level = EventLevel.Verbose, Keywords = SqlClientEventSourceKeywords.Scope)]
        internal void ScopeLeave(long scopeId)
        {
            if (!Log.IsEnabled())
            {
                WriteEvent(ExitScopeId, scopeId);
            }
            
        }

        [Event(TraceBinId, Level = EventLevel.Informational, Keywords = SqlClientEventSourceKeywords.Trace)]
        internal void TraceBin(string message, byte[] whereabout, int length)
        {
            if (Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.Trace))
            {
                WriteEvent(TraceBinId, message, whereabout, length);
            }
        }

        [Event(CorrelationTraceId, Level = EventLevel.Informational, Keywords = SqlClientEventSourceKeywords.Correlation, Opcode = EventOpcode.Start)]
        internal void CorrelationTrace(string message)
        { 
            WriteEvent(CorrelationTraceId, message);
        }

        [Event(NotificationsScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = SqlClientEventSourceKeywords.NotificationScope)]
        internal long NotificationsScopeEnter(string message)
        {
            long scopeId = 0;
            if (Log.IsEnabled())
            {
                StringBuilder MsgstrBldr = new StringBuilder(message);
                scopeId = Interlocked.Increment(ref s_nextNotificationScopeId);
                WriteEvent(NotificationsScopeEnterId, MsgstrBldr.Append($" Scope ID ='[{ scopeId}]'"));
            }
            return scopeId;
        }

        [Event(PoolerScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = SqlClientEventSourceKeywords.PoolerScope)]
        internal long PoolerScopeEnter(string message)
        {
            long scopeId = 0;
            if (Log.IsEnabled())
            {
                StringBuilder MsgstrBldr = new StringBuilder(message);
                WriteEvent(PoolerScopeEnterId, MsgstrBldr.Append($" Scope ID ='[{ scopeId}]'"));
            }
            return scopeId;
        }

        [Event(NotificationsTraceId, Level = EventLevel.Informational, Keywords = SqlClientEventSourceKeywords.Trace)]
        internal void NotificationsTrace(string message)
        {
            WriteEvent(PoolerScopeEnterId, message);
        }

        [Event(PoolerTraceId, Level = EventLevel.Informational, Keywords = SqlClientEventSourceKeywords.PoolerTrace)]
        internal void PoolerTrace(string message)
        {
            WriteEvent(PoolerTraceId, message);
        }

        [Event(PutStrId, Level = EventLevel.Informational, Keywords = SqlClientEventSourceKeywords.StringPrintOut)]
        internal void PutStr(string message)
        {
            if (Log.IsEnabled(EventLevel.Informational, SqlClientEventSourceKeywords.StringPrintOut))
            {
                WriteEvent(PutStrId, message);
            }
        }
    }
}
