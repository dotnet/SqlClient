// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using System.Globalization;
using Microsoft.Data.SqlClient.Internal;

namespace Microsoft.Data.SqlClient;

/// <summary>Bridges a supported, non-PII subset of process-wide Azure-Identity events into SqlClient's EventSource.</summary>
/// <remarks>
/// <para>
/// <b>Lifetime and source discovery.</b> One listener exists per process. It is created lazily by
/// <see cref="Instance"/> on the first token request that uses Azure.Identity, and is never disposed. Azure.Core
/// names its identity source "Azure-Identity", or "Azure-Identity-N" when another load context already owns that
/// name. The listener finds sources that already exist through <see cref="EventSource.GetSources"/>, and sources
/// created later through <see cref="OnEventSourceCreated"/>. Each source is tracked by a weak reference, so the
/// listener never keeps a collectible load context alive.
/// </para>
/// <para>
/// <b>Subscription follows demand.</b> The listener subscribes to SDK sources only while some collector enables
/// SqlClient's AuthenticationTrace keyword. It hooks <see cref="EventSource.EventCommandExecuted"/> on
/// <see cref="SqlClientEventSource.Log"/>; each enable or disable command recalculates the desired level and
/// reapplies it to every tracked source. With no collector, SDK events are not raised at all.
/// </para>
/// <para>
/// <b>What is forwarded.</b> Only reviewed events are copied: MSAL log events (single string payload, produced by
/// clients with PII logging disabled), the DefaultAzureCredential selected type, and a generic message for
/// GetTokenFailed. GetTokenFailed is correlated to a connection through its parent request ID; its raw exception
/// payload is dropped because it can contain personal information. All other events are ignored.
/// </para>
/// <para>
/// <b>Concurrency.</b> <see cref="_gate"/> protects the source list and <see cref="_version"/>. SDK
/// enable/disable calls are made outside the lock, because the framework takes its own listener lock and can call
/// back into this class. Each demand change bumps the version. A pass that sees the version change restarts and
/// forces every source to be reapplied, so a stale level from an interrupted pass is always overwritten.
/// </para>
/// <para>
/// <b>Applied level.</b> <see cref="SourceState.Level"/> records the level this listener last applied, which
/// lets unchanged sources be skipped. <see cref="EventSource.IsEnabled(EventLevel, EventKeywords)"/> cannot be
/// used for this, because it merges every listener on the source.
/// </para>
/// <para>
/// The class stays as one unit because the discovery, demand, and forwarding parts share the same framework
/// callbacks and lock; splitting them would spread one ordering contract across several types.
/// <see cref="BeforeApplyForTest"/> and <see cref="GetAppliedLevel"/> exist only for tests.
/// </para>
/// </remarks>
internal sealed class AzureAuthenticationEventListener : EventListener
{
    /// <summary>The process-wide listener, created on first use and thread-safe by default.</summary>
    private static readonly Lazy<AzureAuthenticationEventListener> s_instance = new(() => new AzureAuthenticationEventListener());

    /// <summary>Protects <see cref="_sources"/> and <see cref="_version"/>; never held while calling SDK hooks.</summary>
    private readonly object _gate = new();

    /// <summary>Supported SDK sources seen so far, held weakly with the level this listener applied to each.</summary>
    private readonly List<SourceState> _sources = new();

    /// <summary>
    /// False while the base constructor runs. The base class calls <see cref="OnEventSourceCreated"/> before this
    /// class's fields are set, so those early calls are ignored and the constructor replays the sources instead.
    /// </summary>
    private readonly bool _ready;

    /// <summary>Bumped on every source or demand change, so an apply pass can detect that it is stale.</summary>
    private int _version;

    /// <summary>Observes current and future SDK sources without retaining their defining assemblies.</summary>
    private AzureAuthenticationEventListener()
    {
        // The base constructor has already called OnEventSourceCreated for every existing source, while _ready was
        // false, so those calls were ignored. From here on, new sources are handled as they are created.
        _ready = true;

        // Recalculate SDK subscriptions whenever a collector enables or disables SqlClient's EventSource.
        SqlClientEventSource.Log.EventCommandExecuted += OnSqlClientCommand;

        // Replay the sources ignored during base construction. A source created concurrently may be seen both here
        // and in OnEventSourceCreated; RegisterSource ignores the duplicate.
        foreach (EventSource source in EventSource.GetSources())
        {
            if (IsSupportedSourceName(source.Name))
            {
                RegisterSource(source);
            }
        }

        // Apply the current collector demand to the sources found above.
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

            // A running apply pass took its snapshot without this source; make it restart.
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

        // Apply now, inside the framework callback, so the source is configured before it writes its first event.
        ApplySource(RegisterSource(eventSource), eventSource, GetDesiredLevel());
    }

    /// <summary>Applies current demand before a collector's enable or disable command returns.</summary>
    /// <param name="sender">The SqlClient source.</param>
    /// <param name="args">The collector command.</param>
    private void OnSqlClientCommand(object? sender, EventCommandEventArgs args)
    {
        // Other commands, such as SendManifest, do not change which levels collectors want.
        if (args.Command is not (EventCommand.Enable or EventCommand.Disable))
        {
            return;
        }

        // Mark any pass in progress as stale before starting a new one.
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
            // Snapshot the sources under the lock, dropping ones that have been collected, then work outside it.
            lock (_gate)
            {
                version = _version;
                _sources.RemoveAll(state => !state.Source.TryGetTarget(out _));
                sources = _sources.ToArray();
            }

            EventLevel? desired = GetDesiredLevel();
            foreach (SourceState state in sources)
            {
                // A newer command or source arrived; stop and start over with fresh demand.
                if (version != Volatile.Read(ref _version))
                {
                    break;
                }

                if (state.Source.TryGetTarget(out EventSource? source))
                {
                    ApplySource(state, source, desired, force);
                }
            }

            // Nothing changed during the pass, so every source has the latest level.
            if (version == Volatile.Read(ref _version))
            {
                return;
            }

            // The interrupted pass may have applied a level after a newer pass applied its own, leaving the cached
            // level wrong. Reapply all sources on the next pass instead of trusting the cache.
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
        // Skip sources that already have the desired level, unless a restart requires a full reapply.
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
            // Keep the cache matching the SDK source, so a later pass retries this update.
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
        // Find the most detailed level any collector wants. LogAlways (0) is checked first, so a higher level wins.
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
        // EventListener receives events from every source it enabled; ignore anything else.
        if (!IsSupportedSourceName(eventData.EventSource.Name))
        {
            return;
        }

        // Each SDK event must match a reviewed ID, name, and payload shape before anything is copied.
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
