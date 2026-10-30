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
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests for the native SNI certificate-detail interop: the <see cref="SniCertValidationInfo"/>
    /// layout, reading the presented certificate, mapping of native availability and bitmasks to
    /// <see cref="ServerCertificateCapture"/>, and graceful degradation when the loaded SNI library
    /// does not export the functions.
    /// </summary>
    public class SniNativeCertificateCaptureTests
    {
        private const uint AllAvailable = 0xFF;
        private const uint CertificateAvailable = 0x4;
        private const uint CipherSuiteAvailable = 0x80;
        private const uint TlsClient12 = 0x00000800;
        private const uint TlsAes256GcmSha384 = 0x1302;

        #region Layout

        [Fact]
        public void Struct_IsThirtyTwoBytes()
        {
            Assert.Equal(32, Marshal.SizeOf<SniCertValidationInfo>());
            Assert.Equal(32, SniCertValidationInfo.Size);
            Assert.Equal(1u, SniCertValidationInfo.CurrentVersion);
        }

        [Theory]
        [InlineData(nameof(SniCertValidationInfo.cbSize), 0)]
        [InlineData(nameof(SniCertValidationInfo.dwVersion), 4)]
        [InlineData(nameof(SniCertValidationInfo.dwAvailable), 8)]
        [InlineData(nameof(SniCertValidationInfo.dwValidationMode), 12)]
        [InlineData(nameof(SniCertValidationInfo.dwProtocol), 16)]
        [InlineData(nameof(SniCertValidationInfo.dwValidationErrors), 20)]
        [InlineData(nameof(SniCertValidationInfo.dwChainStatus), 24)]
        [InlineData(nameof(SniCertValidationInfo.dwCipherSuite), 28)]
        public void Struct_FieldOffsets_MatchNativeContract(string field, int expectedOffset)
        {
            Assert.Equal(expectedOffset, Marshal.OffsetOf<SniCertValidationInfo>(field).ToInt32());
        }

        [Fact]
        public void Create_InitializesSize()
        {
            SniCertValidationInfo info = SniCertValidationInfo.Create();
            Assert.Equal(32u, info.cbSize);
            Assert.Equal(0u, info.dwAvailable);
            Assert.Equal(0u, info.dwCipherSuite);
        }

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

        [Fact]
        public void ValidationModes_MatchNativeContract()
        {
            Assert.Equal(0u, (uint)SniCertValidationMode.Unknown);
            Assert.Equal(1u, (uint)SniCertValidationMode.NotValidated);
            Assert.Equal(2u, (uint)SniCertValidationMode.SchannelAutomatic);
            Assert.Equal(3u, (uint)SniCertValidationMode.ChainPolicy);
            Assert.Equal(4u, (uint)SniCertValidationMode.PinnedCertificate);
        }

        [Fact]
        public void ValidationErrorBits_MatchManagedEnum()
        {
            Assert.Equal(1, (int)SqlServerCertificateValidationErrors.ServerCertificateMissing);
            Assert.Equal(2, (int)SqlServerCertificateValidationErrors.HostNameMismatch);
            Assert.Equal(4, (int)SqlServerCertificateValidationErrors.ChainError);
            Assert.Equal(8, (int)SqlServerCertificateValidationErrors.PinnedCertificateMismatch);
        }

        [Fact]
        public void Struct_ReadFromNativeBytes_RoundTripsFields()
        {
            SniCertValidationInfo info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);

            Assert.Equal(32u, info.cbSize);
            Assert.Equal(1u, info.dwVersion);
            Assert.Equal(AllAvailable, info.dwAvailable);
            Assert.Equal(3u, info.dwValidationMode);
            Assert.Equal(TlsClient12, info.dwProtocol);
            Assert.Equal(4u, info.dwValidationErrors);
            Assert.Equal(0x20u, info.dwChainStatus);
            Assert.Equal(0xC030u, info.dwCipherSuite);
        }

        #endregion

        #region Availability

        public static IEnumerable<object[]> AvailabilityCombinations() =>
            Enumerable.Range(0, 0x100).Select(i => new object[] { (uint)i });

        [Theory]
        [MemberData(nameof(AvailabilityCombinations))]
        public void Map_PopulatesOnlyAvailableFields(uint available)
        {
            byte[] certificate = Certificate(0x10);
            SniCertValidationInfo info = Build(available, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);

            ServerCertificateCapture? capture = SniNativeCertificateCapture.Map(info, certificate);

            bool hasCertificate = (available & 0x4) != 0;
            bool hasErrors = (available & 0x8) != 0;
            bool hasChain = (available & 0x10) != 0;
            bool hasProtocol = (available & 0x2) != 0;
            bool hasCipherSuite = (available & 0x80) != 0;

            if (!hasCertificate && !hasErrors && !hasChain && !hasProtocol && !hasCipherSuite)
            {
                // Only ATTEMPT_STARTED / HANDSHAKE_COMPLETED / LEAF_TIME_ONLY qualifiers: nothing to report.
                Assert.Null(capture);
                return;
            }

            Assert.NotNull(capture);
            Assert.Equal(hasCertificate ? certificate : null, capture!.RawCertificate);
            Assert.Equal(hasErrors ? SqlServerCertificateValidationErrors.ChainError : null, capture.ValidationErrors);
            Assert.Equal(hasChain ? X509ChainStatusFlags.UntrustedRoot : null, capture.ChainStatus);
            Assert.Equal(hasProtocol ? SslProtocols.Tls12 : null, capture.Protocol);
            Assert.Equal(hasCipherSuite ? 0xC030 : null, capture.CipherSuite);
        }

        [Fact]
        public void Map_CertificateFlagWithoutBytes_LeavesCertificateNull()
        {
            SniCertValidationInfo info = Build(available: CertificateAvailable | 0x8, mode: 3, protocol: 0, errors: 4, chain: 0);

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info, rawCertificate: null)!;

            Assert.Null(capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capture.ValidationErrors);
            Assert.Null(SniNativeCertificateCapture.Map(Build(available: CertificateAvailable, mode: 3, protocol: 0, errors: 0, chain: 0)));
        }

        [Fact]
        public void Map_ZeroBitmasksWithAvailability_AreReportedAsNoErrorNotNull()
        {
            SniCertValidationInfo info = Build(available: 0x8 | 0x10, mode: 3, protocol: 0, errors: 0, chain: 0);

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info)!;

            Assert.Equal(SqlServerCertificateValidationErrors.None, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, capture.ChainStatus);
            Assert.Null(capture.RawCertificate);
            Assert.Null(capture.Protocol);
        }

        [Fact]
        public void Map_ValidationFieldsWithoutAvailability_LeaveValidationFieldsNull()
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

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info, certificate)!;

            Assert.Null(capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
            Assert.Equal(certificate, capture.RawCertificate);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Theory]
        [InlineData((uint)SniCertValidationMode.SchannelAutomatic, 0x4u)]
        [InlineData((uint)SniCertValidationMode.ChainPolicy, 0x2u | 0x4u)]
        public void Map_DeferredValidationFailure_ReportsVerdictChainStatusAndCertificate(uint mode, uint errors)
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

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info, certificate)!;

            Assert.Equal((SqlServerCertificateValidationErrors)errors, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, capture.ChainStatus);
            Assert.Equal(certificate, capture.RawCertificate);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(1u)]
        [InlineData(2u)]
        [InlineData(3u)]
        [InlineData(4u)]
        [InlineData(99u)]
        public void Map_NoAvailability_IsNullForEveryValidationMode(uint mode)
        {
            SniCertValidationInfo info = Build(available: 0x1 | 0x20 | 0x40, mode: mode, protocol: TlsClient12, errors: 0xF, chain: 0xFFFF, cipherSuite: 0xC030);

            Assert.Null(SniNativeCertificateCapture.Map(info, Certificate(1)));
        }

        #endregion

        #region Validation errors and chain status

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
        public void Map_ValidationErrors_PreservesEveryBit(uint errors)
        {
            SniCertValidationInfo info = Build(available: 0x8, mode: 3, protocol: 0, errors: errors, chain: 0);

            Assert.Equal(unchecked((SqlServerCertificateValidationErrors)(int)errors), SniNativeCertificateCapture.Map(info)!.ValidationErrors);
        }

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

        [Theory]
        [MemberData(nameof(ChainStatusValues))]
        public void Map_ChainStatus_PreservesEveryBit(uint chain)
        {
            SniCertValidationInfo info = Build(available: 0x10, mode: 3, protocol: 0, errors: 0, chain: chain);

            X509ChainStatusFlags? mapped = SniNativeCertificateCapture.Map(info)!.ChainStatus;

            Assert.Equal(unchecked((X509ChainStatusFlags)(int)chain), mapped);
            Assert.Equal(chain, unchecked((uint)mapped!.Value));
        }

        [Fact]
        public void Map_ChainStatusLeafTimeOnly_StillReportsChainStatusWhenAvailable()
        {
            SniCertValidationInfo info = Build(available: 0x10 | 0x40, mode: (uint)SniCertValidationMode.PinnedCertificate, protocol: 0, errors: 0, chain: (uint)X509ChainStatusFlags.NotTimeValid);

            Assert.Equal(X509ChainStatusFlags.NotTimeValid, SniNativeCertificateCapture.Map(info)!.ChainStatus);
        }

        #endregion

        #region Protocol

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
        public void Map_Protocol_MapsClientBits(uint nativeProtocol, int expected)
        {
            SniCertValidationInfo info = Build(available: 0x2, mode: 0, protocol: nativeProtocol, errors: 0, chain: 0);

            SslProtocols? protocol = SniNativeCertificateCapture.Map(info)!.Protocol;

            Assert.Equal((SslProtocols)expected, protocol);
#if NET
            if (expected == 0x3000)
            {
                Assert.Equal(SslProtocols.Tls13, protocol);
            }
#endif
        }

        [Theory]
        [InlineData(0x0u)]
        [InlineData(0x00040000u)]
        public void Map_Protocol_UnmappedBitsAreUnavailable(uint nativeProtocol)
        {
            SniCertValidationInfo info = Build(available: 0x2, mode: 0, protocol: nativeProtocol, errors: 0, chain: 0);

            Assert.Null(SniNativeCertificateCapture.Map(info));
        }

        [Fact]
        public void ToSslProtocols_MatchesHandshakeMapping()
        {
            Assert.Equal(SslProtocols.Tls12, SniNativeWrapper.ToSslProtocols(SniSslProtocols.SP_PROT_TLS1_2_CLIENT));
            Assert.Equal((SslProtocols)0x3000, SniNativeWrapper.ToSslProtocols(SniSslProtocols.SP_PROT_TLS1_3_CLIENT));
            Assert.Equal(SslProtocols.None, SniNativeWrapper.ToSslProtocols(SniSslProtocols.SP_PROT_NONE));
        }

        #endregion

        #region Certificate size and structure validation

        [Theory]
        [InlineData(1)]
        [InlineData(ServerCertificateCapture.MaxRawCertificateSize)]
        public void Map_CertificateWithinLimit_IsKept(int length)
        {
            byte[] certificate = new byte[length];
            SniCertValidationInfo info = Build(available: CertificateAvailable, mode: 0, protocol: 0, errors: 0, chain: 0);

            Assert.Same(certificate, SniNativeCertificateCapture.Map(info, certificate)!.RawCertificate);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(ServerCertificateCapture.MaxRawCertificateSize + 1)]
        public void Map_CertificateOutsideLimit_IsUnavailable(int length)
        {
            SniCertValidationInfo info = Build(available: CertificateAvailable, mode: 0, protocol: 0, errors: 0, chain: 0);

            Assert.Null(SniNativeCertificateCapture.Map(info, new byte[length]));
        }

        [Theory]
        [InlineData(0u, 1u)]
        [InlineData(16u, 1u)]
        [InlineData(31u, 1u)]
        [InlineData(32u, 0u)]
        public void Map_MalformedHeader_IsUnavailable(uint cbSize, uint version)
        {
            SniCertValidationInfo info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cbSize: cbSize, version: version, cipherSuite: 0xC030);

            Assert.Null(SniNativeCertificateCapture.Map(info, Certificate(9)));
        }

        [Theory]
        [InlineData(32u, 2u)]
        [InlineData(128u, 7u)]
        public void Map_NewerVersionOrLargerSize_IsAccepted(uint cbSize, uint version)
        {
            SniCertValidationInfo info = Build(available: 0x8, mode: 3, protocol: 0, errors: 2, chain: 0, cbSize: cbSize, version: version);

            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch, SniNativeCertificateCapture.Map(info)!.ValidationErrors);
        }

        #endregion

        #region Cipher suite

        [Fact]
        public void Map_CipherSuiteFlag_ReportsCipherSuite()
        {
            SniCertValidationInfo info = Build(available: 0x2 | CipherSuiteAvailable, mode: 2, protocol: TlsClient12, errors: 0, chain: 0, cipherSuite: TlsAes256GcmSha384);

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info)!;

            Assert.Equal((int)TlsAes256GcmSha384, capture.CipherSuite);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Fact]
        public void Map_CipherSuiteOnly_IsNotDiscarded()
        {
            SniCertValidationInfo info = Build(available: CipherSuiteAvailable, mode: 0, protocol: 0, errors: 0, chain: 0, cipherSuite: 0xC030);

            Assert.Equal(0xC030, SniNativeCertificateCapture.Map(info)!.CipherSuite);
        }

        [Fact]
        public void Map_WithoutCipherSuiteFlag_LeavesCipherSuiteNull()
        {
            SniCertValidationInfo info = Build(available: AllAvailable & ~CipherSuiteAvailable, mode: 3, protocol: TlsClient12, errors: 0, chain: 0, cipherSuite: TlsAes256GcmSha384);

            Assert.Null(SniNativeCertificateCapture.Map(info, Certificate(2))!.CipherSuite);
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(0x10000u)]
        [InlineData(uint.MaxValue)]
        public void Map_CipherSuiteOutOfRange_LeavesCipherSuiteNull(uint value)
        {
            SniCertValidationInfo info = Build(available: 0x8 | CipherSuiteAvailable, mode: 3, protocol: 0, errors: 0, chain: 0, cipherSuite: value);

            Assert.Null(SniNativeCertificateCapture.Map(info)!.CipherSuite);
        }

        [Fact]
        public void Map_MaximumCipherSuite_IsReported()
        {
            SniCertValidationInfo info = Build(available: CipherSuiteAvailable, mode: 0, protocol: 0, errors: 0, chain: 0, cipherSuite: 0xFFFF);

            Assert.Equal(0xFFFF, SniNativeCertificateCapture.Map(info)!.CipherSuite);
        }

        [Fact]
        public void EncryptionInfoCapture_DeferredValidationFailure_KeepsCipherSuite()
        {
            // Native deferred validation: the handshake completes, so the capture taken after EnableSsl
            // carries the cipher suite. The verdict then fails the LOGIN7 write; the observed cipher
            // suite survives the failure-path rule together with the protocol and certificate fields.
            ServerCertificateCapture? certificate = SniNativeCertificateCapture.Map(
                Build(
                    0x1 | 0x2 | 0x4 | 0x8 | 0x10 | CipherSuiteAvailable,
                    mode: 2,
                    protocol: TlsClient12,
                    errors: (uint)SqlServerCertificateValidationErrors.ChainError,
                    chain: (uint)X509ChainStatusFlags.UntrustedRoot,
                    cipherSuite: 0xC030),
                Certificate(8));

            var capture = new EncryptionInfoCapture();
            capture.Reset();
            capture.RecordServerCertificate(certificate);
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(0xC030, snapshot.NegotiatedCipherSuiteId);
#if NET
            Assert.Equal(
                System.Net.Security.TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                snapshot.NegotiatedCipherSuite);
#endif
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
            Assert.Equal(Certificate(8), snapshot.PresentedServerCertificateRawData!.Value.ToArray());
        }

        [Fact]
        public void EncryptionInfoCapture_ResetClearsNativeCipherSuite()
        {
            var capture = new EncryptionInfoCapture();
            capture.RecordServerCertificate(new ServerCertificateCapture(null, null, null, SslProtocols.Tls12, 0xC030));
            capture.Reset();

            Assert.Null(capture.ToSnapshot().NegotiatedCipherSuiteId);
            Assert.False(capture.HasValues);
        }

        #endregion

        #region Query, error codes, and degradation

        [Fact]
        public void TryCapture_Success_PassesInitializedStructAndReadsCertificate()
        {
            byte[] certificate = Certificate(0x44);
            uint seenSize = 0;
            var certificateQuery = new FakeCertificateQuery(certificate);
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    seenSize = info.cbSize;
                    info = Build(available: 0x4 | 0x8 | 0x10 | CipherSuiteAvailable, mode: 3, protocol: 0, errors: 0, chain: 0, cipherSuite: 0xC02F);
                    return 0;
                },
                certificateQuery.Query);

            ServerCertificateCapture result = capture.TryCapture(null)!;

            Assert.Equal(32u, seenSize);
            Assert.Equal(certificate, result.RawCertificate);
            Assert.Equal(new[] { 0, certificate.Length }, certificateQuery.BufferLengths);
            Assert.Equal(SqlServerCertificateValidationErrors.None, result.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, result.ChainStatus);
            Assert.Equal(0xC02F, result.CipherSuite);
        }

        [Fact]
        public void TryCapture_CertificateNotAvailable_DoesNotReadCertificate()
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x2 | 0x8, mode: 2, protocol: TlsClient12, errors: 0, chain: 0);
                    return 0;
                },
                ThrowingCertificateQuery);

            ServerCertificateCapture result = capture.TryCapture(null)!;

            Assert.Null(result.RawCertificate);
            Assert.Equal(SslProtocols.Tls12, result.Protocol);
        }

        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INVALID_PARAMETER)] // Includes earlier preview libraries that reject cbSize = 32.
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER)]
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND)]
        [InlineData(5u)]
        public void TryCapture_ValidationInfoErrorCode_LeavesFieldsNull(uint errorCode)
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    // Even if native wrote data, a failure code means nothing is trusted.
                    info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cipherSuite: 0xC030);
                    return errorCode;
                },
                ThrowingCertificateQuery);

            Assert.Null(capture.TryCapture(null));
            Assert.False(capture.IsValidationInfoUnavailable);
        }

        [Fact]
        public void TryCapture_MalformedValidationInfo_DoesNotReadCertificate()
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, version: 0);
                    return 0;
                },
                ThrowingCertificateQuery);

            Assert.Null(capture.TryCapture(null));
        }

        [Fact]
        public void TryCapture_MissingValidationInfoExport_DegradesOnceWithoutCertificateFallback()
        {
            int infoCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    infoCalls++;
                    throw new EntryPointNotFoundException("SNIGetCertValidationInfoWrapper");
                },
                ThrowingCertificateQuery);

            for (int i = 0; i < 3; i++)
            {
                Assert.Null(capture.TryCapture(null));
            }

            Assert.True(capture.IsValidationInfoUnavailable);
            Assert.False(capture.IsCertificateUnavailable);
            Assert.Equal(1, infoCalls);
        }

        [Fact]
        public void TryCapture_MissingCertificateExport_KeepsOtherFieldsAndDegradesOnce()
        {
            int certificateCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    info = Build(available: 0x2 | 0x4 | 0x8 | CipherSuiteAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0, cipherSuite: 0xC030);
                    return 0;
                },
                (SNIHandle? _, Span<byte> buffer, out uint size) =>
                {
                    certificateCalls++;
                    throw new EntryPointNotFoundException("SNIGetServerCertificateWrapper");
                });

            ServerCertificateCapture first = capture.TryCapture(null)!;
            ServerCertificateCapture second = capture.TryCapture(null)!;

            Assert.True(capture.IsCertificateUnavailable);
            Assert.False(capture.IsValidationInfoUnavailable);
            Assert.Equal(1, certificateCalls);
            foreach (ServerCertificateCapture result in new[] { first, second })
            {
                Assert.Null(result.RawCertificate);
                Assert.Equal(SqlServerCertificateValidationErrors.ChainError, result.ValidationErrors);
                Assert.Equal(SslProtocols.Tls12, result.Protocol);
                Assert.Equal(0xC030, result.CipherSuite);
            }
        }

        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INVALID_PARAMETER, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_SUCCESS, 100u)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, (uint)ServerCertificateCapture.MaxRawCertificateSize + 1)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, uint.MaxValue)]
        public void TryCapture_UnusableCertificateSize_LeavesCertificateNullAndSkipsRead(uint result, uint size)
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

            ServerCertificateCapture capturedDetails = capture.TryCapture(null)!;

            Assert.Null(capturedDetails.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capturedDetails.ValidationErrors);
            Assert.Equal(new[] { 0 }, reads);
            Assert.False(capture.IsCertificateUnavailable);
        }

        [Fact]
        public void TryCapture_MaximumCertificateSize_IsRead()
        {
            byte[] certificate = new byte[ServerCertificateCapture.MaxRawCertificateSize];
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

            Assert.Equal(certificate, capture.TryCapture(null)!.RawCertificate);
        }

        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, 200u)] // A new attempt replaced the certificate.
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_SUCCESS, 50u)]
        public void TryCapture_CertificateReadFailure_LeavesCertificateNull(uint result, uint written)
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

            ServerCertificateCapture capturedDetails = capture.TryCapture(null)!;

            Assert.Equal(2, calls);
            Assert.Null(capturedDetails.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capturedDetails.ValidationErrors);
        }

        public static IEnumerable<object[]> CaptureExceptions() => new[]
        {
            new object[] { typeof(InvalidOperationException) },
            new object[] { typeof(AccessViolationException) },
            new object[] { typeof(NullReferenceException) },
            new object[] { typeof(ObjectDisposedException) },
            new object[] { typeof(DllNotFoundException) },
            new object[] { typeof(PlatformNotSupportedException) },
            new object[] { typeof(SEHException) },
            new object[] { typeof(OutOfMemoryException) },
        };

        private static Exception CreateException(Type exceptionType) =>
            exceptionType == typeof(ObjectDisposedException)
                ? new ObjectDisposedException("handle")
                : (Exception)Activator.CreateInstance(exceptionType)!;

        [Theory]
        [MemberData(nameof(CaptureExceptions))]
        public void TryCapture_QueryThrows_IsSwallowedAndNotCached(Type exceptionType)
        {
            Exception exception = CreateException(exceptionType);
            int infoCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    infoCalls++;
                    throw exception;
                },
                ThrowingCertificateQuery);

            Assert.Null(capture.TryCapture(null));
            Assert.Null(capture.TryCapture(null));
            Assert.Equal(2, infoCalls);
            Assert.False(capture.IsValidationInfoUnavailable);
        }

        [Theory]
        [MemberData(nameof(CaptureExceptions))]
        public void TryCapture_CertificateQueryThrows_KeepsOtherFieldsAndIsNotCached(Type exceptionType)
        {
            Exception exception = CreateException(exceptionType);
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
                    throw exception;
                });

            ServerCertificateCapture first = capture.TryCapture(null)!;
            ServerCertificateCapture second = capture.TryCapture(null)!;

            Assert.Null(first.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, first.ValidationErrors);
            Assert.Null(second.RawCertificate);
            Assert.Equal(2, certificateCalls);
            Assert.False(capture.IsCertificateUnavailable);
        }

        [Fact]
        public void Default_WithoutUsableHandle_NeverThrows()
        {
            // Exercises the real P/Invoke binding: older SNI libraries lack the export, non-Windows
            // platforms lack the library, and a null handle is rejected; all must degrade to null.
            Assert.Null(SniNativeCertificateCapture.Default.TryCapture(null));
            Assert.Null(SniNativeCertificateCapture.Default.TryCapture(null));
        }

        #endregion

        #region Capture context integration

        [Fact]
        public void EncryptionInfoCapture_TransportProtocol_UsedWhenNotOtherwiseRecorded()
        {
            var capture = new EncryptionInfoCapture();
            capture.Reset();

            capture.RecordServerCertificate(new ServerCertificateCapture(Certificate(1), SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot, SslProtocols.Tls12));

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, snapshot.ChainStatus);
        }

        [Fact]
        public void EncryptionInfoCapture_HandshakeProtocol_TakesPrecedence()
        {
            var capture = new EncryptionInfoCapture();
            capture.RecordServerCertificate(new ServerCertificateCapture(null, null, null, (SslProtocols)0x3000));
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.Tls12);

            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Fact]
        public void EncryptionInfoCapture_UnreportedHandshakeProtocol_KeepsTransportProtocol()
        {
            var capture = new EncryptionInfoCapture();
            capture.RecordServerCertificate(new ServerCertificateCapture(null, null, null, SslProtocols.Tls12));
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);

            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Fact]
        public void EncryptionInfoCapture_TransportProtocol_DoesNotOverrideRecordedProtocol()
        {
            var capture = new EncryptionInfoCapture();
            capture.Record(SqlConnectionEncryptionMode.Strict, SslProtocols.Tls12);
            capture.RecordServerCertificate(new ServerCertificateCapture(null, null, null, (SslProtocols)0x3000));

            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Fact]
        public void EncryptionInfoCapture_DeferredValidationFailure_KeepsObservedNegotiationAndCertificateFields()
        {
            // Native ChainPolicy failure: EnableSsl and the handshake wait report success, so the
            // certificate details and the negotiated values are recorded; the verdict then surfaces as
            // an SSL provider error on the LOGIN7 write, and ProcessSNIError applies the failure-path
            // rule. The protocol was observed for this attempt, so mode and protocol are kept.
            byte[] certificate = Certificate(7);
            const uint available = 0x1 | 0x2 | 0x4 | 0x8 | 0x10;
            ServerCertificateCapture? details = SniNativeCertificateCapture.Map(
                Build(
                    available,
                    mode: 3,
                    protocol: 0x800,
                    errors: (uint)(SqlServerCertificateValidationErrors.HostNameMismatch | SqlServerCertificateValidationErrors.ChainError),
                    chain: (uint)X509ChainStatusFlags.UntrustedRoot),
                certificate);

            var capture = new EncryptionInfoCapture();
            capture.Reset();
            capture.RecordServerCertificate(details);
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(certificate, snapshot.PresentedServerCertificateRawData!.Value.ToArray());
            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch | SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, snapshot.ChainStatus);
        }

        [Fact]
        public void EncryptionInfoCapture_FailureWithoutObservedProtocol_DiscardsMode()
        {
            byte[] certificate = Certificate(9);
            var capture = new EncryptionInfoCapture();
            capture.RecordServerCertificate(new ServerCertificateCapture(certificate, SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(certificate, snapshot.PresentedServerCertificateRawData!.Value.ToArray());
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
        }

        [Fact]
        public void EncryptionInfoCapture_RecordModeIfProtocolObserved_RequiresProtocol()
        {
            var withoutProtocol = new EncryptionInfoCapture();
            withoutProtocol.RecordServerCertificate(new ServerCertificateCapture(Certificate(1), SqlServerCertificateValidationErrors.ChainError, null));
            withoutProtocol.RecordModeIfProtocolObserved(SqlConnectionEncryptionMode.Full);
            Assert.Null(withoutProtocol.Mode);

            var withProtocol = new EncryptionInfoCapture();
            withProtocol.RecordServerCertificate(new ServerCertificateCapture(Certificate(1), SqlServerCertificateValidationErrors.ChainError, null, SslProtocols.Tls12));
            withProtocol.RecordModeIfProtocolObserved(SqlConnectionEncryptionMode.Strict);
            Assert.Equal(SqlConnectionEncryptionMode.Strict, withProtocol.Mode);
            Assert.Equal(SslProtocols.Tls12, withProtocol.Protocol);
        }

        [Fact]
        public void EncryptionInfoCapture_ResetClearsTransportProtocol()
        {
            var capture = new EncryptionInfoCapture();
            capture.RecordServerCertificate(new ServerCertificateCapture(null, null, null, SslProtocols.Tls12));
            capture.Reset();

            Assert.Null(capture.Protocol);
            Assert.False(capture.HasValues);
        }

        #endregion

        #region Helpers

        private static uint ThrowingCertificateQuery(SNIHandle? handle, Span<byte> certificate, out uint certificateSize) =>
            throw new InvalidOperationException("Certificate query should not be called.");

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
            private readonly byte[] _certificate;

            internal FakeCertificateQuery(byte[] certificate) => _certificate = certificate;

            internal List<int> BufferLengths { get; } = new List<int>();

            internal uint Query(SNIHandle? handle, Span<byte> buffer, out uint certificateSize)
            {
                BufferLengths.Add(buffer.Length);
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
            uint cbSize = 32,
            uint version = 1,
            uint cipherSuite = 0)
        {
            byte[] raw = new byte[32];
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0), cbSize);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), version);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), available);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(12), mode);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(16), protocol);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(20), errors);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(24), chain);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(28), cipherSuite);
            return MemoryMarshal.Read<SniCertValidationInfo>(raw);
        }

        #endregion
    }
}
