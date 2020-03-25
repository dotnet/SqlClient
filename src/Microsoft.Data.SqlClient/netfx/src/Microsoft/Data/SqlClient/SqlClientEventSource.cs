// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    [EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
    internal class SqlClientEventSource : EventSource
    {
        // Defines the singleton instance for the Resources ETW provider
        internal static readonly SqlClientEventSource Log = new SqlClientEventSource();

        #region Event IDs
        // Initialized static Scope IDs
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

        /// <summary>
        /// Defines EventId for Trace() events
        /// </summary>
        private const int TraceEventId = 3;

        /// <summary>
        /// Defines EventId for ScopeEnter() events
        /// </summary>
        private const int ScopeEnterId = 4;

        /// <summary>
        /// Defines EventId for ScopeLeave() events
        /// </summary>
        private const int ScopeExitId = 5;

        /// <summary>
        /// Defines EventId for NotificationScopeEnter() events
        /// </summary>
        private const int NotificationScopeEnterId = 6;

        /// <summary>
        /// Defines EventId for NotificationScopeLeave() events
        /// </summary>
        private const int NotificationScopeExitId = 7;

        /// <summary>
        /// Defines EventId for NotificationScopeTrace() events
        /// </summary>
        private const int NotificationTraceId = 8;

        /// <summary>
        /// Defines EventId for PoolerScopeEnter() events
        /// </summary>
        private const int PoolerScopeEnterId = 9;

        /// <summary>
        /// Defines EventId for PoolerScopeLeave() events
        /// </summary>
        private const int PoolerScopeExitId = 10;

        /// <summary>
        /// Defines EventId for PoolerTrace() events
        /// </summary>
        private const int PoolerTraceId = 11;

        /// <summary>
        /// Defines EventId for AdvancedTrace() events
        /// </summary>
        private const int AdvancedTraceId = 12;

        /// <summary>
        /// Defines EventId for AdvancedScopeEnter() events
        /// </summary>
        private const int AdvancedScopeEnterId = 13;

        /// <summary>
        /// Defines EventId for AdvancedScopeLeave() events
        /// </summary>
        private const int AdvancedScopeExitId = 14;

        /// <summary>
        /// Defines EventId for AdvancedTraceBin() events
        /// </summary>
        private const int AdvancedTraceBinId = 15;

        /// <summary>
        /// Defines EventId for CorrelationTrace() events
        /// </summary>
        private const int CorrelationTraceId = 16;

        /// <summary>
        /// Defines EventId for StateDump() events
        /// </summary>
        private const int StateDumpEventId = 17;
        #endregion

        /// <summary>
        /// These represent logical groups of events that can be turned on and off independently
        /// Often each task has a keyword, but where tasks are determined by subsystem, keywords
        /// are determined by usefulness to end users to filter.
        /// 
        /// Generally users don't mind extra events if they are not high volume, so grouping low 
        /// volume events together in a single keywords is OK (users can post-filter by task if desired)
        /// 
        /// <remarks>
        /// The visibility of the enum has to be public, otherwise there will be an ArgumentException 
        /// on calling related WriteEvent() method.
        /// 
        /// The Keywords class has to be a nested class.
        /// Each keyword must be a power of 2.
        /// </remarks>
        /// 
        /// </summary>
        #region Keywords
        public class Keywords
        {
            /// <summary>
            /// Captures Start/Stop events before and after command execution.
            /// </summary>
            internal const EventKeywords ExecutionTrace = (EventKeywords)1;

            /// <summary>
            /// Captures basic application flow trace events.
            /// </summary>
            internal const EventKeywords Trace = (EventKeywords)2;

            /// <summary>
            /// Captures basic application scope entering and exiting events.
            /// </summary>
            internal const EventKeywords Scope = (EventKeywords)4;

            /// <summary>
            /// Captures `SqlNotification` flow trace events.
            /// </summary>
            internal const EventKeywords NotificationTrace = (EventKeywords)8;

            /// <summary>
            /// Captures `SqlNotification` scope entering and exiting events.
            /// </summary>
            internal const EventKeywords NotificationScope = (EventKeywords)16;

            /// <summary>
            /// Captures connection pooling flow trace events.
            /// </summary>
            internal const EventKeywords PoolerTrace = (EventKeywords)32;

            /// <summary>
            /// Captures connection pooling scope trace events.
            /// </summary>
            internal const EventKeywords PoolerScope = (EventKeywords)64;

            /// <summary>
            /// Captures advanced flow trace events.
            /// </summary>
            internal const EventKeywords AdvancedTrace = (EventKeywords)128;

            /// <summary>
            /// Captures advanced flow trace events with additional information.
            /// </summary>
            internal const EventKeywords AdvancedTraceBin = (EventKeywords)256;

            /// <summary>
            /// Captures correlation flow trace events.
            /// </summary>
            internal const EventKeywords CorrelationTrace = (EventKeywords)512;

            /// <summary>
            /// Captures full state dump of `SqlConnection`
            /// </summary>
            internal const EventKeywords StateDump = (EventKeywords)1024;
        }
        #endregion

        #region Tasks
        /// <summary>
        /// Tasks supported by SqlClient's EventSource implementation
        /// </summary>
        public static class Tasks
        {
            /// <summary>
            /// Task that tracks SqlCommand execution.
            /// </summary>
            public const EventTask ExecuteCommand = (EventTask)1;
        }
        #endregion

        #region Enable/Disable Events
        [NonEvent]
        internal bool IsExecutionTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.ExecutionTrace);

        [NonEvent]
        internal bool IsTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.Trace);

        [NonEvent]
        internal bool IsScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.Scope);

        [NonEvent]
        internal bool IsNotificationTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.NotificationTrace);

        [NonEvent]
        internal bool IsNotificationScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.NotificationScope);

        [NonEvent]
        internal bool IsPoolerTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.PoolerTrace);

        [NonEvent]
        internal bool IsPoolerScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.PoolerScope);

        [NonEvent]
        internal bool IsAdvancedTraceOn() => Log.IsEnabled(EventLevel.Informational, Keywords.AdvancedTrace);

        [NonEvent]
        internal bool IsCorrelationEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.CorrelationTrace);

        [NonEvent]
        internal bool IsStateDumpEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.StateDump);

        #endregion

        #region overloads
        //Never use event writer directly as they are not checking for enabled/disabled situations. Always use overloads.

        #region Trace
        [NonEvent]
        internal void TraceEvent(string message)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(message);
            }
        }

        [NonEvent]
        internal void TraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? "Null"));
            }
        }

        [NonEvent]
        internal void TraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? "Null", args1?.ToString() ?? "Null"));
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
        #endregion

        #region Scope
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
        internal long ScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0));
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
        internal void ScopeLeaveEvent(long scopeId)
        {
            if (Log.IsScopeEnabled())
            {
                ScopeLeave(scopeId);
            }
        }
        #endregion

        #region Notification Trace
        [NonEvent]
        internal void NotificationTraceEvent(string message)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(message);
            }
        }

        [NonEvent]
        internal void NotificationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void NotificationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void NotificationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void NotificationTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0, args1, args2, args3));
            }
        }
        #endregion

        #region Notification Scope
        [NonEvent]
        internal long NotificationScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0, args1));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0, args1, args2));
            }
            return 0;
        }

        [NonEvent]
        internal long NotificationScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0, args1, args2, args3));
            }
            return 0;
        }

        [NonEvent]
        internal void NotificationScopeLeaveEvent(long scopeId)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                NotificationScopeLeave(scopeId);
            }
        }
        #endregion

        #region Pooler Trace
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
        #endregion

        #region Pooler Scope
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
        internal void PoolerScopeLeaveEvent(long scopeId)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                PoolerScopeLeave(scopeId);
            }
        }
        #endregion

        #region AdvancedTrace
        [NonEvent]
        internal void AdvancedTraceEvent(string message)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(message);
            }
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0));
            }
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0, args1));
            }
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0, args1, args2));
            }
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0, args1, args2, args3));
            }
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0, args1, args2, args3, args4, args5));
            }
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1, T2, T3, T4, T5, T6, T7>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0, args1, args2, args3, args4, args5, args6, args7));
            }
        }

        [NonEvent]
        internal long AdvancedScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsAdvancedTraceOn())
            {
                return AdvancedScopeEnter(string.Format(message, args0));
            }
            return 0;
        }

        [NonEvent]
        internal void AdvanceScopeLeave(long scopeId)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedScopeLeave(scopeId);
            }
        }

        [NonEvent]
        internal void AdvancedTraceBinEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTraceBin(string.Format(message, args0, args1));
            }
        }
        #endregion

        #region Correlation Trace
        [NonEvent]
        internal void CorrelationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? "Null"));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? "Null", args1?.ToString() ?? "Null"));
            }
        }

        [NonEvent]
        internal void CorrelationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? "Null", args1?.ToString() ?? "Null", args2?.ToString() ?? "Null"));
            }
        }
        #endregion

        #region State Dump
        [NonEvent]
        internal void StateDumpEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsStateDumpEnabled())
            {
                StateDump(string.Format(message, args0, args1));
            }
        }
        #endregion

        #endregion

        #region Write Events
        [Event(BeginExecuteEventId, Keywords = Keywords.ExecutionTrace, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Start)]
        internal void BeginExecute(int objectId, string dataSource, string database, string commandText)
        {
            if (Log.IsExecutionTraceEnabled())
            {
                WriteEvent(BeginExecuteEventId, objectId, dataSource, database, commandText);
            }
        }

        [Event(EndExecuteEventId, Keywords = Keywords.ExecutionTrace, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Stop)]
        internal void EndExecute(int objectId, int compositeState, int sqlExceptionNumber)
        {
            if (Log.IsExecutionTraceEnabled())
            {
                WriteEvent(EndExecuteEventId, objectId, compositeState, sqlExceptionNumber);
            }
        }

        [Event(TraceEventId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void Trace(string message)
        {
            WriteEvent(TraceEventId, message);
        }

        [Event(ScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.Scope)]
        internal long ScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(ScopeEnterId, message);
            return scopeId;
        }

        [Event(ScopeExitId, Level = EventLevel.Informational, Opcode = EventOpcode.Stop, Keywords = Keywords.Scope)]
        internal void ScopeLeave(long scopeId)
        {
            WriteEvent(ScopeExitId, scopeId);
        }

        [Event(NotificationTraceId, Level = EventLevel.Informational, Keywords = Keywords.NotificationTrace)]
        internal void NotificationTrace(string message)
        {
            WriteEvent(NotificationTraceId, message);
        }

        [Event(NotificationScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.NotificationScope)]
        internal long NotificationScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextNotificationScopeId);
            WriteEvent(NotificationScopeEnterId, message);
            return scopeId;
        }

        [Event(NotificationScopeExitId, Level = EventLevel.Informational, Opcode = EventOpcode.Stop, Keywords = Keywords.NotificationScope)]
        internal void NotificationScopeLeave(long scopeId)
        {
            WriteEvent(NotificationScopeExitId, scopeId);
        }

        [Event(PoolerTraceId, Level = EventLevel.Informational, Keywords = Keywords.PoolerTrace)]
        internal void PoolerTrace(string message)
        {
            WriteEvent(PoolerTraceId, message);
        }

        [Event(PoolerScopeEnterId, Level = EventLevel.Informational, Opcode = EventOpcode.Start, Keywords = Keywords.PoolerScope)]
        internal long PoolerScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextPoolerScopeId);
            WriteEvent(PoolerScopeEnterId, message);
            return scopeId;
        }

        [Event(PoolerScopeExitId, Level = EventLevel.Informational, Opcode = EventOpcode.Stop, Keywords = Keywords.PoolerScope)]
        internal void PoolerScopeLeave(long scopeId)
        {
            WriteEvent(PoolerScopeExitId, scopeId);
        }

        [Event(AdvancedTraceId, Level = EventLevel.Verbose, Keywords = Keywords.AdvancedTrace)]
        internal void AdvancedTrace(string message)
        {
            WriteEvent(AdvancedTraceId, message);
        }

        [Event(AdvancedScopeEnterId, Level = EventLevel.Verbose, Opcode = EventOpcode.Start, Keywords = Keywords.AdvancedTrace)]
        internal long AdvancedScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(AdvancedScopeEnterId, message);
            return scopeId;
        }

        [Event(AdvancedScopeExitId, Level = EventLevel.Verbose, Opcode = EventOpcode.Stop, Keywords = Keywords.AdvancedTrace)]
        internal void AdvancedScopeLeave(long scopeId)
        {
            WriteEvent(AdvancedScopeExitId, scopeId);
        }

        [Event(AdvancedTraceBinId, Level = EventLevel.Informational, Keywords = Keywords.AdvancedTraceBin)]
        internal void AdvancedTraceBin(string message)
        {
            WriteEvent(AdvancedTraceBinId, message);
        }

        [Event(CorrelationTraceId, Level = EventLevel.Informational, Keywords = Keywords.CorrelationTrace, Opcode = EventOpcode.Start)]
        internal void CorrelationTrace(string message)
        {
            WriteEvent(CorrelationTraceId, message);
        }

        [Event(StateDumpEventId, Level = EventLevel.Verbose, Keywords = Keywords.StateDump)]
        internal void StateDump(string message)
        {
            WriteEvent(StateDumpEventId, message);
        }
        #endregion
    }
}
