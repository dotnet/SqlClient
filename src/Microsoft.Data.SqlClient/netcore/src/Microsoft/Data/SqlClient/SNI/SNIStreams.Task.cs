// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    // NetCore2.1:
    // DO NOT OVERRIDE ValueTask versions of ReadAsync and WriteAsync because the underlying SslStream implements them
    // by calling the Task versions which are already overridden meaning that if a caller uses Task WriteAsync this would
    // call ValueTask WriteAsync which then called TaskWriteAsync introducing a lock cycle and never return

    internal sealed partial class SNISslStream
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _readAsyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _readAsyncSemaphore.Release();
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _writeAsyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeAsyncSemaphore.Release();
            }
        }
    }

    internal sealed partial class SNINetworkStream
    {
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _readAsyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await base.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _readAsyncSemaphore.Release();
            }
        }

        // Prevent the WriteAsync collisions by running the task in a Semaphore Slim
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await _writeAsyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await base.WriteAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeAsyncSemaphore.Release();
            }
        }
    }
}
