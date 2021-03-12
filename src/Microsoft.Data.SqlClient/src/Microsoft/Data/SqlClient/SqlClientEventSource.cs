// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.Tracing;
using System.Text;
using System.Threading;

namespace Microsoft.Data.SqlClient
{
    internal abstract class SqlClientEventSourceBase : EventSource
    {
        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            base.OnEventCommand(command);
            EventCommandMethodCall(command);
        }

        protected virtual void EventCommandMethodCall(EventCommandEventArgs command) { }

        #region not implemented for .Net core 2.1, .Net standard 2.0 and lower
        internal virtual void HardConnectRequest() { /*no-op*/ }

        internal virtual void HardDisconnectRequest() { /*no-op*/ }

        internal virtual void SoftConnectRequest() { /*no-op*/ }

        internal virtual void SoftDisconnectRequest() { /*no-op*/ }

        internal virtual void EnterNonPooledConnection() { /*no-op*/ }

        internal virtual void ExitNonPooledConnection() { /*no-op*/ }

        internal virtual void EnterPooledConnection() { /*no-op*/ }

        internal virtual void ExitPooledConnection() { /*no-op*/ }

        internal virtual void EnterActiveConnectionPoolGroup() { /*no-op*/ }

        internal virtual void ExitActiveConnectionPoolGroup() { /*no-op*/ }

        internal virtual void EnterInactiveConnectionPoolGroup() { /*no-op*/ }

        internal virtual void ExitInactiveConnectionPoolGroup() { /*no-op*/ }

        internal virtual void EnterActiveConnectionPool() { /*no-op*/ }

        internal virtual void ExitActiveConnectionPool() { /*no-op*/ }

        internal virtual void EnterInactiveConnectionPool() { /*no-op*/ }

        internal virtual void ExitInactiveConnectionPool() { /*no-op*/ }

        internal virtual void EnterActiveConnection() { /*no-op*/ }

        internal virtual void ExitActiveConnection() { /*no-op*/ }

        internal virtual void EnterFreeConnection() { /*no-op*/ }

        internal virtual void ExitFreeConnection() { /*no-op*/ }

        internal virtual void EnterStasisConnection() { /*no-op*/ }

        internal virtual void ExitStasisConnection() { /*no-op*/ }

        internal virtual void ReclaimedConnectionRequest() { /*no-op*/ }
        #endregion
    }

    [EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
    internal partial class SqlClientEventSource : SqlClientEventSourceBase
    {
        // Defines the singleton instance for the Resources ETW provider
        internal static readonly SqlClientEventSource Log = new SqlClientEventSource();

        private const string NullStr = "null";
        private const string SqlCommand_ClassName = nameof(SqlCommand);

        #region Event IDs
        // Initialized static Scope IDs
        private static long s_nextScopeId = 0;
        private static long s_nextNotificationScopeId = 0;
        private static long s_nextPoolerScopeId = 0;
        private static long s_nextSNIScopeId = 0;

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
        /// Defines EventId for AdvancedTraceError() events
        /// </summary>
        private const int AdvancedTraceErrorId = 16;

        /// <summary>
        /// Defines EventId for CorrelationTrace() events
        /// </summary>
        private const int CorrelationTraceId = 17;

        /// <summary>
        /// Defines EventId for StateDump() events
        /// </summary>
        private const int StateDumpEventId = 18;

        /// <summary>
        /// Defines EventId for SNITrace() events
        /// </summary>
        private const int SNITraceEventId = 19;

        /// <summary>
        /// Defines EventId for SNIEnterScope() events
        /// </summary>
        private const int SNIScopeEnterId = 20;

        /// <summary>
        /// Defines EventId for SNIExitScope() events
        /// </summary>
        private const int SNIScopeExitId = 21;
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

            /// <summary>
            /// Captures application flow traces from Managed networking implementation
            /// </summary>
            internal const EventKeywords SNITrace = (EventKeywords)2048;

            /// <summary>
            /// Captures scope trace events from Managed networking implementation
            /// </summary>
            internal const EventKeywords SNIScope = (EventKeywords)4096;
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

            /// <summary>
            /// Task that tracks trace scope.
            /// </summary>
            public const EventTask Scope = (EventTask)2;

            /// <summary>
            /// Task that tracks trace scope.
            /// </summary>
            public const EventTask PoolerScope = (EventTask)3;

            /// <summary>
            /// Task that tracks trace scope.
            /// </summary>
            public const EventTask NotificationScope = (EventTask)4;

            /// <summary>
            /// Task that tracks trace scope.
            /// </summary>
            public const EventTask SNIScope = (EventTask)5;
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
        internal bool IsAdvancedTraceOn() => Log.IsEnabled(EventLevel.Verbose, Keywords.AdvancedTrace);

        [NonEvent]
        internal bool IsCorrelationEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.CorrelationTrace);

        [NonEvent]
        internal bool IsStateDumpEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.StateDump);

        [NonEvent]
        internal bool IsSNITraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.SNITrace);

        [NonEvent]
        internal bool IsSNIScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.SNIScope);
        #endregion

        private string GetFormattedMessage(string className, string memberName, string eventType, string message) =>
            new StringBuilder(className).Append(".").Append(memberName).Append(eventType).Append(message).ToString();

        #region overloads
        //Never use event writer directly as they are not checking for enabled/disabled situations. Always use overloads.

        #region Trace

        #region Traces without if statements
        [NonEvent]
        internal void TraceEvent<T0, T1>(string message, T0 args0, T1 args1, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }

        [NonEvent]
        internal void TraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
        }

        [NonEvent]
        internal void TraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
        }
        #endregion

        #region Traces with if statements
        [NonEvent]
        internal void TryTraceEvent(string message)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(message);
            }
        }

        [NonEvent]
        internal void TryTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryTraceEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr));
            }
        }
        #endregion

        #endregion

        #region Scope
        [NonEvent]
        internal long TryScopeEnterEvent(string className, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsScopeEnabled())
            {
                StringBuilder sb = new StringBuilder(className);
                sb.Append(".").Append(memberName).Append(" | INFO | SCOPE | Entering Scope {0}");
                return SNIScopeEnter(sb.ToString());
            }
            return 0;
        }

        [NonEvent]
        internal long TryScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal long TryScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal long TryScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal long TryScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal void TryScopeLeaveEvent(long scopeId)
        {
            if (Log.IsScopeEnabled())
            {
                ScopeLeave(string.Format("Exit Scope {0}", scopeId));
            }
        }
        #endregion

        #region Execution Trace
        [NonEvent]
        internal void TryBeginExecuteEvent(int objectId, object connectionId, string commandText, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsExecutionTraceEnabled())
            {
                BeginExecute(GetFormattedMessage(SqlCommand_ClassName, memberName, EventType.INFO,
                    string.Format("Object Id {0}, Client Connection Id {1}, Command Text {2}", objectId, connectionId, commandText)));
            }
        }

        [NonEvent]
        internal void TryEndExecuteEvent(int objectId, object connectionId, int compositeState, int sqlExceptionNumber, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsExecutionTraceEnabled())
            {
                EndExecute(GetFormattedMessage(SqlCommand_ClassName, memberName, EventType.INFO,
                    string.Format("Object Id {0}, Client Connection Id {1}, Composite State {2}, Sql Exception Number {3}", objectId, connectionId, compositeState, sqlExceptionNumber)));
            }
        }
        #endregion

        #region Notification Trace

        #region Notification Traces without if statements
        [NonEvent]
        internal void NotificationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }
        #endregion

        #region Notification Traces with if statements
        [NonEvent]
        internal void TryNotificationTraceEvent(string message)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(message);
            }
        }

        [NonEvent]
        internal void TryNotificationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryNotificationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryNotificationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryNotificationTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }
        #endregion

        #endregion

        #region Notification Scope
        [NonEvent]
        internal long TryNotificationScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal long TryNotificationScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal long TryNotificationScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal long TryNotificationScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal void TryNotificationScopeLeaveEvent(long scopeId)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                NotificationScopeLeave(string.Format("Exit Notification Scope {0}", scopeId));
            }
        }
        #endregion

        #region Pooler Trace
        [NonEvent]
        internal void TryPoolerTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryPoolerTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryPoolerTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryPoolerTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }
        #endregion

        #region Pooler Scope
        [NonEvent]
        internal long TryPoolerScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                return PoolerScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal void TryPoolerScopeLeaveEvent(long scopeId)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                PoolerScopeLeave(string.Format("Exit Pooler Scope {0}", scopeId));
            }
        }
        #endregion

        #region AdvancedTrace

        #region AdvancedTraces without if statements
        [NonEvent]
        internal void AdvancedTraceEvent<T0>(string message, T0 args0)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr));
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
        }

        [NonEvent]
        internal void AdvancedTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
        }
        #endregion

        #region AdvancedTraces with if statements
        [NonEvent]
        internal void TryAdvancedTraceEvent(string message)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(message);
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceEvent<T0, T1, T2, T3, T4, T5, T6, T7>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr, args6?.ToString() ?? NullStr, args7?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal long TryAdvancedScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsAdvancedTraceOn())
            {
                return AdvancedScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        [NonEvent]
        internal void TryAdvanceScopeLeave(long scopeId)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedScopeLeave(string.Format("Exit Advanced Scope {0}", scopeId));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceBinEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTraceBin(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryAdvancedTraceErrorEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTraceError(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }
        #endregion

        #endregion

        #region Correlation Trace
        [NonEvent]
        internal void TryCorrelationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryCorrelationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryCorrelationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryCorrelationTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryCorrelationTraceEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }

        [NonEvent]
        internal void TryCorrelationTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr));
            }
        }
        #endregion

        #region State Dump without if statements
        [NonEvent]
        internal void StateDumpEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            StateDump(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }
        #endregion

        #region SNI Trace
        [NonEvent]
        internal void TrySNITraceEvent(string className, string eventType, string message, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, message));
            }
        }

        [NonEvent]
        internal void TrySNITraceEvent<T0>(string className, string eventType, string message, T0 args0, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr)));
            }
        }

        [NonEvent]
        internal void TrySNITraceEvent<T0, T1>(string className, string eventType, string message, T0 args0, T1 args1, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr)));
            }
        }

        [NonEvent]
        internal void TrySNITraceEvent<T0, T1, T2>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr)));
            }
        }

        [NonEvent]
        internal void TrySNITraceEvent<T0, T1, T2, T3>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, T3 args3, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr)));
            }
        }

        [NonEvent]
        internal void TrySNITraceEvent<T0, T1, T2, T3, T4>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr)));
            }
        }

        [NonEvent]
        internal void TrySNITraceEvent<T0, T1, T2, T3, T4, T5>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr)));
            }
        }
        #endregion

        #region SNI Scope
        [NonEvent]
        internal long TrySNIScopeEnterEvent(string className, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (Log.IsSNIScopeEnabled())
            {
                StringBuilder sb = new StringBuilder(className);
                sb.Append(".").Append(memberName).Append(" | SNI | INFO | SCOPE | Entering Scope {0}");
                return SNIScopeEnter(sb.ToString());
            }
            return 0;
        }

        [NonEvent]
        internal void TrySNIScopeLeaveEvent(long scopeId)
        {
            if (Log.IsSNIScopeEnabled())
            {
                SNIScopeLeave(string.Format("Exit SNI Scope {0}", scopeId));
            }
        }
        #endregion

        #endregion

        #region Write Events
        [Event(BeginExecuteEventId, Keywords = Keywords.ExecutionTrace, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Start)]
        internal void BeginExecute(string message) =>
            WriteEvent(BeginExecuteEventId, message);

        [Event(EndExecuteEventId, Keywords = Keywords.ExecutionTrace, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Stop)]
        internal void EndExecute(string message) =>
            WriteEvent(EndExecuteEventId, message);

        [Event(TraceEventId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void Trace(string message) =>
            WriteEvent(TraceEventId, message);

        [Event(ScopeEnterId, Level = EventLevel.Informational, Task = Tasks.Scope, Opcode = EventOpcode.Start, Keywords = Keywords.Scope)]
        internal long ScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(ScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        [Event(ScopeExitId, Level = EventLevel.Informational, Task = Tasks.Scope, Opcode = EventOpcode.Stop, Keywords = Keywords.Scope)]
        internal void ScopeLeave(string message) =>
            WriteEvent(ScopeExitId, message);

        [Event(NotificationTraceId, Level = EventLevel.Informational, Keywords = Keywords.NotificationTrace)]
        internal void NotificationTrace(string message) =>
            WriteEvent(NotificationTraceId, message);

        [Event(NotificationScopeEnterId, Level = EventLevel.Informational, Task = Tasks.NotificationScope, Opcode = EventOpcode.Start, Keywords = Keywords.NotificationScope)]
        internal long NotificationScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextNotificationScopeId);
            WriteEvent(NotificationScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        [Event(NotificationScopeExitId, Level = EventLevel.Informational, Task = Tasks.NotificationScope, Opcode = EventOpcode.Stop, Keywords = Keywords.NotificationScope)]
        internal void NotificationScopeLeave(string message) =>
            WriteEvent(NotificationScopeExitId, message);

        [Event(PoolerTraceId, Level = EventLevel.Informational, Keywords = Keywords.PoolerTrace)]
        internal void PoolerTrace(string message) =>
            WriteEvent(PoolerTraceId, message);

        [Event(PoolerScopeEnterId, Level = EventLevel.Informational, Task = Tasks.PoolerScope, Opcode = EventOpcode.Start, Keywords = Keywords.PoolerScope)]
        internal long PoolerScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextPoolerScopeId);
            WriteEvent(PoolerScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        [Event(PoolerScopeExitId, Level = EventLevel.Informational, Task = Tasks.PoolerScope, Opcode = EventOpcode.Stop, Keywords = Keywords.PoolerScope)]
        internal void PoolerScopeLeave(string message) =>
            WriteEvent(PoolerScopeExitId, message);

        [Event(AdvancedTraceId, Level = EventLevel.Verbose, Keywords = Keywords.AdvancedTrace)]
        internal void AdvancedTrace(string message) =>
            WriteEvent(AdvancedTraceId, message);

        [Event(AdvancedScopeEnterId, Level = EventLevel.Verbose, Opcode = EventOpcode.Start, Keywords = Keywords.AdvancedTrace)]
        internal long AdvancedScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(AdvancedScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        [Event(AdvancedScopeExitId, Level = EventLevel.Verbose, Opcode = EventOpcode.Stop, Keywords = Keywords.AdvancedTrace)]
        internal void AdvancedScopeLeave(string message) =>
            WriteEvent(AdvancedScopeExitId, message);

        [Event(AdvancedTraceBinId, Level = EventLevel.Verbose, Keywords = Keywords.AdvancedTraceBin)]
        internal void AdvancedTraceBin(string message) =>
            WriteEvent(AdvancedTraceBinId, message);

        [Event(AdvancedTraceErrorId, Level = EventLevel.Error, Keywords = Keywords.AdvancedTrace)]
        internal void AdvancedTraceError(string message) =>
            WriteEvent(AdvancedTraceErrorId, message);

        [Event(CorrelationTraceId, Level = EventLevel.Informational, Keywords = Keywords.CorrelationTrace)]
        internal void CorrelationTrace(string message) =>
            WriteEvent(CorrelationTraceId, message);

        [Event(StateDumpEventId, Level = EventLevel.Verbose, Keywords = Keywords.StateDump)]
        internal void StateDump(string message) =>
            WriteEvent(StateDumpEventId, message);

        [Event(SNITraceEventId, Level = EventLevel.Informational, Keywords = Keywords.SNITrace)]
        internal void SNITrace(string message) =>
            WriteEvent(SNITraceEventId, message);

        [Event(SNIScopeEnterId, Level = EventLevel.Informational, Task = Tasks.SNIScope, Opcode = EventOpcode.Start, Keywords = Keywords.SNIScope)]
        internal long SNIScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextSNIScopeId);
            WriteEvent(SNIScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        [Event(SNIScopeExitId, Level = EventLevel.Informational, Task = Tasks.SNIScope, Opcode = EventOpcode.Stop, Keywords = Keywords.SNIScope)]
        internal void SNIScopeLeave(string message) =>
            WriteEvent(SNIScopeExitId, message);
        #endregion
    }

    internal static class EventType
    {
        public const string INFO = " | INFO | ";
        public const string ERR = " | ERR | ";
    }
    
    internal readonly struct SNIEventScope : IDisposable
    {
        private readonly long _scopeId;

        public SNIEventScope(long scopeID) => _scopeId = scopeID;
        public void Dispose() =>
            SqlClientEventSource.Log.SNIScopeLeave(string.Format("Exit SNI Scope {0}", _scopeId));

        public static SNIEventScope Create(string message) => new SNIEventScope(SqlClientEventSource.Log.SNIScopeEnter(message));
    }
}
