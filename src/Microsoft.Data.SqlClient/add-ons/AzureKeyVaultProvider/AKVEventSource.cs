// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    [EventSource(Name = "Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.EventSource")]
    internal class AKVEventSource : EventSource
    {
        // Defines the singleton instance for the Resources ETW provider
        public static AKVEventSource Log = new();

        private AKVEventSource() { }

        // Initialized static Scope IDs
        private static long s_nextScopeId = 0;

        // Provides Correlation Manager Activity Id for current scope.
        private static Guid GetCorrelationActivityId()
        {
            if (Trace.CorrelationManager.ActivityId == Guid.Empty)
            {
                Trace.CorrelationManager.ActivityId = Guid.NewGuid();
            }
            return Trace.CorrelationManager.ActivityId;
        }

        #region Keywords
        public class Keywords
        {
            /// <summary>
            /// Captures basic application flow trace events.
            /// </summary>
            internal const EventKeywords Trace = (EventKeywords)1;

            /// <summary>
            /// Captures basic application scope entering and exiting events.
            /// </summary>
            internal const EventKeywords Scope = (EventKeywords)2;
        }
        #endregion

        #region Tasks
        /// <summary>
        /// Tasks supported by AKV Provider's EventSource implementation
        /// </summary>
        public class Tasks
        {
            /// <summary>
            /// Task that tracks trace scope.
            /// </summary>
            public const EventTask Scope = (EventTask)1;
        }
        #endregion

        [NonEvent]
        internal bool IsTraceEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.Trace);

        [NonEvent]
        internal bool IsScopeEnabled() => Log.IsEnabled(EventLevel.Informational, Keywords.Scope);

        #region Event Methods
        [NonEvent]
        internal void TryTraceEvent(string message, object p1 = null, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "")
        {
            if (IsTraceEnabled())
            {
                WriteTrace(string.Format("Caller: {0}, Message: {1}", memberName,
                    p1 == null ? message :
                    string.Format(message, p1.ToString().Length > 10 ?
                    p1.ToString().Substring(0, 10) + "..." : p1)));
            }
        }
        [NonEvent]
        internal long TryScopeEnterEvent(string memberName)
        {
            if (IsScopeEnabled())
            {
                return ScopeEnter(memberName);
            }
            return default;
        }
        [NonEvent]
        internal void TryScopeExitEvent(long scopeId)
        {
            if (Log.IsScopeEnabled())
            {
                ScopeExit(scopeId);
            }
        }
        #endregion

        #region Write Events
        [Event(1, Level = EventLevel.Informational, Keywords = Keywords.Trace)]
        internal void WriteTrace(string message) => WriteEvent(1, message);

        [Event(2, Level = EventLevel.Informational, Task = Tasks.Scope, Opcode = EventOpcode.Start, Keywords = Keywords.Scope)]
        internal long ScopeEnter(string caller)
        {
            SetCurrentThreadActivityId(GetCorrelationActivityId());
            long scopeId = Interlocked.Increment(ref s_nextScopeId);
            WriteEvent(2, string.Format("Entered Scope: {0}, Caller: {1}", scopeId, caller));
            return scopeId;
        }

        [Event(3, Level = EventLevel.Informational, Task = Tasks.Scope, Opcode = EventOpcode.Stop, Keywords = Keywords.Scope)]
        internal void ScopeExit(long scopeId) => WriteEvent(3, scopeId);
        #endregion
    }

    internal readonly struct AKVScope : IDisposable
    {
        private readonly long _scopeId;

        public AKVScope(long scopeID) => _scopeId = scopeID;
        public void Dispose() =>
            AKVEventSource.Log.TryScopeExitEvent(_scopeId);

        public static AKVScope Create([System.Runtime.CompilerServices.CallerMemberName] string memberName = "") =>
            new(AKVEventSource.Log.TryScopeEnterEvent(memberName));
    }
}
