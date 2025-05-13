// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Sockets;

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
    }
}
