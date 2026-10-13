// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.PerformanceTests
{
    /// <summary>A scheduled arrival, including rejected arrivals and generator lateness.</summary>
    public sealed class OpenAsyncLoadOffer
    {
        public int RequestId { get; set; }
        public string Phase { get; set; } = string.Empty;
        public double ScheduledMilliseconds { get; set; }
        public double ActualOfferMilliseconds { get; set; }
        public bool Accepted { get; set; }
    }

    /// <summary>Fixed offered-load parameters and complete admission/drain evidence.</summary>
    public sealed class OpenAsyncLoadResult
    {
        public int PhaseMilliseconds { get; set; }
        public int QueueCapacity { get; set; }
        public int BaseRate { get; set; }
        public int OverloadRate { get; set; }
        public OpenAsyncLoadOffer[] Offers { get; set; } = Array.Empty<OpenAsyncLoadOffer>();
        public double GenerationStartMilliseconds { get; set; }
        public double GenerationCompletedMilliseconds { get; set; }
        public double DrainCompletedMilliseconds { get; set; }

        internal void EnsureValid(OpenAsyncScoreboardCase configuration, OpenAsyncSample[] samples)
        {
            if (PhaseMilliseconds != configuration.LoadPhaseMilliseconds ||
                QueueCapacity != configuration.LoadQueueCapacity || BaseRate != configuration.LoadBaseRate ||
                OverloadRate != configuration.LoadOverloadRate ||
                !double.IsFinite(GenerationStartMilliseconds) || GenerationStartMilliseconds < 0 ||
                GenerationCompletedMilliseconds < GenerationStartMilliseconds + 3.0 * PhaseMilliseconds ||
                DrainCompletedMilliseconds < GenerationCompletedMilliseconds)
            {
                throw new InvalidOperationException("Incomplete or mismatched load phases/drain.");
            }
            OpenAsyncLoadOffer[] expected = OpenAsyncLoadWorkload.Schedule(configuration).ToArray();
            if (Offers.Length != expected.Length)
            {
                throw new InvalidOperationException("Scheduled arrivals were omitted.");
            }
            var accepted = new Dictionary<int, OpenAsyncLoadOffer>();
            double previousOffer = 0;
            for (int index = 0; index < Offers.Length; index++)
            {
                OpenAsyncLoadOffer offer = Offers[index];
                if (offer == null || offer.RequestId != expected[index].RequestId ||
                    offer.Phase != expected[index].Phase ||
                    offer.ScheduledMilliseconds != GenerationStartMilliseconds + expected[index].ScheduledMilliseconds ||
                    !double.IsFinite(offer.ActualOfferMilliseconds) ||
                    offer.ActualOfferMilliseconds < offer.ScheduledMilliseconds ||
                    offer.ActualOfferMilliseconds < previousOffer ||
                    offer.ActualOfferMilliseconds > GenerationCompletedMilliseconds)
                {
                    throw new InvalidOperationException("Invalid scheduled arrival accounting.");
                }
                previousOffer = offer.ActualOfferMilliseconds;
                if (offer.Accepted)
                {
                    accepted.Add(offer.RequestId, offer);
                }
            }
            if (samples.Length != accepted.Count)
            {
                throw new InvalidOperationException("Accepted arrivals were not fully drained.");
            }
            foreach (OpenAsyncSample sample in samples)
            {
                if (!accepted.TryGetValue(sample.RequestId, out OpenAsyncLoadOffer? offer) ||
                    sample.Phase != offer.Phase || sample.OfferedMilliseconds != offer.ScheduledMilliseconds ||
                    sample.StartMilliseconds < offer.ActualOfferMilliseconds ||
                    sample.Worker < 0 || sample.Worker >= configuration.Concurrency ||
                    !sample.LifecycleCompletionMilliseconds.HasValue ||
                    sample.LifecycleCompletionMilliseconds > DrainCompletedMilliseconds)
                {
                    throw new InvalidOperationException("A sample does not match an accepted arrival.");
                }
            }
            foreach (IGrouping<int, OpenAsyncSample> worker in samples.GroupBy(sample => sample.Worker))
            {
                if (!worker.OrderBy(sample => sample.Operation).Select(sample => sample.Operation)
                    .SequenceEqual(Enumerable.Range(0, worker.Count())))
                {
                    throw new InvalidOperationException("Worker operation indexes are not sequential.");
                }
            }
        }
    }

    /// <summary>
    /// Independently scheduled, bounded open-loop load. Only the generator has a dedicated thread;
    /// Sync and Async opens both run on ordinary ThreadPool workers with one open per worker.
    /// </summary>
    public static class OpenAsyncLoadWorkload
    {
        /// <summary>
        /// Runs three fixed phases, rejecting full-queue arrivals rather than blocking or skipping ticks.
        /// Observer timing follows OpenAsyncWorkload: startup is after the epoch but before release;
        /// completion is after the generator and all accepted work finish and measurements are captured.
        /// </summary>
        public static Task<OpenAsyncInvocation> RunAsync(OpenAsyncScoreboardCase configuration,
            Func<DbConnection> connectionFactory, CancellationToken cancellationToken,
            Action<long, Func<(int Outstanding, int Owned)>>? onStarted = null, Action? onCompleted = null) =>
            RunCoreAsync(configuration, connectionFactory, cancellationToken, onStarted, onCompleted);

        // The offer observer is a test-only coordination seam; production never executes a callback per offer.
        internal static async Task<OpenAsyncInvocation> RunCoreAsync(OpenAsyncScoreboardCase configuration,
            Func<DbConnection> connectionFactory, CancellationToken cancellationToken,
            Action<long, Func<(int Outstanding, int Owned)>>? onStarted = null, Action? onCompleted = null,
            Action<OpenAsyncLoadOffer>? onOffered = null)
        {
            if (configuration.Scenario != "E6")
            {
                throw new ArgumentException("Open-loop load requires E6.", nameof(configuration));
            }
            var load = new OpenAsyncLoadResult
            {
                PhaseMilliseconds = configuration.LoadPhaseMilliseconds,
                QueueCapacity = configuration.LoadQueueCapacity,
                BaseRate = configuration.LoadBaseRate,
                OverloadRate = configuration.LoadOverloadRate
            };
            var result = new OpenAsyncInvocation
            {
                Load = load,
                AdmissionMilliseconds = Enumerable.Repeat(-1.0, configuration.Concurrency).ToArray()
            };
            var queue = Channel.CreateBounded<OpenAsyncLoadOffer>(new BoundedChannelOptions(load.QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleWriter = true,
                AllowSynchronousContinuations = false
            });
            var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var samples = new ConcurrentQueue<OpenAsyncSample>();
            var queries = new ConcurrentQueue<OpenAsyncQuerySample>();
            var offers = new List<OpenAsyncLoadOffer>();
            var counters = new OpenAsyncWorkload.Counters();
            using var measurement = new OpenAsyncWorkload.Measurement(result, counters);
            Task[] workers = Enumerable.Range(0, configuration.Concurrency).Select(worker => Task.Run(async () =>
            {
                if (!await start.Task.ConfigureAwait(false))
                {
                    return;
                }
                int operation = 0;
                // Cancellation must not discard accepted work: each queued item gets a canceled sample.
                await foreach (OpenAsyncLoadOffer offer in queue.Reader.ReadAllAsync().ConfigureAwait(false))
                {
                    var sample = new OpenAsyncSample
                    {
                        RequestId = offer.RequestId,
                        Worker = worker,
                        Operation = operation++,
                        OfferedMilliseconds = offer.ScheduledMilliseconds,
                        Phase = offer.Phase
                    };
                    samples.Enqueue(sample);
                    await OpenAsyncWorkload.ExecuteAsync(configuration, connectionFactory, cancellationToken,
                        measurement, sample, queries).ConfigureAwait(false);
                    if (sample.Operation == 0)
                    {
                        result.AdmissionMilliseconds[worker] = sample.StartMilliseconds;
                    }
                    await Task.Yield();
                }
            })).ToArray();
            Task generator = Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!start.Task.GetAwaiter().GetResult())
                    {
                        return;
                    }
                    load.GenerationStartMilliseconds = measurement.Timestamp();
                    foreach (OpenAsyncLoadOffer offer in Schedule(configuration))
                    {
                        offer.ScheduledMilliseconds += load.GenerationStartMilliseconds;
                        WaitUntil(offer.ScheduledMilliseconds);
                        offer.ActualOfferMilliseconds = measurement.Timestamp();
                        offer.Accepted = queue.Writer.TryWrite(offer);
                        offers.Add(offer);
                        onOffered?.Invoke(offer);
                    }
                    WaitUntil(load.GenerationStartMilliseconds + 3.0 * load.PhaseMilliseconds);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    result.WasCanceled = true;
                }
                finally
                {
                    load.GenerationCompletedMilliseconds = measurement.Timestamp();
                    queue.Writer.TryComplete();
                }
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Task all = Task.WhenAll(workers.Append(generator));
            try
            {
                onStarted?.Invoke(measurement.Epoch, counters.Read);
                start.SetResult(true);
                await all.ConfigureAwait(false);
            }
            finally
            {
                start.TrySetResult(false);
                try
                {
                    await all.ConfigureAwait(false);
                }
                finally
                {
                    try
                    {
                        load.DrainCompletedMilliseconds = measurement.Timestamp();
                        load.Offers = offers.ToArray();
                        result.Samples = samples.OrderBy(sample => sample.RequestId).ToArray();
                        result.WasCanceled |= cancellationToken.IsCancellationRequested;
                        measurement.Complete();
                    }
                    finally
                    {
                        onCompleted?.Invoke();
                    }
                }
            }
            return result;

            void WaitUntil(double dueMilliseconds)
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    double remaining = dueMilliseconds - measurement.Timestamp();
                    if (remaining <= 0)
                    {
                        return;
                    }
                    cancellationToken.WaitHandle.WaitOne((int)Math.Min(int.MaxValue, Math.Ceiling(remaining)));
                }
            }
        }

        internal static IEnumerable<OpenAsyncLoadOffer> Schedule(OpenAsyncScoreboardCase configuration)
        {
            int requestId = 0;
            string[] phases = { "Baseline", "Overload", "Recovery" };
            for (int phase = 0; phase < phases.Length; phase++)
            {
                int rate = phase == 1 ? configuration.LoadOverloadRate : configuration.LoadBaseRate;
                long count = ((long)configuration.LoadPhaseMilliseconds * rate + 999) / 1000;
                for (long tick = 0; tick < count; tick++)
                {
                    yield return new OpenAsyncLoadOffer
                    {
                        RequestId = checked(requestId++),
                        Phase = phases[phase],
                        ScheduledMilliseconds = phase * (double)configuration.LoadPhaseMilliseconds + tick * 1000.0 / rate
                    };
                }
            }
        }
    }
}
