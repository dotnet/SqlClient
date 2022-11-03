using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// Provides a TaskScheduler that provides control over priorities, fairness, and the underlying threads utilized.
    /// </summary>
    [DebuggerDisplay("Id={Id}, Queues={DebugQueueCount}, ScheduledTasks = {DebugTaskCount}")]
    internal sealed class QueuedTaskScheduler : TaskScheduler, IDisposable
    {
        /// <summary>Cancellation token used for disposal.</summary>
        private readonly CancellationTokenSource _disposeCancellation = new CancellationTokenSource();
        /// <summary>
        /// The maximum allowed concurrency level of this scheduler.  If custom threads are
        /// used, this represents the number of created threads.
        /// </summary>
        private readonly int _concurrencyLevel;
        /// <summary>Whether we're processing tasks on the current thread.</summary>
        private static readonly ThreadLocal<bool> s_taskProcessingThread = new ThreadLocal<bool>();

        /// <summary>The threads used by the scheduler to process work.</summary>
        private readonly Thread[] _threads;
        /// <summary>The collection of tasks to be executed on our custom threads.</summary>
        private readonly BlockingCollection<Task> _blockingTaskQueue;

        private readonly TaskFactory _factory;

        /// <summary>Initializes the scheduler.</summary>
        /// <param name="threadCount">The number of threads to create and use for processing work items.</param>
        public QueuedTaskScheduler(int threadCount) : this(threadCount, string.Empty, false, ThreadPriority.Normal, 0, null, null) { }

        /// <summary>Initializes the scheduler.</summary>
        /// <param name="threadCount">The number of threads to create and use for processing work items.</param>
        /// <param name="threadName">The name to use for each of the created threads.</param>
        /// <param name="useForegroundThreads">A Boolean value that indicates whether to use foreground threads instead of background.</param>
        /// <param name="threadPriority">The priority to assign to each thread.</param>
        /// <param name="threadMaxStackSize">The stack size to use for each thread.</param>
        /// <param name="threadInit">An initialization routine to run on each thread.</param>
        /// <param name="threadFinally">A finalization routine to run on each thread.</param>
        public QueuedTaskScheduler(
            int threadCount,
            string threadName = "",
            bool useForegroundThreads = false,
            ThreadPriority threadPriority = ThreadPriority.Normal,
            int threadMaxStackSize = 0,
            Action threadInit = null,
            Action threadFinally = null)
        {
            // Validates arguments (some validation is left up to the Thread type itself).
            // If the thread count is 0, default to the number of logical processors.
            if (threadCount < 0)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            else if (threadCount == 0)
                _concurrencyLevel = Environment.ProcessorCount;
            else
                _concurrencyLevel = threadCount;

            // Initialize the queue used for storing tasks
            _blockingTaskQueue = new BlockingCollection<Task>();

            // Create all of the threads
            _threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                _threads[i] = new Thread(() => DispatchLoop(threadInit, threadFinally), threadMaxStackSize)
                {
                    Priority = threadPriority,
                    IsBackground = !useForegroundThreads,
                };
                if (threadName != null)
                    _threads[i].Name = threadName + " (" + i + ")";
            }

            _factory = new TaskFactory(this);

            // Start all of the threads
            foreach (var thread in _threads)
                thread.Start();
        }

        public TaskFactory Factory => _factory;

        /// <summary>The dispatch loop run by all threads in this scheduler.</summary>
        /// <param name="threadInit">An initialization routine to run when the thread begins.</param>
        /// <param name="threadFinally">A finalization routine to run before the thread ends.</param>
        private void DispatchLoop(Action threadInit, Action threadFinally)
        {
            s_taskProcessingThread.Value = true;
            threadInit?.Invoke();
            try
            {
                // If the scheduler is disposed, the cancellation token will be set and
                // we'll receive an OperationCanceledException.  That OCE should not crash the process.
                try
                {
                    // If a thread abort occurs, we'll try to reset it and continue running.
                    while (true)
                    {
                        try
                        {
                            // For each task queued to the scheduler, try to execute it.
                            foreach (var task in _blockingTaskQueue.GetConsumingEnumerable(_disposeCancellation.Token))
                            {
                                // If the task is not null, that means it was queued to this scheduler directly.
                                // Run it.
                                if (task != null)
                                {
                                    bool tried = TryExecuteTask(task);
                                }
                            }
                        }
                        catch (ThreadAbortException)
                        {
                            // If we received a thread abort, and that thread abort was due to shutting down
                            // or unloading, let it pass through.  Otherwise, reset the abort so we can
                            // continue processing work items.
                            if (!Environment.HasShutdownStarted && !AppDomain.CurrentDomain.IsFinalizingForUnload())
                            {
                                Thread.ResetAbort();
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { }
            }
            finally
            {
                // Run a cleanup routine if there was one
                threadFinally?.Invoke();
                s_taskProcessingThread.Value = false;
            }
        }

        /// <summary>Queues a task to the scheduler.</summary>
        /// <param name="task">The task to be queued.</param>
        protected override void QueueTask(Task task)
        {
            // If we've been disposed, no one should be queueing
            if (_disposeCancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
            _blockingTaskQueue.Add(task);
        }

        /// <summary>Tries to execute a task synchronously on the current thread.</summary>
        /// <param name="task">The task to execute.</param>
        /// <param name="taskWasPreviouslyQueued">Whether the task was previously queued.</param>
        /// <returns>true if the task was executed; otherwise, false.</returns>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>
            // If we're already running tasks on this threads, enable inlining
            false; // s_taskProcessingThread.Value && TryExecuteTask(task);

        /// <summary>Gets the tasks scheduled to this scheduler.</summary>
        /// <returns>An enumerable of all tasks queued to this scheduler.</returns>
        /// <remarks>This does not include the tasks on sub-schedulers.  Those will be retrieved by the debugger separately.</remarks>
        protected override IEnumerable<Task> GetScheduledTasks()
        {
            // Get all of the tasks, filtering out nulls, which are just placeholders
            // for tasks in other sub-schedulers
            return _blockingTaskQueue.Where(t => t != null).ToList();
        }

        /// <summary>Gets the maximum concurrency level to use when processing tasks.</summary>
        public override int MaximumConcurrencyLevel => _concurrencyLevel;

        /// <summary>Initiates shutdown of the scheduler.</summary>
        public void Dispose() => _disposeCancellation.Cancel();
    }
}
