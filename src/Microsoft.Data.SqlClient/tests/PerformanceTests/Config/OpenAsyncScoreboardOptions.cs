// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Configures bounded E1-E4 workloads through the existing runner configuration.</summary>
    public sealed class OpenAsyncScoreboardOptions
    {
        public string[] Scenarios = { "E1", "E2", "E3", "E4" };
        public string[] Apis = { "Sync", "Async" };
        public int[] Concurrency = { 1, 8, 32 };
        public int OperationsPerWorker = 16;
        public int SaturatedPoolSize = 4;
        public int HoldMilliseconds = 10;

        /// <summary>Validates the complete matrix before any connection or benchmark starts.</summary>
        public OpenAsyncScoreboardCase[] GetCases()
        {
            ValidateChoices(Scenarios, new[] { "E1", "E2", "E3", "E4" }, nameof(Scenarios));
            ValidateChoices(Apis, new[] { "Sync", "Async" }, nameof(Apis));
            if (Concurrency == null || Concurrency.Length == 0 ||
                Concurrency.Any(value => value <= 0) || Concurrency.Distinct().Count() != Concurrency.Length)
            {
                throw new ArgumentException("Concurrency must contain distinct positive worker counts.");
            }
            if (OperationsPerWorker <= 0 || SaturatedPoolSize <= 0 || HoldMilliseconds <= 0)
            {
                throw new ArgumentException("OperationsPerWorker, SaturatedPoolSize and HoldMilliseconds must be positive.");
            }

            return (from scenario in Scenarios
                    from concurrency in Concurrency
                    from api in Apis
                    select new OpenAsyncScoreboardCase(scenario, api, concurrency,
                        scenario == "E4" ? 1 : OperationsPerWorker,
                        scenario == "E3" ? 0 : scenario == "E2" ? SaturatedPoolSize : concurrency,
                        scenario == "E2" ? HoldMilliseconds : 0)).ToArray();
        }

        private static void ValidateChoices(string[] values, string[] allowed, string name)
        {
            if (values == null || values.Length == 0 || values.Distinct().Count() != values.Length ||
                values.Any(value => !allowed.Contains(value)))
            {
                throw new ArgumentException($"{name} must contain distinct values from {string.Join(", ", allowed)}.");
            }
        }
    }

    /// <summary>Identifies one process-isolated API/scenario/concurrency combination.</summary>
    public sealed class OpenAsyncScoreboardCase
    {
        public string Scenario { get; }
        public string Api { get; }
        public int Concurrency { get; }
        public int OperationsPerWorker { get; }
        public int PoolSize { get; }
        public int HoldMilliseconds { get; }
        public string CaseId => $"OpenAsyncScoreboard-{Scenario}-{Api}-{Concurrency}";
        public int OperationCount => checked(Concurrency * OperationsPerWorker);

        internal OpenAsyncScoreboardCase(string scenario, string api, int concurrency,
            int operationsPerWorker, int poolSize, int holdMilliseconds)
        {
            Scenario = scenario;
            Api = api;
            Concurrency = concurrency;
            OperationsPerWorker = operationsPerWorker;
            PoolSize = poolSize;
            HoldMilliseconds = holdMilliseconds;
            _ = OperationCount;
        }

        public override string ToString() => CaseId;
    }
}
