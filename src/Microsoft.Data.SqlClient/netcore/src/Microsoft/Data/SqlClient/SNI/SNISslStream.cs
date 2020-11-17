// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Security;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Data.SqlClient.SNI
{
    /// <summary>
    /// This class extends SslStream to customize stream behavior for Managed SNI implementation.
    /// </summary>
    internal class SNISslStream : SslStream
    {
        private readonly ConcurrentQueueSemaphore _writeAsyncQueueSemaphore;
        private readonly ConcurrentQueueSemaphore _readAsyncQueueSemaphore;

        public SNISslStream(Stream innerStream, bool leaveInnerStreamOpen, RemoteCertificateValidationCallback userCertificateValidationCallback)
            : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback)
        {
            _writeAsyncQueueSemaphore = new ConcurrentQueueSemaphore(1);
            _readAsyncQueueSemaphore = new ConcurrentQueueSemaphore(1);
        }

        // Prevent the ReadAsync's collision by running task in Semaphore Slim
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                return _readAsyncQueueSemaphore.WaitAsync()
                    .ContinueWith<int>(_ => base.ReadAsync(buffer, offset, count, cancellationToken).GetAwaiter().GetResult());
            }
            finally
            {
                _readAsyncQueueSemaphore.Release();
            }
        }

        // Prevent the WriteAsync's collision by running task in Semaphore Slim
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            try
            {
                return _writeAsyncQueueSemaphore.WaitAsync().ContinueWith(_ => base.WriteAsync(buffer, offset, count, cancellationToken));
            }
            finally
            {
                _writeAsyncQueueSemaphore.Release();
            }
        }
    }
}
