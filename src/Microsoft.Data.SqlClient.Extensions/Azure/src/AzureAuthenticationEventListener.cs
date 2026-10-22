// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using System.Globalization;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>Bridges a supported subset of process-wide Azure-Identity events into active provider scopes.</summary>
/// <remarks>One listener serves all providers; acquisition scopes supply ownership instead of cached credentials.</remarks>
internal sealed class AzureAuthenticationEventListener : EventListener
{
    // Provider-specific commands run subscription updates inside EventSource's serialized command path.
    private const EventCommand RefreshCommand = (EventCommand)1;
    private static readonly Lazy<Task<AzureAuthenticationEventListener>> s_instance = new(CreateAsync);
    private readonly object _gate = new();
    private readonly List<SourceState> _sources = new();
    private int _activeScopes;
    private int _callbackScopes;
    private readonly List<TaskCompletionSource<bool>> _pending = new();
    private bool _ready;
    private int _version;
    private int _refreshRequested;
    private int _refreshing;

    /// <summary>Observes current and future SDK sources without retaining their defining assemblies.</summary>
    private AzureAuthenticationEventListener()
    {
        Volatile.Write(ref _ready, true);
        SqlClientEventSource.Log.EventCommandExecuted += OnSqlClientCommand;
        foreach (EventSource source in EventSource.GetSources())
        {
            if (IsSupportedSourceName(source.Name))
            {
                RegisterSource(source);
            }
        }
    }

    /// <summary>Creates the listener off the acquisition thread without inheriting application callbacks.</summary>
    private static Task<AzureAuthenticationEventListener> CreateAsync()
    {
        if (ExecutionContext.IsFlowSuppressed())
        {
            return Task.Run(() => new AzureAuthenticationEventListener());
        }

        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(() => new AzureAuthenticationEventListener());
        }
    }

    /// <summary>Gets the shared initialization task; individual acquisitions can cancel their wait.</summary>
    internal static Task<AzureAuthenticationEventListener> GetInstanceAsync() => s_instance.Value;

    /// <summary>Registers an acquisition and completes only after its SDK demand is applied.</summary>
    /// <param name="logger">The acquisition's immutable policy.</param>
    internal Task Enter(AuthenticationLogger logger)
    {
        lock (_gate)
        {
            _activeScopes++;
            if (logger.HasCallback)
            {
                _callbackScopes++;
            }
        }
        return WaitForRefreshAsync();
    }

    /// <summary>Releases acquisition demand without waiting on an SDK command or the global EventSource lock.</summary>
    /// <param name="logger">The policy supplied when the acquisition entered.</param>
    internal void Leave(AuthenticationLogger logger)
    {
        lock (_gate)
        {
            _activeScopes--;
            if (logger.HasCallback)
            {
                _callbackScopes--;
            }

            _version++;
        }
        RequestRefresh();
    }

    /// <summary>Waits for a subscription refresh, including previously released demand.</summary>
    internal Task WaitForRefreshAsync()
    {
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _pending.Add(ready);
            _version++;
        }
        RequestRefresh();
        return ready.Task;
    }

    /// <summary>Registers a source weakly so disposing it can release a collectible load context.</summary>
    /// <param name="source">The supported SDK source.</param>
    private SourceState RegisterSource(EventSource source)
    {
        lock (_gate)
        {
            SourceState? existing = _sources.Find(state => state.Source.TryGetTarget(out EventSource? target) && ReferenceEquals(target, source));
            if (existing is not null)
            {
                return existing;
            }

            var state = new SourceState(source);
            _sources.Add(state);
            _version++;
            return state;
        }
    }

    /// <summary>Configures new sources synchronously before their constructor can emit its first event.</summary>
    /// <param name="eventSource">A source discovered by the framework under its subscription lock.</param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (!Volatile.Read(ref _ready) || !IsSupportedSourceName(eventSource.Name))
        {
            return;
        }

        SourceState state = RegisterSource(eventSource);
        EventLevel? desired;
        lock (_gate)
        {
            desired = GetDesiredLevel();
        }

        ApplySource(state, eventSource, desired);
    }

    /// <summary>Applies current demand before a collector's enable command returns.</summary>
    /// <param name="sender">The SqlClient source.</param>
    /// <param name="args">A collector command or the extension's internal refresh command.</param>
    private void OnSqlClientCommand(object? sender, EventCommandEventArgs args)
    {
        if (args.Command != RefreshCommand && args.Command != EventCommand.Enable && args.Command != EventCommand.Disable)
        {
            return;
        }

        lock (_gate)
        {
            _version++;
        }

        ApplySubscriptions();
    }

    /// <summary>Coalesces background command requests without capturing an acquisition's execution context.</summary>
    private void RequestRefresh()
    {
        Interlocked.Exchange(ref _refreshRequested, 1);
        if (Interlocked.CompareExchange(ref _refreshing, 1, 0) != 0)
        {
            return;
        }

        if (ExecutionContext.IsFlowSuppressed())
        {
            _ = Task.Run(Refresh);
        }
        else
        {
            using (ExecutionContext.SuppressFlow())
            {
                _ = Task.Run(Refresh);
            }
        }
    }

    /// <summary>Requests serialized updates without blocking acquisition startup or disposal.</summary>
    private void Refresh()
    {
        try
        {
            do
            {
                Interlocked.Exchange(ref _refreshRequested, 0);
                EventSource.SendCommand(SqlClientEventSource.Log, RefreshCommand, null);
            }
            while (Volatile.Read(ref _refreshRequested) != 0);
        }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
            if (Volatile.Read(ref _refreshRequested) != 0)
            {
                RequestRefresh();
            }
        }
    }

    /// <summary>Applies subscriptions within EventSource command serialization, never under the demand lock.</summary>
    /// <remarks>Reentrant collector commands take effect immediately; a stale outer snapshot cannot undo them.</remarks>
    private void ApplySubscriptions()
    {
        while (true)
        {
            EventLevel? desired;
            SourceState[] sources;
            TaskCompletionSource<bool>[] pending;
            int version;
            lock (_gate)
            {
                version = _version;
                desired = GetDesiredLevel();
                _sources.RemoveAll(state => !state.Source.TryGetTarget(out _));
                sources = _sources.ToArray();
                pending = _pending.ToArray();
                _pending.Clear();
            }
            try
            {
                foreach (SourceState state in sources)
                {
                    if (version != Volatile.Read(ref _version))
                    {
                        break;
                    }

                    if (state.Source.TryGetTarget(out EventSource? source))
                    {
                        ApplySource(state, source, desired);
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (TaskCompletionSource<bool> ready in pending)
                {
                    ready.TrySetException(ex);
                }

                throw;
            }
            lock (_gate)
            {
                if (version != _version)
                {
                    _pending.AddRange(pending);
                    continue;
                }
            }
            foreach (TaskCompletionSource<bool> ready in pending)
            {
                ready.TrySetResult(true);
            }

            return;
        }
    }

    /// <summary>Updates one SDK source; callers are already inside framework subscription serialization.</summary>
    private void ApplySource(SourceState state, EventSource source, EventLevel? desired)
    {
        if (state.Level == desired)
        {
            return;
        }
        // Publish before calling SDK hooks so nested collector commands see and can supersede this update.
        EventLevel? previous = state.Level;
        state.Level = desired;
        try
        {
            if (desired.HasValue)
            {
                EnableEvents(source, desired.Value);
            }
            else
            {
                DisableEvents(source);
            }
        }
        catch
        {
            state.Level = previous;
            throw;
        }
    }

    /// <summary>Tracks this listener's subscription without owning the SDK source.</summary>
    private sealed class SourceState
    {
        /// <summary>Observes a source without extending its lifetime.</summary>
        internal SourceState(EventSource source) => Source = new(source);
        /// <summary>Gets the weakly held SDK source.</summary>
        internal WeakReference<EventSource> Source { get; }
        /// <summary>Gets or sets the level last applied by this listener under framework serialization.</summary>
        internal EventLevel? Level { get; set; }
    }

    /// <summary>Calculates SDK capture demand from active callbacks and current SqlClient collectors.</summary>
    /// <returns>The most detailed required SDK level, or null when no sink needs capture.</returns>
    /// <remarks>The caller holds the demand lock; this method never takes EventListener's global lock.</remarks>
    private EventLevel? GetDesiredLevel()
    {
        if (_activeScopes == 0)
        {
            return null;
        }

        if (_callbackScopes > 0)
        {
            return EventLevel.Verbose;
        }

        EventLevel? desired = null;
        for (EventLevel level = EventLevel.LogAlways; level <= EventLevel.Verbose; level++)
        {
            if (SqlClientEventSource.Log.IsEnabled(level, SqlClientEventSource.Keywords.Trace))
            {
                desired = level;
            }
        }
        // A LogAlways subscription means all levels to EventListener, not Always-only.
        // Critical is the narrowest subscription that includes Always; dispatch still honors collector settings.
        return desired == EventLevel.LogAlways ? EventLevel.Critical : desired;
    }

    /// <summary>Attributes allowlisted SDK events to the current live extension acquisition.</summary>
    /// <param name="eventData">The SDK event; unrecognized or unsafe payloads are not copied.</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        AuthenticationLogger.Scope? scope = AuthenticationLogger.CurrentScope;
        if (scope is not { CaptureAzure: true } || !IsSupportedSourceName(eventData.EventSource.Name))
        {
            return;
        }

        EventLevel? level = GetMsalLevel(eventData.EventId, eventData.EventName);

        // Only extension-owned credentials with unsafe/account/content logging disabled enter this scope.
        // Azure's MSAL event schema does not retain the original PII flag.
        if (level.HasValue && eventData.Payload?.Count == 1 && eventData.Payload[0] is string message)
        {
            scope.Logger.Write(level.Value, message, false, "Azure-Identity");
        }
        else if (eventData.EventId == 13 && eventData.EventName == "DefaultAzureCredentialCredentialSelected" &&
            eventData.Payload?.Count == 1 && eventData.Payload[0] is string credentialType)
        {
            scope.Logger.Write(EventLevel.Informational,
                $"DefaultAzureCredential selected {credentialType}.", false, "Azure-Identity");
        }
        else if (eventData.EventId == 3 && eventData.EventName == "GetTokenFailed")
        {
            // The SDK's raw exception payload can contain personal information.
            scope.Logger.Write(EventLevel.Error,
                "Azure credential token request failed.", false, "Azure-Identity");
        }
    }

    /// <summary>Recognizes the SDK's provider name, including numeric suffixes used to avoid duplicate names.</summary>
    /// <param name="name">The name assigned by Azure.Core to an identity event source.</param>
    private static bool IsSupportedSourceName(string name)
    {
        const string prefix = "Azure-Identity-";
        return name == "Azure-Identity" ||
            (name.StartsWith(prefix, StringComparison.Ordinal) &&
            int.TryParse(name.Substring(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int suffix) && suffix > 0);
    }

    /// <summary>Recognizes the reviewed Azure SDK MSAL event IDs and names together.</summary>
    /// <param name="eventId">The SDK event identifier.</param>
    /// <param name="eventName">The SDK event name.</param>
    /// <returns>The framework event severity, or null for an unsupported schema.</returns>
    internal static EventLevel? GetMsalLevel(int eventId, string? eventName) => (eventId, eventName) switch
    {
        (7, "LogMsalVerbose") => EventLevel.Verbose,
        (8, "LogMsalInformational") => EventLevel.Informational,
        (9, "LogMsalWarning") => EventLevel.Warning,
        (10, "LogMsalError") => EventLevel.Error,
        (23, "LogMsalCritical") => EventLevel.Critical,
        (24, "LogMsalAlways") => EventLevel.LogAlways,
        _ => null
    };
}
