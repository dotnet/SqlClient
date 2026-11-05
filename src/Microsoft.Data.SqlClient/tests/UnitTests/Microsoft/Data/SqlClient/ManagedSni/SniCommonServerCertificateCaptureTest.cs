// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Data.SqlClient.Connection;
using Microsoft.Data.SqlClient.ManagedSni;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.ManagedSni
{
    /// <summary>
    /// Tests for the certificate details that managed SNI captures from the TLS validation callback:
    /// SqlClient's effective validation verdict, the original callback chain status, and the DER
    /// encoding of the presented certificate.
    /// </summary>
    public class SniCommonServerCertificateCaptureTest
    {
        private const string ServerName = "server.contoso.com";

        public enum ChainKind
        {
            // No chain supplied to the callback.
            None,
            // A chain that was evaluated and found clean.
            Clean,
            // A chain that was evaluated and found untrusted.
            Untrusted,
        }

        public enum PinKind
        {
            None,
            Match,
            Mismatch,
            Unreadable,
        }

        public static IEnumerable<object?[]> VerdictCases()
        {
            // policyErrors, chain, pin, hostNameInCertificate, targetServer, nullCert, expectSuccess, expectedErrors, expectChainFromCallback
            const SslPolicyErrors None = SslPolicyErrors.None;
            const SslPolicyErrors Chain = SslPolicyErrors.RemoteCertificateChainErrors;
            const SslPolicyErrors Name = SslPolicyErrors.RemoteCertificateNameMismatch;
            const SslPolicyErrors NotAvailable = SslPolicyErrors.RemoteCertificateNotAvailable;

            // No pin: the platform verdict, refined by SqlClient's host name handling, decides trust.
            yield return new object?[] { None, ChainKind.Clean, PinKind.None, null, ServerName, false, true, SqlServerCertificateValidationErrors.None, true };
            yield return new object?[] { None, ChainKind.None, PinKind.None, null, ServerName, false, true, SqlServerCertificateValidationErrors.None, true };
            yield return new object?[] { Chain, ChainKind.Untrusted, PinKind.None, null, ServerName, false, false, SqlServerCertificateValidationErrors.ChainError, true };
            yield return new object?[] { Name, ChainKind.Clean, PinKind.None, null, "other.contoso.com", false, false, SqlServerCertificateValidationErrors.HostNameMismatch, true };
            yield return new object?[] { Chain | Name, ChainKind.Untrusted, PinKind.None, null, "other.contoso.com", false, false, SqlServerCertificateValidationErrors.ChainError | SqlServerCertificateValidationErrors.HostNameMismatch, true };
            // HostNameInCertificate resolves the platform's name mismatch: effective verdict has no host name error.
            yield return new object?[] { Name, ChainKind.Clean, PinKind.None, ServerName, "other.contoso.com", false, true, SqlServerCertificateValidationErrors.None, true };
            yield return new object?[] { Chain | Name, ChainKind.Untrusted, PinKind.None, ServerName, "other.contoso.com", false, false, SqlServerCertificateValidationErrors.ChainError, true };
            // HostNameInCertificate that also mismatches is still reported.
            yield return new object?[] { Name, ChainKind.Clean, PinKind.None, "third.contoso.com", ServerName, false, false, SqlServerCertificateValidationErrors.HostNameMismatch, true };
            // No certificate presented.
            yield return new object?[] { NotAvailable, ChainKind.None, PinKind.None, null, ServerName, true, false, SqlServerCertificateValidationErrors.ServerCertificateMissing, false };
            yield return new object?[] { NotAvailable, ChainKind.Clean, PinKind.None, null, ServerName, false, false, SqlServerCertificateValidationErrors.ServerCertificateMissing, false };

            // Pin: the exact comparison decides trust and the chain is not consulted.
            yield return new object?[] { None, ChainKind.Clean, PinKind.Match, null, ServerName, false, true, SqlServerCertificateValidationErrors.None, false };
            yield return new object?[] { Chain, ChainKind.Untrusted, PinKind.Match, null, ServerName, false, true, SqlServerCertificateValidationErrors.None, false };
            yield return new object?[] { None, ChainKind.Clean, PinKind.Mismatch, null, ServerName, false, false, SqlServerCertificateValidationErrors.PinnedCertificateMismatch, false };
            yield return new object?[] { Chain, ChainKind.Untrusted, PinKind.Mismatch, null, ServerName, false, false, SqlServerCertificateValidationErrors.PinnedCertificateMismatch, false };
            yield return new object?[] { NotAvailable, ChainKind.None, PinKind.Match, null, ServerName, true, false, SqlServerCertificateValidationErrors.ServerCertificateMissing, false };
            yield return new object?[] { NotAvailable, ChainKind.Clean, PinKind.Match, null, ServerName, false, false, SqlServerCertificateValidationErrors.ServerCertificateMissing, false };
            yield return new object?[] { None, ChainKind.None, PinKind.Match, null, ServerName, true, false, SqlServerCertificateValidationErrors.ServerCertificateMissing, false };
        }

        [Theory]
        [MemberData(nameof(VerdictCases))]
        public void ValidateSslServerCertificate_RecordsEffectiveVerdict(
            SslPolicyErrors policyErrors,
            ChainKind chainKind,
            PinKind pinKind,
            string? hostNameInCertificate,
            string targetServerName,
            bool nullCert,
            bool expectSuccess,
            SqlServerCertificateValidationErrors expectedErrors,
            bool expectChainFromCallback)
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Certificate2 otherCert = CreateSelfSignedCertificate("other.contoso.com");
            using X509Chain? chain = CreateChain(chainKind, serverCert);
            using TempCertFile? pinFile = pinKind switch
            {
                PinKind.Match => new TempCertFile(serverCert),
                PinKind.Mismatch => new TempCertFile(otherCert),
                _ => null,
            };
            string? pinPath = pinFile?.Path;

            Exception? exception = Validate(targetServerName, hostNameInCertificate, nullCert ? null : serverCert, pinPath, policyErrors, chain, out ServerCertificateVerdict verdict);

            if (expectSuccess)
            {
                Assert.Null(exception);
            }
            else
            {
                Assert.IsType<AuthenticationException>(exception);
            }

            Assert.Equal(expectedErrors, verdict.ValidationErrors);

            if (expectChainFromCallback)
            {
                X509ChainStatusFlags? expectedChain = chain is null ? null : SniCommon.CombineChainStatus(chain.ChainStatus, policyErrors);
                Assert.Equal(expectedChain, verdict.ChainStatus);
            }
            else
            {
                Assert.Null(verdict.ChainStatus);
            }
        }

        [Fact]
        public void ValidateSslServerCertificate_UntrustedChain_ReportsOriginalCallbackFlags()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Chain chain = CreateChain(ChainKind.Untrusted, serverCert)!;

            X509ChainStatusFlags expected = X509ChainStatusFlags.NoError;
            foreach (X509ChainStatus status in chain.ChainStatus)
            {
                expected |= status.Status;
            }
            Assert.NotEqual(X509ChainStatusFlags.NoError, expected);

            Validate(ServerName, null, serverCert, null, SslPolicyErrors.RemoteCertificateChainErrors, chain, out ServerCertificateVerdict verdict);

            Assert.Equal(expected, verdict.ChainStatus);
        }

        [Fact]
        public void ValidateSslServerCertificate_CleanChain_ReportsNoErrorDistinctFromUnavailable()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Chain chain = CreateChain(ChainKind.Clean, serverCert)!;

            Validate(ServerName, null, serverCert, null, SslPolicyErrors.None, chain, out ServerCertificateVerdict verdict);

            Assert.Equal(SqlServerCertificateValidationErrors.None, verdict.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, verdict.ChainStatus);

            Validate(ServerName, null, serverCert, null, SslPolicyErrors.None, callbackChain: null, out ServerCertificateVerdict noChain);
            Assert.Equal(SqlServerCertificateValidationErrors.None, noChain.ValidationErrors);
            Assert.Null(noChain.ChainStatus);
        }

        [Fact]
        public void ValidateSslServerCertificate_UnreadablePin_VerdictNotEvaluated()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            string missingPath = Path.Combine(Path.GetTempPath(), "SqlClient_MissingPin_" + Guid.NewGuid().ToString("N") + ".cer");

            Exception? exception = Validate(ServerName, null, serverCert, missingPath, SslPolicyErrors.None, null, out ServerCertificateVerdict verdict);

            Assert.IsType<AuthenticationException>(exception);
            Assert.Null(verdict.ValidationErrors);
            Assert.Null(verdict.ChainStatus);
        }

        /// <summary>
        /// Managed SNI named pipes pass "." as the target server name. MatchesHostname rejects it with
        /// an <see cref="ArgumentException"/>, which predates encryption info capture. Errors already
        /// established must still be captured, and the exception must stay exactly as it was.
        /// </summary>
        [Theory]
        [InlineData(SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch, true)]
        [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
        public void ValidateSslServerCertificate_NamedPipeDotTarget_CapturesKnownErrorsAndKeepsException(SslPolicyErrors policyErrors, bool chainErrors)
        {
            const string NamedPipeTarget = ".";
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Chain chain = CreateChain(chainErrors ? ChainKind.Untrusted : ChainKind.Clean, serverCert)!;

            Exception? legacy = null;
            try
            {
                SniCommon.ValidateSslServerCertificate(Guid.NewGuid(), NamedPipeTarget, null, serverCert, null, policyErrors);
            }
            catch (Exception e)
            {
                legacy = e;
            }

            Exception? capturing = Validate(NamedPipeTarget, null, serverCert, null, policyErrors, chain, out ServerCertificateVerdict verdict);

            // The pre-existing exception is neither caught nor changed.
            Assert.IsAssignableFrom<ArgumentException>(legacy);
            Assert.Equal(legacy!.GetType(), capturing?.GetType());
            Assert.Equal(legacy.Message, capturing!.Message);

            if (chainErrors)
            {
                Assert.Equal(SqlServerCertificateValidationErrors.ChainError, verdict.ValidationErrors);
                Assert.Equal(SniCommon.CombineChainStatus(chain.ChainStatus, policyErrors), verdict.ChainStatus);
                Assert.NotNull(verdict.ChainStatus);
                Assert.NotEqual(X509ChainStatusFlags.NoError, verdict.ChainStatus);
            }
            else
            {
                // Nothing was established before the throw; a passing None must not be reported.
                Assert.Null(verdict.ValidationErrors);
                Assert.Null(verdict.ChainStatus);
            }
        }

        /// <summary>
        /// Capturing the verdict must not change the outcome or the message of validation.
        /// </summary>
        [Theory]
        [MemberData(nameof(VerdictCases))]
        public void ValidateSslServerCertificate_CaptureDoesNotChangeOutcomeOrMessage(
            SslPolicyErrors policyErrors,
            ChainKind chainKind,
            PinKind pinKind,
            string? hostNameInCertificate,
            string targetServerName,
            bool nullCert,
            bool expectSuccess,
            SqlServerCertificateValidationErrors expectedErrors,
            bool expectChainFromCallback)
        {
            _ = expectedErrors;
            _ = expectChainFromCallback;

            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Certificate2 otherCert = CreateSelfSignedCertificate("other.contoso.com");
            using X509Chain? chain = CreateChain(chainKind, serverCert);
            using TempCertFile? pinFile = pinKind switch
            {
                PinKind.Match => new TempCertFile(serverCert),
                PinKind.Mismatch => new TempCertFile(otherCert),
                _ => null,
            };
            X509Certificate2? cert = nullCert ? null : serverCert;

            Exception? legacy = null;
            try
            {
                SniCommon.ValidateSslServerCertificate(Guid.NewGuid(), targetServerName, hostNameInCertificate, cert, pinFile?.Path, policyErrors);
            }
            catch (Exception e)
            {
                legacy = e;
            }

            Exception? capturing = Validate(targetServerName, hostNameInCertificate, cert, pinFile?.Path, policyErrors, chain, out ServerCertificateVerdict verdict);

            Assert.Equal(expectSuccess, legacy is null);
            Assert.Equal(legacy?.GetType(), capturing?.GetType());
            Assert.Equal(legacy?.Message, capturing?.Message);

            if (capturing is not null && cert is not null)
            {
                ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(cert, verdict);
                AssertNoLeak(capture.RawCertificate!, capturing.Message);
                AssertNoLeak(capture.RawCertificate!, capturing.ToString());
            }
        }

        [Fact]
        public void CreateServerCertificateCapture_CapturesDer()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(serverCert, new ServerCertificateVerdict(SqlServerCertificateValidationErrors.None, X509ChainStatusFlags.NoError));

            Assert.NotNull(capture.RawCertificate);
            Assert.Equal(serverCert.Export(X509ContentType.Cert), capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.None, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, capture.ChainStatus);

            // The capture owns its copy; it is not the certificate's internal buffer.
            capture.RawCertificate![0] ^= 0xFF;
            Assert.NotEqual(capture.RawCertificate, serverCert.RawData);
        }

        [Fact]
        public void CreateServerCertificateCapture_OverSizeCap_CertificateUnavailable()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName, ServerCertificateCapture.MaxRawCertificateSize);
            Assert.True(serverCert.RawData.Length > ServerCertificateCapture.MaxRawCertificateSize);

            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(
                serverCert,
                new ServerCertificateVerdict(SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));

            // Only the certificate is dropped; the other captured values are unaffected.
            Assert.Null(capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, capture.ChainStatus);
        }

        [Fact]
        public void CreateServerCertificateCapture_AtSizeCap_CertificateCaptured()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName, ServerCertificateCapture.MaxRawCertificateSize / 2);
            Assert.InRange(serverCert.RawData.Length, 1, ServerCertificateCapture.MaxRawCertificateSize);

            Assert.Equal(serverCert.RawData, SniCommon.GetRawCertificate(serverCert));
        }

        [Fact]
        public void CreateServerCertificateCapture_FirstCertificateWins()
        {
            using X509Certificate2 first = CreateSelfSignedCertificate(ServerName);
            using X509Certificate2 second = CreateSelfSignedCertificate("other.contoso.com");

            ServerCertificateCapture previous = SniCommon.CreateServerCertificateCapture(first, default);
            ServerCertificateCapture next = SniCommon.CreateServerCertificateCapture(
                second,
                new ServerCertificateVerdict(SqlServerCertificateValidationErrors.HostNameMismatch, X509ChainStatusFlags.NoError),
                sender: null,
                previous: previous);

            // The certificate stays the first one presented in the attempt; the verdict is the latest.
            Assert.Equal(first.RawData, next.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch, next.ValidationErrors);

            // A previous capture without a certificate does not block capturing a later one.
            ServerCertificateCapture empty = SniCommon.CreateServerCertificateCapture(null!, default);
            Assert.Equal(second.RawData, SniCommon.CreateServerCertificateCapture(second, default, previous: empty).RawCertificate);
        }

        [Fact]
        public void CreateServerCertificateCapture_Bypass_CertificateOnly()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            // TrustServerCertificate bypass: validation is never run, so the verdict stays default.
            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(serverCert, default);

            Assert.Equal(serverCert.RawData, capture.RawCertificate);
            Assert.Null(capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
            Assert.Null(capture.Protocol);
        }

        [Fact]
        public void CreateServerCertificateCapture_SenderNotSslStream_ProtocolUnavailable()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(serverCert, default, new object());

            Assert.Null(capture.Protocol);
            Assert.Null(capture.CipherSuite);
        }

        [Fact]
        public void GetNegotiatedCipherSuite_NoStreamOrNoHandshake_Unavailable()
        {
            Assert.Null(SniCommon.GetNegotiatedCipherSuite(null!));

            // Reading before any handshake must neither throw nor report the null cipher suite (0).
            using SslStream sslStream = new(new MemoryStream());
            Assert.Null(SniCommon.GetNegotiatedCipherSuite(sslStream));
        }

        [Fact]
        public void RecordServerCertificate_CipherSuite_AdoptedOnlyWhenNotAlreadyRecorded()
        {
            EncryptionInfoCapture capture = new();
            capture.RecordServerCertificate(new ServerCertificateCapture(null, null, null, cipherSuite: 0xC030));
            Assert.Equal(0xC030, capture.CipherSuite);

            EncryptionInfoCapture alreadyKnown = new();
            alreadyKnown.RecordCipherSuite(0x1301);
            alreadyKnown.RecordServerCertificate(new ServerCertificateCapture(null, null, null, cipherSuite: 0xC030));
            Assert.Equal(0x1301, alreadyKnown.CipherSuite);
        }

        [Fact]
        public void CreateServerCertificateCapture_NoCertificate_CertificateUnavailable()
        {
            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(
                null!,
                new ServerCertificateVerdict(SqlServerCertificateValidationErrors.ServerCertificateMissing, null));

            Assert.Null(capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ServerCertificateMissing, capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
        }

        public static IEnumerable<object[]> AllChainStatusFlags()
        {
            foreach (X509ChainStatusFlags flag in Enum.GetValues<X509ChainStatusFlags>())
            {
                if (flag != X509ChainStatusFlags.NoError)
                {
                    yield return new object[] { flag };
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllChainStatusFlags))]
        public void CombineChainStatus_PreservesEachFlag(X509ChainStatusFlags flag)
        {
            X509ChainStatus[] statuses = { new() { Status = flag } };

            Assert.Equal(flag, SniCommon.CombineChainStatus(statuses, SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [Fact]
        public void CombineChainStatus_PreservesFullFlagSet()
        {
            List<X509ChainStatus> statuses = new();
            X509ChainStatusFlags all = X509ChainStatusFlags.NoError;
            foreach (X509ChainStatusFlags flag in Enum.GetValues<X509ChainStatusFlags>())
            {
                statuses.Add(new X509ChainStatus { Status = flag });
                all |= flag;
            }

            Assert.Equal(all, SniCommon.CombineChainStatus(statuses.ToArray(), SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [Fact]
        public void CombineChainStatus_NoEntries_NoErrorUnlessChainErrorsReported()
        {
            Assert.Equal(X509ChainStatusFlags.NoError, SniCommon.CombineChainStatus(Array.Empty<X509ChainStatus>(), SslPolicyErrors.None));
            Assert.Equal(X509ChainStatusFlags.NoError, SniCommon.CombineChainStatus(Array.Empty<X509ChainStatus>(), SslPolicyErrors.RemoteCertificateNameMismatch));
            Assert.Null(SniCommon.CombineChainStatus(Array.Empty<X509ChainStatus>(), SslPolicyErrors.RemoteCertificateChainErrors));
        }

        [Fact]
        public void GetChainStatus_NullChain_Unavailable()
        {
            Assert.Null(SniCommon.GetChainStatus(null!, SslPolicyErrors.RemoteCertificateChainErrors));
            Assert.Null(SniCommon.GetChainStatus(null!, SslPolicyErrors.None));
        }

        /// <summary>
        /// Capture reports the chain exactly as supplied to the callback; it does not build or
        /// re-evaluate it. A chain that was never built carries no status, so the chain errors reported
        /// by the platform cannot be described and the status is unavailable.
        /// </summary>
        [Fact]
        public void GetChainStatus_DoesNotBuildChain()
        {
            using X509Chain unbuilt = new();

            Assert.Null(SniCommon.GetChainStatus(unbuilt, SslPolicyErrors.RemoteCertificateChainErrors));
            Assert.Empty(unbuilt.ChainElements);
        }

        #region EncryptionInfoCapture

        [Fact]
        public void EncryptionInfoCapture_RecordServerCertificate_PublishesInSnapshot()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            EncryptionInfoCapture capture = new();

            capture.RecordServerCertificate(new ServerCertificateCapture(SniCommon.GetRawCertificate(serverCert), SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));

            Assert.True(capture.HasValues);
            Assert.Equal(serverCert.RawData, capture.ServerCertificateRawData);
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.False(snapshot.IsEmpty);
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(serverCert.RawData, snapshot.PresentedServerCertificateRawData!.Value.ToArray());
            using (X509Certificate2? roundTripped = snapshot.GetServerCertificate())
            {
                Assert.NotNull(roundTripped);
                Assert.Equal(serverCert.RawData, roundTripped.RawData);
                Assert.False(roundTripped.HasPrivateKey);
            }
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, snapshot.ChainStatus);

            capture.Reset();
            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        [Fact]
        public void EncryptionInfoCapture_ZeroValuesAreAvailable()
        {
            EncryptionInfoCapture capture = new();
            capture.RecordServerCertificate(new ServerCertificateCapture(null, SqlServerCertificateValidationErrors.None, X509ChainStatusFlags.NoError));

            Assert.True(capture.HasValues);
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.Equal(SqlServerCertificateValidationErrors.None, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, snapshot.ChainStatus);
            Assert.Null(snapshot.PresentedServerCertificateRawData);
            Assert.Null(snapshot.GetServerCertificate());
        }

        [Fact]
        public void EncryptionInfoCapture_NullCapture_LeavesCertificateFieldsUnavailable()
        {
            EncryptionInfoCapture capture = new();
            capture.RecordServerCertificate(null);

            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        [Fact]
        public void EncryptionInfoCapture_CertificateNotInSnapshotText()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            EncryptionInfoCapture capture = new();
            capture.RecordServerCertificate(new ServerCertificateCapture(serverCert.RawData, SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));

            string text = capture.ToSnapshot().ToString() ?? string.Empty;
            AssertNoLeak(serverCert.RawData, text);
            Assert.DoesNotContain(serverCert.Thumbprint, text, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Helpers

        private static Exception? Validate(
            string targetServerName,
            string? hostNameInCertificate,
            X509Certificate? serverCert,
            string? pinPath,
            SslPolicyErrors policyErrors,
            X509Chain? callbackChain,
            out ServerCertificateVerdict verdict)
        {
            verdict = default;
            try
            {
                SniCommon.ValidateSslServerCertificate(Guid.NewGuid(), targetServerName, hostNameInCertificate, serverCert, pinPath, policyErrors, callbackChain, ref verdict);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        private static X509Chain? CreateChain(ChainKind kind, X509Certificate2 certificate)
        {
            if (kind == ChainKind.None)
            {
                return null;
            }

            X509Chain chain = new();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            if (kind == ChainKind.Clean)
            {
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(certificate);
            }

            bool valid = chain.Build(certificate);
            Assert.Equal(kind == ChainKind.Clean, valid);
            return chain;
        }

        private static void AssertNoLeak(byte[] certificate, string text)
        {
            Assert.DoesNotContain(Convert.ToHexString(certificate), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToBase64String(certificate), text, StringComparison.Ordinal);
            Assert.DoesNotContain(BitConverter.ToString(certificate), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToHexString(SHA256.HashData(certificate)), text, StringComparison.OrdinalIgnoreCase);
        }

        /// <param name="subjectCommonName">The subject and DNS SAN.</param>
        /// <param name="paddingBytes">
        /// When non-zero, adds a private, non-critical extension of this many bytes to grow the DER
        /// encoding (used to test the capture size cap).
        /// </param>
        private static X509Certificate2 CreateSelfSignedCertificate(string subjectCommonName, int paddingBytes = 0)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new($"CN={subjectCommonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName(subjectCommonName);
            request.CertificateExtensions.Add(san.Build());
            if (paddingBytes > 0)
            {
                // A DER OCTET STRING keeps the extension value well-formed.
                System.Formats.Asn1.AsnWriter writer = new(System.Formats.Asn1.AsnEncodingRules.DER);
                writer.WriteOctetString(new byte[paddingBytes]);
                request.CertificateExtensions.Add(new X509Extension("1.3.6.1.4.1.311.999.1", writer.Encode(), critical: false));
            }

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            return request.CreateSelfSigned(notBefore, notBefore.AddHours(1));
        }

        private sealed class TempCertFile : IDisposable
        {
            public TempCertFile(X509Certificate2 cert)
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SqlClient_CapturePin_" + Guid.NewGuid().ToString("N") + ".cer");
                File.WriteAllBytes(Path, cert.Export(X509ContentType.Cert));
            }

            public string Path { get; }

            public void Dispose()
            {
                try
                {
                    File.Delete(Path);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        #endregion
    }
}

#endif
