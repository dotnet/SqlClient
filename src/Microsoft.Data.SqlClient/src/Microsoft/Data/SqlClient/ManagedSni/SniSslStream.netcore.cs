// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net.Security;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    /// <summary>
    /// This class extends SslStream to customize stream behavior for Managed SNI implementation.
    /// </summary>
    internal sealed partial class SniSslStream : SslStream
    {
        private readonly ConcurrentQueueSemaphore _writeAsyncSemaphore;
        private readonly ConcurrentQueueSemaphore _readAsyncSemaphore;

        public SniSslStream(
            Stream innerStream,
            bool leaveInnerStreamOpen,
            RemoteCertificateValidationCallback userCertificateValidationCallback)
            : base(innerStream, leaveInnerStreamOpen, userCertificateValidationCallback)
        {
            _writeAsyncSemaphore = new ConcurrentQueueSemaphore(1);
            _readAsyncSemaphore = new ConcurrentQueueSemaphore(1);
        }
    }
}
