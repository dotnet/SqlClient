// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/SqlAuthenticationLogEntry/*'/>
public sealed class SqlAuthenticationLogEntry
{
    /// <summary>Creates a stable snapshot before invoking an application's logging callback.</summary>
    /// <param name="level">The SqlClient-owned diagnostic severity.</param>
    /// <param name="message">The formatted diagnostic text.</param>
    /// <param name="containsPii">Whether the message requires sensitive-data handling.</param>
    /// <param name="source">The diagnostic origin.</param>
    /// <param name="connectionId">The connection identifier, when correlation is available.</param>
    internal SqlAuthenticationLogEntry(SqlAuthenticationLogLevel level, string message, bool containsPii,
        string source, Guid? connectionId)
    {
        Level = level;
        Message = message;
        ContainsPii = containsPii;
        Source = source;
        ConnectionId = connectionId;
    }

    /// <summary>Gets the severity of this diagnostic.</summary>
    public SqlAuthenticationLogLevel Level { get; }

    /// <summary>Gets the formatted diagnostic message.</summary>
    public string Message { get; }

    /// <summary>Gets whether the message may contain personal or organizational information.</summary>
    public bool ContainsPii { get; }

    /// <summary>Gets the diagnostic origin, such as MSAL, Azure-Identity, or SqlClient.</summary>
    public string Source { get; }

    /// <summary>Gets the connection identifier, or null when the diagnostic cannot be correlated.</summary>
    public Guid? ConnectionId { get; }
}
