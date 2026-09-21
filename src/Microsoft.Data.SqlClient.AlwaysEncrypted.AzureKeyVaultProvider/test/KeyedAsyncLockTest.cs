// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.Test
{
    /// <summary>
    /// Unit tests for <see cref="KeyedAsyncLock{TKey}"/>.
    /// </summary>
    /// <remarks>
    /// Every test that depends on a caller reaching a particular point uses an explicit
    /// <see cref="TaskCompletionSource{TResult}"/> handshake rather than a delay, so the tests are
    /// deterministic rather than timing dependent.
    /// </remarks>
    public class KeyedAsyncLockTest
    {
        /// <summary>
        /// Bounds how long a test will wait for an expected signal before failing, so a regression
        /// surfaces as a failure rather than a hung test run.
        /// </summary>
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        [Fact]
        public async Task AcquireAsync_UncontendedAcquisition_Succeeds()
        {
            KeyedAsyncLock<string> sut = new();

            using (await sut.AcquireAsync("key", CancellationToken.None))
            {
                Assert.Equal(1, sut.GateCount);
            }

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_SameKeyHeld_BlocksSecondCallerUntilReleased()
        {
            KeyedAsyncLock<string> sut = new();

            KeyedAsyncLock<string>.Releaser first = await sut.AcquireAsync("key", CancellationToken.None);

            Task<KeyedAsyncLock<string>.Releaser> second = sut.AcquireAsync("key", CancellationToken.None);
            Assert.False(second.IsCompleted, "The second caller must wait while the first holds the key.");

            first.Dispose();

            using (await WithTimeout(second))
            {
                Assert.Equal(1, sut.GateCount);
            }

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_DifferentKeys_DoNotBlockEachOther()
        {
            KeyedAsyncLock<string> sut = new();

            using (await sut.AcquireAsync("first", CancellationToken.None))
            using (await sut.AcquireAsync("second", CancellationToken.None))
            {
                Assert.Equal(2, sut.GateCount);
            }

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_ConcurrentCallersOnOneKey_NeverOverlap()
        {
            const int callerCount = 32;

            KeyedAsyncLock<string> sut = new();
            int active = 0;
            int maxActive = 0;

            async Task Contend()
            {
                using (await sut.AcquireAsync("key", CancellationToken.None))
                {
                    int current = Interlocked.Increment(ref active);
                    InterlockedMax(ref maxActive, current);

                    // Yield inside the guarded region so an overlap would be observed rather than
                    // hidden by the work completing synchronously.
                    await Task.Yield();

                    Interlocked.Decrement(ref active);
                }
            }

            await WithTimeout(Task.WhenAll(Enumerable.Range(0, callerCount).Select(_ => Task.Run(Contend))));

            Assert.Equal(1, maxActive);
            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_AlreadyCancelledToken_ThrowsWithoutCreatingGate()
        {
            KeyedAsyncLock<string> sut = new();
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sut.AcquireAsync("key", cts.Token));

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_CancelledWhileWaiting_ThrowsAndLeavesNoGate()
        {
            KeyedAsyncLock<string> sut = new();
            using CancellationTokenSource cts = new();

            KeyedAsyncLock<string>.Releaser holder = await sut.AcquireAsync("key", CancellationToken.None);

            Task waiter = sut.AcquireAsync("key", cts.Token);
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(waiter));

            // The holder still owns the gate, so it must not have been reclaimed by the abandoned wait.
            Assert.Equal(1, sut.GateCount);

            holder.Dispose();

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_ManyCancelledWaiters_DoNotAccumulateGates()
        {
            const int waiterCount = 64;

            KeyedAsyncLock<string> sut = new();
            using CancellationTokenSource cts = new();

            KeyedAsyncLock<string>.Releaser holder = await sut.AcquireAsync("key", CancellationToken.None);

            Task[] waiters = Enumerable
                .Range(0, waiterCount)
                .Select(_ => sut.AcquireAsync("key", cts.Token))
                .Cast<Task>()
                .ToArray();

            cts.Cancel();

            // Every waiter must fault; none may acquire the key while it is held.
            foreach (Task waiter in waiters)
            {
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(waiter));
            }

            holder.Dispose();

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task AcquireAsync_CancellationOfWaiter_DoesNotDisturbWorkInFlight()
        {
            KeyedAsyncLock<string> sut = new();
            using CancellationTokenSource cts = new();

            KeyedAsyncLock<string>.Releaser holder = await sut.AcquireAsync("key", CancellationToken.None);

            Task cancelled = sut.AcquireAsync("key", cts.Token);
            Task<KeyedAsyncLock<string>.Releaser> survivor = sut.AcquireAsync("key", CancellationToken.None);

            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WithTimeout(cancelled));

            Assert.False(survivor.IsCompleted, "Cancelling one waiter must not hand the key to another.");

            holder.Dispose();

            using (await WithTimeout(survivor))
            {
                Assert.Equal(1, sut.GateCount);
            }

            Assert.Equal(0, sut.GateCount);
        }

        [Fact]
        public async Task Releaser_DisposedTwice_DoesNotHandKeyToTwoCallers()
        {
            KeyedAsyncLock<string> sut = new();

            KeyedAsyncLock<string>.Releaser releaser = await sut.AcquireAsync("key", CancellationToken.None);
            releaser.Dispose();
            releaser.Dispose();

            // A second release would have raised the gate's count, letting two callers in at once.
            using (await sut.AcquireAsync("key", CancellationToken.None))
            {
                Task<KeyedAsyncLock<string>.Releaser> blocked = sut.AcquireAsync("key", CancellationToken.None);
                Assert.False(blocked.IsCompleted, "Repeated disposal must not permit concurrent acquisition.");
            }
        }

        [Fact]
        public async Task AcquireAsync_GuardedWork_RunsOncePerKeyUnderContention()
        {
            const int keyCount = 8;
            const int callersPerKey = 8;

            KeyedAsyncLock<string> sut = new();
            ConcurrentDictionary<string, string> results = new();
            ConcurrentDictionary<string, int> invocations = new();

            async Task<string> GetOrCreate(string key)
            {
                if (results.TryGetValue(key, out string existing))
                {
                    return existing;
                }

                using (await sut.AcquireAsync(key, CancellationToken.None))
                {
                    if (results.TryGetValue(key, out existing))
                    {
                        return existing;
                    }

                    invocations.AddOrUpdate(key, 1, static (_, count) => count + 1);

                    // Simulate an awaited round trip so callers genuinely queue behind the gate.
                    await Task.Yield();

                    string created = "value:" + key;
                    results[key] = created;
                    return created;
                }
            }

            Task<string>[] callers = Enumerable
                .Range(0, keyCount)
                .SelectMany(keyIndex => Enumerable
                    .Range(0, callersPerKey)
                    .Select(_ => Task.Run(() => GetOrCreate("key" + keyIndex))))
                .ToArray();

            string[] values = await WithTimeout(Task.WhenAll(callers));

            Assert.Equal(keyCount, invocations.Count);
            Assert.All(invocations.Values, count => Assert.Equal(1, count));
            Assert.All(values, Assert.NotNull);
            Assert.Equal(0, sut.GateCount);
        }

        private static async Task<T> WithTimeout<T>(Task<T> task)
        {
            await WithTimeout((Task)task).ConfigureAwait(false);
            return await task.ConfigureAwait(false);
        }

        private static async Task WithTimeout(Task task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(Timeout)).ConfigureAwait(false);
            Assert.True(ReferenceEquals(completed, task), "Timed out waiting for the operation to complete.");

            await task.ConfigureAwait(false);
        }

        private static void InterlockedMax(ref int target, int value)
        {
            int current = Volatile.Read(ref target);
            while (value > current)
            {
                int observed = Interlocked.CompareExchange(ref target, value, current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }
    }
}
