// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.ProviderBase;
using Microsoft.Data.SqlClient.ManagedSni;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Tests for <see cref="SniNpHandle"/>.
    /// </summary>
    public sealed class SniNpHandleTests
    {
        /// <summary>
        /// Upper bound for EnableSsl to return. Generous, so it only separates "honors the token"
        /// from "blocks until the peer closes the pipe".
        /// </summary>
        private static readonly TimeSpan HandshakeBudget = TimeSpan.FromSeconds(30);

        /// <summary>
        /// When the peer accepts the TLS ClientHello and never answers, EnableSsl must stop the
        /// handshake once its token is cancelled and report an SNI timeout error, for both TLS
        /// inside TDS and TLS-first (strict encryption). This is what bounds the handshake by
        /// Connect Timeout over named pipes (GitHub issues #4834 and #5045).
        /// </summary>
        /// <param name="tlsFirst">Whether the handle negotiates TLS before any TDS traffic.</param>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EnableSsl_PeerNeverAnswersHandshake_ReturnsTimeoutErrorWhenTokenIsCancelled(bool tlsFirst)
        {
            // Arrange
            // Connect the handle to a local pipe server that will stay silent.
            string pipeName = $"SqlClient-{Guid.NewGuid():N}";
            using NamedPipeServerStream peer = new(
                pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using CancellationTokenSource acceptTimeout = new(TimeSpan.FromSeconds(5));
            Task accept = peer.WaitForConnectionAsync(acceptTimeout.Token);
            SniNpHandle handle = new(
                ".",
                pipeName,
                TimeoutTimer.StartNew(TimeSpan.FromSeconds(5)),
                tlsFirst,
                hostNameInCertificate: null,
                serverCertificateFilename: null);
            await accept;
            Assert.Equal(TdsEnums.SNI_SUCCESS, handle.Status);
            using CancellationTokenSource handshakeTimeout = new(TimeSpan.FromMilliseconds(500));

            try
            {
                // Act
                // SNI errors are thread-static, so read the error on the thread that ran EnableSsl.
                Task<(uint Result, SniError Error)> enableSsl = Task.Run(
                    () => (handle.EnableSsl(0, handshakeTimeout.Token), SniLoadHandle.LastError));
                Task settled = await Task.WhenAny(enableSsl, Task.Delay(HandshakeBudget));

                // Assert
                Assert.True(
                    ReferenceEquals(enableSsl, settled),
                    $"EnableSsl did not return within {HandshakeBudget.TotalSeconds:N0}s after its token was cancelled.");
                (uint result, SniError error) = await enableSsl;
                Assert.Equal(TdsEnums.SNI_ERROR, result);
                Assert.Equal(SniProviders.NP_PROV, error.provider);
                Assert.Equal((uint)SniCommon.ConnTimeoutError, error.sniError);
                Assert.Equal((int)TdsEnums.SNI_WAIT_TIMEOUT, error.nativeError);
            }
            finally
            {
                // Cleanup
                // Disposing the handle also releases a handshake that ignored the token.
                handle.Dispose();
            }
        }
    }
}

#endif
