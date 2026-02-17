// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.Data.SqlClient
{
    // Any changes to event writers might be considered as a breaking change.
    // Other libraries such as OpenTelemetry and ApplicationInsight have based part of their code on BeginExecute and EndExecute arguments number.

    /// <summary>
    /// ETW EventSource for Microsoft.Data.SqlClient tracing and diagnostics.
    /// </summary>
    [EventSource(Name = "Microsoft.Data.SqlClient.EventSource")]
    public class SqlClientEventSource : EventSource
    {
        /// <summary>
        /// Defines the singleton instance for the Resources ETW provider.
        /// </summary>
        public static readonly SqlClientEventSource Log = new();

        /// <summary>
        /// A callback that is invoked when the EventSource receives an Enable command.
        /// Can be used to hook metrics or other subsystems that need to respond to EventSource enablement.
        /// </summary>
        public static Action? OnEventSourceEnabled { get; set; }

        /// <summary>
        /// Gets a value indicating whether the EventSource has been enabled at least once.
        /// This can be checked by downstream consumers to handle early enabling that may
        /// occur before callbacks are registered.
        /// </summary>
        public static bool WasEnabled { get; private set; }

        private SqlClientEventSource() { }

        private const string NullStr = "null";
        private const string SqlCommand_ClassName = "SqlCommand";

        /// <inheritdoc />
        protected override void OnEventCommand(EventCommandEventArgs command)
        {
            base.OnEventCommand(command);

            if (command.Command == EventCommand.Enable)
            {
                WasEnabled = true;
                OnEventSourceEnabled?.Invoke();
            }
        }

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
        /// These represent logical groups of events that can be turned on and off independently.
        /// Often each task has a keyword, but where tasks are determined by subsystem, keywords
        /// are determined by usefulness to end users to filter.
        /// </summary>
        /// <remarks>
        /// The visibility of the enum has to be public, otherwise there will be an ArgumentException
        /// on calling related WriteEvent() method.
        /// The Keywords class has to be a nested class.
        /// Each keyword must be a power of 2.
        /// </remarks>
        #region Keywords
        public class Keywords
        {
            /// <summary>
            /// Captures Start/Stop events before and after command execution.
            /// </summary>
            public const EventKeywords ExecutionTrace = (EventKeywords)1;

            /// <summary>
            /// Captures basic application flow trace events.
            /// </summary>
            public const EventKeywords Trace = (EventKeywords)2;

            /// <summary>
            /// Captures basic application scope entering and exiting events.
            /// </summary>
            public const EventKeywords Scope = (EventKeywords)4;

            /// <summary>
            /// Captures <c>SqlNotification</c> flow trace events.
            /// </summary>
            public const EventKeywords NotificationTrace = (EventKeywords)8;

            /// <summary>
            /// Captures <c>SqlNotification</c> scope entering and exiting events.
            /// </summary>
            public const EventKeywords NotificationScope = (EventKeywords)16;

            /// <summary>
            /// Captures connection pooling flow trace events.
            /// </summary>
            public const EventKeywords PoolerTrace = (EventKeywords)32;

            /// <summary>
            /// Captures connection pooling scope trace events.
            /// </summary>
            public const EventKeywords PoolerScope = (EventKeywords)64;

            /// <summary>
            /// Captures advanced flow trace events.
            /// </summary>
            public const EventKeywords AdvancedTrace = (EventKeywords)128;

            /// <summary>
            /// Captures advanced flow trace events with additional information.
            /// </summary>
            public const EventKeywords AdvancedTraceBin = (EventKeywords)256;

            /// <summary>
            /// Captures correlation flow trace events.
            /// </summary>
            public const EventKeywords CorrelationTrace = (EventKeywords)512;

            /// <summary>
            /// Captures full state dump of <c>SqlConnection</c>.
            /// </summary>
            public const EventKeywords StateDump = (EventKeywords)1024;

            /// <summary>
            /// Captures application flow traces from Managed networking implementation.
            /// </summary>
            public const EventKeywords SNITrace = (EventKeywords)2048;

            /// <summary>
            /// Captures scope trace events from Managed networking implementation.
            /// </summary>
            public const EventKeywords SNIScope = (EventKeywords)4096;
        }
        #endregion

        #region Tasks
        /// <summary>
        /// Tasks supported by SqlClient's EventSource implementation.
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
            /// Task that tracks pooler scope.
            /// </summary>
            public const EventTask PoolerScope = (EventTask)3;

            /// <summary>
            /// Task that tracks notification scope.
            /// </summary>
            public const EventTask NotificationScope = (EventTask)4;

            /// <summary>
            /// Task that tracks SNI scope.
            /// </summary>
            public const EventTask SNIScope = (EventTask)5;
        }
        #endregion

        #region Enable/Disable Events
        /// <summary>
        /// Checks if execution trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsExecutionTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.ExecutionTrace);

        /// <summary>
        /// Checks if trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.Trace);

        /// <summary>
        /// Checks if scope events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.Scope);

        /// <summary>
        /// Checks if notification trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsNotificationTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.NotificationTrace);

        /// <summary>
        /// Checks if notification scope events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsNotificationScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.NotificationScope);

        /// <summary>
        /// Checks if pooler trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsPoolerTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.PoolerTrace);

        /// <summary>
        /// Checks if pooler scope events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsPoolerScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.PoolerScope);

        /// <summary>
        /// Checks if advanced trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsAdvancedTraceOn() => Log.IsEnabled(EventLevel.Verbose, Keywords.AdvancedTrace);

        /// <summary>
        /// Checks if advanced trace binary events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsAdvancedTraceBinOn() => Log.IsEnabled(EventLevel.Verbose, Keywords.AdvancedTraceBin);

        /// <summary>
        /// Checks if correlation trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsCorrelationEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.CorrelationTrace);

        /// <summary>
        /// Checks if state dump events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsStateDumpEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.StateDump);

        /// <summary>
        /// Checks if SNI trace events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsSNITraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.SNITrace);

        /// <summary>
        /// Checks if SNI scope events are enabled.
        /// </summary>
        [NonEvent]
        public bool IsSNIScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.SNIScope);
        #endregion

        private string GetFormattedMessage(string className, string memberName, string eventType, string message) =>
            new StringBuilder(className).Append(".").Append(memberName).Append(eventType).Append(message).ToString();

        #region overloads
        //Never use event writer directly as they are not checking for enabled/disabled situations. Always use overloads.

        #region Trace

        #region Traces without if statements
        /// <summary>
        /// Writes a formatted trace event with two arguments.
        /// </summary>
        [NonEvent]
        public void TraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }

        /// <summary>
        /// Writes a formatted trace event with three arguments.
        /// </summary>
        [NonEvent]
        public void TraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
        }

        /// <summary>
        /// Writes a formatted trace event with four arguments.
        /// </summary>
        [NonEvent]
        public void TraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
        }
        #endregion

        #region Traces with if statements
        /// <summary>
        /// Writes a trace event if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent(string message)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(message);
            }
        }

        /// <summary>
        /// Writes a formatted trace event with one argument if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted trace event with two arguments if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted trace event with three arguments if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted trace event with four arguments if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted trace event with five arguments if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted trace event with six arguments if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted trace event with seven arguments if trace is enabled.
        /// </summary>
        [NonEvent]
        public void TryTraceEvent<T0, T1, T2, T3, T4, T5, T6>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 arg6)
        {
            if (Log.IsTraceEnabled())
            {
                Trace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr, arg6?.ToString() ?? NullStr));
            }
        }
        #endregion

        #endregion

        #region Scope
        /// <summary>
        /// Enters a scope and writes a scope event if scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryScopeEnterEvent(string className, [CallerMemberName] string memberName = "")
        {
            if (Log.IsScopeEnabled())
            {
                StringBuilder sb = new StringBuilder(className);
                sb.Append(".").Append(memberName).Append(" | INFO | SCOPE | Entering Scope {0}");
                return ScopeEnter(sb.ToString());
            }
            return 0;
        }

        /// <summary>
        /// Enters a scope with one formatted argument if scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Enters a scope with two formatted arguments if scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Enters a scope with three formatted arguments if scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Enters a scope with four formatted arguments if scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsScopeEnabled())
            {
                return ScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Leaves a scope if scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryScopeLeaveEvent(long scopeId)
        {
            if (Log.IsScopeEnabled())
            {
                ScopeLeave(string.Format("Exit Scope {0}", scopeId));
            }
        }
        #endregion

        #region Execution Trace
        /// <summary>
        /// Writes a BeginExecute trace event if execution tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryBeginExecuteEvent(int objectId, string dataSource, string database, string commandText, Guid? connectionId, [CallerMemberName] string memberName = "")
        {
            if (Log.IsExecutionTraceEnabled())
            {
                BeginExecute(objectId, dataSource, database, commandText, GetFormattedMessage(SqlCommand_ClassName, memberName, EventType.INFO, $"Object Id {objectId}, Client connection Id {connectionId}, Command Text {commandText}"));
            }
        }

        /// <summary>
        /// Writes an EndExecute trace event if execution tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryEndExecuteEvent(int objectId, int compositeState, int sqlExceptionNumber, Guid? connectionId, [CallerMemberName] string memberName = "")
        {
            if (Log.IsExecutionTraceEnabled())
            {
                EndExecute(objectId, compositeState, sqlExceptionNumber, GetFormattedMessage(SqlCommand_ClassName, memberName, EventType.INFO, $"Object Id {objectId}, Client Connection Id {connectionId}, Composite State {compositeState}, Sql Exception Number {sqlExceptionNumber}"));
            }
        }
        #endregion

        #region Notification Trace

        #region Notification Traces without if statements
        /// <summary>
        /// Writes a formatted notification trace event with two arguments.
        /// </summary>
        [NonEvent]
        public void NotificationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }
        #endregion

        #region Notification Traces with if statements
        /// <summary>
        /// Writes a notification trace event if notification tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryNotificationTraceEvent(string message)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(message);
            }
        }

        /// <summary>
        /// Writes a formatted notification trace event with one argument if notification tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryNotificationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted notification trace event with two arguments if notification tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryNotificationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted notification trace event with three arguments if notification tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryNotificationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted notification trace event with four arguments if notification tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryNotificationTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationTraceEnabled())
            {
                NotificationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }
        #endregion

        #endregion

        #region Notification Scope
        /// <summary>
        /// Enters a notification scope with one formatted argument if notification scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryNotificationScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Enters a notification scope with two formatted arguments if notification scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryNotificationScopeEnterEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Enters a notification scope with three formatted arguments if notification scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryNotificationScopeEnterEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Enters a notification scope with four formatted arguments if notification scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryNotificationScopeEnterEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                return NotificationScopeEnter(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Leaves a notification scope if notification scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryNotificationScopeLeaveEvent(long scopeId)
        {
            if (Log.IsNotificationScopeEnabled())
            {
                NotificationScopeLeave(string.Format("Exit Notification Scope {0}", scopeId));
            }
        }
        #endregion

        #region Pooler Trace
        /// <summary>
        /// Writes a pooler trace event if pooler tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryPoolerTraceEvent(string message)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(message);
            }
        }

        /// <summary>
        /// Writes a formatted pooler trace event with one argument if pooler tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryPoolerTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted pooler trace event with two arguments if pooler tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryPoolerTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted pooler trace event with three arguments if pooler tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryPoolerTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted pooler trace event with four arguments if pooler tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryPoolerTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsPoolerTraceEnabled())
            {
                PoolerTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }
        #endregion

        #region Pooler Scope
        /// <summary>
        /// Enters a pooler scope with one formatted argument if pooler scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryPoolerScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                return PoolerScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Leaves a pooler scope if pooler scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryPoolerScopeLeaveEvent(long scopeId)
        {
            if (Log.IsPoolerScopeEnabled())
            {
                PoolerScopeLeave(string.Format("Exit Pooler Scope {0}", scopeId));
            }
        }
        #endregion

        #region AdvancedTrace

        #region AdvancedTraces without if statements
        /// <summary>
        /// Writes a formatted advanced trace event with one argument.
        /// </summary>
        [NonEvent]
        public void AdvancedTraceEvent<T0>(string message, T0 args0)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr));
        }

        /// <summary>
        /// Writes a formatted advanced trace event with two arguments.
        /// </summary>
        [NonEvent]
        public void AdvancedTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }

        /// <summary>
        /// Writes a formatted advanced trace event with three arguments.
        /// </summary>
        [NonEvent]
        public void AdvancedTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
        }

        /// <summary>
        /// Writes a formatted advanced trace event with four arguments.
        /// </summary>
        [NonEvent]
        public void AdvancedTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
        }
        #endregion

        #region AdvancedTraces with if statements
        /// <summary>
        /// Writes an advanced trace event if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent(string message)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(message);
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with one argument if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with two arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with three arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with four arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with five arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with six arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with eight arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1, T2, T3, T4, T5, T6, T7>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6, T7 args7)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr, args6?.ToString() ?? NullStr, args7?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace event with seven arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceEvent<T0, T1, T2, T3, T4, T5, T6>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, T6 args6)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr, args6?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Enters an advanced scope with one formatted argument if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TryAdvancedScopeEnterEvent<T0>(string message, T0 args0)
        {
            if (Log.IsAdvancedTraceOn())
            {
                return AdvancedScopeEnter(string.Format(message, args0?.ToString() ?? NullStr));
            }
            return 0;
        }

        /// <summary>
        /// Leaves an advanced scope if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvanceScopeLeave(long scopeId)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedScopeLeave(string.Format("Exit Advanced Scope {0}", scopeId));
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace binary event with three arguments if advanced binary tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceBinEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsAdvancedTraceBinOn())
            {
                if (args1 is byte[] args1Bytes)
                {
                    AdvancedTraceBin(string.Format(message, args0?.ToString() ?? NullStr, BitConverter.ToString(args1Bytes).Replace("-", ""), args2?.ToString() ?? NullStr));
                }
                else
                {
                    AdvancedTraceBin(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
                }
            }
        }

        /// <summary>
        /// Writes a formatted advanced trace error event with five arguments if advanced tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryAdvancedTraceErrorEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsAdvancedTraceOn())
            {
                AdvancedTraceError(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }
        #endregion

        #endregion

        #region Correlation Trace

        /// <summary>
        /// Writes a correlation trace event if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent(string message)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(message);
            }
        }

        /// <summary>
        /// Writes a formatted correlation trace event with one argument if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent<T0>(string message, T0 args0)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted correlation trace event with two arguments if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted correlation trace event with three arguments if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted correlation trace event with four arguments if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent<T0, T1, T2, T3>(string message, T0 args0, T1 args1, T2 args2, T3 args3)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted correlation trace event with five arguments if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent<T0, T1, T2, T3, T4>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr));
            }
        }

        /// <summary>
        /// Writes a formatted correlation trace event with six arguments if correlation tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TryCorrelationTraceEvent<T0, T1, T2, T3, T4, T5>(string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5)
        {
            if (Log.IsCorrelationEnabled())
            {
                CorrelationTrace(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr));
            }
        }
        #endregion

        #region State Dump without if statements
        /// <summary>
        /// Writes a formatted state dump event with two arguments.
        /// </summary>
        [NonEvent]
        public void StateDumpEvent<T0, T1>(string message, T0 args0, T1 args1)
        {
            StateDump(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr));
        }

        /// <summary>
        /// Writes a formatted state dump event with three arguments.
        /// </summary>
        [NonEvent]
        public void StateDumpEvent<T0, T1, T2>(string message, T0 args0, T1 args1, T2 args2)
        {
            StateDump(string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr));
        }
        #endregion

        #region SNI Trace
        /// <summary>
        /// Writes an SNI trace event if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent(string className, string eventType, string message, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, message));
            }
        }

        /// <summary>
        /// Writes a formatted SNI trace event with one argument if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent<T0>(string className, string eventType, string message, T0 args0, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr)));
            }
        }

        /// <summary>
        /// Writes a formatted SNI trace event with two arguments if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent<T0, T1>(string className, string eventType, string message, T0 args0, T1 args1, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr)));
            }
        }

        /// <summary>
        /// Writes a formatted SNI trace event with three arguments if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent<T0, T1, T2>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr)));
            }
        }

        /// <summary>
        /// Writes a formatted SNI trace event with four arguments if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent<T0, T1, T2, T3>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, T3 args3, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr)));
            }
        }

        /// <summary>
        /// Writes a formatted SNI trace event with five arguments if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent<T0, T1, T2, T3, T4>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr)));
            }
        }

        /// <summary>
        /// Writes a formatted SNI trace event with six arguments if SNI tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNITraceEvent<T0, T1, T2, T3, T4, T5>(string className, string eventType, string message, T0 args0, T1 args1, T2 args2, T3 args3, T4 args4, T5 args5, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNITraceEnabled())
            {
                SNITrace(GetFormattedMessage(className, memberName, eventType, string.Format(message, args0?.ToString() ?? NullStr, args1?.ToString() ?? NullStr, args2?.ToString() ?? NullStr, args3?.ToString() ?? NullStr, args4?.ToString() ?? NullStr, args5?.ToString() ?? NullStr)));
            }
        }
        #endregion

        #region SNI Scope
        /// <summary>
        /// Enters an SNI scope if SNI scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public long TrySNIScopeEnterEvent(string className, [CallerMemberName] string memberName = "")
        {
            if (Log.IsSNIScopeEnabled())
            {
                long scopeId = Interlocked.Increment(ref s_nextSNIScopeId);
                WriteEvent(SNIScopeEnterId, $"{className}.{memberName}  | SNI | INFO | SCOPE | Entering Scope {scopeId}");
                return scopeId;
            }
            return 0;
        }

        /// <summary>
        /// Leaves an SNI scope if SNI scope tracing is enabled.
        /// </summary>
        [NonEvent]
        public void TrySNIScopeLeaveEvent(long scopeId)
        {
            if (Log.IsSNIScopeEnabled())
            {
                SNIScopeLeave(string.Format("Exit SNI Scope {0}", scopeId));
            }
        }
        #endregion

        #endregion

        #region Write Events
        // Do not change the first 4 arguments in this Event writer as OpenTelemetry and ApplicationInsight are relating to the same format, 
        // unless you have checked with them and they are able to change their design. Additional items could be added at the end.

        /// <summary>
        /// Writes a BeginExecute event.
        /// </summary>
        [Event(BeginExecuteEventId, Keywords = Keywords.ExecutionTrace, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Start)]
        public void BeginExecute(int objectId, string dataSource, string database, string commandText, string message)
        {
            WriteEvent(BeginExecuteEventId, objectId, dataSource, database, commandText, message);
        }

        // Do not change the first 3 arguments in this Event writer as OpenTelemetry and ApplicationInsight are relating to the same format, 
        // unless you have checked with them and they are able to change their design. Additional items could be added at the end.

        /// <summary>
        /// Writes an EndExecute event.
        /// </summary>
        [Event(EndExecuteEventId, Keywords = Keywords.ExecutionTrace, Task = Tasks.ExecuteCommand, Opcode = EventOpcode.Stop)]
        public void EndExecute(int objectId, int compositestate, int sqlExceptionNumber, string message)
        {
            WriteEvent(EndExecuteEventId, objectId, compositestate, sqlExceptionNumber, message);
        }

        /// <summary>
        /// Writes a Trace event.
        /// </summary>
        [Event(TraceEventId, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        public void Trace(string message) =>
            WriteEvent(TraceEventId, message);

        /// <summary>
        /// Enters a scope.
        /// </summary>
        [Event(ScopeEnterId, Level = EventLevel.Informational, Task = Tasks.Scope, Opcode = EventOpcode.Start, Keywords = Keywords.Scope)]
        public long ScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(ScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        /// <summary>
        /// Leaves a scope.
        /// </summary>
        [Event(ScopeExitId, Level = EventLevel.Informational, Task = Tasks.Scope, Opcode = EventOpcode.Stop, Keywords = Keywords.Scope)]
        public void ScopeLeave(string message) =>
            WriteEvent(ScopeExitId, message);

        /// <summary>
        /// Writes a notification trace event.
        /// </summary>
        [Event(NotificationTraceId, Level = EventLevel.Informational, Keywords = Keywords.NotificationTrace)]
        public void NotificationTrace(string message) =>
            WriteEvent(NotificationTraceId, message);

        /// <summary>
        /// Enters a notification scope.
        /// </summary>
        [Event(NotificationScopeEnterId, Level = EventLevel.Informational, Task = Tasks.NotificationScope, Opcode = EventOpcode.Start, Keywords = Keywords.NotificationScope)]
        public long NotificationScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextNotificationScopeId);
            WriteEvent(NotificationScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        /// <summary>
        /// Leaves a notification scope.
        /// </summary>
        [Event(NotificationScopeExitId, Level = EventLevel.Informational, Task = Tasks.NotificationScope, Opcode = EventOpcode.Stop, Keywords = Keywords.NotificationScope)]
        public void NotificationScopeLeave(string message) =>
            WriteEvent(NotificationScopeExitId, message);

        /// <summary>
        /// Writes a pooler trace event.
        /// </summary>
        [Event(PoolerTraceId, Level = EventLevel.Informational, Keywords = Keywords.PoolerTrace)]
        public void PoolerTrace(string message) =>
            WriteEvent(PoolerTraceId, message);

        /// <summary>
        /// Enters a pooler scope.
        /// </summary>
        [Event(PoolerScopeEnterId, Level = EventLevel.Informational, Task = Tasks.PoolerScope, Opcode = EventOpcode.Start, Keywords = Keywords.PoolerScope)]
        public long PoolerScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextPoolerScopeId);
            WriteEvent(PoolerScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        /// <summary>
        /// Leaves a pooler scope.
        /// </summary>
        [Event(PoolerScopeExitId, Level = EventLevel.Informational, Task = Tasks.PoolerScope, Opcode = EventOpcode.Stop, Keywords = Keywords.PoolerScope)]
        public void PoolerScopeLeave(string message) =>
            WriteEvent(PoolerScopeExitId, message);

        /// <summary>
        /// Writes an advanced trace event.
        /// </summary>
        [Event(AdvancedTraceId, Level = EventLevel.Verbose, Keywords = Keywords.AdvancedTrace)]
        public void AdvancedTrace(string message) =>
            WriteEvent(AdvancedTraceId, message);

        /// <summary>
        /// Enters an advanced scope.
        /// </summary>
        [Event(AdvancedScopeEnterId, Level = EventLevel.Verbose, Opcode = EventOpcode.Start, Keywords = Keywords.AdvancedTrace)]
        public long AdvancedScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(AdvancedScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        /// <summary>
        /// Leaves an advanced scope.
        /// </summary>
        [Event(AdvancedScopeExitId, Level = EventLevel.Verbose, Opcode = EventOpcode.Stop, Keywords = Keywords.AdvancedTrace)]
        public void AdvancedScopeLeave(string message) =>
            WriteEvent(AdvancedScopeExitId, message);

        /// <summary>
        /// Writes an advanced trace binary event.
        /// </summary>
        [Event(AdvancedTraceBinId, Level = EventLevel.Verbose, Keywords = Keywords.AdvancedTraceBin)]
        public void AdvancedTraceBin(string message) =>
            WriteEvent(AdvancedTraceBinId, message);

        /// <summary>
        /// Writes an advanced trace error event.
        /// </summary>
        [Event(AdvancedTraceErrorId, Level = EventLevel.Error, Keywords = Keywords.AdvancedTrace)]
        public void AdvancedTraceError(string message) =>
            WriteEvent(AdvancedTraceErrorId, message);

        /// <summary>
        /// Writes a correlation trace event.
        /// </summary>
        [Event(CorrelationTraceId, Level = EventLevel.Informational, Keywords = Keywords.CorrelationTrace)]
        public void CorrelationTrace(string message) =>
            WriteEvent(CorrelationTraceId, message);

        /// <summary>
        /// Writes a state dump event.
        /// </summary>
        [Event(StateDumpEventId, Level = EventLevel.Verbose, Keywords = Keywords.StateDump)]
        public void StateDump(string message) =>
            WriteEvent(StateDumpEventId, message);

        /// <summary>
        /// Writes an SNI trace event.
        /// </summary>
        [Event(SNITraceEventId, Level = EventLevel.Informational, Keywords = Keywords.SNITrace)]
        public void SNITrace(string message) =>
            WriteEvent(SNITraceEventId, message);

        /// <summary>
        /// Enters an SNI scope.
        /// </summary>
        [Event(SNIScopeEnterId, Level = EventLevel.Informational, Task = Tasks.SNIScope, Opcode = EventOpcode.Start, Keywords = Keywords.SNIScope)]
        public long SNIScopeEnter(string message)
        {
            long scopeId = Interlocked.Increment(ref s_nextSNIScopeId);
            WriteEvent(SNIScopeEnterId, string.Format(message, scopeId));
            return scopeId;
        }

        /// <summary>
        /// Leaves an SNI scope.
        /// </summary>
        [Event(SNIScopeExitId, Level = EventLevel.Informational, Task = Tasks.SNIScope, Opcode = EventOpcode.Stop, Keywords = Keywords.SNIScope)]
        public void SNIScopeLeave(string message) =>
            WriteEvent(SNIScopeExitId, message);
        #endregion
    }

    /// <summary>
    /// Constants for event type labels used in formatted event messages.
    /// </summary>
    public static class EventType
    {
        /// <summary>
        /// Informational event type label.
        /// </summary>
        public const string INFO = " | INFO | ";

        /// <summary>
        /// Error event type label.
        /// </summary>
        public const string ERR = " | ERR | ";
    }

    /// <summary>
    /// A disposable scope for SNI event tracing. Automatically leaves the scope when disposed.
    /// </summary>
    public readonly struct TrySNIEventScope : IDisposable
    {
        private readonly long _scopeId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrySNIEventScope"/> struct.
        /// </summary>
        /// <param name="scopeID">The scope identifier.</param>
        public TrySNIEventScope(long scopeID) => _scopeId = scopeID;

        /// <inheritdoc />
        public void Dispose()
        {
            if (_scopeId == 0)
            {
                return;
            }
            SqlClientEventSource.Log.TrySNIScopeLeaveEvent(_scopeId);
        }

        /// <summary>
        /// Creates a new SNI event scope.
        /// </summary>
        /// <param name="className">The class name for the scope.</param>
        /// <param name="memberName">The member name for the scope (auto-populated by caller).</param>
        /// <returns>A new <see cref="TrySNIEventScope"/> instance.</returns>
        public static TrySNIEventScope Create(string className, [CallerMemberName] string memberName = "")
            => new TrySNIEventScope(SqlClientEventSource.Log.TrySNIScopeEnterEvent(className, memberName));
    }

    /// <summary>
    /// A disposable scope for general event tracing. Automatically leaves the scope when disposed.
    /// </summary>
    public readonly ref struct TryEventScope
    {
        private readonly long _scopeId;

        /// <summary>
        /// Initializes a new instance of the <see cref="TryEventScope"/> struct.
        /// </summary>
        /// <param name="scopeID">The scope identifier.</param>
        public TryEventScope(long scopeID) => _scopeId = scopeID;

        /// <summary>
        /// Disposes the scope, leaving the event scope if the scope ID is non-zero.
        /// </summary>
        public void Dispose()
        {
            if (_scopeId != 0)
            {
                SqlClientEventSource.Log.TryScopeLeaveEvent(_scopeId);
            }
        }

        /// <summary>
        /// Creates a new event scope with one formatted argument.
        /// </summary>
        public static TryEventScope Create<T0>(string message, T0 args0) => new TryEventScope(SqlClientEventSource.Log.TryScopeEnterEvent(message, args0));

        /// <summary>
        /// Creates a new event scope with two formatted arguments.
        /// </summary>
        public static TryEventScope Create<T0, T1>(string message, T0 args0, T1 args1) => new TryEventScope(SqlClientEventSource.Log.TryScopeEnterEvent(message, args0, args1));

        /// <summary>
        /// Creates a new event scope for a class with the calling member name.
        /// </summary>
        public static TryEventScope Create(string className, [CallerMemberName] string memberName = "") => new TryEventScope(SqlClientEventSource.Log.TryScopeEnterEvent(className, memberName));

        /// <summary>
        /// Creates a new event scope with a pre-existing scope identifier.
        /// </summary>
        public static TryEventScope Create(long scopeId) => new TryEventScope(scopeId);
    }
}
