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
    /// Unit tests for <see cref="EncryptionInfoCapture"/>, the per-attempt collector of negotiated
    /// encryption values.
    /// </summary>
    public class EncryptionInfoCaptureTests
    {
        // IANA identifier for TLS_AES_128_GCM_SHA256.
        private const int OtherCipherSuite = 0x1301;

        #region DiscardUnobservedNegotiation

        /// <summary>
        /// When a protocol was observed, discarding unobserved negotiation keeps the mode, protocol
        /// and cipher suite.
        /// </summary>
        [Fact]
        public void DiscardUnobservedNegotiation_ProtocolObserved_KeepsAllValues()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            capture.DiscardUnobservedNegotiation();

            // Assert
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// When no protocol was observed, discarding unobserved negotiation drops the mode but keeps
        /// an observed cipher suite.
        /// </summary>
        [Fact]
        public void DiscardUnobservedNegotiation_NoProtocol_DropsModeAndKeepsCipherSuite()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            capture.DiscardUnobservedNegotiation();

            // Assert
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// Discarding unobserved negotiation never invents a cipher suite that was not recorded.
        /// </summary>
        [Fact]
        public void DiscardUnobservedNegotiation_NoCipherSuite_DoesNotInventOne()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            // Act
            capture.DiscardUnobservedNegotiation();

            // Assert
            Assert.Null(capture.CipherSuite);
            Assert.Null(capture.ToSnapshot().NegotiatedCipherSuiteId);
        }

        #endregion

        #region Record

        /// <summary>
        /// Recording <see cref="SslProtocols.None"/> means the protocol was not reported, so it is
        /// stored as unavailable while the mode is kept.
        /// </summary>
        [Fact]
        public void Record_ProtocolNone_RecordsModeAndNullProtocol()
        {
            // Arrange
            EncryptionInfoCapture capture = new();

            // Act
            capture.Record(SqlConnectionEncryptionMode.None, SslProtocols.None);

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.None, capture.Mode);
            Assert.Null(capture.Protocol);
            Assert.True(capture.HasValues);
        }

        #endregion

        #region RecordCipherSuite

        /// <summary>
        /// The smallest and largest valid IANA identifiers are kept.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(0xFFFF)]
        public void RecordCipherSuite_BoundaryValue_Kept(int cipherSuite)
        {
            // Arrange
            EncryptionInfoCapture capture = new();

            // Act
            capture.RecordCipherSuite(cipherSuite);

            // Assert
            Assert.Equal(cipherSuite, capture.ToSnapshot().NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// A value outside the IANA identifier range is ignored without throwing, and leaves an
        /// earlier valid value in place.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(0x10000)]
        public void RecordCipherSuite_OutOfRange_Ignored(int cipherSuite)
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            capture.RecordCipherSuite(cipherSuite);

            // Assert
            Assert.Equal(SampleCipherSuite, capture.CipherSuite);
            Assert.Equal(SampleCipherSuite, capture.ToSnapshot().NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// A cipher suite recorded on its own makes the capture non-empty.
        /// </summary>
        [Fact]
        public void RecordCipherSuite_Alone_HasValuesAndSnapshotNotEmpty()
        {
            // Arrange
            EncryptionInfoCapture capture = new();

            // Act
            capture.RecordCipherSuite(SampleCipherSuite);

            // Assert
            Assert.True(capture.HasValues);
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.NotSame(SqlConnectionEncryptionInfo.Empty, snapshot);
            Assert.Equal(SampleCipherSuite, snapshot.NegotiatedCipherSuiteId);
        }

        #endregion

        #region RecordModeIfProtocolObserved

        /// <summary>
        /// Without an observed protocol, the mode is not recorded and the cipher suite is untouched.
        /// </summary>
        [Fact]
        public void RecordModeIfProtocolObserved_NoProtocol_LeavesModeNullAndCipherSuite()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            capture.RecordModeIfProtocolObserved(SqlConnectionEncryptionMode.Strict);

            // Assert
            Assert.Null(capture.Mode);
            Assert.Equal(SampleCipherSuite, capture.CipherSuite);
        }

        /// <summary>
        /// With an observed protocol, the mode is recorded and the protocol is kept.
        /// </summary>
        [Fact]
        public void RecordModeIfProtocolObserved_ProtocolObserved_RecordsMode()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.None, SslProtocols.Tls12);

            // Act
            capture.RecordModeIfProtocolObserved(SqlConnectionEncryptionMode.Strict);

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.Strict, capture.Mode);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        #endregion

        #region Reset

        /// <summary>
        /// Resetting clears every recorded value, so the snapshot becomes empty.
        /// </summary>
        [Fact]
        public void Reset_AfterRecord_ClearsAllValues()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            capture.Reset();

            // Assert
            Assert.Null(capture.Mode);
            Assert.Null(capture.Protocol);
            Assert.Null(capture.CipherSuite);
            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        #endregion

        #region ToSnapshot

        /// <summary>
        /// With nothing recorded, the snapshot is the shared empty instance.
        /// </summary>
        [Fact]
        public void ToSnapshot_NothingRecorded_ReturnsEmpty()
        {
            // Arrange
            EncryptionInfoCapture capture = new();

            // Act
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

            // Assert
            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, snapshot);
        }

        /// <summary>
        /// The snapshot carries the recorded mode, protocol and cipher suite, and no certificate fields.
        /// </summary>
        [Fact]
        public void ToSnapshot_AllRecorded_SurfacesValuesWithoutCertificateFields()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

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
        /// Without a recorded cipher suite, the snapshot reports none.
        /// </summary>
        [Fact]
        public void ToSnapshot_CipherSuiteNotRecorded_ReturnsNullCipherSuite()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            // Act
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

            // Assert
            Assert.Null(capture.CipherSuite);
            Assert.Null(snapshot.NegotiatedCipherSuiteId);
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
        }

#if NET
        /// <summary>
        /// On .NET, the recorded identifier is also surfaced as the matching
        /// <see cref="System.Net.Security.TlsCipherSuite"/> value.
        /// </summary>
        [Fact]
        public void ToSnapshot_CipherSuiteRecorded_SurfacesTlsCipherSuite()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);

            // Act
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

            // Assert
            Assert.Equal(System.Net.Security.TlsCipherSuite.TLS_AES_256_GCM_SHA384, snapshot.NegotiatedCipherSuite);
        }
#endif

        /// <summary>
        /// A snapshot is unaffected when the capture is later reset and reused for another attempt.
        /// </summary>
        [Fact]
        public void ToSnapshot_LaterResetAndRecord_SnapshotUnchanged()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            capture.RecordCipherSuite(SampleCipherSuite);
            SqlConnectionEncryptionInfo first = capture.ToSnapshot();

            // Act
            capture.Reset();
            capture.RecordCipherSuite(OtherCipherSuite);

            // Assert
            Assert.Equal(SampleCipherSuite, first.NegotiatedCipherSuiteId);
            Assert.Equal(OtherCipherSuite, capture.ToSnapshot().NegotiatedCipherSuiteId);
        }

        #endregion
    }
}
