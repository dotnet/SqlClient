// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Configures command and reader workloads independently of fixture setup.</summary>
    public class CommandRunnerJob : RunnerJob
    {
        // Shared defaults for command and reader jobs; configuration only needs overrides.
        // Enabled retains its inherited false default, so a job must be explicitly selected.
        public CommandRunnerJob()
        {
            LaunchCount = 1;
            IterationCount = 20;
            InvocationCount = 1;
            WarmupCount = 1;
            RowCount = 10000;
        }

        // Applies to SQL setup and measured commands, independently of the BDN case timeout.
        public int CommandTimeoutSeconds = 1800;

        // Sizes are parameters in the benchmark reports, so baseline comparisons retain them.
        // Encrypted jobs retain their smaller payloads; plaintext adds 128 MiB below.
        public int[] PayloadSizesBytes = { 1_048_576, 5_242_880, 10_485_760, 20_971_520 };

        /// <summary>Rejects invalid execution settings before SQL fixtures are acquired.</summary>
        public void Validate()
        {
            if (LaunchCount <= 0 || IterationCount <= 0 || InvocationCount <= 0 || WarmupCount < 0)
            {
                throw new ArgumentException("Launch, iteration, and invocation counts must be positive; warmup must be nonnegative.");
            }
            if (TimeoutMinutes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(TimeoutMinutes));
            }
            if (!string.IsNullOrEmpty(RunStrategy) &&
                !string.Equals(RunStrategy, "Throughput", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(RunStrategy, "Monitoring", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(RunStrategy, "ColdStart", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("RunStrategy must be Throughput, Monitoring, or ColdStart.", nameof(RunStrategy));
            }
            if (CommandTimeoutSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(CommandTimeoutSeconds));
            }
            if (RowCount < 0 || RowCount > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(RowCount));
            }
            if (PayloadSizesBytes == null || PayloadSizesBytes.Length == 0)
            {
                throw new ArgumentException("At least one payload size is required.",
                    nameof(PayloadSizesBytes));
            }
            HashSet<int> sizes = new();
            foreach (int size in PayloadSizesBytes)
            {
                if (size <= 0 || size > 1_073_741_824 || size % 2 != 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(PayloadSizesBytes),
                        "Payload sizes must be positive, even byte counts of at most 1 GiB.");
                }
                if (!sizes.Add(size))
                {
                    throw new ArgumentException("Payload sizes must not contain duplicates.", nameof(PayloadSizesBytes));
                }
            }
        }
    }

    /// <summary>Includes the default 128 MiB plaintext workload without enlarging encrypted jobs.</summary>
    public sealed class PlaintextLargeDataRunnerJob : CommandRunnerJob
    {
        public PlaintextLargeDataRunnerJob()
        {
            PayloadSizesBytes = [1_048_576, 5_242_880, 10_485_760, 20_971_520, 134_217_728];
            IterationCount = 5;
            RowCount = 0;
            RunStrategy = "Monitoring";
            TimeoutMinutes = 120;
        }
    }
}
