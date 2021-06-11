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
        private static readonly Action<Task, object> s_continuePop = ContinuePop;

        private readonly SemaphoreSlim _semaphore;
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _queue =
            new ConcurrentQueue<TaskCompletionSource<bool>>();

        public ConcurrentQueueSemaphore(int initialCount)
        {
            _semaphore = new SemaphoreSlim(initialCount);
        }

        public void Release()
        {
            _semaphore.Release();
        }

        private static void ContinuePop(Task task, object state)
        {
            ConcurrentQueue<TaskCompletionSource<bool>> queue = (ConcurrentQueue<TaskCompletionSource<bool>>)state;
            if (queue.TryDequeue(out TaskCompletionSource<bool> popped))
            {
                popped.SetResult(true);
            }
        }
    }

}
