// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Authentication;
using Microsoft.Data.SqlClient.Connection;
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for <see cref="EncryptionInfoAccumulator"/>, the per-attempt holder of negotiated
    /// encryption values.
    /// </summary>
    public class EncryptionInfoAccumulatorTests
    {
        // IANA identifier for TLS_AES_128_GCM_SHA256.
        private const int OtherCipherSuite = 0x1301;

        #region Reset

        /// <summary>
        /// Resetting clears every value, so the snapshot becomes empty.
        /// </summary>
        [Fact]
        public void Reset_ValuesSet_ClearsAllValues()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.Mode = SqlConnectionEncryptionMode.Full;
            accumulator.Protocol = SslProtocols.Tls12;
            accumulator.CipherSuite = ToConstructorCipherSuite(SampleCipherSuite);

            // Act
            accumulator.Reset();

            // Assert
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            Assert.Null(accumulator.CipherSuite);
            Assert.False(accumulator.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, accumulator.ToSnapshot());
        }

        #endregion

        #region ToSnapshot

        /// <summary>
        /// With nothing set, the snapshot is the shared empty instance.
        /// </summary>
        [Fact]
        public void ToSnapshot_NothingSet_ReturnsEmpty()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();

            // Assert
            Assert.False(accumulator.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, snapshot);
        }

        /// <summary>
        /// The snapshot carries the mode, protocol and cipher suite, and no certificate fields.
        /// </summary>
        [Fact]
        public void ToSnapshot_AllSet_SurfacesValuesWithoutCertificateFields()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.Mode = SqlConnectionEncryptionMode.Full;
            accumulator.Protocol = SslProtocols.Tls12;
            accumulator.CipherSuite = ToConstructorCipherSuite(SampleCipherSuite);

            // Act
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuiteId);
            Assert.Null(snapshot.PresentedServerCertificateRawData);
            Assert.Null(snapshot.GetServerCertificate());
            Assert.Null(snapshot.ValidationErrors);
            Assert.Null(snapshot.ChainStatus);
        }

        /// <summary>
        /// Without a cipher suite, the snapshot reports none.
        /// </summary>
        [Fact]
        public void ToSnapshot_CipherSuiteNotSet_ReturnsNullCipherSuite()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.Mode = SqlConnectionEncryptionMode.Full;
            accumulator.Protocol = SslProtocols.Tls12;

            // Act
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();

            // Assert
            Assert.Null(accumulator.CipherSuite);
            Assert.Null(snapshot.NegotiatedCipherSuiteId);
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
        }

#if NET
        /// <summary>
        /// On .NET, the <see cref="System.Net.Security.TlsCipherSuite"/> is surfaced
        /// unchanged, and its IANA identifier is surfaced as the integer identifier.
        /// </summary>
        [Fact]
        public void ToSnapshot_CipherSuiteSet_SurfacesTlsCipherSuite()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.CipherSuite = System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384;

            // Act
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();

            // Assert
            Assert.Equal(System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384, snapshot.NegotiatedCipherSuite);
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuiteId);
        }
#endif

        /// <summary>
        /// A snapshot is unaffected when the accumulator is later reset and reused for another attempt.
        /// </summary>
        [Fact]
        public void ToSnapshot_LaterResetAndSet_SnapshotUnchanged()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.CipherSuite = ToConstructorCipherSuite(SampleCipherSuite);
            SqlConnectionEncryptionInfo first = accumulator.ToSnapshot();

            // Act
            accumulator.Reset();
            accumulator.CipherSuite = ToConstructorCipherSuite(OtherCipherSuite);

            // Assert
            Assert.Equal(SampleCipherSuite, first.NegotiatedCipherSuiteId);
            Assert.Equal(OtherCipherSuite, accumulator.ToSnapshot().NegotiatedCipherSuiteId);
        }

        #endregion
    }
}
