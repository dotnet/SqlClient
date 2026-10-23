// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;
using Microsoft.Data.SqlClient.Internal;
using Microsoft.IdentityModel.Abstractions;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Routes non-PII MSAL and Azure.Identity diagnostics to <see cref="SqlClientEventSource.Log"/>.
/// </summary>
/// <remarks>
/// Correlation is best effort. MSAL events use the per-request correlation ID, which the provider sets to the
/// connection ID. Azure.Identity token events use the request's parent request ID. Events that cannot be
/// correlated are written with an empty connection ID.
/// </remarks>
internal static class AuthenticationLogger
{
    private const string ParentRequestIdPrefix = "SqlClient-";

    /// <summary>Checks collector demand before an SDK formats a diagnostic.</summary>
    /// <param name="level">The mapped SDK severity.</param>
    internal static bool IsEnabled(EventLevel level) =>
        SqlClientEventSource.Log.IsEnabled(level, SqlClientEventSource.Keywords.AuthenticationTrace);

    /// <summary>Writes an authentication diagnostic.</summary>
    /// <param name="level">The framework event severity.</param>
    /// <param name="message">
    /// The non-PII diagnostic. Callers pass fixed text or SDK output from clients with PII logging disabled,
    /// never raw exception payloads.
    /// </param>
    /// <param name="source">The diagnostic origin: SqlClient, MSAL, or Azure-Identity.</param>
    /// <param name="connectionId">The correlated connection ID, or <see cref="Guid.Empty"/> when unknown.</param>
    internal static void Write(EventLevel level, string message, string source, Guid connectionId) =>
        SqlClientEventSource.Log.TryAuthTraceEvent(level, message, source, connectionId);

    /// <summary>Builds the parent request ID passed to Azure credentials for a connection.</summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <remarks>The prefix keeps app-owned requests that use plain GUIDs from being attributed to SqlClient.</remarks>
    internal static string GetParentRequestId(Guid connectionId) => ParentRequestIdPrefix + connectionId.ToString("D");

    /// <summary>Reads the connection ID from a parent request ID created by <see cref="GetParentRequestId"/>.</summary>
    /// <param name="parentRequestId">The SDK event's parent request ID.</param>
    /// <param name="connectionId">The connection ID when parsing succeeds.</param>
    /// <returns><see langword="true"/> when the request was started by SqlClient.</returns>
    internal static bool TryGetConnectionId(string? parentRequestId, out Guid connectionId)
    {
        connectionId = Guid.Empty;
        return parentRequestId is not null &&
            parentRequestId.StartsWith(ParentRequestIdPrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(parentRequestId.Substring(ParentRequestIdPrefix.Length), "D", out connectionId);
    }

    /// <summary>Gets the stateless non-PII logger shared by cached clients; IsEnabled gates SDK formatting.</summary>
    internal static IIdentityLogger MsalLogger { get; } = new MsalEventLogger();

    /// <summary>Adapts MSAL's logger for clients created with PII logging disabled.</summary>
    private sealed class MsalEventLogger : IIdentityLogger
    {
        /// <inheritdoc />
        public bool IsEnabled(EventLogLevel eventLogLevel) =>
            Map(eventLogLevel) is { } level && AuthenticationLogger.IsEnabled(level);

        /// <inheritdoc />
        public void Log(LogEntry entry)
        {
            if (Map(entry.EventLogLevel) is { } level)
            {
                // The provider sets each request's MSAL correlation ID to the connection ID.
                Guid.TryParse(entry.CorrelationId, out Guid connectionId);
                Write(level, entry.Message ?? "MSAL emitted a diagnostic without message text.", "MSAL", connectionId);
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
}
