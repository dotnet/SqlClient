// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Verifies how the corrective post-recovery <c>USE</c> batch derives its timeout from the
    /// remaining login budget.
    /// <para>
    /// The reconnection tests all run with a generous connect timeout and an immediate server
    /// response, so they never reach these boundaries.  Driving the calculation directly with a
    /// virtual clock covers them without depending on real elapsed time.
    /// </para>
    /// </summary>
    public class CorrectiveBatchTimeoutTest
    {
        /// <summary>
        /// An infinite login timeout must produce zero, the parser's "no timeout" value, rather
        /// than being clamped to a finite budget.  Zero ticks is the sentinel the timer treats
        /// as infinite, matching <c>Connect Timeout=0</c>.
        /// </summary>
        [Fact]
        public void InfiniteTimeout_ReturnsNoTimeout()
        {
            TimeoutTimer timeout = TimeoutTimer.StartNew(TimeSpan.Zero);

            Assert.True(timeout.IsInfinite);
            Assert.Equal(0, SqlConnectionInternal.GetCorrectiveBatchTimeoutSeconds(timeout));
        }

        /// <summary>
        /// An exhausted budget must fail instead of granting the batch more time, which is what
        /// restarting the connect timeout would have done.
        /// </summary>
        [Fact]
        public void ExpiredTimeout_Throws()
        {
            TimeoutTimer timeout = TimeoutTimer.StartExpired();

            Assert.True(timeout.IsExpired);
            Assert.Throws<SqlException>(
                () => SqlConnectionInternal.GetCorrectiveBatchTimeoutSeconds(timeout));
        }

        /// <summary>
        /// The batch timeout tracks the remaining budget rather than the original duration, so a
        /// login that already consumed part of the budget leaves correspondingly less.
        /// </summary>
        [Fact]
        public void PartiallyConsumedTimeout_ReturnsRemainingSeconds()
        {
            FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
            TimeoutTimer timeout = TimeoutTimer.StartNew(TimeSpan.FromSeconds(30), fake);

            fake.Advance(TimeSpan.FromSeconds(12));

            Assert.Equal(18, SqlConnectionInternal.GetCorrectiveBatchTimeoutSeconds(timeout));
        }

        /// <summary>
        /// A sub-second remainder is raised to one second.  Truncating to zero would reach the
        /// parser as "no timeout", which is the opposite of the intent, so the batch is allowed
        /// to outlive the remaining budget by under a second.
        /// </summary>
        [Fact]
        public void SubSecondRemainder_IsRaisedToOneSecond()
        {
            FakeTimeProvider fake = new(DateTimeOffset.UtcNow);
            TimeoutTimer timeout = TimeoutTimer.StartNew(TimeSpan.FromSeconds(10), fake);

            fake.Advance(TimeSpan.FromMilliseconds(9_500));

            Assert.False(timeout.IsExpired);
            Assert.Equal(1, SqlConnectionInternal.GetCorrectiveBatchTimeoutSeconds(timeout));
        }
    }
}
