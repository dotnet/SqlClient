// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Tracing;

namespace Microsoft.Data.SqlClient;

/// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/SqlAuthenticationLogEntry/*'/>
public sealed class SqlAuthenticationLogEntry
{
    /// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/ctor/*'/>
    internal SqlAuthenticationLogEntry(
        EventLevel level,
        string message,
        bool containsPii,
        string source,
        Guid? connectionId)
    {
        Level = level;
        Message = message;
        ContainsPii = containsPii;
        Source = source;
        ConnectionId = connectionId;
    }

    /// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/Level/*'/>
    public EventLevel Level { get; }

    /// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/Message/*'/>
    public string Message { get; }

    /// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/ContainsPii/*'/>
    public bool ContainsPii { get; }

    /// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/Source/*'/>
    public string Source { get; }

    /// <include file='../doc/SqlAuthenticationLogEntry.xml' path='docs/members[@name="SqlAuthenticationLogEntry"]/ConnectionId/*'/>
    public Guid? ConnectionId { get; }
}
