// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Data.SqlClient.Telemetry
{
    internal sealed class SqlClientTelemetry
    {
        private static SqlClientTelemetry s_instance;

        public static SqlClientTelemetry Instance => s_instance ??= new SqlClientTelemetry();

        public ComponentTelemetry General { get; private set; }

        public ComponentTelemetry<IConnectionMetrics> Connection { get; private set; }

        public ComponentTelemetry<ICommandMetrics> Command { get; private set; }

        public ComponentTelemetry<ITransactionMetrics> Transaction { get; private set; }

        private SqlClientTelemetry()
        {
            SqlClientMetrics clientMetrics = new(enablePlatformSpecificMetrics: true);

            General = new ComponentTelemetry(SqlClientEventSource.Log);
            Connection = new ComponentTelemetry<IConnectionMetrics>(SqlClientEventSource.Log, clientMetrics);
            Command = new ComponentTelemetry<ICommandMetrics>(SqlClientEventSource.Log, clientMetrics);
            Transaction = new ComponentTelemetry<ITransactionMetrics>(SqlClientEventSource.Log, clientMetrics);
        }
    }
}
