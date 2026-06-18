// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.Extensions.TimeProvider.Testing;
using Microsoft.Data.SqlClient.ConnectionPool;
using Xunit;

#nullable enable

namespace Microsoft.Data.SqlClient.UnitTests.ConnectionPool
{
    /// <summary>
    /// Comprehensive unit tests for <see cref="BlockingPeriodErrorState"/> covering:
    /// - Initial state and error caching
    /// - <see cref="BlockingPeriodErrorState.Enter"/> and exception handling
    /// - <see cref="BlockingPeriodErrorState.Clear"/> and state reset
    /// - Exponential backoff progression (verified with <see cref="FakeTimeProvider"/>)
    /// - Timer-driven exit behavior (verified with <see cref="FakeTimeProvider"/>)
    /// - <see cref="IDisposable"/> implementation and timer cleanup
    /// - Callback invocation and re-entrancy safety
    /// </summary>
    public class BlockingPeriodErrorStateTest
    {
        #region HasError / initial state

        /// <summary>
        /// Verifies that a newly constructed <see cref="BlockingPeriodErrorState"/> has
        /// <see cref="BlockingPeriodErrorState.HasError"/> set to false.
        /// </summary>
        [Fact]
        public void HasError_InitialState_IsFalse()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);

            // Act & Assert
            Assert.False(state.HasError);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.ThrowIfActive"/> does not throw
        /// when called on a newly constructed instance with no cached error.
        /// </summary>
        [Fact]
        public void ThrowIfActive_InitialState_DoesNotThrow()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);

            // Act & Assert
            state.ThrowIfActive(); // Should complete without throwing
        }

        #endregion

        #region Enter

        /// <summary>
        /// Verifies that calling <see cref="BlockingPeriodErrorState.Enter"/> sets
        /// <see cref="BlockingPeriodErrorState.HasError"/> to true.
        /// </summary>
        [Fact]
        public void Enter_SetsHasErrorToTrue()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);

            // Act
            state.Enter(new InvalidOperationException("test"));

            // Assert
            Assert.True(state.HasError);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.ThrowIfActive"/> throws
        /// the exact exception type that was cached by <see cref="BlockingPeriodErrorState.Enter"/>.
        /// </summary>
        [Fact]
        public void Enter_ThrowIfActive_ThrowsCachedExceptionType()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            var exception = new InvalidOperationException("boom");

            // Act
            state.Enter(exception);

            // Assert
            var ex = Assert.Throws<InvalidOperationException>(() => state.ThrowIfActive());
            Assert.Equal("boom", ex.Message);
        }

        /// <summary>
        /// Verifies that when a <see cref="SqlException"/> is cached, <see cref="BlockingPeriodErrorState.ThrowIfActive"/>
        /// throws a cloned instance rather than the original, to avoid sharing stack traces across callers.
        /// </summary>
        [Fact]
        public void Enter_WithSqlException_ThrowsClonedInstance()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            var original = SqlExceptionHelper.CreateSqlException("connection failed");

            // Act
            state.Enter(original);
            var thrown = Assert.Throws<SqlException>(() => state.ThrowIfActive());

            // Assert
            Assert.NotSame(original, thrown);
            Assert.Equal(original.Message, thrown.Message);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Enter"/> invokes the optional
        /// onEnter callback after entering the blocking period.
        /// </summary>
        [Fact]
        public void Enter_InvokesOnEnterCallback()
        {
            // Arrange
            int callCount = 0;
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, onEnter: () => callCount++);

            // Act
            state.Enter(new Exception());

            // Assert
            Assert.Equal(1, callCount);
        }

        /// <summary>
        /// Verifies that calling <see cref="BlockingPeriodErrorState.Enter"/> a second time
        /// replaces the cached exception, invokes the callback again, and the new exception is thrown.
        /// </summary>
        [Fact]
        public void Enter_CalledTwice_ReplacesExceptionAndInvokesOnEnterAgain()
        {
            // Arrange
            int enterCount = 0;
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, onEnter: () => enterCount++);

            // Act
            state.Enter(new InvalidOperationException("first"));
            state.Enter(new ArgumentException("second"));

            // Assert
            Assert.Equal(2, enterCount);
            var ex = Assert.Throws<ArgumentException>(() => state.ThrowIfActive());
            Assert.Equal("second", ex.Message);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Enter"/> does not invoke
        /// the onExit callback (only the onEnter callback or the timer should trigger onExit).
        /// </summary>
        [Fact]
        public void Enter_DoesNotInvokeOnExitCallback()
        {
            // Arrange
            int exitCount = 0;
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, onExit: () => exitCount++);

            // Act
            state.Enter(new Exception());

            // Assert
            Assert.Equal(0, exitCount);
        }

        #endregion

        #region Clear

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Clear"/> resets
        /// <see cref="BlockingPeriodErrorState.HasError"/> to false.
        /// </summary>
        [Fact]
        public void Clear_AfterEnter_ResetsHasError()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            state.Enter(new Exception());

            // Act
            state.Clear();

            // Assert
            Assert.False(state.HasError);
        }

        /// <summary>
        /// Verifies that after <see cref="BlockingPeriodErrorState.Clear"/>, <see cref="BlockingPeriodErrorState.ThrowIfActive"/>
        /// does not throw because the cached error has been cleared.
        /// </summary>
        [Fact]
        public void Clear_AfterEnter_ThrowIfActiveDoesNotThrow()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            state.Enter(new Exception());

            // Act
            state.Clear();

            // Assert
            state.ThrowIfActive(); // Must not throw
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Clear"/> invokes the optional
        /// onExit callback after clearing the error state.
        /// </summary>
        [Fact]
        public void Clear_AfterEnter_InvokesOnExitCallback()
        {
            // Arrange
            int exitCount = 0;
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, onExit: () => exitCount++);
            state.Enter(new Exception());

            // Act
            state.Clear();

            // Assert
            Assert.Equal(1, exitCount);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Clear"/> on an initial (no-error) state
        /// does not invoke the onExit callback because there is nothing to clear.
        /// </summary>
        [Fact]
        public void Clear_OnInitialState_DoesNotInvokeOnExitCallback()
        {
            // Arrange
            int exitCount = 0;
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, onExit: () => exitCount++);

            // Act
            state.Clear();

            // Assert
            Assert.Equal(0, exitCount);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Clear"/> is idempotent:
        /// calling it a second time does not invoke the onExit callback again.
        /// </summary>
        [Fact]
        public void Clear_CalledTwice_OnExitInvokedOnce()
        {
            // Arrange
            int exitCount = 0;
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, onExit: () => exitCount++);
            state.Enter(new Exception());

            // Act
            state.Clear();
            state.Clear();

            // Assert
            Assert.Equal(1, exitCount);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Clear"/> resets the backoff
        /// timeout to its initial value, so the next <see cref="BlockingPeriodErrorState.Enter"/>
        /// uses the initial wait duration instead of the accumulated backoff.
        /// </summary>
        [Fact]
        public void Clear_ResetsBackoffSoNextEnterUsesInitialWait()
        {
            // Arrange
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            state.Enter(new Exception("first"));

            // Act
            state.Clear();
            state.Enter(new Exception("second"));

            // Assert
            Assert.True(state.HasError);
        }

        #endregion

        #region Backoff progression

        /// <summary>
        /// Verifies that the initial enter schedules the timer with the 5-second initial wait.
        /// The error state should persist until the timer fires, after which it clears automatically.
        /// </summary>
        [Fact]
        public void Enter_FirstEntry_SchedulesInitialWaitTimer()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, timeProvider: fakeTime);
            state.Enter(new Exception());

            // Act: advance just under the initial 5s wait
            fakeTime.Advance(TimeSpan.FromSeconds(4));

            // Assert: timer has not fired yet
            Assert.True(state.HasError);

            // Act: advance past the due time
            fakeTime.Advance(TimeSpan.FromSeconds(1));

            // Assert: timer has fired, error cleared
            Assert.False(state.HasError);
        }

        /// <summary>
        /// Verifies that successive timer-driven exits double the backoff each time:
        /// 5 s → 10 s → 20 s → 40 s → 60 s (capped at MaxWait).
        /// Each Enter schedules the timer for the current accumulated wait and the error
        /// persists until exactly that duration elapses.
        /// </summary>
        [Fact]
        public void Enter_BackoffDoublesOnSuccessiveTimerFiredEntries()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, timeProvider: fakeTime);

            // (expectedWaitSeconds, _) — the wait used by Enter for that iteration
            int[] expectedWaits = [5, 10, 20, 40, 60, 60];

            // Act & Assert
            foreach (int wait in expectedWaits)
            {
                state.Enter(new Exception($"attempt at wait={wait}s"));

                // One second before due time: error still active
                fakeTime.Advance(TimeSpan.FromSeconds(wait - 1));
                Assert.True(state.HasError, $"HasError should be true after {wait - 1}s (scheduled wait={wait}s)");

                // Final second: timer fires, error clears
                fakeTime.Advance(TimeSpan.FromSeconds(1));
                Assert.False(state.HasError, $"HasError should be false after {wait}s (scheduled wait={wait}s)");
            }
        }

        /// <summary>
        /// Verifies that a timer-driven exit does NOT reset the backoff. The accumulated
        /// backoff is preserved so the next failure uses the doubled wait, reflecting
        /// continued instability. Only <see cref="BlockingPeriodErrorState.Clear"/> resets
        /// the backoff to the initial value. In this way, we only reset the backoff when
        /// a connection is successfully established.
        /// </summary>
        [Fact]
        public void Enter_WhenTimerFires_DoesNotResetBackoff()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, timeProvider: fakeTime);

            // First enter: uses 5s wait; _nextWait advances to 10s
            state.Enter(new Exception("first"));
            fakeTime.Advance(TimeSpan.FromSeconds(5)); // timer fires
            Assert.False(state.HasError);

            // Act: enter again — should use 10s, not the initial 5s
            state.Enter(new Exception("second"));

            // Assert: not cleared after 9s
            fakeTime.Advance(TimeSpan.FromSeconds(9));
            Assert.True(state.HasError);

            // Assert: cleared after the full 10s
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            Assert.False(state.HasError);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Clear"/> resets the backoff
        /// to the initial 5-second wait even after the timer has doubled it, so the next
        /// enter cycle starts fresh.
        /// </summary>
        [Fact]
        public void Clear_AfterTimerFiredEntry_ResetsBackoffToInitialWait()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, timeProvider: fakeTime);

            // First enter/timer-exit: _nextWait advances from 5s to 10s
            state.Enter(new Exception("first"));
            fakeTime.Advance(TimeSpan.FromSeconds(5));
            Assert.False(state.HasError);

            // Second enter to accumulate more backoff; then Clear resets it
            state.Enter(new Exception("second")); // _nextWait advances to 20s
            state.Clear();                        // _nextWait resets to 5s

            // Act: enter again — should use the initial 5s wait
            state.Enter(new Exception("third"));

            // Assert: not cleared after 4s
            fakeTime.Advance(TimeSpan.FromSeconds(4));
            Assert.True(state.HasError);

            // Assert: cleared after the initial 5s
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            Assert.False(state.HasError);
        }

        #endregion

        #region Timer behavior

        /// <summary>
        /// Verifies that the timer-driven exit invokes the onExit callback, the same
        /// callback path used by <see cref="BlockingPeriodErrorState.Clear"/>.
        /// </summary>
        [Fact]
        public void Enter_WhenTimerFires_InvokesOnExitCallback()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            int exitCount = 0;
            using var state = new BlockingPeriodErrorState(
                ownerPoolId: 1,
                onExit: () => exitCount++,
                timeProvider: fakeTime);
            state.Enter(new Exception());

            // Act
            fakeTime.Advance(TimeSpan.FromSeconds(5));

            // Assert
            Assert.Equal(1, exitCount);
            Assert.False(state.HasError);
        }

        /// <summary>
        /// Verifies that the timer does not fire before its due time, confirming the
        /// scheduled wait is respected and not fired early.
        /// </summary>
        [Fact]
        public void Enter_TimerDoesNotFireBeforeDueTime()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            using var state = new BlockingPeriodErrorState(ownerPoolId: 1, timeProvider: fakeTime);
            state.Enter(new Exception());

            // Act: advance to 1ms before the 5s due time
            fakeTime.Advance(TimeSpan.FromMilliseconds(4999));

            // Assert
            Assert.True(state.HasError);
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Dispose"/> on an initial state
        /// does not throw and completes successfully.
        /// </summary>
        [Fact]
        public void Dispose_OnInitialState_DoesNotThrow()
        {
            // Arrange & Act
            var state = new BlockingPeriodErrorState(ownerPoolId: 1);

            // Assert
            state.Dispose(); // Should not throw
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Dispose"/> clears the cached
        /// error state so <see cref="BlockingPeriodErrorState.HasError"/> is false after disposal.
        /// </summary>
        [Fact]
        public void Dispose_AfterEnter_ClearsHasError()
        {
            // Arrange
            var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            state.Enter(new Exception());

            // Act
            state.Dispose();

            // Assert
            Assert.False(state.HasError);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Dispose"/> is idempotent:
        /// calling it multiple times does not throw and completes successfully.
        /// </summary>
        [Fact]
        public void Dispose_CalledMultipleTimes_DoesNotThrow()
        {
            // Arrange & Act
            var state = new BlockingPeriodErrorState(ownerPoolId: 1);
            state.Dispose();

            // Assert
            state.Dispose(); // Must be idempotent and not throw
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Dispose"/> does not invoke
        /// the onExit callback because disposal is a resource-cleanup path, not a logical
        /// "exit blocking period" event.
        /// </summary>
        [Fact]
        public void Dispose_DoesNotInvokeOnExitCallback()
        {
            // Arrange
            int exitCount = 0;
            var state = new BlockingPeriodErrorState(ownerPoolId: 1, onExit: () => exitCount++);
            state.Enter(new Exception());

            // Act
            state.Dispose();

            // Assert
            Assert.Equal(0, exitCount);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState.Dispose"/> properly releases
        /// and cancels the internal exit timer, preventing stale callbacks from firing after disposal.
        /// Uses <see cref="FakeTimeProvider"/> to advance time deterministically past the timer's due
        /// time without relying on real-time sleeps.
        /// </summary>
        [Fact]
        public void Dispose_ReleasesTimer_NoCallbackAfterDispose()
        {
            // Arrange
            var fakeTime = new FakeTimeProvider();
            int exitCount = 0;
            var state = new BlockingPeriodErrorState(
                ownerPoolId: 1,
                onExit: () => Interlocked.Increment(ref exitCount),
                timeProvider: fakeTime);
            state.Enter(new Exception());

            // Act: dispose cancels the pending timer
            state.Dispose();

            // Advance well past the 5s due time — the cancelled timer must not fire
            fakeTime.Advance(TimeSpan.FromSeconds(60));

            // Assert
            Assert.Equal(0, exitCount);
        }

        /// <summary>
        /// Verifies that <see cref="BlockingPeriodErrorState"/> works correctly in a standard
        /// <c>using</c> statement, with no exceptions thrown during disposal.
        /// </summary>
        [Fact]
        public void Dispose_WithUsingStatement_DoesNotThrow()
        {
            // Arrange & Act
            using (var state = new BlockingPeriodErrorState(ownerPoolId: 1))
            {
                state.Enter(new Exception());
                Assert.True(state.HasError);
            }

            // Assert
            // No exception expected when the using block exits
        }

        #endregion

        #region Callback behaviour

        /// <summary>
        /// Verifies that both onEnter and onExit callbacks are optional (nullable)
        /// and the instance works correctly when neither is provided.
        /// </summary>
        [Fact]
        public void Callbacks_AreNotRequiredAndDefaultToNull()
        {
            // Arrange & Act
            using var state = new BlockingPeriodErrorState(ownerPoolId: 42);
            state.Enter(new Exception());

            // Assert
            state.Clear(); // Should work without callbacks
        }

        /// <summary>
        /// Verifies that the onEnter callback is invoked after the internal lock is released.
        /// The callback reads <see cref="BlockingPeriodErrorState.HasError"/> and calls
        /// <see cref="BlockingPeriodErrorState.ThrowIfActive"/> — operations that are safe only
        /// when the lock is not held. If the implementation were changed to hold the lock during
        /// the callback invocation, any re-entrant call from the callback that tries to acquire the
        /// same lock (on a non-re-entrant lock) would deadlock.
        /// </summary>
        [Fact]
        public void OnEnter_CalledOutsideLock_CanCallBackIntoState()
        {
            // Arrange
            bool hasErrorObservedInCallback = false;
            using var state = new BlockingPeriodErrorState(
                ownerPoolId: 1,
                onEnter: () =>
                {
                    // Observe state from inside the callback.
                    // HasError must already be true at this point.
                    hasErrorObservedInCallback = state.HasError;

                    // Calling ThrowIfActive from the callback must not deadlock.
                    Assert.Throws<InvalidOperationException>(() => state.ThrowIfActive());
                });

            // Act
            state.Enter(new InvalidOperationException("test"));

            // Assert
            Assert.True(hasErrorObservedInCallback);
        }

        /// <summary>
        /// Verifies that the onExit callback is invoked after the internal lock is released.
        /// The callback reads <see cref="BlockingPeriodErrorState.HasError"/> — confirming it
        /// observes the cleared state — and calls <see cref="BlockingPeriodErrorState.ThrowIfActive"/>
        /// without deadlocking. If the implementation were changed to hold the lock during the
        /// callback, any re-entrant call from the callback would deadlock on a non-re-entrant lock.
        /// </summary>
        [Fact]
        public void OnExit_CalledOutsideLock_CanCallBackIntoState()
        {
            // Arrange
            bool hasErrorObservedInCallback = true; // initialized to true; must be false after Clear
            using var state = new BlockingPeriodErrorState(
                ownerPoolId: 1,
                onExit: () =>
                {
                    // HasError must already be false when onExit is called.
                    hasErrorObservedInCallback = state.HasError;

                    // Calling ThrowIfActive from the callback must not deadlock.
                    state.ThrowIfActive(); // must not throw
                });

            // Act
            state.Enter(new Exception());
            state.Clear();

            // Assert
            Assert.False(hasErrorObservedInCallback);
        }

        #endregion
    }

    /// <summary>
    /// Test helper for creating <see cref="SqlException"/> instances. Since <see cref="SqlException"/> has
    /// an internal constructor, instances must be created via the <see cref="SqlException.CreateException"/> factory method.
    /// </summary>
    internal static class SqlExceptionHelper
    {
        /// <summary>
        /// Creates a <see cref="SqlException"/> with the specified message using the internal factory method.
        /// </summary>
        /// <param name="message">The error message for the exception.</param>
        /// <returns>A new <see cref="SqlException"/> with the specified message.</returns>
        internal static SqlException CreateSqlException(string message)
        {
            var collection = new SqlErrorCollection();
            collection.Add(new SqlError(0, (byte)0, (byte)0, "TestServer", message, "", 0));
            return SqlException.CreateException(collection, "");
        }
    }
}
