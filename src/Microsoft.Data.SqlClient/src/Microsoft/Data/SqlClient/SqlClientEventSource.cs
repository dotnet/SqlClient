// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Diagnostics;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Internal bridge that provides access to SqlClientMetrics from the main driver.
    /// The SqlClientEventSource class itself is now defined in the
    /// Microsoft.Data.SqlClient.Extensions.Logging package.
    /// </summary>
    internal static class SqlClientDiagnostics
    {
        private static bool s_initialMetricsEnabled = false;

        // Provides access to metrics counters.
        public static readonly SqlClientMetrics Metrics;

        static SqlClientDiagnostics()
        {
            // Register callback to handle EventSource Enable events for metrics.
            // This callback may fire before Metrics is assigned (during type initialization),
            // so we capture the flag to pass to the SqlClientMetrics constructor.
            SqlClientEventSource.OnEventSourceEnabled = () =>
            {
                if (Metrics is null)
                {
                    s_initialMetricsEnabled = true;
                }
                else
                {
                    Metrics.EnableEventCounters();
                }
            };

            Metrics = new SqlClientMetrics(
                SqlClientEventSource.Log,
                SqlClientEventSource.WasEnabled || s_initialMetricsEnabled);
        }
    }
}
