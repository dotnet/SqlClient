// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Telemetry
{
    internal class ComponentTelemetry
    {
        public SqlClientEventSource Tracing { get; }

        public ComponentTelemetry(SqlClientEventSource tracing)
        {
            Tracing = tracing;
        }
    }

    internal sealed class ComponentTelemetry<TMetrics> : ComponentTelemetry
        where TMetrics : class, IMetrics
    {
        public TMetrics Metrics { get; }

        public ComponentTelemetry(SqlClientEventSource tracing, TMetrics metrics)
            : base(tracing)
        {
            Metrics = metrics;
        }
    }
}
