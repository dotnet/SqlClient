// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using System.Globalization;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>Bridges a supported, non-PII subset of process-wide Azure-Identity events into SqlClient's EventSource.</summary>
/// <remarks>
/// Subscriptions follow collector demand for the SqlClient AuthenticationTrace keyword. Token failure events are
/// correlated through their parent request ID; other events are written with an empty connection ID.
/// </remarks>
internal sealed class AzureAuthenticationEventListener : EventListener
{
    private static readonly Lazy<AzureAuthenticationEventListener> s_instance = new(() => new AzureAuthenticationEventListener());
    private readonly object _gate = new();
    private readonly List<SourceState> _sources = new();
    private readonly bool _ready;
    private int _version;

    /// <summary>Observes current and future SDK sources without retaining their defining assemblies.</summary>
    private AzureAuthenticationEventListener()
    {
        _ready = true;
        SqlClientEventSource.Log.EventCommandExecuted += OnSqlClientCommand;
        foreach (EventSource source in EventSource.GetSources())
        {
            if (IsSupportedSourceName(source.Name))
            {
                RegisterSource(source);
            }
        }

        ApplySubscriptions();
    }

    /// <summary>Gets the shared listener, creating it on first use.</summary>
    internal static AzureAuthenticationEventListener Instance => s_instance.Value;

    /// <summary>Registers a source weakly so disposing it can release a collectible load context.</summary>
    /// <param name="source">The supported SDK source.</param>
    private SourceState RegisterSource(EventSource source)
    {
        lock (_gate)
        {
            SourceState? existing = Find(source);
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

    /// <summary>Configures new sources before their constructor can emit its first event.</summary>
    /// <param name="eventSource">A source discovered by the framework under its subscription lock.</param>
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        // The base constructor reports existing sources before this instance's fields are initialized.
        if (!_ready || !IsSupportedSourceName(eventSource.Name))
        {
            return;
        }

        ApplySource(RegisterSource(eventSource), eventSource, GetDesiredLevel());
    }

    /// <summary>Applies current demand before a collector's enable or disable command returns.</summary>
    /// <param name="sender">The SqlClient source.</param>
    /// <param name="args">The collector command.</param>
    private void OnSqlClientCommand(object? sender, EventCommandEventArgs args)
    {
        if (args.Command is not (EventCommand.Enable or EventCommand.Disable))
        {
            return;
        }

        lock (_gate)
        {
            _version++;
        }

        ApplySubscriptions();
    }

    /// <summary>Applies subscriptions outside the source lock.</summary>
    /// <remarks>
    /// Restarts when a nested or concurrent collector command changes demand. A restart reapplies every source,
    /// because a stale enable from the interrupted pass may have landed after the newer one.
    /// </remarks>
    private void ApplySubscriptions()
    {
        bool force = false;
        while (true)
        {
            SourceState[] sources;
            int version;
            lock (_gate)
            {
                version = _version;
                _sources.RemoveAll(state => !state.Source.TryGetTarget(out _));
                sources = _sources.ToArray();
            }

            EventLevel? desired = GetDesiredLevel();
            foreach (SourceState state in sources)
            {
                if (version != Volatile.Read(ref _version))
                {
                    break;
                }

                if (state.Source.TryGetTarget(out EventSource? source))
                {
                    ApplySource(state, source, desired, force);
                }
            }

            if (version == Volatile.Read(ref _version))
            {
                return;
            }

            force = true;
        }
    }

    /// <summary>Updates one SDK source.</summary>
    /// <param name="state">This listener's tracked subscription for the source.</param>
    /// <param name="source">The SDK source.</param>
    /// <param name="desired">The level to apply, or null to unsubscribe.</param>
    /// <param name="force">Reapplies even when the cached level matches, to repair a stale enable.</param>
    private void ApplySource(SourceState state, EventSource source, EventLevel? desired, bool force = false)
    {
        if (!force && state.Level == desired)
        {
            return;
        }

        // Publish before calling SDK hooks so nested collector commands see and can supersede this update.
        EventLevel? previous = state.Level;
        state.Level = desired;
        try
        {
            BeforeApplyForTest?.Invoke(source);
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

    /// <summary>Test hook run after a level is published and before it is applied to the SDK source.</summary>
    internal Action<EventSource>? BeforeApplyForTest { get; set; }

    /// <summary>Gets the level this listener last applied to a source.</summary>
    /// <remarks>
    /// <see cref="EventSource.IsEnabled(EventLevel, EventKeywords)"/> merges all listeners, and on .NET Framework
    /// it does not drop to a lower level on a later enable. This reports only this listener's subscription.
    /// </remarks>
    /// <param name="source">The SDK source.</param>
    /// <returns>The applied level, or null when not subscribed or not tracked.</returns>
    internal EventLevel? GetAppliedLevel(EventSource source)
    {
        lock (_gate)
        {
            return Find(source)?.Level;
        }
    }

    /// <summary>Finds a tracked source; the caller holds the lock.</summary>
    private SourceState? Find(EventSource source) =>
        _sources.Find(state => state.Source.TryGetTarget(out EventSource? target) && ReferenceEquals(target, source));

    /// <summary>Tracks this listener's subscription without owning the SDK source.</summary>
    private sealed class SourceState
    {
        /// <summary>Observes a source without extending its lifetime.</summary>
        internal SourceState(EventSource source) => Source = new(source);

        /// <summary>Gets the weakly held SDK source.</summary>
        internal WeakReference<EventSource> Source { get; }

        /// <summary>Gets or sets the level last applied by this listener.</summary>
        internal EventLevel? Level { get; set; }
    }

    /// <summary>Calculates SDK capture demand from current SqlClient collectors.</summary>
    /// <returns>The most detailed required SDK level, or null when no collector needs capture.</returns>
    private static EventLevel? GetDesiredLevel()
    {
        EventLevel? desired = null;
        for (EventLevel level = EventLevel.LogAlways; level <= EventLevel.Verbose; level++)
        {
            if (SqlClientEventSource.Log.IsEnabled(level, SqlClientEventSource.Keywords.AuthenticationTrace))
            {
                desired = level;
            }
        }

        // SDK failures (GetTokenFailed) are Informational but forwarded as Error, so subscribe at least at
        // Informational. This also covers LogAlways, which EventListener treats as all levels.
        // Write still filters each forwarded event by the collector's level.
        return desired is null or > EventLevel.Informational ? desired : EventLevel.Informational;
    }

    /// <summary>Forwards allowlisted SDK events.</summary>
    /// <param name="eventData">The SDK event; unrecognized or unsafe payloads are not copied.</param>
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (!IsSupportedSourceName(eventData.EventSource.Name))
        {
            return;
        }

        EventLevel? level = GetMsalLevel(eventData.EventId, eventData.EventName);

        // Azure credential MSAL events carry no request ID, so they cannot be correlated.
        // Extension-owned credentials keep unsafe and account logging disabled.
        if (level.HasValue && eventData.Payload?.Count == 1 && eventData.Payload[0] is string message)
        {
            AuthenticationLogger.Write(level.Value, message, "Azure-Identity", Guid.Empty);
        }
        else if (eventData.EventId == 13 && eventData.EventName == "DefaultAzureCredentialCredentialSelected" &&
            eventData.Payload?.Count == 1 && eventData.Payload[0] is string credentialType)
        {
            AuthenticationLogger.Write(EventLevel.Informational,
                $"DefaultAzureCredential selected {credentialType}.", "Azure-Identity", Guid.Empty);
        }
        else if (eventData.EventId == 3 && eventData.EventName == "GetTokenFailed" &&
            AuthenticationLogger.TryGetConnectionId(GetPayload(eventData, "parentRequestId"), out Guid connectionId))
        {
            // The SDK's raw exception payload can contain personal information.
            AuthenticationLogger.Write(EventLevel.Error,
                "Azure credential token request failed.", "Azure-Identity", connectionId);
        }
    }

    /// <summary>Reads a named string payload value.</summary>
    private static string? GetPayload(EventWrittenEventArgs eventData, string name)
    {
        int index = eventData.PayloadNames?.IndexOf(name) ?? -1;
        return index >= 0 && eventData.Payload is { } payload && index < payload.Count ? payload[index] as string : null;
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
