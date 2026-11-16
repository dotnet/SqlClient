// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Authentication;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.Parser;
using Xunit;
using static Microsoft.Data.SqlClient.UnitTests.Utilities.EncryptionInfoTestData;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Unit tests for the <see cref="TdsParser"/> rules that record negotiated encryption values into
    /// an <see cref="EncryptionInfoAccumulator"/>.
    /// </summary>
    public class TdsParserTests
    {
        #region DiscardUnobservedNegotiation

        /// <summary>
        /// When a protocol was observed, discarding unobserved negotiation keeps the mode, protocol
        /// and cipher suite.
        /// </summary>
        [Fact]
        public void DiscardUnobservedNegotiation_ProtocolObserved_KeepsAllValues()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.RecordEncryption(accumulator, SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);
            TdsParser.RecordCipherSuite(accumulator, ToConstructorCipherSuite(SampleCipherSuite));

            // Act
            TdsParser.DiscardUnobservedNegotiation(accumulator);

            // Assert
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();
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
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.RecordEncryption(accumulator, SqlConnectionEncryptionMode.Full, SslProtocols.None);
            TdsParser.RecordCipherSuite(accumulator, ToConstructorCipherSuite(SampleCipherSuite));

            // Act
            TdsParser.DiscardUnobservedNegotiation(accumulator);

            // Assert
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();
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
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.RecordEncryption(accumulator, SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            // Act
            TdsParser.DiscardUnobservedNegotiation(accumulator);

            // Assert
            Assert.Null(accumulator.CipherSuite);
            Assert.Null(accumulator.ToSnapshot().NegotiatedCipherSuiteId);
        }

        #endregion

        #region RecordEncryption

        /// <summary>
        /// Recording <see cref="SslProtocols.None"/> means the protocol was not reported, so it is
        /// stored as unavailable while the mode is kept.
        /// </summary>
        [Fact]
        public void RecordEncryption_ProtocolNone_RecordsModeAndNullProtocol()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            TdsParser.RecordEncryption(accumulator, SqlConnectionEncryptionMode.None, SslProtocols.None);

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.None, accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            Assert.True(accumulator.HasValues);
        }

        /// <summary>
        /// A reported protocol is recorded together with the mode.
        /// </summary>
        [Fact]
        public void RecordEncryption_ProtocolReported_RecordsModeAndProtocol()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            TdsParser.RecordEncryption(accumulator, SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.Full, accumulator.Mode);
            Assert.Equal(SslProtocols.Tls12, accumulator.Protocol);
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
            EncryptionInfoAccumulator accumulator = new();

            // Act
            TdsParser.RecordCipherSuite(accumulator, ToConstructorCipherSuite(cipherSuite));

            // Assert
            Assert.Equal(cipherSuite, accumulator.ToSnapshot().NegotiatedCipherSuiteId);
        }

#if NETFRAMEWORK
        /// <summary>
        /// On .NET Framework, where the identifier is recorded as an integer, a value outside the IANA
        /// identifier range is ignored without throwing, and leaves an earlier valid value in place.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(0x10000)]
        public void RecordCipherSuite_OutOfRange_Ignored(int cipherSuite)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.RecordCipherSuite(accumulator, SampleCipherSuite);

            // Act
            TdsParser.RecordCipherSuite(accumulator, cipherSuite);

            // Assert
            Assert.Equal(SampleCipherSuite, accumulator.CipherSuite);
            Assert.Equal(SampleCipherSuite, accumulator.ToSnapshot().NegotiatedCipherSuiteId);
        }
#endif

        /// <summary>
        /// A cipher suite recorded on its own makes the accumulator non-empty.
        /// </summary>
        [Fact]
        public void RecordCipherSuite_Alone_HasValuesAndSnapshotNotEmpty()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            TdsParser.RecordCipherSuite(accumulator, ToConstructorCipherSuite(SampleCipherSuite));

            // Assert
            Assert.True(accumulator.HasValues);
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();
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
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.RecordCipherSuite(accumulator, ToConstructorCipherSuite(SampleCipherSuite));

            // Act
            TdsParser.RecordModeIfProtocolObserved(accumulator, SqlConnectionEncryptionMode.Strict);

            // Assert
            Assert.Null(accumulator.Mode);
            Assert.Equal(ToConstructorCipherSuite(SampleCipherSuite), accumulator.CipherSuite);
        }

        /// <summary>
        /// With an observed protocol, the mode is recorded and the protocol is kept.
        /// </summary>
        [Fact]
        public void RecordModeIfProtocolObserved_ProtocolObserved_RecordsMode()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            TdsParser.RecordEncryption(accumulator, SqlConnectionEncryptionMode.None, SslProtocols.Tls12);

            // Act
            TdsParser.RecordModeIfProtocolObserved(accumulator, SqlConnectionEncryptionMode.Strict);

            // Assert
            Assert.Equal(SqlConnectionEncryptionMode.Strict, accumulator.Mode);
            Assert.Equal(SslProtocols.Tls12, accumulator.Protocol);
        }

        #endregion
    }
}