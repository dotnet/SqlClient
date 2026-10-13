// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>Configures E1-E6 workloads through the existing runner configuration.</summary>
    public sealed class OpenAsyncScoreboardOptions
    {
        public string[] Scenarios = { "E1", "E2", "E3", "E4" };
        public string[] Apis = { "Sync", "Async" };
        public string[] InstrumentationModes = { "Sampled" };
        public int[] Concurrency = { 1, 8, 32 };
        public int[] LoadConcurrency = { 1, 8, 32, 64 };
        // E6 preserves this metadata but derives its operation count from accepted scheduled arrivals.
        public int OperationsPerWorker = 16;
        public int SaturatedPoolSize = 4;
        public int HoldMilliseconds = 10;
        public int LoadPhaseMilliseconds = 2000;
        public int LoadQueueCapacity = 64;
        public int LoadBaseRate = 25;
        public int LoadOverloadRate = 800;

        /// <summary>Validates the complete matrix before any connection or benchmark starts.</summary>
        public OpenAsyncScoreboardCase[] GetCases()
        {
            ValidateChoices(Scenarios, new[] { "E1", "E2", "E3", "E4", "E5", "E6" }, nameof(Scenarios));
            ValidateChoices(Apis, new[] { "Sync", "Async" }, nameof(Apis));
            ValidateConcurrency(Concurrency, nameof(Concurrency));
            ValidateConcurrency(LoadConcurrency, nameof(LoadConcurrency));
            ValidateChoices(InstrumentationModes, new[] { "Sampled", "Diagnostic" }, nameof(InstrumentationModes));
            if (OperationsPerWorker <= 0 || SaturatedPoolSize <= 0 || HoldMilliseconds <= 0)
            {
                throw new ArgumentException("OperationsPerWorker, SaturatedPoolSize and HoldMilliseconds must be positive.");
            }
            if (LoadPhaseMilliseconds <= 0 || LoadQueueCapacity <= 0 || LoadBaseRate <= 0 ||
                LoadOverloadRate <= LoadBaseRate)
            {
                throw new ArgumentException("Load duration, capacity and rates must be positive, and overload must exceed baseline.");
            }
            _ = checked((int)(2 * (((long)LoadPhaseMilliseconds * LoadBaseRate + 999) / 1000) +
                ((long)LoadPhaseMilliseconds * LoadOverloadRate + 999) / 1000));

            return (from scenario in Scenarios
                    from concurrency in scenario == "E6" ? LoadConcurrency : Concurrency
                    from api in Apis
                    from instrumentation in InstrumentationModes
                    select new OpenAsyncScoreboardCase(scenario, api, concurrency,
                        scenario == "E4" ? 1 : OperationsPerWorker,
                        scenario == "E3" ? 0 : scenario == "E2" || scenario == "E6" ? SaturatedPoolSize : concurrency,
                        scenario == "E2" || scenario == "E6" ? HoldMilliseconds : 0,
                        instrumentation, LoadPhaseMilliseconds, LoadQueueCapacity, LoadBaseRate, LoadOverloadRate)).ToArray();
        }

        private static void ValidateConcurrency(int[] values, string name)
        {
            if (values == null || values.Length == 0 ||
                values.Any(value => value <= 0) || values.Distinct().Count() != values.Length)
            {
                throw new ArgumentException($"{name} must contain distinct positive worker counts.");
            }
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
        /// <summary>Cohort operation count per worker; retained as metadata but unused by E6.</summary>
        public int OperationsPerWorker { get; }
        public int PoolSize { get; }
        public int HoldMilliseconds { get; }
        public string Instrumentation { get; }
        public int LoadPhaseMilliseconds { get; }
        public int LoadQueueCapacity { get; }
        public int LoadBaseRate { get; }
        public int LoadOverloadRate { get; }
        public string CaseId => $"OpenAsyncScoreboard-{Scenario}-{Api}-{Concurrency}" +
            (Instrumentation == "Diagnostic" ? "-Diagnostic" : string.Empty);
        public int OperationCount => checked(Concurrency * OperationsPerWorker);

        internal OpenAsyncScoreboardCase(string scenario, string api, int concurrency,
            int operationsPerWorker, int poolSize, int holdMilliseconds,
            string instrumentation = "Sampled", int loadPhaseMilliseconds = 2000,
            int loadQueueCapacity = 64, int loadBaseRate = 25, int loadOverloadRate = 800)
        {
            Scenario = scenario;
            Api = api;
            Concurrency = concurrency;
            OperationsPerWorker = operationsPerWorker;
            PoolSize = poolSize;
            HoldMilliseconds = holdMilliseconds;
            Instrumentation = instrumentation;
            LoadPhaseMilliseconds = loadPhaseMilliseconds;
            LoadQueueCapacity = loadQueueCapacity;
            LoadBaseRate = loadBaseRate;
            LoadOverloadRate = loadOverloadRate;
            _ = OperationCount;
        }

        public override string ToString() => CaseId;
    }
}
