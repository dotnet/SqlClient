// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Interop.Windows.Sni;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.NativeSni;
using Microsoft.Data.SqlClient.UnitTests.Utilities;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests for the native SNI certificate-detail interop: the <see cref="SniCertValidationInfo"/>
    /// layout, reading the presented certificate, mapping of native availability and bitmasks to
    /// <see cref="EncryptionInfoAccumulator"/>, and graceful degradation when the loaded SNI library
    /// does not export the functions.
    /// </summary>
    public class SniNativeCertificateCaptureTests
    {
        private const uint AllAvailable = 0xFF;
        private const uint CertificateAvailable = 0x4;
        private const uint CipherSuiteAvailable = 0x80;
        private const uint TlsClient12 = 0x00000800;
        private const uint TlsAes256GcmSha384 = EncryptionInfoTestData.SampleCipherSuite;

        #region Layout

        /// <summary>Verifies the managed structure matches the 24-byte native size.</summary>
        [Fact]
        public void Struct_IsTwentyFourBytes()
        {
            Assert.Equal(24, Marshal.SizeOf<SniCertValidationInfo>());
        }

        /// <summary>Verifies each field sits at the offset defined by the native contract.</summary>
        [Theory]
        [InlineData(nameof(SniCertValidationInfo.dwAvailable), 0)]
        [InlineData(nameof(SniCertValidationInfo.dwValidationMode), 4)]
        [InlineData(nameof(SniCertValidationInfo.dwProtocol), 8)]
        [InlineData(nameof(SniCertValidationInfo.dwValidationErrors), 12)]
        [InlineData(nameof(SniCertValidationInfo.dwChainStatus), 16)]
        [InlineData(nameof(SniCertValidationInfo.dwCipherSuite), 20)]
        public void Struct_FieldOffsets_MatchNativeContract(string field, int expectedOffset)
        {
            Assert.Equal(expectedOffset, Marshal.OffsetOf<SniCertValidationInfo>(field).ToInt32());
        }

        /// <summary>Verifies the availability flag values match the native contract.</summary>
        [Fact]
        public void AvailabilityFlags_MatchNativeContract()
        {
            Assert.Equal(0x1u, (uint)SniCertInfoAvailability.AttemptStarted);
            Assert.Equal(0x2u, (uint)SniCertInfoAvailability.Protocol);
            Assert.Equal(0x4u, (uint)SniCertInfoAvailability.Certificate);
            Assert.Equal(0x8u, (uint)SniCertInfoAvailability.ValidationErrors);
            Assert.Equal(0x10u, (uint)SniCertInfoAvailability.ChainStatus);
            Assert.Equal(0x20u, (uint)SniCertInfoAvailability.HandshakeCompleted);
            Assert.Equal(0x40u, (uint)SniCertInfoAvailability.ChainStatusLeafTimeOnly);
            Assert.Equal(0x80u, (uint)SniCertInfoAvailability.CipherSuite);
        }

        /// <summary>Verifies the validation mode values match the native contract.</summary>
        [Fact]
        public void ValidationModes_MatchNativeContract()
        {
            Assert.Equal(0u, (uint)SniCertValidationMode.Unknown);
            Assert.Equal(1u, (uint)SniCertValidationMode.NotValidated);
            Assert.Equal(2u, (uint)SniCertValidationMode.SchannelAutomatic);
            Assert.Equal(3u, (uint)SniCertValidationMode.ChainPolicy);
            Assert.Equal(4u, (uint)SniCertValidationMode.PinnedCertificate);
        }

        /// <summary>Verifies native validation error bits equal the public enum values.</summary>
        [Fact]
        public void ValidationErrorBits_MatchManagedEnum()
        {
            Assert.Equal(1, (int)SqlServerCertificateValidationErrors.ServerCertificateMissing);
            Assert.Equal(2, (int)SqlServerCertificateValidationErrors.HostNameMismatch);
            Assert.Equal(4, (int)SqlServerCertificateValidationErrors.ChainError);
            Assert.Equal(8, (int)SqlServerCertificateValidationErrors.PinnedCertificateMismatch);
        }

        /// <summary>Verifies a structure read from native-layout bytes exposes every field.</summary>
        [Fact]
        public void Struct_ReadFromNativeBytes_RoundTripsFields()
        {
            SniCertValidationInfo info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);

            Assert.Equal(AllAvailable, info.dwAvailable);
            Assert.Equal(3u, info.dwValidationMode);
            Assert.Equal(TlsClient12, info.dwProtocol);
            Assert.Equal(4u, info.dwValidationErrors);
            Assert.Equal(0x20u, info.dwChainStatus);
            Assert.Equal(0xC030u, info.dwCipherSuite);
        }

        #endregion

        #region Availability

        /// <summary>Provides every combination of the eight availability flags.</summary>
        public static IEnumerable<object[]> AvailabilityCombinations() =>
            Enumerable.Range(0, 0x100).Select(i => new object[] { (uint)i });

        /// <summary>Verifies only fields whose availability flag is set are reported.</summary>
        [Theory]
        [MemberData(nameof(AvailabilityCombinations))]
        public void Apply_PopulatesOnlyAvailableFields(uint available)
        {
            byte[] certificate = Certificate(0x10);
            SniCertValidationInfo info = Build(available, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);

            EncryptionInfoAccumulator? capture = Map(info, certificate);

            bool hasCertificate = (available & 0x4) != 0;
            bool hasErrors = (available & 0x8) != 0;
            bool hasChain = (available & 0x10) != 0 && (available & 0x40) == 0;
            bool hasProtocol = (available & 0x2) != 0;
            bool hasCipherSuite = (available & 0x80) != 0;

            if (!hasCertificate && !hasErrors && !hasChain && !hasProtocol && !hasCipherSuite)
            {
                // Only ATTEMPT_STARTED / HANDSHAKE_COMPLETED / LEAF_TIME_ONLY qualifiers: nothing to report.
                Assert.Null(capture);
                return;
            }

            Assert.NotNull(capture);
            Assert.Equal(hasCertificate ? certificate : null, Der(capture));
            Assert.Equal(hasErrors ? SqlServerCertificateValidationErrors.ChainError : null, capture.ValidationErrors);
            Assert.Equal(hasChain ? X509ChainStatusFlags.UntrustedRoot : null, capture.ChainStatus);
            Assert.Equal(hasProtocol ? SslProtocols.Tls12 : null, capture.Protocol);
            Assert.Equal(hasCipherSuite ? Suite(0xC030) : null, capture.CipherSuite);
        }

        /// <summary>Verifies a certificate flag without certificate bytes leaves the certificate unknown.</summary>
        [Fact]
        public void Apply_CertificateFlagWithoutBytes_LeavesCertificateNull()
        {
            SniCertValidationInfo info = Build(available: CertificateAvailable | 0x8, mode: 3, protocol: 0, errors: 4, chain: 0);

            EncryptionInfoAccumulator? capture = Map(info, derBytes: null);

            Assert.NotNull(capture);

            Assert.Null(Der(capture));
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capture.ValidationErrors);
            Assert.Null(Map(Build(available: CertificateAvailable, mode: 3, protocol: 0, errors: 0, chain: 0)));
        }

        /// <summary>Verifies available zero bitmasks are reported as no error rather than unknown.</summary>
        [Fact]
        public void Apply_ZeroBitmasksWithAvailability_AreReportedAsNoErrorNotNull()
        {
            SniCertValidationInfo info = Build(available: 0x8 | 0x10, mode: 3, protocol: 0, errors: 0, chain: 0);

            EncryptionInfoAccumulator? capture = Map(info);

            Assert.NotNull(capture);

            Assert.Equal(SqlServerCertificateValidationErrors.None, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, capture.ChainStatus);
            Assert.Null(Der(capture));
            Assert.Null(capture.Protocol);
        }

        /// <summary>Verifies bitmask values without availability flags are not reported.</summary>
        [Fact]
        public void Apply_ValidationFieldsWithoutAvailability_LeaveValidationFieldsNull()
        {
            // An older sni.dll may report a SchannelAutomatic attempt without VALIDATION_ERRORS or
            // CHAIN_STATUS availability. Stale or garbage bitmask values must not leak through.
            byte[] certificate = Certificate(0x33);
            SniCertValidationInfo info = Build(
                available: 0x1 | 0x2 | 0x4,
                mode: (uint)SniCertValidationMode.SchannelAutomatic,
                protocol: TlsClient12,
                errors: 0x4,
                chain: 0x20);

            EncryptionInfoAccumulator? capture = Map(info, certificate);

            Assert.NotNull(capture);

            Assert.Null(capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
            Assert.Equal(certificate, Der(capture));
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        /// <summary>Verifies a deferred validation failure reports the verdict, chain status, and certificate.</summary>
        [Theory]
        [InlineData((uint)SniCertValidationMode.SchannelAutomatic, 0x4u)]
        [InlineData((uint)SniCertValidationMode.ChainPolicy, 0x2u | 0x4u)]
        public void Apply_DeferredValidationFailure_ReportsVerdictChainStatusAndCertificate(uint mode, uint errors)
        {
            // SNI with deferred validation reports the verdict, chain status, and certificate in every
            // mode, and reports HostNameMismatch together with ChainError, matching Managed SNI.
            byte[] certificate = Certificate(0x44);
            SniCertValidationInfo info = Build(
                available: 0x1 | 0x2 | 0x4 | 0x8 | 0x10,
                mode: mode,
                protocol: TlsClient12,
                errors: errors,
                chain: (uint)X509ChainStatusFlags.UntrustedRoot);

            EncryptionInfoAccumulator? capture = Map(info, certificate);

            Assert.NotNull(capture);

            Assert.Equal((SqlServerCertificateValidationErrors)errors, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, capture.ChainStatus);
            Assert.Equal(certificate, Der(capture));
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        /// <summary>Verifies no details are reported without data flags, in every validation mode.</summary>
        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(2u)]
        [InlineData(3u)]
        [InlineData(4u)]
        [InlineData(99u)]
        public void Apply_NoAvailability_IsNullForEveryValidationMode(uint mode)
        {
            SniCertValidationInfo info = Build(available: 0x1 | 0x20 | 0x40, mode: mode, protocol: TlsClient12, errors: 0xF, chain: 0xFFFF, cipherSuite: 0xC030);

            Assert.Null(Map(info, Certificate(1)));
        }

        #endregion

        #region Validation errors and chain status

        /// <summary>Verifies validation error bits, including unknown ones, are preserved.</summary>
        [Theory]
        [InlineData(0x0u)]
        [InlineData(0x1u)]
        [InlineData(0x2u)]
        [InlineData(0x4u)]
        [InlineData(0x8u)]
        [InlineData(0x6u)]
        [InlineData(0xFu)]
        [InlineData(0x100u)]
        [InlineData(0x80000004u)]
        public void Apply_ValidationErrors_PreservesEveryBit(uint errors)
        {
            SniCertValidationInfo info = Build(available: 0x8, mode: 3, protocol: 0, errors: errors, chain: 0);

            EncryptionInfoAccumulator? mappedCapture = Map(info);
            Assert.NotNull(mappedCapture);
            Assert.Equal(unchecked((SqlServerCertificateValidationErrors)(int)errors), mappedCapture.ValidationErrors);
        }

        /// <summary>Provides every known chain status flag, all of them combined, and unknown bits.</summary>
        public static IEnumerable<object[]> ChainStatusValues()
        {
            foreach (X509ChainStatusFlags flag in Enum.GetValues(typeof(X509ChainStatusFlags)))
            {
                yield return new object[] { (uint)flag };
            }

            uint all = 0;
            foreach (X509ChainStatusFlags flag in Enum.GetValues(typeof(X509ChainStatusFlags)))
            {
                all |= (uint)flag;
            }

            yield return new object[] { all };

            // Unknown future CERT_TRUST bits are preserved, alone and combined with known ones.
            yield return new object[] { 0x40000000u };
            yield return new object[] { 0x80000000u | (uint)X509ChainStatusFlags.Revoked };
            yield return new object[] { all | 0xC0000000u };
        }

        /// <summary>Verifies chain status bits, including unknown ones, are preserved.</summary>
        [Theory]
        [MemberData(nameof(ChainStatusValues))]
        public void Apply_ChainStatus_PreservesEveryBit(uint chain)
        {
            SniCertValidationInfo info = Build(available: 0x10, mode: 3, protocol: 0, errors: 0, chain: chain);

            EncryptionInfoAccumulator? mappedCapture = Map(info);
            Assert.NotNull(mappedCapture);
            X509ChainStatusFlags mapped = Assert.NotNull(mappedCapture.ChainStatus);

            Assert.Equal(unchecked((X509ChainStatusFlags)(int)chain), mapped);
            Assert.Equal(chain, unchecked((uint)mapped));
        }

        /// <summary>Verifies a leaf-time-only status leaves chain status unknown and keeps other fields.</summary>
        [Theory]
        [InlineData((uint)X509ChainStatusFlags.NotTimeValid)]
        [InlineData(0u)]
        public void Apply_ChainStatusLeafTimeOnly_LeavesChainStatusNullAndKeepsOtherFields(uint chain)
        {
            // Arrange
            // Pinned-certificate expiry: native checks only the leaf validity period and builds no
            // chain, so a NotTimeValid or zero status must not be reported as a chain status.
            byte[] certificate = Certificate(0x55);
            SniCertValidationInfo info = Build(
                available: 0x2 | 0x4 | 0x8 | 0x10 | 0x40 | CipherSuiteAvailable,
                mode: (uint)SniCertValidationMode.PinnedCertificate,
                protocol: TlsClient12,
                errors: (uint)SqlServerCertificateValidationErrors.ChainError,
                chain: chain,
                cipherSuite: 0xC030);

            // Act
            EncryptionInfoAccumulator? capture = Map(info, certificate);
            Assert.NotNull(capture);

            // Assert
            Assert.Null(capture.ChainStatus);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capture.ValidationErrors);
            Assert.Equal(certificate, Der(capture));
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
            Assert.Equal(Suite(0xC030), capture.CipherSuite);
        }

        /// <summary>Verifies a leaf-time-only status with no other data reports nothing.</summary>
        [Fact]
        public void Apply_ChainStatusLeafTimeOnlyWithoutOtherFields_IsUnavailable()
        {
            // Arrange
            SniCertValidationInfo info = Build(
                available: 0x10 | 0x40,
                mode: (uint)SniCertValidationMode.PinnedCertificate,
                protocol: 0,
                errors: 0,
                chain: (uint)X509ChainStatusFlags.NotTimeValid);

            // Act
            EncryptionInfoAccumulator? capture = Map(info);

            // Assert
            Assert.Null(capture);
        }

        #endregion

        #region Protocol

        /// <summary>Verifies native client protocol bits map to the matching protocol.</summary>
        [Theory]
        [InlineData(0x00000800u, (int)SslProtocols.Tls12)]
        [InlineData(0x00000400u, (int)SslProtocols.Tls12)]
        [InlineData(0x00002000u, 0x3000)]
        [InlineData(0x00001000u, 0x3000)]
#pragma warning disable CS0618, SYSLIB0039 // Obsolete protocols are only mapped, not used.
        [InlineData(0x00000200u, (int)SslProtocols.Tls11)]
        [InlineData(0x00000080u, (int)SslProtocols.Tls)]
        [InlineData(0x00000020u, (int)SslProtocols.Ssl3)]
        [InlineData(0x00000008u, (int)SslProtocols.Ssl2)]
#pragma warning restore CS0618, SYSLIB0039
        public void Apply_Protocol_MapsClientBits(uint nativeProtocol, int expected)
        {
            SniCertValidationInfo info = Build(available: 0x2, mode: 0, protocol: nativeProtocol, errors: 0, chain: 0);

            EncryptionInfoAccumulator? mappedCapture = Map(info);
            Assert.NotNull(mappedCapture);
            SslProtocols? protocol = mappedCapture.Protocol;

            Assert.Equal((SslProtocols)expected, protocol);
#if NET
            if (expected == 0x3000)
            {
                Assert.Equal(SslProtocols.Tls13, protocol);
            }
#endif
        }

        /// <summary>Verifies protocol bits with no mapping are not reported.</summary>
        [Theory]
        [InlineData(0x0u)]
        [InlineData(0x00040000u)]
        public void Apply_Protocol_UnmappedBitsAreUnavailable(uint nativeProtocol)
        {
            SniCertValidationInfo info = Build(available: 0x2, mode: 0, protocol: nativeProtocol, errors: 0, chain: 0);

            Assert.Null(Map(info));
        }

        /// <summary>Verifies the shared protocol mapping used by the handshake path.</summary>
        [Fact]
        public void ToSslProtocols_MatchesHandshakeMapping()
        {
            Assert.Equal(SslProtocols.Tls12, SniNativeWrapper.ToSslProtocols(SniSslProtocols.SP_PROT_TLS1_2_CLIENT));
            Assert.Equal((SslProtocols)0x3000, SniNativeWrapper.ToSslProtocols(SniSslProtocols.SP_PROT_TLS1_3_CLIENT));
            Assert.Equal(SslProtocols.None, SniNativeWrapper.ToSslProtocols(SniSslProtocols.SP_PROT_NONE));
        }

        #endregion

        #region Certificate size and structure validation

        /// <summary>Verifies certificates within the size limit are kept.</summary>
        [Theory]
        [InlineData(1)]
        [InlineData(EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize)]
        public void Apply_CertificateWithinLimit_IsKept(int length)
        {
            byte[] certificate = new byte[length];
            SniCertValidationInfo info = Build(available: CertificateAvailable, mode: 0, protocol: 0, errors: 0, chain: 0);

            EncryptionInfoAccumulator? mappedCapture = Map(info, certificate);
            Assert.NotNull(mappedCapture);
            Assert.Equal(certificate, Der(mappedCapture));
        }

        /// <summary>Verifies empty or oversized certificates are not reported.</summary>
        [Theory]
        [InlineData(0)]
        [InlineData(EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize + 1)]
        public void Apply_CertificateOutsideLimit_IsUnavailable(int length)
        {
            SniCertValidationInfo info = Build(available: CertificateAvailable, mode: 0, protocol: 0, errors: 0, chain: 0);

            Assert.Null(Map(info, new byte[length]));
        }

        #endregion

        #region Cipher suite

        /// <summary>Verifies the cipher suite is reported when its flag is set.</summary>
        [Fact]
        public void Apply_CipherSuiteFlag_ReportsCipherSuite()
        {
            SniCertValidationInfo info = Build(available: 0x2 | CipherSuiteAvailable, mode: 2, protocol: TlsClient12, errors: 0, chain: 0, cipherSuite: TlsAes256GcmSha384);

            EncryptionInfoAccumulator? capture = Map(info);

            Assert.NotNull(capture);

            Assert.Equal(Suite((int)TlsAes256GcmSha384), capture.CipherSuite);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        /// <summary>Verifies a cipher suite alone is enough to report details.</summary>
        [Fact]
        public void Apply_CipherSuiteOnly_IsNotDiscarded()
        {
            SniCertValidationInfo info = Build(available: CipherSuiteAvailable, mode: 0, protocol: 0, errors: 0, chain: 0, cipherSuite: 0xC030);

            EncryptionInfoAccumulator? mappedCapture = Map(info);
            Assert.NotNull(mappedCapture);
            Assert.Equal(Suite(0xC030), mappedCapture.CipherSuite);
        }

        /// <summary>Verifies the cipher suite is not reported without its flag.</summary>
        [Fact]
        public void Apply_WithoutCipherSuiteFlag_LeavesCipherSuiteNull()
        {
            SniCertValidationInfo info = Build(available: AllAvailable & ~CipherSuiteAvailable, mode: 3, protocol: TlsClient12, errors: 0, chain: 0, cipherSuite: TlsAes256GcmSha384);

            EncryptionInfoAccumulator? mappedCapture = Map(info, Certificate(2));
            Assert.NotNull(mappedCapture);
            Assert.Null(mappedCapture.CipherSuite);
        }

        /// <summary>Verifies zero or out-of-range cipher suite values are not reported.</summary>
        [Theory]
        [InlineData(0u)]
        [InlineData(0x10000u)]
        [InlineData(uint.MaxValue)]
        public void Apply_CipherSuiteOutOfRange_LeavesCipherSuiteNull(uint value)
        {
            SniCertValidationInfo info = Build(available: 0x8 | CipherSuiteAvailable, mode: 3, protocol: 0, errors: 0, chain: 0, cipherSuite: value);

            EncryptionInfoAccumulator? mappedCapture = Map(info);
            Assert.NotNull(mappedCapture);
            Assert.Null(mappedCapture.CipherSuite);
        }

        /// <summary>Verifies the largest valid cipher suite value is reported.</summary>
        [Fact]
        public void Apply_MaximumCipherSuite_IsReported()
        {
            SniCertValidationInfo info = Build(available: CipherSuiteAvailable, mode: 0, protocol: 0, errors: 0, chain: 0, cipherSuite: 0xFFFF);

            EncryptionInfoAccumulator? mappedCapture = Map(info);
            Assert.NotNull(mappedCapture);
            Assert.Equal(Suite(0xFFFF), mappedCapture.CipherSuite);
        }

        #endregion

        #region Query, error codes, and degradation

        /// <summary>Verifies a successful query reads the certificate and reports every available field.</summary>
        [Fact]
        public void Record_Success_ReadsCertificate()
        {
            byte[] certificate = Certificate(0x44);
            var certificateQuery = new FakeCertificateQuery(certificate);
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x4 | 0x8 | 0x10 | CipherSuiteAvailable, mode: 3, protocol: 0, errors: 0, chain: 0, cipherSuite: 0xC02F);
                    return 0;
                },
                certificateQuery.Query);

            EncryptionInfoAccumulator? result = Read(capture);

            Assert.NotNull(result);

            Assert.Equal(certificate, Der(result));
            Assert.Equal(new[] { 0, certificate.Length }, certificateQuery.BufferLengths);
            Assert.Equal(SqlServerCertificateValidationErrors.None, result.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, result.ChainStatus);
            Assert.Equal(Suite(0xC02F), result.CipherSuite);
        }

        /// <summary>Verifies the certificate is not queried when it is not available.</summary>
        [Fact]
        public void Record_CertificateNotAvailable_DoesNotReadCertificate()
        {
            var certificateQuery = new FakeCertificateQuery(Certificate(0x77));
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x2 | 0x8, mode: 2, protocol: TlsClient12, errors: 0, chain: 0);
                    return 0;
                },
                certificateQuery.Query);

            EncryptionInfoAccumulator? result = Read(capture);

            Assert.NotNull(result);

            Assert.Null(Der(result));
            Assert.Equal(SslProtocols.Tls12, result.Protocol);
            Assert.Empty(certificateQuery.BufferLengths);
        }

        /// <summary>Verifies error codes from the validation info query report nothing and skip the certificate query.</summary>
        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INVALID_PARAMETER)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER)]
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND)]
        [InlineData(SniNativeWrapper.ErrorProcNotFound)] // Export missing from the loaded sni.dll.
        [InlineData(5u)]
        public void Record_ValidationInfoErrorCode_LeavesFieldsNull(uint errorCode)
        {
            var certificateQuery = new FakeCertificateQuery(Certificate(0x77));
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    // Even if native wrote data, a failure code means nothing is trusted.
                    info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);
                    return errorCode;
                },
                certificateQuery.Query);

            Assert.Null(Read(capture));
            Assert.Empty(certificateQuery.BufferLengths);
        }

        /// <summary>Verifies a missing certificate export keeps the other fields.</summary>
        [Fact]
        public void Record_MissingCertificateExport_KeepsOtherFields()
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x2 | 0x4 | 0x8 | CipherSuiteAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0, cipherSuite: 0xC030);
                    return 0;
                },
                (SNIHandle? _, Span<byte> buffer, out uint size) =>
                {
                    size = 0;
                    return SniNativeWrapper.ErrorProcNotFound;
                });

            EncryptionInfoAccumulator? result = Read(capture);

            Assert.NotNull(result);
            Assert.Null(Der(result));
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, result.ValidationErrors);
            Assert.Equal(SslProtocols.Tls12, result.Protocol);
            Assert.Equal(Suite(0xC030), result.CipherSuite);
        }

        /// <summary>Verifies an unusable size query leaves the certificate unknown and skips the read.</summary>
        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INVALID_PARAMETER, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_SUCCESS, 100u)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, (uint)EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize + 1)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, uint.MaxValue)]
        public void Record_UnusableCertificateSize_LeavesCertificateNullAndSkipsRead(uint result, uint size)
        {
            var reads = new List<int>();
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x4 | 0x8, mode: 3, protocol: 0, errors: 4, chain: 0);
                    return 0;
                },
                (SNIHandle? _, Span<byte> buffer, out uint certificateSize) =>
                {
                    reads.Add(buffer.Length);
                    certificateSize = size;
                    return result;
                });

            EncryptionInfoAccumulator? capturedDetails = Read(capture);

            Assert.NotNull(capturedDetails);

            Assert.Null(Der(capturedDetails));
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capturedDetails.ValidationErrors);
            Assert.Equal(new[] { 0 }, reads);
        }

        /// <summary>Verifies a certificate at the size limit is read.</summary>
        [Fact]
        public void Record_MaximumCertificateSize_IsRead()
        {
            byte[] certificate = new byte[EncryptionInfoAccumulator.MaxServerCertificateDerBytesSize];
            certificate[0] = 0x30;
            certificate[certificate.Length - 1] = 0xAB;
            var certificateQuery = new FakeCertificateQuery(certificate);
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: CertificateAvailable, mode: 0, protocol: 0, errors: 0, chain: 0);
                    return 0;
                },
                certificateQuery.Query);

            EncryptionInfoAccumulator? mappedCapture = Read(capture);
            Assert.NotNull(mappedCapture);
            Assert.Equal(certificate, Der(mappedCapture));
        }

        /// <summary>Verifies a failed certificate read leaves the certificate unknown.</summary>
        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, 200u)] // A new attempt replaced the certificate.
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_SUCCESS, 50u)]
        public void Record_CertificateReadFailure_LeavesCertificateNull(uint result, uint written)
        {
            int calls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x4 | 0x8, mode: 3, protocol: 0, errors: 4, chain: 0);
                    return 0;
                },
                (SNIHandle? _, Span<byte> buffer, out uint certificateSize) =>
                {
                    if (calls++ == 0)
                    {
                        certificateSize = 100;
                        return SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER;
                    }

                    Assert.Equal(100, buffer.Length);
                    buffer.Fill(0xAB);
                    certificateSize = written;
                    return result;
                });

            EncryptionInfoAccumulator? capturedDetails = Read(capture);

            Assert.NotNull(capturedDetails);

            Assert.Equal(2, calls);
            Assert.Null(Der(capturedDetails));
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capturedDetails.ValidationErrors);
        }

        /// <summary>Verifies an unexpected exception from the validation info query propagates and is not cached.</summary>
        [Fact]
        public void Record_QueryThrows_PropagatesAndIsNotCached()
        {
            // Arrange
            int infoCalls = 0;
            var certificateQuery = new FakeCertificateQuery(Certificate(0x77));
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    infoCalls++;
                    throw new InvalidOperationException("boom");
                },
                certificateQuery.Query);

            // Act and assert
            Assert.Throws<InvalidOperationException>(() => Read(capture));
            Assert.Throws<InvalidOperationException>(() => Read(capture));
            Assert.Equal(2, infoCalls);
            Assert.Empty(certificateQuery.BufferLengths);
        }

        /// <summary>Verifies an unexpected exception from the certificate query propagates and is not cached.</summary>
        [Fact]
        public void Record_CertificateQueryThrows_PropagatesAndIsNotCached()
        {
            // Arrange
            int certificateCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x4 | 0x8, mode: 3, protocol: 0, errors: 4, chain: 0);
                    return 0;
                },
                (SNIHandle? _, Span<byte> buffer, out uint size) =>
                {
                    certificateCalls++;
                    throw new InvalidOperationException("boom");
                });

            // Act and assert
            Assert.Throws<InvalidOperationException>(() => Read(capture));
            Assert.Throws<InvalidOperationException>(() => Read(capture));
            Assert.Equal(2, certificateCalls);
        }

        /// <summary>Verifies a missing handle is rejected without native code and records nothing.</summary>
        [Fact]
        public void Record_ProductionBindingWithoutHandle_LeavesAccumulatorUnchanged()
        {
            // A null handle must never reach the P/Invoke binding (it would throw on platforms
            // without the native library); the production binding rejects it instead.
            var capture = new SniNativeCertificateCapture();

            Assert.Null(Read(capture));
            Assert.Null(Read(capture));
        }

        /// <summary>Verifies a null accumulator records nothing and does not query native SNI.</summary>
        [Fact]
        public void Record_NullAccumulator_DoesNotQuery()
        {
            int infoCalls = 0;
            var certificateQuery = new FakeCertificateQuery(Certificate(0x77));
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    infoCalls++;
                    return 0;
                },
                certificateQuery.Query);

            capture.Record(null, null);

            Assert.Equal(0, infoCalls);
            Assert.Empty(certificateQuery.BufferLengths);
        }

        /// <summary>
        /// Verifies a later usable report replaces every value of an earlier one, including with
        /// null, because native SNI is the single source of these values.
        /// </summary>
        [Fact]
        public void Record_LaterReport_ReplacesEarlierValues()
        {
            SniCertValidationInfo next = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);
            var certificateQuery = new FakeCertificateQuery(Certificate(0x10));
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = next;
                    return 0;
                },
                certificateQuery.Query);
            var accumulator = new EncryptionInfoAccumulator();

            capture.Record(null, accumulator);
            Assert.NotNull(accumulator.ServerCertificateDerBytes);

            next = Build(available: 0x2, mode: 2, protocol: TlsClient12, errors: 0, chain: 0);
            capture.Record(null, accumulator);

            Assert.Equal(SslProtocols.Tls12, accumulator.Protocol);
            Assert.Null(accumulator.ServerCertificateDerBytes);
            Assert.Null(accumulator.ValidationErrors);
            Assert.Null(accumulator.ChainStatus);
            Assert.Null(accumulator.CipherSuite);
        }

        /// <summary>Verifies an unusable report leaves earlier values in place.</summary>
        [Fact]
        public void Record_UnusableReport_LeavesAccumulatorUnchanged()
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) => SniNativeCertificateCapture.ERROR_NOT_FOUND,
                new FakeCertificateQuery(Certificate(0x77)).Query);
            var accumulator = new EncryptionInfoAccumulator { Protocol = SslProtocols.Tls12 };

            capture.Record(null, accumulator);

            Assert.Equal(SslProtocols.Tls12, accumulator.Protocol);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Records a fixed native structure and certificate into a new accumulator through
        /// <see cref="SniNativeCertificateCapture.Record"/>; null if nothing was recorded.
        /// </summary>
        private static EncryptionInfoAccumulator? Map(SniCertValidationInfo info, byte[]? derBytes = null)
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo result) =>
                {
                    result = info;
                    return SniNativeCertificateCapture.ERROR_SUCCESS;
                },
                new FakeCertificateQuery(derBytes).Query);
            return Read(capture);
        }

        /// <summary>Records through <paramref name="capture"/> into a new accumulator; null if nothing was recorded.</summary>
        private static EncryptionInfoAccumulator? Read(SniNativeCertificateCapture capture)
        {
            var accumulator = new EncryptionInfoAccumulator();
            capture.Record(null, accumulator);
            return accumulator.HasValues ? accumulator : null;
        }

        /// <summary>Gets a copy of the recorded certificate encoding, or null.</summary>
        private static byte[]? Der(EncryptionInfoAccumulator? accumulator) =>
            accumulator?.ServerCertificateDerBytes?.ToArray();

        /// <summary>Converts an IANA identifier to the cipher suite type of <see cref="EncryptionInfoAccumulator"/>.</summary>
#if NET
        private static System.Net.Security.TlsCipherSuite? Suite(int value) =>
            (System.Net.Security.TlsCipherSuite)value;
#else
        private static int? Suite(int value) => value;
#endif

        /// <summary>
        /// Arbitrary bytes standing in for a DER-encoded certificate; the interop layer does not parse them.
        /// </summary>
        private static byte[] Certificate(byte seed) =>
            Enumerable.Range(0, 600).Select(i => (byte)(seed + i)).ToArray();

        /// <summary>
        /// Emulates <c>SNIGetServerCertificateWrapper</c>: a buffer that is empty or too small yields
        /// <c>ERROR_INSUFFICIENT_BUFFER</c> with the required size; otherwise the certificate is copied.
        /// </summary>
        private sealed class FakeCertificateQuery
        {
            private readonly byte[]? _certificate;

            /// <param name="certificate">The certificate to report, or null to emulate no captured certificate.</param>
            internal FakeCertificateQuery(byte[]? certificate) => _certificate = certificate;

            internal List<int> BufferLengths { get; } = new List<int>();

            internal uint Query(SNIHandle? handle, Span<byte> buffer, out uint certificateSize)
            {
                BufferLengths.Add(buffer.Length);
                if (_certificate is null)
                {
                    certificateSize = 0;
                    return SniNativeCertificateCapture.ERROR_NOT_FOUND;
                }

                certificateSize = (uint)_certificate.Length;
                if (buffer.Length < _certificate.Length)
                {
                    return SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER;
                }

                _certificate.CopyTo(buffer);
                return SniNativeCertificateCapture.ERROR_SUCCESS;
            }
        }

        /// <summary>
        /// Builds a structure from raw bytes at the native contract's documented offsets, so the
        /// managed layout is validated against the contract rather than against itself.
        /// </summary>
        private static SniCertValidationInfo Build(
            uint available,
            uint mode,
            uint protocol,
            uint errors,
            uint chain,
            uint cipherSuite = 0)
        {
            byte[] raw = new byte[24];
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0), available);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), mode);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), protocol);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(12), errors);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(16), chain);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(20), cipherSuite);
            return MemoryMarshal.Read<SniCertValidationInfo>(raw);
        }

        #endregion
    }
}
