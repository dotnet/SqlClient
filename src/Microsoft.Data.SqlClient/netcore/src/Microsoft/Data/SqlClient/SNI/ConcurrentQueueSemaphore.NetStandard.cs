// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    internal sealed partial class ConcurrentQueueSemaphore
    {
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
                    continuationAction: s_continuePop,
                    state: _queue,
                    cancellationToken: cancellationToken
                );
                return tcs.Task;
            }
        }
    }
}
