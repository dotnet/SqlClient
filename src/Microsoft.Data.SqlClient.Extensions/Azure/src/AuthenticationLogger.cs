// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.IdentityModel.Abstractions;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Routes non-PII MSAL and Azure.Identity diagnostics to <see cref="SqlClientEventSource.Log"/>.
/// SDK callbacks do not carry a connection ID, so an async-local scope supplies it.
/// </summary>
internal static class AuthenticationLogger
{
    private static readonly AsyncLocal<Scope?> s_scope = new();
    private static readonly AsyncLocal<bool> s_dispatching = new();

    /// <summary>Gets the active acquisition, excluding disposed scopes and reentrant logging.</summary>
    internal static Scope? CurrentScope => !s_dispatching.Value && s_scope.Value is { IsActive: true } scope ? scope : null;

    /// <summary>Checks collector demand before an SDK formats a diagnostic.</summary>
    /// <param name="level">The mapped SDK severity.</param>
    internal static bool IsEnabled(EventLevel level) =>
        SqlClientEventSource.Log.IsEnabled(level, SqlClientEventSource.Keywords.AuthenticationTrace);

    /// <summary>Starts a nested, async-flow-local acquisition scope.</summary>
    /// <param name="connectionId">The connection identifier supplied to the authentication provider.</param>
    /// <param name="captureAzure">Whether this acquisition uses extension-owned Azure credentials.</param>
    /// <returns>A scope that must be disposed to restore its predecessor and release listener demand.</returns>
    /// <remarks>Await <see cref="Scope.WaitForReadyAsync"/> before calling Azure credential APIs.</remarks>
    internal static Scope Begin(Guid connectionId, bool captureAzure = false) => new(connectionId, captureAzure);

    /// <summary>Writes a diagnostic for the active acquisition.</summary>
    /// <remarks>
    /// All authentication events use this path so in-process collectors cannot attribute their own
    /// credential or SqlClient work to this acquisition.
    /// </remarks>
    /// <param name="level">The framework event severity.</param>
    /// <param name="message">
    /// The non-PII diagnostic. Callers pass fixed text or SDK output from clients with PII logging disabled,
    /// never raw exception payloads.
    /// </param>
    /// <param name="source">The diagnostic origin: SqlClient, MSAL, or Azure-Identity.</param>
    internal static void Write(EventLevel level, string message, string source)
    {
        Scope? scope = CurrentScope;
        if (scope is null || !IsEnabled(level))
        {
            return;
        }

        // In-process collectors can synchronously trigger more SDK work.
        s_dispatching.Value = true;
        try
        {
            SqlClientEventSource.Log.TryAuthTraceEvent(level, message, source, scope.ConnectionId);
        }
        finally
        {
            s_dispatching.Value = false;
        }
    }

    /// <summary>Writes an ordinary Trace event without attributing collector work to the active acquisition.</summary>
    /// <param name="message">The trace message.</param>
    internal static void Trace(string message)
    {
        if (SqlClientEventSource.Log.IsTraceEnabled())
        {
            Dispatch(message);
        }
    }

    /// <summary>Writes a formatted ordinary Trace event without attributing collector work to the active acquisition.</summary>
    /// <typeparam name="T0">The type of the first argument.</typeparam>
    /// <param name="message">A composite format string.</param>
    /// <param name="arg0">The first format argument.</param>
    internal static void Trace<T0>(string message, T0 arg0)
    {
        if (SqlClientEventSource.Log.IsTraceEnabled())
        {
            Dispatch(string.Format(message, arg0?.ToString() ?? "null"));
        }
    }

    /// <summary>Writes a formatted ordinary Trace event without attributing collector work to the active acquisition.</summary>
    /// <typeparam name="T0">The type of the first argument.</typeparam>
    /// <typeparam name="T1">The type of the second argument.</typeparam>
    /// <param name="message">A composite format string.</param>
    /// <param name="arg0">The first format argument.</param>
    /// <param name="arg1">The second format argument.</param>
    internal static void Trace<T0, T1>(string message, T0 arg0, T1 arg1)
    {
        if (SqlClientEventSource.Log.IsTraceEnabled())
        {
            Dispatch(string.Format(message, arg0?.ToString() ?? "null", arg1?.ToString() ?? "null"));
        }
    }

    /// <summary>Runs collector dispatch with SDK attribution suppressed, restoring the prior guard state.</summary>
    private static void Dispatch(string message)
    {
        bool previous = s_dispatching.Value;
        s_dispatching.Value = true;
        try
        {
            SqlClientEventSource.Log.TryTraceEvent(message);
        }
        finally
        {
            s_dispatching.Value = previous;
        }
    }

    /// <summary>Gets the stateless non-PII logger shared by cached clients; IsEnabled gates SDK formatting.</summary>
    internal static IIdentityLogger MsalLogger { get; } = new ScopedMsalLogger();

    /// <summary>Adapts the SDK's dynamically gated logger only for clients with PII permanently disabled.</summary>
    private sealed class ScopedMsalLogger : IIdentityLogger
    {
        /// <inheritdoc />
        public bool IsEnabled(EventLogLevel eventLogLevel) => Map(eventLogLevel) is { } level &&
            CurrentScope is not null && AuthenticationLogger.IsEnabled(level);

        /// <inheritdoc />
        public void Log(LogEntry entry)
        {
            if (Map(entry.EventLogLevel) is { } level)
            {
                Write(level, entry.Message ?? "MSAL emitted a diagnostic without message text.", "MSAL");
            }
        }

        /// <summary>Maps the independent identity-logger severity contract explicitly.</summary>
        private static EventLevel? Map(EventLogLevel level) => level switch
        {
            EventLogLevel.LogAlways => EventLevel.LogAlways,
            EventLogLevel.Critical => EventLevel.Critical,
            EventLogLevel.Error => EventLevel.Error,
            EventLogLevel.Warning => EventLevel.Warning,
            EventLogLevel.Informational => EventLevel.Informational,
            EventLogLevel.Verbose => EventLevel.Verbose,
            _ => null
        };
    }

    /// <summary>Owns correlation and listener demand for one acquisition across asynchronous continuations.</summary>
    internal sealed class Scope : IDisposable
    {
        private readonly Scope? _previous;
        private readonly object _gate = new();
        private AzureAuthenticationEventListener? _listener;
        private Task _ready = Task.CompletedTask;
        private int _active = 1;

        /// <summary>Saves the predecessor and establishes this acquisition's correlation and SDK demand.</summary>
        /// <param name="connectionId">The connection correlation identifier.</param>
        /// <param name="captureAzure">Whether to capture supported Azure credential diagnostics.</param>
        internal Scope(Guid connectionId, bool captureAzure)
        {
            ConnectionId = connectionId;
            CaptureAzure = captureAzure;
            _previous = s_scope.Value;
            s_scope.Value = this;
        }

        /// <summary>Waits for SDK capture to meet collector demand.</summary>
        /// <param name="cancellationToken">Cancels waiting without blocking EventSource command callbacks.</param>
        /// <returns>A task that completes when capture is ready.</returns>
        internal async Task WaitForReadyAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CaptureAzure)
            {
                return;
            }

            Task<AzureAuthenticationEventListener> initialization = AzureAuthenticationEventListener.GetInstanceAsync();
            await WaitWithCancellationAsync(initialization, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (!IsActive)
                {
                    throw new ObjectDisposedException(nameof(Scope));
                }

                if (_listener is null)
                {
                    _listener = initialization.GetAwaiter().GetResult();
                    _ready = _listener.Enter();
                }
            }
            await WaitWithCancellationAsync(_ready, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Cancels only this acquisition's wait, not the shared listener or subscription update.</summary>
        private static async Task WaitWithCancellationAsync(Task ready, CancellationToken cancellationToken)
        {
            if (!ready.IsCompleted && cancellationToken.CanBeCanceled)
            {
                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => canceled.TrySetResult(true)))
                {
                    await Task.WhenAny(ready, canceled.Task).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            await ready.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>Gets the connection identifier carried by trace events.</summary>
        internal Guid ConnectionId { get; }

        /// <summary>Gets whether this scope may attribute supported Azure-Identity events.</summary>
        internal bool CaptureAzure { get; }

        /// <summary>Gets whether the scope remains active, including in inherited execution contexts.</summary>
        internal bool IsActive => Volatile.Read(ref _active) != 0;

        /// <summary>Deactivates this scope once, restores its predecessor, and releases SDK demand.</summary>
        public void Dispose()
        {
            // AsyncLocal values are copied into child contexts; the shared active flag invalidates all copies.
            if (Interlocked.Exchange(ref _active, 0) == 0)
            {
                return;
            }
            s_scope.Value = _previous;
            lock (_gate)
            {
                _listener?.Leave();
            }
        }
    }
}
