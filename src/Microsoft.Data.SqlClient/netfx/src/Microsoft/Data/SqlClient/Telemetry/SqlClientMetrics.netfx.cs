// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;

namespace Microsoft.Data.SqlClient.Telemetry
{
    internal sealed partial class SqlClientMetrics
    {
        private void InitializePlatformSpecificMetrics() { }

        private void IncrementPlatformSpecificMetric(string metricName, in TagList tagList) { }

        private void DecrementPlatformSpecificMetric(string metricName, in TagList tagList) { }

        private void DisposePlatformSpecificMetrics() { }
    }
}
