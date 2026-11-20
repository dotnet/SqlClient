// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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

        // Stand-in DER bytes; the accumulator stores the certificate without parsing it.
        private static readonly byte[] SampleCertificate = { 0x30, 0x82, 0x01, 0x0A, 0x5A, 0xC3, 0x7E, 0x11 };

        private const X509ChainStatusFlags SampleChainStatus =
            X509ChainStatusFlags.UntrustedRoot | X509ChainStatusFlags.NotTimeValid;

        #region Test data

        /// <summary>
        /// Every <see cref="SqlConnectionEncryptionMode"/> value.
        /// </summary>
        public static TheoryData<SqlConnectionEncryptionMode> AllModes =>
            new((SqlConnectionEncryptionMode[])Enum.GetValues(typeof(SqlConnectionEncryptionMode)));

        /// <summary>
        /// Every <see cref="SslProtocols"/> value other than <see cref="SslProtocols.None"/>,
        /// enumerated at run time so values added in future frameworks are covered. Obsolete
        /// values are included: enumerating them does not reference them by name, so the compiler
        /// does not warn.
        /// </summary>
        public static TheoryData<SslProtocols> ReportedProtocols =>
            new(((SslProtocols[])Enum.GetValues(typeof(SslProtocols)))
                .Where(protocol => protocol != SslProtocols.None));

        /// <summary>
        /// Every <see cref="SqlServerCertificateValidationErrors"/> value, including
        /// <see cref="SqlServerCertificateValidationErrors.None"/>, plus all flags combined.
        /// </summary>
        public static TheoryData<SqlServerCertificateValidationErrors> AllValidationErrors
        {
            get
            {
                SqlServerCertificateValidationErrors[] values =
                    (SqlServerCertificateValidationErrors[])Enum.GetValues(typeof(SqlServerCertificateValidationErrors));
                TheoryData<SqlServerCertificateValidationErrors> data = new(values);
                data.Add(values.Aggregate((all, value) => all | value));
                return data;
            }
        }

        /// <summary>
        /// A clean chain, a single chain status flag, and every chain status flag combined.
        /// </summary>
        public static TheoryData<X509ChainStatusFlags> ChainStatuses
        {
            get
            {
                X509ChainStatusFlags[] values =
                    (X509ChainStatusFlags[])Enum.GetValues(typeof(X509ChainStatusFlags));
                return new TheoryData<X509ChainStatusFlags>
                {
                    X509ChainStatusFlags.NoError,
                    X509ChainStatusFlags.UntrustedRoot,
                    values.Aggregate((all, value) => all | value),
                };
            }
        }

        #endregion

        #region Mode

        /// <summary>
        /// Setting the mode stores it and leaves every other value unset.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllModes))]
        public void Mode_Set_StoresValueOnly(SqlConnectionEncryptionMode mode)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            accumulator.Mode = mode;

            // Assert
            Assert.NotNull(accumulator.Mode);
            Assert.Equal(mode, accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            Assert.Null(accumulator.CipherSuite);
            AssertCertificateFieldsUnset(accumulator);
            Assert.True(accumulator.HasValues);
        }

        #endregion

        #region Protocol

        /// <summary>
        /// <see cref="SslProtocols.None"/> means the protocol was not reported, so it is stored as
        /// unavailable, replacing an earlier value, and leaves the other values unset.
        /// </summary>
        [Fact]
        public void Protocol_SetNone_StoresNull()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.Protocol = SslProtocols.Tls12;

            // Act
            accumulator.Protocol = SslProtocols.None;

            // Assert
            Assert.Null(accumulator.Protocol);
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.CipherSuite);
            AssertCertificateFieldsUnset(accumulator);
            Assert.False(accumulator.HasValues);
        }

        /// <summary>
        /// A reported protocol is stored unchanged and leaves every other value unset.
        /// </summary>
        [Theory]
        [MemberData(nameof(ReportedProtocols))]
        public void Protocol_SetReported_StoresValueOnly(SslProtocols protocol)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            accumulator.Protocol = protocol;

            // Assert
            Assert.NotNull(accumulator.Protocol);
            Assert.Equal(protocol, accumulator.Protocol);
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.CipherSuite);
            AssertCertificateFieldsUnset(accumulator);
            Assert.True(accumulator.HasValues);
        }

        #endregion

        #region CipherSuite

        /// <summary>
        /// The smallest and largest IANA identifiers that can be negotiated are stored and leave
        /// every other value unset.
        /// </summary>
        [Theory]
        [InlineData(1)]
        [InlineData(0xFFFF)]
        public void CipherSuite_BoundaryValue_StoresValueOnly(int cipherSuite)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            accumulator.CipherSuite = ToConstructorCipherSuite(cipherSuite);

            // Assert
            Assert.NotNull(accumulator.CipherSuite);
            Assert.Equal(ToConstructorCipherSuite(cipherSuite), accumulator.CipherSuite);
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            AssertCertificateFieldsUnset(accumulator);
            Assert.True(accumulator.HasValues);
            Assert.Equal(cipherSuite, accumulator.ToSnapshot().NegotiatedCipherSuiteId);
        }

        /// <summary>
        /// The null cipher suite (TLS_NULL_WITH_NULL_NULL, <c>0</c>) is never negotiated, so it means
        /// none was reported: it is stored as unavailable, replacing an earlier value, and leaves the
        /// other values unset.
        /// </summary>
        [Fact]
        public void CipherSuite_SetNullCipherSuite_StoresNull()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.CipherSuite = ToConstructorCipherSuite(SampleCipherSuite);

            // Act
            accumulator.CipherSuite = ToConstructorCipherSuite(0);

            // Assert
            Assert.Null(accumulator.CipherSuite);
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            AssertCertificateFieldsUnset(accumulator);
            Assert.False(accumulator.HasValues);
        }

#if NETFRAMEWORK
        /// <summary>
        /// On .NET Framework, where the identifier is an integer, a value outside the IANA identifier
        /// range is ignored without throwing, leaves an earlier valid value in place, and leaves the
        /// mode and protocol unset.
        /// </summary>
        [Theory]
        [InlineData(-1)]
        [InlineData(0x10000)]
        public void CipherSuite_OutOfRange_Ignored(int cipherSuite)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.CipherSuite = SampleCipherSuite;

            // Act
            accumulator.CipherSuite = cipherSuite;

            // Assert
            Assert.Equal(SampleCipherSuite, accumulator.CipherSuite);
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            AssertCertificateFieldsUnset(accumulator);
            Assert.Equal(SampleCipherSuite, accumulator.ToSnapshot().NegotiatedCipherSuiteId);
        }
#endif

        #endregion

        #region Certificate

        /// <summary>
        /// Setting the presented certificate stores it and leaves the other values unset.
        /// </summary>
        [Fact]
        public void ServerCertificateDerBytes_Set_StoresValueOnly()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            accumulator.ServerCertificateDerBytes = SampleCertificate;

            // Assert
            Assert.Equal(SampleCertificate, Assert.NotNull(accumulator.ServerCertificateDerBytes).ToArray());
            Assert.Null(accumulator.ValidationErrors);
            Assert.Null(accumulator.ChainStatus);
            AssertNegotiatedValuesUnset(accumulator);
            Assert.True(accumulator.HasValues);
            Assert.Equal(SampleCertificate, Assert.NotNull(accumulator.ToSnapshot().PresentedServerCertificateRawData).ToArray());
        }

        /// <summary>
        /// A certificate of exactly the maximum reported size is stored.
        /// </summary>
        [Fact]
        public void ServerCertificateDerBytes_SetMaximumSize_StoresValue()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            byte[] derBytes = new byte[EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize];

            // Act
            accumulator.ServerCertificateDerBytes = derBytes;

            // Assert
            Assert.Equal(derBytes.Length, Assert.NotNull(accumulator.ServerCertificateDerBytes).Length);
            Assert.Null(accumulator.ValidationErrors);
            Assert.Null(accumulator.ChainStatus);
            AssertNegotiatedValuesUnset(accumulator);
        }

        /// <summary>
        /// An empty or oversized certificate is not reported: it is stored as unavailable, replacing
        /// an earlier value, rather than truncated, and leaves the other values unset.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize + 1)]
        public void ServerCertificateDerBytes_SetUnreportableSize_StoresNull(int length)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.ServerCertificateDerBytes = SampleCertificate;

            // Act
            accumulator.ServerCertificateDerBytes = new byte[length];

            // Assert
            Assert.Null(accumulator.ServerCertificateDerBytes);
            Assert.Null(accumulator.ValidationErrors);
            Assert.Null(accumulator.ChainStatus);
            AssertNegotiatedValuesUnset(accumulator);
            Assert.False(accumulator.HasValues);
        }

        /// <summary>
        /// The accumulator stores its own copy of the encoding, so mutating the source afterwards
        /// changes neither the accumulator nor a snapshot that shares its copy.
        /// </summary>
        [Fact]
        public void ServerCertificateDerBytes_SourceMutated_StoredCopyUnchanged()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            byte[] derBytes = SampleCertificate.ToArray();

            // Act
            accumulator.ServerCertificateDerBytes = derBytes;
            SqlConnectionEncryptionInfo snapshot = accumulator.ToSnapshot();
            derBytes[0] ^= 0xFF;

            // Assert
            Assert.Equal(SampleCertificate, Assert.NotNull(accumulator.ServerCertificateDerBytes).ToArray());
            Assert.Equal(SampleCertificate, Assert.NotNull(snapshot.PresentedServerCertificateRawData).ToArray());
        }

        /// <summary>
        /// Setting the validation verdict stores it, including the zero value
        /// <see cref="SqlServerCertificateValidationErrors.None"/>, which is available rather than
        /// unset, and leaves the other values unset.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllValidationErrors))]
        public void ValidationErrors_Set_StoresValueOnly(SqlServerCertificateValidationErrors errors)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            accumulator.ValidationErrors = errors;

            // Assert
            Assert.NotNull(accumulator.ValidationErrors);
            Assert.Equal(errors, accumulator.ValidationErrors);
            Assert.Null(accumulator.ServerCertificateDerBytes);
            Assert.Null(accumulator.ChainStatus);
            AssertNegotiatedValuesUnset(accumulator);
            Assert.True(accumulator.HasValues);
            Assert.Equal(errors, accumulator.ToSnapshot().ValidationErrors);
        }

        /// <summary>
        /// Setting the chain status stores every flag unchanged, including the zero value
        /// <see cref="X509ChainStatusFlags.NoError"/>, which is available rather than unset, and
        /// leaves the other values unset.
        /// </summary>
        [Theory]
        [MemberData(nameof(ChainStatuses))]
        public void ChainStatus_Set_StoresValueOnly(X509ChainStatusFlags chainStatus)
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();

            // Act
            accumulator.ChainStatus = chainStatus;

            // Assert
            Assert.NotNull(accumulator.ChainStatus);
            Assert.Equal(chainStatus, accumulator.ChainStatus);
            Assert.Null(accumulator.ServerCertificateDerBytes);
            Assert.Null(accumulator.ValidationErrors);
            AssertNegotiatedValuesUnset(accumulator);
            Assert.True(accumulator.HasValues);
            Assert.Equal(chainStatus, accumulator.ToSnapshot().ChainStatus);
        }

        /// <summary>
        /// The snapshot's text never contains the certificate or any common encoding of it.
        /// </summary>
        [Fact]
        public void ToSnapshot_CertificateSet_NotInSnapshotText()
        {
            // Arrange
            EncryptionInfoAccumulator accumulator = new();
            accumulator.ServerCertificateDerBytes = SampleCertificate;
            accumulator.ValidationErrors = SqlServerCertificateValidationErrors.ChainError;

            // Act
            string text = accumulator.ToSnapshot().ToString() ?? string.Empty;

            // Assert
            Assert.DoesNotContain(Convert.ToBase64String(SampleCertificate), text);
            Assert.DoesNotContain(
                BitConverter.ToString(SampleCertificate).Replace("-", string.Empty),
                text,
                StringComparison.OrdinalIgnoreCase);
        }

        #endregion

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
            accumulator.ServerCertificateDerBytes = SampleCertificate;
            accumulator.ValidationErrors = SqlServerCertificateValidationErrors.ChainError;
            accumulator.ChainStatus = SampleChainStatus;

            // Act
            accumulator.Reset();

            // Assert
            AssertNegotiatedValuesUnset(accumulator);
            AssertCertificateFieldsUnset(accumulator);
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
        /// Without certificate details, the snapshot carries the mode, protocol and cipher suite, and
        /// reports the certificate fields as unavailable.
        /// </summary>
        [Fact]
        public void ToSnapshot_CertificateFieldsNotSet_ReturnsNullCertificateFields()
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

        #region Helpers

        /// <summary>
        /// Asserts that the mode, protocol and cipher suite are unset.
        /// </summary>
        private static void AssertNegotiatedValuesUnset(EncryptionInfoAccumulator accumulator)
        {
            Assert.Null(accumulator.Mode);
            Assert.Null(accumulator.Protocol);
            Assert.Null(accumulator.CipherSuite);
        }

        /// <summary>
        /// Asserts that the certificate, validation errors and chain status are unset.
        /// </summary>
        private static void AssertCertificateFieldsUnset(EncryptionInfoAccumulator accumulator)
        {
            Assert.Null(accumulator.ServerCertificateDerBytes);
            Assert.Null(accumulator.ValidationErrors);
            Assert.Null(accumulator.ChainStatus);
        }

        #endregion
    }
}
