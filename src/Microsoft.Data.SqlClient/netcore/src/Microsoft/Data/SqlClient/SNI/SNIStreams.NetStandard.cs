// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Security;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace Microsoft.Data.SqlClient.SNI
{
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
