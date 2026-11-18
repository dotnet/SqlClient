// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET
using System.Net.Security;
#endif
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Parser;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Parser
{
    /// <summary>
    /// Unit tests for how <see cref="TdsParser"/> applies a transport's TLS handshake record to
    /// the login attempt's encryption info, and when it falls back to the protocol reported by
    /// the completed handshake.
    /// </summary>
    public class TdsParserTests
    {
        private static readonly byte[] SampleDerBytes = { 0x30, 0x82, 0x01, 0x0A, 0x5A, 0xC3 };

#if NET
        private const TlsCipherSuite SampleSuite = TlsCipherSuite.TLS_AES_256_GCM_SHA384;
#else
        private const int SampleSuite = 0x1302;
#endif

        /// <summary>
        /// Verifies that every value of a handshake record is copied into the accumulator.
        /// </summary>
        [Fact]
        public void ApplyTlsHandshakeInfo_CompleteRecord_AssignsEveryValue()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            TlsHandshakeInfo record = new(
                X509ChainStatusFlags.UntrustedRoot,
                SampleSuite,
                SampleDerBytes,
                SslProtocols.Tls12,
                SqlServerCertificateValidationErrors.ChainError);

            // Act
            TdsParser.ApplyTlsHandshakeInfo(
                accumulator,
                record);

            // Assert
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, accumulator.ChainStatus);
            Assert.Equal(SampleSuite, accumulator.CipherSuite);
            Assert.Same(SampleDerBytes, accumulator.ServerCertificateDerBytes);
            Assert.Equal(SslProtocols.Tls12, accumulator.Protocol);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, accumulator.ValidationErrors);
        }

        /// <summary>
        /// Verifies that a later record replaces every value of an earlier one, including with
        /// unavailable values, so the accumulator reflects only the latest record.
        /// </summary>
        [Fact]
        public void ApplyTlsHandshakeInfo_LaterEmptyRecord_ReplacesEarlierValues()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.ApplyTlsHandshakeInfo(
                accumulator,
                new TlsHandshakeInfo(
                    X509ChainStatusFlags.UntrustedRoot,
                    SampleSuite,
                    SampleDerBytes,
                    SslProtocols.Tls12,
                    SqlServerCertificateValidationErrors.ChainError));

            // Act
            TdsParser.ApplyTlsHandshakeInfo(
                accumulator,
                new TlsHandshakeInfo(
                    chainStatus: null,
                    cipherSuite: null,
                    derBytes: null,
                    protocol: null,
                    validationErrors: null));

            // Assert
            Assert.Null(accumulator.ChainStatus);
            Assert.Null(accumulator.CipherSuite);
            Assert.Null(accumulator.ServerCertificateDerBytes);
            Assert.Null(accumulator.Protocol);
            Assert.Null(accumulator.ValidationErrors);
        }

        /// <summary>
        /// Verifies that a transport without a record leaves the accumulator unchanged.
        /// </summary>
        [Fact]
        public void ApplyTlsHandshakeInfo_NullRecord_LeavesAccumulatorUnchanged()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new()
            {
                Protocol = SslProtocols.Tls12,
                ValidationErrors = SqlServerCertificateValidationErrors.None,
            };

            // Act
            TdsParser.ApplyTlsHandshakeInfo(
                accumulator,
                record: null);

            // Assert
            Assert.Equal(SslProtocols.Tls12, accumulator.Protocol);
            Assert.Equal(SqlServerCertificateValidationErrors.None, accumulator.ValidationErrors);
        }

        /// <summary>
        /// Verifies that applying a record without an accumulator does not throw.
        /// </summary>
        [Fact]
        public void ApplyTlsHandshakeInfo_NullAccumulator_DoesNotThrow()
        {
            // Act
            TdsParser.ApplyTlsHandshakeInfo(
                accumulator: null,
                new TlsHandshakeInfo(
                    chainStatus: null,
                    SampleSuite,
                    derBytes: null,
                    SslProtocols.Tls12,
                    validationErrors: null));

            // Assert: reaching this point means the call did not throw.
        }

        /// <summary>
        /// Verifies that the handshake's protocol is required when the transport has no record.
        /// </summary>
        [Fact]
        public void RequiresHandshakeProtocol_NullRecord_ReturnsTrue()
        {
            // Act
            bool required = TdsParser.RequiresHandshakeProtocol(record: null);

            // Assert
            Assert.True(required);
        }

        /// <summary>
        /// Verifies that a native SNI record which reports other values but no protocol falls
        /// back to the protocol of the completed handshake.
        /// </summary>
        [Fact]
        public void RequiresHandshakeProtocol_RecordWithoutProtocol_ReturnsTrue()
        {
            // Arrange
            TlsHandshakeInfo record = new(
                X509ChainStatusFlags.NoError,
                SampleSuite,
                SampleDerBytes,
                protocol: null,
                SqlServerCertificateValidationErrors.None);

            // Act
            bool required = TdsParser.RequiresHandshakeProtocol(record);

            // Assert
            Assert.True(required);
        }

        /// <summary>
        /// Verifies that a record which reports a protocol is the protocol's only source.
        /// </summary>
        [Fact]
        public void RequiresHandshakeProtocol_RecordWithProtocol_ReturnsFalse()
        {
            // Arrange
            TlsHandshakeInfo record = new(
                chainStatus: null,
                cipherSuite: null,
                derBytes: null,
                SslProtocols.Tls12,
                validationErrors: null);

            // Act
            bool required = TdsParser.RequiresHandshakeProtocol(record);

            // Assert
            Assert.False(required);
        }
    }
}
