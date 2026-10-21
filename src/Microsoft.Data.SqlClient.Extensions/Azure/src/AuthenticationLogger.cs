// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Abstractions;

namespace Microsoft.Data.SqlClient;

/// <summary>Adapts SDK diagnostics to the provider callback and shared SqlClient EventSource.</summary>
internal sealed class AuthenticationLogger
{
    private static readonly AsyncLocal<Scope?> s_scope = new();
    private static readonly AsyncLocal<bool> s_dispatching = new();
    private readonly Action<SqlAuthenticationLogEntry>? _callback;

    /// <summary>Copies the provider-owned logging settings.</summary>
    /// <param name="options">The provider options, already checked for null by the provider constructor.</param>
    internal AuthenticationLogger(ActiveDirectoryAuthenticationProviderOptions options)
    {
        _callback = options.AuthenticationLoggingCallback;
        EnableMsalPiiLogging = options.EnableMsalPiiLogging && HasCallback;
    }

    /// <summary>Gets whether direct MSAL may produce PII-bearing entries for an opted-in callback.</summary>
    internal bool EnableMsalPiiLogging { get; }

    /// <summary>Gets whether active acquisitions require SDK diagnostics independently of tracing.</summary>
    internal bool HasCallback => _callback is not null;

    /// <summary>Gets the active acquisition, excluding disposed scopes and callback-induced logging.</summary>
    internal static Scope? CurrentScope => !s_dispatching.Value && s_scope.Value is { IsActive: true } scope ? scope : null;

    /// <summary>Checks output demand before MSAL formats a diagnostic or the dispatcher allocates payloads.</summary>
    /// <param name="level">The mapped SDK severity.</param>
    internal bool IsEnabled(EventLevel level) => HasCallback ||
        SqlClientEventSource.Log.IsEnabled(level, SqlClientEventSource.Keywords.Trace);

    /// <summary>Starts a nested, async-flow-local acquisition and registers any Azure SDK logging demand.</summary>
    /// <param name="connectionId">The connection identifier supplied to the authentication provider.</param>
    /// <param name="captureAzure">Whether this acquisition uses extension-owned Azure credentials.</param>
    /// <returns>A scope that must be disposed to restore its predecessor and release listener demand.</returns>
    /// <remarks>Await <see cref="Scope.WaitForReadyAsync"/> before calling Azure credential APIs.</remarks>
    internal Scope Begin(Guid connectionId, bool captureAzure = false) => new(this, connectionId, captureAzure);

    /// <summary>Adapts the supported MSAL callback without exposing SDK severity values to consumers.</summary>
    /// <param name="level">The severity supplied by MSAL.</param>
    /// <param name="message">The original formatted diagnostic.</param>
    /// <param name="containsPii">The privacy marker supplied by MSAL.</param>
    internal void LogMsal(LogLevel level, string message, bool containsPii)
    {
        EventLevel? mapped = level switch
        {
            LogLevel.Always => EventLevel.LogAlways,
            LogLevel.Error => EventLevel.Error,
            LogLevel.Warning => EventLevel.Warning,
            LogLevel.Info => EventLevel.Informational,
            LogLevel.Verbose => EventLevel.Verbose,
            _ => null
        };
        if (mapped.HasValue)
        {
            Write(mapped.Value, message, containsPii, "MSAL");
        }
    }

    /// <summary>Filters and independently delivers a diagnostic belonging to the active logging policy.</summary>
    /// <param name="level">The framework event severity.</param>
    /// <param name="message">The formatted diagnostic, not an unclassified exception payload.</param>
    /// <param name="containsPii">Whether the message requires callback-only PII opt-in.</param>
    /// <param name="source">The diagnostic origin, such as MSAL, Azure-Identity, or SqlClient.</param>
    /// <remarks>
    /// Callbacks must not throw; nonfatal synchronous failures are defensively isolated and reported
    /// without forwarding exception text. This cannot isolate exceptions from async-void callbacks.
    /// </remarks>
    internal void Write(EventLevel level, string message, bool containsPii, string source)
    {
        Scope? scope = CurrentScope;
        if (!IsEnabled(level) || scope is null || !ReferenceEquals(this, scope.Logger) || (containsPii && !EnableMsalPiiLogging))
        {
            return;
        }

        // The guard flows into SDK work started by a callback, preventing indirect logging loops.
        s_dispatching.Value = true;
        try
        {
            if (!containsPii && SqlClientEventSource.Log.IsEnabled(level, SqlClientEventSource.Keywords.Trace))
            {
                // Deliver tracing first so a callback failure cannot suppress this independent sink.
                SqlClientEventSource.Log.TryAuthenticationTrace(level, message, source, scope.ConnectionId.ToString());
            }

            if (_callback is not null)
            {
                try
                {
                    _callback(new SqlAuthenticationLogEntry(level, message, containsPii, source, scope.ConnectionId));
                }
                catch (Exception ex) when (ex is not OutOfMemoryException
                    and not StackOverflowException and not AccessViolationException and not ThreadAbortException)
                {
                    // Callback content and exception messages can themselves contain PII.
                    SqlClientEventSource.Log.TryAuthenticationTrace(EventLevel.Error,
                        "Authentication logging callback failed.", "SqlClient", scope.ConnectionId.ToString());
                }
            }
        }
        finally
        {
            s_dispatching.Value = false;
        }
    }

    /// <summary>Routes cached SDK clients through the current acquisition without retaining a provider.</summary>
    /// <param name="level">The severity supplied by MSAL.</param>
    /// <param name="message">The formatted SDK diagnostic.</param>
    /// <param name="containsPii">The original SDK privacy marker.</param>
    internal static void LogMsalForCurrentScope(LogLevel level, string message, bool containsPii) =>
        CurrentScope?.Logger.LogMsal(level, message, containsPii);

    /// <summary>Gets the stateless non-PII logger shared by cached clients; IsEnabled gates SDK formatting.</summary>
    internal static IIdentityLogger MsalLogger { get; } = new ScopedMsalLogger();

    /// <summary>Adapts the SDK's dynamically gated logger only for clients with PII permanently disabled.</summary>
    private sealed class ScopedMsalLogger : IIdentityLogger
    {
        /// <inheritdoc />
        public bool IsEnabled(EventLogLevel eventLogLevel) => Map(eventLogLevel) is { } level &&
            CurrentScope?.Logger.IsEnabled(level) == true;

        /// <inheritdoc />
        public void Log(LogEntry entry)
        {
            if (Map(entry.EventLogLevel) is { } level)
            {
                CurrentScope?.Logger.Write(level, entry.Message ?? "MSAL emitted a diagnostic without message text.", false, "MSAL");
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
        /// <param name="logger">The immutable logging policy for this acquisition.</param>
        /// <param name="connectionId">The connection correlation identifier.</param>
        /// <param name="captureAzure">Whether to capture supported Azure credential diagnostics.</param>
        internal Scope(AuthenticationLogger logger, Guid connectionId, bool captureAzure)
        {
            Logger = logger;
            ConnectionId = connectionId;
            CaptureAzure = captureAzure;
            _previous = s_scope.Value;
            s_scope.Value = this;
        }

        /// <summary>Waits for SDK capture to meet active callback and collector demand.</summary>
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
                    _ready = _listener.Enter(Logger);
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

        /// <summary>Gets the policy that owns diagnostics in this scope.</summary>
        internal AuthenticationLogger Logger { get; }

        /// <summary>Gets the connection identifier carried by callback entries and trace events.</summary>
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
                _listener?.Leave(Logger);
            }
        }
    }
}
