// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// This class extends NetworkStream to customize stream behavior for Managed SNI implementation.
    /// </summary>
    internal sealed partial class SniNetworkStream : NetworkStream
    {
        private readonly ConcurrentQueueSemaphore _writeAsyncSemaphore;
        private readonly ConcurrentQueueSemaphore _readAsyncSemaphore;

        public SniNetworkStream(Socket socket, bool ownsSocket) : base(socket, ownsSocket)
        {
            _writeAsyncSemaphore = new ConcurrentQueueSemaphore(1);
            _readAsyncSemaphore = new ConcurrentQueueSemaphore(1);
        }
        
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _readAsyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await base.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniSslStream), EventType.ERR, "Internal Exception occurred while reading data: {0}", args0: e?.Message);
                throw;
            }
            finally
            {
                _readAsyncSemaphore.Release();
            }
        }

        // Prevent the WriteAsync collisions by running the task in a Semaphore Slim
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return WriteAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await _writeAsyncSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                SqlClientEventSource.Log.TrySNITraceEvent(nameof(SniSslStream), EventType.ERR, "Internal Exception occurred while reading data: {0}", args0: e?.Message);
                throw;
            }
            finally
            {
                _writeAsyncSemaphore.Release();
            }
        }
    }
}
