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
    /// SqlClient's effective validation verdict, the original callback chain status, and the SHA-256
    /// hash of the presented certificate.
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
                AssertNoLeak(capture.Hash256!, capturing.Message);
                AssertNoLeak(capture.Hash256!, capturing.ToString());
            }
        }

        [Fact]
        public void CreateServerCertificateCapture_HashIsSha256OfDer()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(serverCert, new ServerCertificateVerdict(SqlServerCertificateValidationErrors.None, X509ChainStatusFlags.NoError));

            Assert.NotNull(capture.Hash256);
            Assert.Equal(32, capture.Hash256!.Length);

            using SHA256 sha256 = SHA256.Create();
            Assert.Equal(sha256.ComputeHash(serverCert.Export(X509ContentType.Cert)), capture.Hash256);
            Assert.Equal(SqlServerCertificateValidationErrors.None, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, capture.ChainStatus);
        }

        [Fact]
        public void CreateServerCertificateCapture_Bypass_HashOnly()
        {
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            // TrustServerCertificate bypass: validation is never run, so the verdict stays default.
            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(serverCert, default);

            Assert.Equal(SHA256.HashData(serverCert.RawData), capture.Hash256);
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
        }

        [Fact]
        public void CreateServerCertificateCapture_NoCertificate_HashUnavailable()
        {
            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(
                null!,
                new ServerCertificateVerdict(SqlServerCertificateValidationErrors.ServerCertificateMissing, null));

            Assert.Null(capture.Hash256);
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
            byte[] hash = SHA256.HashData(new byte[] { 1, 2, 3 });
            EncryptionInfoCapture capture = new();

            capture.RecordServerCertificate(new ServerCertificateCapture(hash, SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));

            Assert.True(capture.HasValues);
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();
            Assert.False(snapshot.IsEmpty);
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(hash, snapshot.PresentedServerCertificateHash256!.Value.ToArray());
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
            Assert.Null(snapshot.PresentedServerCertificateHash256);
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
        public void EncryptionInfoCapture_HashNotInSnapshotText()
        {
            byte[] hash = SHA256.HashData(new byte[] { 4, 5, 6 });
            EncryptionInfoCapture capture = new();
            capture.RecordServerCertificate(new ServerCertificateCapture(hash, SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));

            AssertNoLeak(hash, capture.ToSnapshot().ToString() ?? string.Empty);
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

        private static void AssertNoLeak(byte[] hash, string text)
        {
            Assert.DoesNotContain(Convert.ToHexString(hash), text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToBase64String(hash), text, StringComparison.Ordinal);
            Assert.DoesNotContain(BitConverter.ToString(hash), text, StringComparison.OrdinalIgnoreCase);
        }

        private static X509Certificate2 CreateSelfSignedCertificate(string subjectCommonName)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new($"CN={subjectCommonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName(subjectCommonName);
            request.CertificateExtensions.Add(san.Build());

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
