// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    public sealed class AsyncHealthOptions
    {
        public int DiagnosticRuns { get; set; } = 1;
        public int CadenceMilliseconds { get; set; } = 25;
        public int ProbeCapacity { get; set; } = 64;
        public int RecoveryMilliseconds { get; set; } = 500;
        public int MinimumWindowMilliseconds { get; set; } = 1000;
        public int MinimumSamples { get; set; } = 1;

        public void Validate()
        {
            if (DiagnosticRuns < 1)
            {
                throw new ArgumentException(
                    "Diagnostic run count must be positive.", nameof(DiagnosticRuns));
            }

            if (CadenceMilliseconds < 1)
            {
                throw new ArgumentException(
                    "Probe cadence must be at least 1 millisecond.", nameof(CadenceMilliseconds));
            }

            if (ProbeCapacity < 1 || ProbeCapacity > 4096)
            {
                throw new ArgumentException(
                    "Probe capacity must be between 1 and 4096, inclusive.", nameof(ProbeCapacity));
            }

            if (RecoveryMilliseconds < 0 || RecoveryMilliseconds > 30000)
            {
                throw new ArgumentException(
                    "Recovery duration must be between 0 and 30000 milliseconds, inclusive.",
                    nameof(RecoveryMilliseconds));
            }

            if (MinimumWindowMilliseconds < 1)
            {
                throw new ArgumentException(
                    "Minimum observation window must be at least 1 millisecond.",
                    nameof(MinimumWindowMilliseconds));
            }

            if (MinimumSamples < 1 || MinimumSamples > 100000)
            {
                throw new ArgumentException(
                    "Minimum sample count must be between 1 and 100000, inclusive.",
                    nameof(MinimumSamples));
            }
        }
    }
}
