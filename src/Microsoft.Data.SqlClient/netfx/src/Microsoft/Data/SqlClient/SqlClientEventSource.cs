// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    [EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
    internal class SqlClientEventSource : EventSource
    {
        internal static readonly SqlClientEventSource Log = new SqlClientEventSource();
        private static long s_nextScopeId = 0;
        private static long s_nextNotificationScopeId = 0;
        private static long s_nextPoolerScopeId = 0;

        private const int TraceEventId = 1;
        private const int EnterScopeId = 2;
        private const int ExitScopeId = 3;
        private const int TraceBinId = 4;
        private const int CorrelationTraceId = 5;
        private const int NotificationsScopeEnterId = 6;
        private const int NotificationsTraceId = 7;
        private const int PoolerScopeEnterId = 8;
        private const int PoolerTraceId = 9;

        //if we add more keywords they need to be a power of 2.
        //Keyword class needs to be nested inside this class otherwise nothing will be logged as the SqlClientEventSource wont have contorol over that.
        #region Keywords
        internal class Keywords
        {
            internal const EventKeywords Trace = (EventKeywords)1;

            internal const EventKeywords Scope = (EventKeywords)2;

            internal const EventKeywords NotificationTrace = (EventKeywords)4;

            internal const EventKeywords Pooling = (EventKeywords)8;

            internal const EventKeywords Correlation = (EventKeywords)16;

            internal const EventKeywords NotificationScope = (EventKeywords)32;

            internal const EventKeywords PoolerScope = (EventKeywords)64;

            internal const EventKeywords PoolerTrace = (EventKeywords)128;

            internal const EventKeywords Advanced = (EventKeywords)256;

            internal const EventKeywords StateDump = (EventKeywords)512;
        }
        #endregion

        #region Enable/Disable Events
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
        internal bool IsAdvanceTraceOn() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Advanced);

        [NonEvent]
        internal bool IsStateDumpEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.StateDump);
        #endregion

        #region overloads
        //Never use event writer directly as they are not checking for enabled/disabled situations. Always use overloads.
        [NonEvent]
        internal void TraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsTraceEnabled())
            {
                TraceEvent(string.Format(message), args0);
            }
        }

        [NonEvent]
        internal void TraceEvent(string message)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(message);
            }
        }

        [NonEvent]
        internal void TraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void TraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void TraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void TraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0, args1, args2, args3, args4, args5));
            }
        }

        [NonEvent]
        internal void AdvanceTrace(string message)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(message);
            }
        }

        [NonEvent]
        internal void AdvanceTrace<T0>(string message, T0 args0)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void AdvanceTrace<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void AdvanceTrace<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void AdvanceTrace<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void AdvanceTrace<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(string.Format(message, args0, args1, args2, args3, args4, args5));
            }
        }

        [NonEvent]
        internal void AdvanceTrace<T0, T1, T2, T3, T4, T5, T6, T7>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7)
        {
            if (Log.IsAdvanceTraceOn())
            {
                Trace(string.Format(message, args0, args1, args2, args3, args4, args5, args6, args7));
            }
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long AdvanceScopeEnter<T0>(string message, T0 args0)
        {
            if (IsAdvanceTraceOn())
            {
                return ScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent(string message)
        {
            if (IsScopeEnabled())
            {
                return ScopeEnter(message);
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0, args1));
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0, args1, args2));
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0, args1, args2, args3));
            }
            return 0;
        }

        [NonEvent]
        internal long PoolerScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (IsPoolerScopeEnabled())
            {
                return PoolerScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0, args1));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0, args1, args2));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0, args1, args2, args3));
            }
            return 0;
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0>(string message, T0 args0)
        {
            if (IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0>(string message, T0 args0)
        {
            if (IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0>(string message, T0 args0)
        {
            if (IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void TraceBinEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsTraceEnabled())
            {
                TraceBin(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void StateDumpEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (IsStateDumpEnabled())
            {
                Trace(string.Format(message, args0, args1));
            }
        }

        #endregion

        #region Events
        [Event(TraceEventId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void Trace(string message)
        {
            WriteEvent(TraceEventId, message);
        }

        [Event(EnterScopeId, Level = EventLevel.Verbose, Keywords = Keywords.Scope)]
        internal long ScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(EnterScopeId, message);
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
        internal void TraceBin(string message)
        {
            WriteEvent(TraceBinId, message);
        }

        [Event(CorrelationTraceId, Level = EventLevel.Informational, Keywords = Keywords.Correlation, Opcode = EventOpcode.Start)]
        internal void CorrelationTrace(string message)
        {
            WriteEvent(CorrelationTraceId, message);
        }

        [Event(NotificationsScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.NotificationScope)]
        internal long NotificationsScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextNotificationScopeId);
            WriteEvent(NotificationsScopeEnterId, message);
            return scopeId;
        }

        [Event(PoolerScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.PoolerScope)]
        internal long PoolerScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextPoolerScopeId);
            WriteEvent(PoolerScopeEnterId, message);
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
        #endregion
    }
}
