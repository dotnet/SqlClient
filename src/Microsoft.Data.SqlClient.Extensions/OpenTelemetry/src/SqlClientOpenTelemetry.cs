// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

namespace Microsoft.Data.SqlClient.Extensions.OpenTelemetry;

/// <summary>Internal-use bridge called by Microsoft.Data.SqlClient when the optional extension is present.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class SqlClientOpenTelemetry
{
    /// <summary>Begins an endpoint-owned telemetry route for one SqlClient connection open.</summary>
    /// <remarks>This entry point is for driver integration and is not intended for application use.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static IDisposable? Begin(string endpoint) => EndpointTelemetry.Begin(endpoint);

    /// <summary>Records metrics for one completed scope on an endpoint-owned route.</summary>
    /// <remarks>This entry point is for driver integration and is not intended for application use.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Record(IDisposable? route, string name, string outcome, string? category,
        double seconds, int retries, string? retryReason) =>
        EndpointTelemetry.Record(route, name, outcome, category, seconds, retries, retryReason);
}
