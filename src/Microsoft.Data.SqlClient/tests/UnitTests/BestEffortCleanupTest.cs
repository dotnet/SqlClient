// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient.Internal;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>Defines result, exception identity, and cleanup ordering for the shared helper.</summary>
    public sealed class BestEffortCleanupTest
    {
        /// <summary>
        /// Cleanup runs once; individual failures retain identity and stacks, while two failures
        /// produce SqlException containing an ordered AggregateException.
        /// </summary>
        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public async Task Run_ReportsFailures(bool async, bool actionFails, bool cleanupFails)
        {
            var original = new InvalidOperationException("Operation failure");
            var secondary = new InvalidOperationException("Cleanup failure");
            int cleanupCalls = 0;
            int result = 0;
            Func<int> action = () => actionFails ? throw original : 42;
            Action cleanup = () =>
            {
                cleanupCalls++;
                if (cleanupFails)
                {
                    throw secondary;
                }
            };
            Exception actual = await Record.ExceptionAsync(async () =>
            {
                result = async
                    ? await BestEffortCleanup.RunAsync(async () =>
                    {
                        await Task.Yield();
                        return action();
                    }, cleanup)
                    : BestEffortCleanup.Run(action, cleanup);
            });

            Assert.Equal(1, cleanupCalls);
            if (actionFails && cleanupFails)
            {
                var sql = Assert.IsType<SqlException>(actual);
                var aggregate = Assert.IsType<AggregateException>(sql.InnerException);
                Assert.Collection(aggregate.InnerExceptions,
                    e => Assert.Same(original, e), e => Assert.Same(secondary, e));
                Assert.Single(sql.Errors);
                Assert.Contains(original.Message, sql.Message);
                Assert.Contains(secondary.Message, sql.Message);
            }
            else if (actionFails || cleanupFails)
            {
                Assert.Same(actionFails ? original : secondary, actual);
            }
            else
            {
                Assert.Null(actual);
                Assert.Equal(42, result);
            }
            if (actionFails)
            {
                Assert.Contains(nameof(Run_ReportsFailures), original.StackTrace);
            }
            if (cleanupFails)
            {
                Assert.Contains(nameof(Run_ReportsFailures), secondary.StackTrace);
            }
        }

        /// <summary>
        /// Uncatchable failures propagate directly, skipping cleanup when the action throws
        /// and bypassing aggregation when cleanup throws.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task Run_UncatchableFailurePropagates(bool async, bool duringCleanup)
        {
            var fatal = new OutOfMemoryException("Uncatchable sentinel");
            int cleanupCalls = 0;
            Func<int> action = () => duringCleanup
                ? throw new InvalidOperationException("Operation failure")
                : throw fatal;
            Action cleanup = () =>
            {
                cleanupCalls++;
                throw fatal;
            };
            Exception actual = await Record.ExceptionAsync(async () =>
            {
                if (async)
                {
                    await BestEffortCleanup.RunAsync(async () =>
                    {
                        await Task.Yield();
                        return action();
                    }, cleanup);
                }
                else
                {
                    BestEffortCleanup.Run(action, cleanup);
                }
            });
            Assert.Same(fatal, actual);
            Assert.Equal(duringCleanup ? 1 : 0, cleanupCalls);
            Assert.Contains(nameof(Run_UncatchableFailurePropagates), fatal.StackTrace);
        }

        /// <summary>Async cleanup waits for the operation to finish, including synchronous throws.</summary>
        [Fact]
        public async Task RunAsync_WaitsBeforeCleanup()
        {
            var gate = new TaskCompletionSource<int>();
            bool cleaned = false;
            Task<int> pending = BestEffortCleanup.RunAsync(() => gate.Task, () => cleaned = true);
            Assert.False(cleaned);
            gate.SetResult(42);
            Assert.Equal(42, await pending);
            Assert.True(cleaned);

            var original = new InvalidOperationException("Synchronous delegate failure");
            cleaned = false;
            Assert.Same(original, await Record.ExceptionAsync(() =>
                BestEffortCleanup.RunAsync<int>(() => throw original, () => cleaned = true)));
            Assert.True(cleaned);
        }
    }
}
