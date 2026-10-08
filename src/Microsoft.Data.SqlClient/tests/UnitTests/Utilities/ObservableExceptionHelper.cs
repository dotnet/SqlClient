// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.UnitTests.Utilities;

/// <summary>
/// Provides a mechanism to observe unhandled exceptions thrown by asynchronous tasks. This helper
/// is specifically designed for cases where tasks may throw unobserved exceptions, allowing
/// these exceptions to be handled and inspected during unit tests or runtime debugging.
/// </summary>
/// <remarks>
/// Usage should follow a pattern similar to the following:
/// * Construct a new instance in the "arrange" section of the test, with `using`.
/// * Set up the method under test such that it throws <see cref="TestException"/>
/// * In "act" section of test, call and await the method under test
/// * Call and await <see cref="Wait"/> and supply with suitable timeout, capturing the returned
///   exception, if any.
/// * Assert that the expected outcome occurred (in the "assert" section of the test). If exception
///   was expected, ensure the returned exception is not <see langword="null"/>. Otherwise, ensure
///   the returned exception is <see langword="null"/>.
/// </remarks>
public class ObservableExceptionHelper : IDisposable
{
    private readonly EventHandler<UnobservedTaskExceptionEventArgs> _handler;
    private readonly TaskCompletionSource<Exception> _unobservedExceptionRaised;

    /// <summary>
    /// Constructs a new instance by initializing a test exception, creating an unobserved
    /// exception handler that observes the test exception, and registering that handler on
    /// the TaskScheduler.
    /// </summary>
    public ObservableExceptionHelper()
    {
        // Create special test exception
        TestException = new ObservableException();

        // Set up unobserved exception handler that only observes our special test exception
        _unobservedExceptionRaised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _handler = (_, args) =>
        {
            if (args.Exception.InnerExceptions.Contains(TestException))
            {
                args.SetObserved();
                _unobservedExceptionRaised.TrySetResult(args.Exception);
            }
        };
        TaskScheduler.UnobservedTaskException += _handler;
    }

    /// <summary>
    /// Exception that method under test should throw. This exception is checked for in the
    /// unobserved exception handler registered at construction.
    /// </summary>
    public ObservableException TestException { get; }

    /// <inheritdoc/>
    /// <remarks>Unregisters unobserved exception handler.</remarks>
    public void Dispose()
    {
        // Unregister unobserved exception handler
        TaskScheduler.UnobservedTaskException -= _handler;
    }

    /// <summary>
    /// Performs a garbage collection, which should trigger the unobserved exception handler that was
    /// registered during construction. Waits for up to 50ms for the unobserved exception handler
    /// to handle the test exception, then loops again, until <paramref name="timeout"/> has been
    /// exceeded. At least one loop will be attempted.
    /// </summary>
    /// <param name="timeout">
    /// Amount of time to wait for our test unobserved exception handler to complete. Use
    /// <see cref="TimeSpan.Zero"/> to only perform one loop.
    /// </param>
    /// <returns>
    /// Exception raised that included the unobserved task, if handler was invoked before the
    /// <paramref name="timeout"/> elapsed. If handler was not invoked before
    /// <paramref name="timeout"/> elapsed, <see langword="null"/> will be returned.
    /// </returns>
    public async Task<Exception?> Wait(TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        do
        {
            // Force GC collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Effectively wait up to 50ms before looping and trying to garbage collect again
            Task delayTask = Task.Delay(TimeSpan.FromMilliseconds(50));
            Task completedTask = await Task.WhenAny(_unobservedExceptionRaised.Task, delayTask);
            if (completedTask == _unobservedExceptionRaised.Task)
            {
                // Unobserved exception completed before we gave up
                return await _unobservedExceptionRaised.Task;
            }
        } while (DateTime.UtcNow < deadline);

        // Unobserved exception did not complete, so we give up and return null
        return null;
    }

    /// <summary>
    /// Test exception type for unique identification of unobserved exceptions.
    /// </summary>
    public class ObservableException : Exception
    {
        /// <summary>
        /// Identifier for uniquely identifying the current instance.
        /// </summary>
        public Guid Identifier { get; } = Guid.NewGuid();
    }
}
