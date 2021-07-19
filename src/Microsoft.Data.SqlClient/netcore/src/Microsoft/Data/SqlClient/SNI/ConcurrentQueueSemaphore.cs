// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// This class implements a FIFO Queue with SemaphoreSlim for ordered execution of parallel tasks.
    /// Currently used in Managed SNI (SNISslStream) to override SslStream's WriteAsync implementation.
    /// </summary>
    internal sealed partial class ConcurrentQueueSemaphore
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _queue;

        public ConcurrentQueueSemaphore(int initialCount)
        {
            _semaphore = new SemaphoreSlim(initialCount);
            _queue = new ConcurrentQueue<TaskCompletionSource<bool>>();
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            // try sync wait with 0 which will not block to see if we need to do an async wait
            if (_semaphore.Wait(0, cancellationToken))
            {
                return Task.CompletedTask;
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                _queue.Enqueue(tcs);
                _semaphore.WaitAsync().ContinueWith(
                    continuationAction: static (Task task, object state) =>
                    {
                        ConcurrentQueue<TaskCompletionSource<bool>> queue = (ConcurrentQueue<TaskCompletionSource<bool>>)state;
                        if (queue.TryDequeue(out TaskCompletionSource<bool> popped))
                        {
                            popped.SetResult(true);
                        }
                    },
                    state: _queue,
                    cancellationToken: cancellationToken
                );
                return tcs.Task;
            }
        }

        public void Release()
        {
            _semaphore.Release();
        }
    }

}
