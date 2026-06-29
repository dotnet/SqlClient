using System.Diagnostics.Tracing;

namespace Microsoft.Data.SqlClient.Samples.ThreadStarvation;

/// <summary>
/// Listens for events from <c>Microsoft.Data.SqlClient.EventSource</c> and emits them via the
/// supplied output function.
/// </summary>
internal sealed class SqlClientEventListener : EventListener
{
    // ──────────────────────────────────────────────────────────────────
    #region Construction / Disposal

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlClientEventListener"/> class.
    /// </summary>
    /// <param name="output">The delegate invoked for each event message.</param>
    /// <param name="prefix">The prefix prepended to each emitted event message.</param>
    internal SqlClientEventListener(Action<string> output, string prefix)
    {
        _out = output;
        _prefix = prefix;
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Derived Methods

    /// <inheritdoc />
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name.Equals(
                "Microsoft.Data.SqlClient.EventSource", StringComparison.Ordinal))
        {
            // Enable all keywords at all levels.
            EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All);
        }
    }

    /// <inheritdoc />
    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        _out($"{_prefix} {eventData.EventName}: " +
            (eventData.Payload != null && eventData.Payload.Count > 0
            ? eventData.Payload[0]
            : string.Empty));
    }

    #endregion

    // ──────────────────────────────────────────────────────────────────
    #region Private Fields

    /// <summary>
    /// The delegate used to emit event messages.
    /// </summary>
    private readonly Action<string> _out;

    /// <summary>
    /// The prefix prepended to each emitted event message.
    /// </summary>
    private readonly string _prefix;

    #endregion
}
