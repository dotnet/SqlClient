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
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests for the native SNI certificate-detail interop: the <see cref="SniCertValidationInfo"/>
    /// layout, mapping of native availability and bitmasks to <see cref="ServerCertificateCapture"/>,
    /// and graceful degradation when the loaded SNI library does not export the functions.
    /// </summary>
    public class SniNativeCertificateCaptureTests
    {
        private const uint AllAvailable = 0x7F;
        private const uint TlsClient12 = 0x00000800;
        private const uint TlsClient13 = 0x00002000;

        #region Layout

        [Fact]
        public void Struct_IsSixtyFourBytes()
        {
            Assert.Equal(64, Marshal.SizeOf<SniCertValidationInfo>());
            Assert.Equal(64, SniCertValidationInfo.Size);
            Assert.Equal(32, SniCertValidationInfo.HashLength);
        }

        [Theory]
        [InlineData(nameof(SniCertValidationInfo.cbSize), 0)]
        [InlineData(nameof(SniCertValidationInfo.dwVersion), 4)]
        [InlineData(nameof(SniCertValidationInfo.dwAvailable), 8)]
        [InlineData(nameof(SniCertValidationInfo.dwValidationMode), 12)]
        [InlineData(nameof(SniCertValidationInfo.dwProtocol), 16)]
        [InlineData(nameof(SniCertValidationInfo.dwValidationErrors), 20)]
        [InlineData(nameof(SniCertValidationInfo.dwChainStatus), 24)]
        [InlineData(nameof(SniCertValidationInfo.cbHash), 28)]
        [InlineData(nameof(SniCertValidationInfo.rgbHash), 32)]
        public void Struct_FieldOffsets_MatchNativeContract(string field, int expectedOffset)
        {
            Assert.Equal(expectedOffset, Marshal.OffsetOf<SniCertValidationInfo>(field).ToInt32());
        }

        [Fact]
        public void Create_InitializesSize()
        {
            SniCertValidationInfo info = SniCertValidationInfo.Create();
            Assert.Equal(64u, info.cbSize);
            Assert.Equal(0u, info.dwAvailable);
        }

        [Fact]
        public void AvailabilityFlags_MatchNativeContract()
        {
            Assert.Equal(0x1u, (uint)SniCertInfoAvailability.AttemptStarted);
            Assert.Equal(0x2u, (uint)SniCertInfoAvailability.Protocol);
            Assert.Equal(0x4u, (uint)SniCertInfoAvailability.Hash);
            Assert.Equal(0x8u, (uint)SniCertInfoAvailability.ValidationErrors);
            Assert.Equal(0x10u, (uint)SniCertInfoAvailability.ChainStatus);
            Assert.Equal(0x20u, (uint)SniCertInfoAvailability.HandshakeCompleted);
            Assert.Equal(0x40u, (uint)SniCertInfoAvailability.ChainStatusLeafTimeOnly);
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
            byte[] hash = Hash(0xA0);
            SniCertValidationInfo info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cbHash: 32, hash: hash);

            Assert.Equal(64u, info.cbSize);
            Assert.Equal(1u, info.dwVersion);
            Assert.Equal(AllAvailable, info.dwAvailable);
            Assert.Equal(3u, info.dwValidationMode);
            Assert.Equal(TlsClient12, info.dwProtocol);
            Assert.Equal(4u, info.dwValidationErrors);
            Assert.Equal(0x20u, info.dwChainStatus);
            Assert.Equal(32u, info.cbHash);
            Assert.Equal(hash, SniNativeCertificateCapture.Map(info)!.Hash256);
        }

        #endregion

        #region Availability

        public static IEnumerable<object[]> AvailabilityCombinations() =>
            Enumerable.Range(0, 0x80).Select(i => new object[] { (uint)i });

        [Theory]
        [MemberData(nameof(AvailabilityCombinations))]
        public void Map_PopulatesOnlyAvailableFields(uint available)
        {
            byte[] hash = Hash(0x10);
            SniCertValidationInfo info = Build(available, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cbHash: 32, hash: hash);

            ServerCertificateCapture? capture = SniNativeCertificateCapture.Map(info);

            bool hasHash = (available & 0x4) != 0;
            bool hasErrors = (available & 0x8) != 0;
            bool hasChain = (available & 0x10) != 0;
            bool hasProtocol = (available & 0x2) != 0;

            if (!hasHash && !hasErrors && !hasChain && !hasProtocol)
            {
                // Only ATTEMPT_STARTED / HANDSHAKE_COMPLETED / LEAF_TIME_ONLY qualifiers: nothing to report.
                Assert.Null(capture);
                return;
            }

            Assert.NotNull(capture);
            Assert.Equal(hasHash ? hash : null, capture!.Hash256);
            Assert.Equal(hasErrors ? SqlServerCertificateValidationErrors.ChainError : null, capture.ValidationErrors);
            Assert.Equal(hasChain ? X509ChainStatusFlags.UntrustedRoot : null, capture.ChainStatus);
            Assert.Equal(hasProtocol ? SslProtocols.Tls12 : null, capture.Protocol);
        }

        [Fact]
        public void Map_ZeroBitmasksWithAvailability_AreReportedAsNoErrorNotNull()
        {
            SniCertValidationInfo info = Build(available: 0x8 | 0x10, mode: 3, protocol: 0, errors: 0, chain: 0, cbHash: 0, hash: null);

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info)!;

            Assert.Equal(SqlServerCertificateValidationErrors.None, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, capture.ChainStatus);
            Assert.Null(capture.Hash256);
            Assert.Null(capture.Protocol);
        }

        [Fact]
        public void Map_ValidationFieldsWithoutAvailability_LeaveValidationFieldsNull()
        {
            // An older sni.dll may report a SchannelAutomatic attempt without VALIDATION_ERRORS or
            // CHAIN_STATUS availability. Stale or garbage bitmask values must not leak through.
            byte[] hash = Hash(0x33);
            SniCertValidationInfo info = Build(
                available: 0x1 | 0x2 | 0x4,
                mode: (uint)SniCertValidationMode.SchannelAutomatic,
                protocol: TlsClient12,
                errors: 0x4,
                chain: 0x20,
                cbHash: 32,
                hash: hash);

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info)!;

            Assert.Null(capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
            Assert.Equal(hash, capture.Hash256);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);
        }

        [Theory]
        [InlineData((uint)SniCertValidationMode.SchannelAutomatic, 0x4u)]
        [InlineData((uint)SniCertValidationMode.ChainPolicy, 0x2u | 0x4u)]
        public void Map_DeferredValidationFailure_ReportsVerdictChainStatusAndHash(uint mode, uint errors)
        {
            // SNI with deferred validation reports the verdict, chain status, and hash in every
            // mode, and reports HostNameMismatch together with ChainError, matching Managed SNI.
            byte[] hash = Hash(0x44);
            SniCertValidationInfo info = Build(
                available: 0x1 | 0x2 | 0x4 | 0x8 | 0x10,
                mode: mode,
                protocol: TlsClient12,
                errors: errors,
                chain: (uint)X509ChainStatusFlags.UntrustedRoot,
                cbHash: 32,
                hash: hash);

            ServerCertificateCapture capture = SniNativeCertificateCapture.Map(info)!;

            Assert.Equal((SqlServerCertificateValidationErrors)errors, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, capture.ChainStatus);
            Assert.Equal(hash, capture.Hash256);
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
            SniCertValidationInfo info = Build(available: 0x1 | 0x20 | 0x40, mode: mode, protocol: TlsClient12, errors: 0xF, chain: 0xFFFF, cbHash: 32, hash: Hash(1));

            Assert.Null(SniNativeCertificateCapture.Map(info));
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
            SniCertValidationInfo info = Build(available: 0x8, mode: 3, protocol: 0, errors: errors, chain: 0, cbHash: 0, hash: null);

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
            SniCertValidationInfo info = Build(available: 0x10, mode: 3, protocol: 0, errors: 0, chain: chain, cbHash: 0, hash: null);

            X509ChainStatusFlags? mapped = SniNativeCertificateCapture.Map(info)!.ChainStatus;

            Assert.Equal(unchecked((X509ChainStatusFlags)(int)chain), mapped);
            Assert.Equal(chain, unchecked((uint)mapped!.Value));
        }

        [Fact]
        public void Map_ChainStatusLeafTimeOnly_StillReportsChainStatusWhenAvailable()
        {
            SniCertValidationInfo info = Build(available: 0x10 | 0x40, mode: (uint)SniCertValidationMode.PinnedCertificate, protocol: 0, errors: 0, chain: (uint)X509ChainStatusFlags.NotTimeValid, cbHash: 0, hash: null);

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
            SniCertValidationInfo info = Build(available: 0x2, mode: 0, protocol: nativeProtocol, errors: 0, chain: 0, cbHash: 0, hash: null);

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
            SniCertValidationInfo info = Build(available: 0x2, mode: 0, protocol: nativeProtocol, errors: 0, chain: 0, cbHash: 0, hash: null);

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

        #region Hash and structure validation

        [Fact]
        public void Map_Hash_IsIndependentCopyOfThirtyTwoBytes()
        {
            byte[] hash = Hash(0x80);
            SniCertValidationInfo info = Build(available: 0x4, mode: 0, protocol: 0, errors: 0, chain: 0, cbHash: 32, hash: hash);

            byte[] first = SniNativeCertificateCapture.Map(info)!.Hash256!;
            byte[] second = SniNativeCertificateCapture.Map(info)!.Hash256!;

            Assert.Equal(32, first.Length);
            Assert.Equal(hash, first);
            Assert.NotSame(first, second);
            first[0] ^= 0xFF;
            Assert.Equal(hash, second);
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(16u)]
        [InlineData(31u)]
        [InlineData(33u)]
        public void Map_HashWithUnexpectedLength_IsUnavailable(uint cbHash)
        {
            SniCertValidationInfo info = Build(available: 0x4, mode: 0, protocol: 0, errors: 0, chain: 0, cbHash: cbHash, hash: Hash(7));

            Assert.Null(SniNativeCertificateCapture.Map(info));
        }

        [Theory]
        [InlineData(0u, 1u)]
        [InlineData(32u, 1u)]
        [InlineData(63u, 1u)]
        [InlineData(64u, 0u)]
        public void Map_MalformedHeader_IsUnavailable(uint cbSize, uint version)
        {
            SniCertValidationInfo info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cbHash: 32, hash: Hash(9), cbSize: cbSize, version: version);

            Assert.Null(SniNativeCertificateCapture.Map(info));
        }

        [Theory]
        [InlineData(64u, 2u)]
        [InlineData(128u, 7u)]
        public void Map_NewerVersionOrLargerSize_IsAccepted(uint cbSize, uint version)
        {
            SniCertValidationInfo info = Build(available: 0x8, mode: 3, protocol: 0, errors: 2, chain: 0, cbHash: 0, hash: null, cbSize: cbSize, version: version);

            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch, SniNativeCertificateCapture.Map(info)!.ValidationErrors);
        }

        #endregion

        #region Query, error codes, and degradation

        [Fact]
        public void TryCapture_Success_PassesInitializedStructAndMaps()
        {
            byte[] hash = Hash(0x44);
            uint seenSize = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    seenSize = info.cbSize;
                    info = Build(available: 0x4 | 0x8 | 0x10, mode: 3, protocol: 0, errors: 0, chain: 0, cbHash: 32, hash: hash);
                    return 0;
                },
                ThrowingHashQuery);

            ServerCertificateCapture result = capture.TryCapture(null)!;

            Assert.Equal(64u, seenSize);
            Assert.Equal(hash, result.Hash256);
            Assert.Equal(SqlServerCertificateValidationErrors.None, result.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, result.ChainStatus);
        }

        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INVALID_PARAMETER)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER)]
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND)]
        [InlineData(5u)]
        public void TryCapture_ValidationInfoErrorCode_LeavesFieldsNull(uint errorCode)
        {
            int hashCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    // Even if native wrote data, a failure code means nothing is trusted.
                    info = Build(available: AllAvailable, mode: 3, protocol: TlsClient12, errors: 4, chain: 0x20, cbHash: 32, hash: Hash(1));
                    return errorCode;
                },
                (SNIHandle? _, Span<byte> hash, out uint written) =>
                {
                    hashCalls++;
                    written = 0;
                    return 0;
                });

            Assert.Null(capture.TryCapture(null));
            Assert.False(capture.IsValidationInfoUnavailable);
            Assert.Equal(0, hashCalls);
        }

        [Fact]
        public void TryCapture_MissingValidationInfoExport_DegradesOnceAndFallsBackToHash()
        {
            byte[] hash = Hash(0x55);
            int infoCalls = 0;
            int hashCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    infoCalls++;
                    throw new EntryPointNotFoundException("SNIGetCertValidationInfoWrapper");
                },
                (SNIHandle? _, Span<byte> buffer, out uint written) =>
                {
                    hashCalls++;
                    Assert.Equal(32, buffer.Length);
                    hash.CopyTo(buffer);
                    written = 32;
                    return 0;
                });

            ServerCertificateCapture first = capture.TryCapture(null)!;
            ServerCertificateCapture second = capture.TryCapture(null)!;

            Assert.True(capture.IsValidationInfoUnavailable);
            Assert.Equal(1, infoCalls);
            Assert.Equal(2, hashCalls);
            Assert.Equal(hash, first.Hash256);
            Assert.Null(first.ValidationErrors);
            Assert.Null(first.ChainStatus);
            Assert.Null(first.Protocol);
            Assert.Equal(hash, second.Hash256);
        }

        [Fact]
        public void TryCapture_BothExportsMissing_ReturnsNullAndStopsQuerying()
        {
            int infoCalls = 0;
            int hashCalls = 0;
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) =>
                {
                    infoCalls++;
                    throw new EntryPointNotFoundException();
                },
                (SNIHandle? _, Span<byte> buffer, out uint written) =>
                {
                    hashCalls++;
                    throw new EntryPointNotFoundException();
                });

            for (int i = 0; i < 3; i++)
            {
                Assert.Null(capture.TryCapture(null));
            }

            Assert.True(capture.IsValidationInfoUnavailable);
            Assert.True(capture.IsHashUnavailable);
            Assert.Equal(1, infoCalls);
            Assert.Equal(1, hashCalls);
        }

        [Theory]
        [InlineData(SniNativeCertificateCapture.ERROR_INVALID_PARAMETER, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_NOT_FOUND, 0u)]
        [InlineData(SniNativeCertificateCapture.ERROR_INSUFFICIENT_BUFFER, 32u)]
        [InlineData(0u, 16u)]
        public void TryCapture_HashFallbackErrorCode_LeavesFieldsNull(uint errorCode, uint written)
        {
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) => throw new EntryPointNotFoundException(),
                (SNIHandle? _, Span<byte> buffer, out uint bytesWritten) =>
                {
                    buffer.Fill(0xAB);
                    bytesWritten = written;
                    return errorCode;
                });

            Assert.Null(capture.TryCapture(null));
            Assert.False(capture.IsHashUnavailable);
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
                ThrowingHashQuery);

            Assert.Null(capture.TryCapture(null));
            Assert.Null(capture.TryCapture(null));
            Assert.Equal(2, infoCalls);
            Assert.False(capture.IsValidationInfoUnavailable);
        }

        [Theory]
        [MemberData(nameof(CaptureExceptions))]
        public void TryCapture_HashFallbackThrows_IsSwallowed(Type exceptionType)
        {
            Exception exception = CreateException(exceptionType);
            var capture = new SniNativeCertificateCapture(
                (SNIHandle? _, ref SniCertValidationInfo info) => throw new EntryPointNotFoundException(),
                (SNIHandle? _, Span<byte> buffer, out uint written) => throw exception);

            Assert.Null(capture.TryCapture(null));
            Assert.False(capture.IsHashUnavailable);
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

            capture.RecordServerCertificate(new ServerCertificateCapture(Hash(1), SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot, SslProtocols.Tls12));

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
            byte[] hash = Hash(7);
            const uint available = 0x1 | 0x2 | 0x4 | 0x8 | 0x10;
            ServerCertificateCapture? certificate = SniNativeCertificateCapture.Map(Build(
                available,
                mode: 3,
                protocol: 0x800,
                errors: (uint)(SqlServerCertificateValidationErrors.HostNameMismatch | SqlServerCertificateValidationErrors.ChainError),
                chain: (uint)X509ChainStatusFlags.UntrustedRoot,
                cbHash: 32,
                hash: hash));

            var capture = new EncryptionInfoCapture();
            capture.Reset();
            capture.RecordServerCertificate(certificate);
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);
            Assert.Equal(SslProtocols.Tls12, capture.Protocol);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(SqlConnectionEncryptionMode.Full, snapshot.NegotiatedEncryptionMode);
            Assert.Equal(SslProtocols.Tls12, snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(hash, snapshot.PresentedServerCertificateHash256!.Value.ToArray());
            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch | SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, snapshot.ChainStatus);
        }

        [Fact]
        public void EncryptionInfoCapture_FailureWithoutObservedProtocol_DiscardsMode()
        {
            byte[] hash = Hash(9);
            var capture = new EncryptionInfoCapture();
            capture.RecordServerCertificate(new ServerCertificateCapture(hash, SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));
            capture.Record(SqlConnectionEncryptionMode.Full, SslProtocols.None);

            capture.DiscardUnobservedNegotiation();

            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(hash, snapshot.PresentedServerCertificateHash256!.Value.ToArray());
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
        }

        [Fact]
        public void EncryptionInfoCapture_RecordModeIfProtocolObserved_RequiresProtocol()
        {
            var withoutProtocol = new EncryptionInfoCapture();
            withoutProtocol.RecordServerCertificate(new ServerCertificateCapture(Hash(1), SqlServerCertificateValidationErrors.ChainError, null));
            withoutProtocol.RecordModeIfProtocolObserved(SqlConnectionEncryptionMode.Full);
            Assert.Null(withoutProtocol.Mode);

            var withProtocol = new EncryptionInfoCapture();
            withProtocol.RecordServerCertificate(new ServerCertificateCapture(Hash(1), SqlServerCertificateValidationErrors.ChainError, null, SslProtocols.Tls12));
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

        private static uint ThrowingHashQuery(SNIHandle? handle, Span<byte> hash, out uint written) =>
            throw new InvalidOperationException("Hash query should not be called.");

        private static byte[] Hash(byte seed) =>
            Enumerable.Range(0, 32).Select(i => (byte)(seed + i)).ToArray();

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
            uint cbHash,
            byte[]? hash,
            uint cbSize = 64,
            uint version = 1)
        {
            byte[] raw = new byte[64];
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(0), cbSize);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(4), version);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(8), available);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(12), mode);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(16), protocol);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(20), errors);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(24), chain);
            BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(28), cbHash);
            hash?.AsSpan().CopyTo(raw.AsSpan(32));
            return MemoryMarshal.Read<SniCertValidationInfo>(raw);
        }

        #endregion
    }
}
