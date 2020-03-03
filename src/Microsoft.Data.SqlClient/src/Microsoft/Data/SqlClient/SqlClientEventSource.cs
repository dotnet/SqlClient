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
        // Defines the singleton instance for the Resources ETW provider
        internal static readonly SqlClientEventSource Log = new SqlClientEventSource();

        private static long s_nextScopeId = 0;
        private static long s_nextNotificationScopeId = 0;
        private static long s_nextPoolerScopeId = 0;

        /// <summary>
        /// Defines EventId for BeginExecute (Reader, Scalar, NonQuery, XmlReader).
        /// </summary>
        private const int BeginExecuteEventId = 1;

        /// <summary>
        /// Defines EventId for EndExecute (Reader, Scalar, NonQuery, XmlReader).
        /// </summary>
        private const int EndExecuteEventId = 2;
        private const int TraceEventId = 3;
        private const int EnterScopeId = 4;
        private const int ExitScopeId = 5;
        private const int TraceBinId = 6;
        private const int CorrelationTraceId = 7;
        private const int NotificationsScopeEnterId = 8;
        private const int NotificationsTraceId = 9;
        private const int PoolerScopeEnterId = 10;
        private const int PoolerTraceId = 11;

        /// <summary>
        /// Keyword definitions.  These represent logical groups of events that can be turned on and off independently
        /// Often each task has a keyword, but where tasks are determined by subsystem, keywords are determined by
        /// usefulness to end users to filter.  Generally users don't mind extra events if they are not high volume
        /// so grouping low volume events together in a single keywords is OK (users can post-filter by task if desired)
        /// <remarks>
        /// The visibility of the enum has to be public, otherwise there will be an ArgumentException on calling related WriteEvent method. the Keywords class has to be a nested class.
        /// Each keyword must be a power of 2.
        /// </remarks>
        /// </summary>
        #region Keywords
        public class Keywords
        {
            internal const EventKeywords SqlClient = 0;

            internal const EventKeywords Trace = (EventKeywords)1;

            internal const EventKeywords TraceBin = (EventKeywords)2;

            internal const EventKeywords Scope = (EventKeywords)4;

            internal const EventKeywords NotificationTrace = (EventKeywords)8;

            internal const EventKeywords Pooling = (EventKeywords)16;

            internal const EventKeywords Correlation = (EventKeywords)32;

            internal const EventKeywords NotificationScope = (EventKeywords)64;

            internal const EventKeywords PoolerScope = (EventKeywords)128;

            internal const EventKeywords PoolerTrace = (EventKeywords)256;

            internal const EventKeywords Advanced = (EventKeywords)512;

            internal const EventKeywords StateDump = (EventKeywords)1024;
        }
        #endregion

        public static class Tasks // this name is important for EventSource
        {
            /// <summary>Task that tracks sql command execute.</summary>
            public const EventTask ExecuteCommand = (EventTask)1;
        }

        #region Enable/Disable Events
        [NonEvent]
        internal bool IsTraceEnabled() => SqlClientEventSource.Log.IsEnabled(EventLevel.Informational, Keywords.Trace);

        [NonEvent]
        internal bool IsTraceBinEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.TraceBin);

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

        [NonEvent]
        internal bool IsSqlClientEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.SqlClient);
        #endregion

        #region overloads
        //Never use event writer directly as they are not checking for enabled/disabled situations. Always use overloads.
        [NonEvent]
        internal void TraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsTraceEnabled())
            {
                TraceEvent(string.Format(message, args0));
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
        internal void AdvanceTraceBin<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsAdvanceTraceOn())
            {
                TraceBin(string.Format(message, args0, args1));
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
            if (Log.IsAdvanceTraceOn())
            {
                return ScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent(string message)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(message);
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0, args1));
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0, args1, args2));
            }
            return 0;
        }

        [NonEvent]
        internal long ScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0, args1, args2, args3));
            }
            return 0;
        }

        [NonEvent]
        internal long PoolerScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                return PoolerScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0, args1));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0, args1, args2));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationsScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationsScopeEnter(string.Format(message, args0, args1, args2, args3));
            }
            return 0;
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void PoolerTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent(string message)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationsTrace(message);
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void NotificationsTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationsTrace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void TraceBinEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsTraceBinEnabled())
            {
                TraceBin(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void StateDumpEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsStateDumpEnabled())
            {
                Trace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void ScopeLeaveEvent(long scopeId)
        {
            if (Log.IsScopeEnabled())
            {
                ScopeLeave(scopeId);
            }
        }

        [NonEvent]
        internal void NotificationsScopeLeaveEvent(long scopeId)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                ScopeLeave(scopeId);
            }
        }

        [NonEvent]
        internal void PoolerScopeLeaveEvent(long scopeId)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                ScopeLeave(scopeId);
            }
        }

        [NonEvent]
        internal void AdvanceScopeLeave(long scopeId)
        {
            if (Log.IsAdvanceTraceOn())
            {
                ScopeLeave(scopeId);
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
            WriteEvent(ExitScopeId, scopeId);
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

        // unfortunately these are not marked as Start/Stop opcodes.  The reason is that we dont want them to participate in 
        // the EventSource activity IDs (because they currently don't use tasks and this simply confuses the logic) and 
        // because of versioning requirements we don't have ActivityOptions capability (because mscorlib and System.Data version 
        // at different rates)  Sigh...
        [Event(BeginExecuteEventId, Keywords = Keywords.SqlClient, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Start)]
        public void BeginExecute(int objectId, string dataSource, string database, string commandText)
        {
            // we do not use unsafe code for better performance optization here because optimized helpers make the code unsafe where that would not be the case otherwise. 
            // This introduces the question of partial trust, which is complex in the SQL case (there are a lot of scenarios and SQL has special security support).   
            if (Log.IsSqlClientEnabled())
            {
                WriteEvent(BeginExecuteEventId, objectId, dataSource, database, commandText);
            }
        }

        // unfortunately these are not marked as Start/Stop opcodes.  The reason is that we dont want them to participate in 
        // the EventSource activity IDs (because they currently don't use tasks and this simply confuses the logic) and 
        // because of versioning requirements we don't have ActivityOptions capability (because mscorlib and System.Data version 
        // at different rates)  Sigh...
        [Event(EndExecuteEventId, Keywords = Keywords.SqlClient, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Stop)]
        public void EndExecute(int objectId, int compositeState, int sqlExceptionNumber)
        {
            if (Log.IsSqlClientEnabled())
            {
                WriteEvent(EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
            }
        }
        #endregion
    }
}
