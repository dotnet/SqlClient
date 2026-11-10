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
using System.Text;
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

        /// <summary>
        /// The chain that a test passes to the validation callback.
        /// </summary>
        public enum ChainKind
        {
            /// <summary>No chain supplied to the callback.</summary>
            None,
            /// <summary>A chain that was evaluated and found clean.</summary>
            Clean,
            /// <summary>A chain that was evaluated and found untrusted.</summary>
            Untrusted,
        }

        /// <summary>
        /// The <c>ServerCertificate</c> pin that a test configures.
        /// </summary>
        public enum PinKind
        {
            /// <summary>No pin is configured.</summary>
            None,
            /// <summary>The pin is the presented certificate.</summary>
            Match,
            /// <summary>The pin is a different certificate.</summary>
            Mismatch,
            /// <summary>The pin file cannot be loaded.</summary>
            Unreadable,
        }

        /// <summary>
        /// Callback inputs and the verdict expected for them: policy errors, callback chain, pin,
        /// HostNameInCertificate, target server name, whether no certificate is presented, whether
        /// validation succeeds, the expected validation errors, and whether the callback chain
        /// status is expected to be reported.
        /// </summary>
        public static TheoryData<SslPolicyErrors, ChainKind, PinKind, string?, string, bool, bool,
            SqlServerCertificateValidationErrors, bool> VerdictCases()
        {
            const SslPolicyErrors None = SslPolicyErrors.None;
            const SslPolicyErrors Chain = SslPolicyErrors.RemoteCertificateChainErrors;
            const SslPolicyErrors Name = SslPolicyErrors.RemoteCertificateNameMismatch;
            const SslPolicyErrors NotAvailable = SslPolicyErrors.RemoteCertificateNotAvailable;
            const string Other = "other.contoso.com";

            TheoryData<SslPolicyErrors, ChainKind, PinKind, string?, string, bool, bool,
                SqlServerCertificateValidationErrors, bool> data = new()
            {
                // No pin: the platform verdict, refined by SqlClient's host name handling, decides
                // trust.
                { None, ChainKind.Clean, PinKind.None, null, ServerName, false, true,
                    SqlServerCertificateValidationErrors.None, true },
                { None, ChainKind.None, PinKind.None, null, ServerName, false, true,
                    SqlServerCertificateValidationErrors.None, true },
                { Chain, ChainKind.Untrusted, PinKind.None, null, ServerName, false, false,
                    SqlServerCertificateValidationErrors.ChainError, true },
                { Name, ChainKind.Clean, PinKind.None, null, Other, false, false,
                    SqlServerCertificateValidationErrors.HostNameMismatch, true },
                { Chain | Name, ChainKind.Untrusted, PinKind.None, null, Other, false, false,
                    SqlServerCertificateValidationErrors.ChainError
                    | SqlServerCertificateValidationErrors.HostNameMismatch, true },
                // HostNameInCertificate resolves the platform's name mismatch: the effective
                // verdict has no host name error.
                { Name, ChainKind.Clean, PinKind.None, ServerName, Other, false, true,
                    SqlServerCertificateValidationErrors.None, true },
                { Chain | Name, ChainKind.Untrusted, PinKind.None, ServerName, Other, false, false,
                    SqlServerCertificateValidationErrors.ChainError, true },
                // HostNameInCertificate that also mismatches is still reported.
                { Name, ChainKind.Clean, PinKind.None, "third.contoso.com", ServerName, false, false,
                    SqlServerCertificateValidationErrors.HostNameMismatch, true },
                // No certificate presented.
                { NotAvailable, ChainKind.None, PinKind.None, null, ServerName, true, false,
                    SqlServerCertificateValidationErrors.ServerCertificateMissing, false },
                { NotAvailable, ChainKind.Clean, PinKind.None, null, ServerName, false, false,
                    SqlServerCertificateValidationErrors.ServerCertificateMissing, false },

                // Pin: the exact comparison decides trust and the chain is not consulted.
                { None, ChainKind.Clean, PinKind.Match, null, ServerName, false, true,
                    SqlServerCertificateValidationErrors.None, false },
                { Chain, ChainKind.Untrusted, PinKind.Match, null, ServerName, false, true,
                    SqlServerCertificateValidationErrors.None, false },
                { None, ChainKind.Clean, PinKind.Mismatch, null, ServerName, false, false,
                    SqlServerCertificateValidationErrors.PinnedCertificateMismatch, false },
                { Chain, ChainKind.Untrusted, PinKind.Mismatch, null, ServerName, false, false,
                    SqlServerCertificateValidationErrors.PinnedCertificateMismatch, false },
                { NotAvailable, ChainKind.None, PinKind.Match, null, ServerName, true, false,
                    SqlServerCertificateValidationErrors.ServerCertificateMissing, false },
                { NotAvailable, ChainKind.Clean, PinKind.Match, null, ServerName, false, false,
                    SqlServerCertificateValidationErrors.ServerCertificateMissing, false },
                { None, ChainKind.None, PinKind.Match, null, ServerName, true, false,
                    SqlServerCertificateValidationErrors.ServerCertificateMissing, false },
            };
            return data;
        }

        /// <summary>
        /// Validation records SqlClient's effective verdict for each combination of callback
        /// inputs: the validation errors after HostNameInCertificate and pin handling, and the
        /// original callback chain status only when the chain is consulted.
        /// </summary>
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
            // Arrange
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

            // Act
            Exception? exception = Validate(targetServerName, hostNameInCertificate,
                nullCert ? null : serverCert, pinPath, policyErrors, chain,
                out ServerCertificateVerdict verdict);

            // Assert
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
                X509ChainStatusFlags? expectedChain = chain is null
                    ? null
                    : SniCommon.CombineChainStatus(chain.ChainStatus, policyErrors);
                Assert.Equal(expectedChain, verdict.ChainStatus);
            }
            else
            {
                Assert.Null(verdict.ChainStatus);
            }
        }

        /// <summary>
        /// An untrusted callback chain is reported as the OR of the chain's own status flags,
        /// without rebuilding the chain.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_UntrustedChain_ReportsOriginalCallbackFlags()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Chain chain = CreateChain(ChainKind.Untrusted, serverCert)!;

            X509ChainStatusFlags expected = X509ChainStatusFlags.NoError;
            foreach (X509ChainStatus status in chain.ChainStatus)
            {
                expected |= status.Status;
            }
            Assert.NotEqual(X509ChainStatusFlags.NoError, expected);

            // Act
            Validate(ServerName, null, serverCert, null,
                SslPolicyErrors.RemoteCertificateChainErrors, chain,
                out ServerCertificateVerdict verdict);

            // Assert
            Assert.Equal(expected, verdict.ChainStatus);
        }

        /// <summary>
        /// A clean callback chain is reported as <see cref="X509ChainStatusFlags.NoError"/>, which
        /// is distinct from the null reported when the callback supplies no chain.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_CleanChain_ReportsNoErrorDistinctFromUnavailable()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Chain chain = CreateChain(ChainKind.Clean, serverCert)!;

            // Act
            Validate(ServerName, null, serverCert, null, SslPolicyErrors.None, chain,
                out ServerCertificateVerdict verdict);
            Validate(ServerName, null, serverCert, null, SslPolicyErrors.None, callbackChain: null,
                out ServerCertificateVerdict noChain);

            // Assert
            Assert.Equal(SqlServerCertificateValidationErrors.None, verdict.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, verdict.ChainStatus);
            Assert.Equal(SqlServerCertificateValidationErrors.None, noChain.ValidationErrors);
            Assert.Null(noChain.ChainStatus);
        }

        /// <summary>
        /// When the pinned certificate file cannot be loaded, SqlClient never evaluates the
        /// presented certificate, so no verdict is recorded.
        /// </summary>
        [Fact]
        public void ValidateSslServerCertificate_UnreadablePin_VerdictNotEvaluated()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            string missingPath = Path.Combine(Path.GetTempPath(),
                "SqlClient_MissingPin_" + Guid.NewGuid().ToString("N") + ".cer");

            // Act
            Exception? exception = Validate(ServerName, null, serverCert, missingPath,
                SslPolicyErrors.None, null, out ServerCertificateVerdict verdict);

            // Assert
            Assert.IsType<AuthenticationException>(exception);
            Assert.Equal(
                StringsHelper.GetString(Strings.SQL_ServerCertificateFileLoadFailed, missingPath),
                exception!.Message);
            Assert.Null(verdict.ValidationErrors);
            Assert.Null(verdict.ChainStatus);
        }

        /// <summary>
        /// Managed SNI named pipes pass "." as the target server name. MatchesHostname rejects it
        /// with an <see cref="ArgumentException"/>, which predates encryption info capture. Errors
        /// already established must still be captured, and the exception must be the one that
        /// MatchesHostname itself throws.
        /// </summary>
        [Theory]
        [InlineData(SslPolicyErrors.RemoteCertificateChainErrors
            | SslPolicyErrors.RemoteCertificateNameMismatch, true)]
        [InlineData(SslPolicyErrors.RemoteCertificateNameMismatch, false)]
        public void ValidateSslServerCertificate_NamedPipeDotTarget_CapturesKnownErrorsAndKeepsException(
            SslPolicyErrors policyErrors, bool chainErrors)
        {
            // Arrange
            const string NamedPipeTarget = ".";
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            using X509Chain chain = CreateChain(
                chainErrors ? ChainKind.Untrusted : ChainKind.Clean, serverCert)!;

            // The exception that the host name check itself throws, independent of SqlClient.
            ArgumentException expected = Assert.ThrowsAny<ArgumentException>(
                () => serverCert.MatchesHostname(NamedPipeTarget));

            // Act
            Exception? capturing = Validate(NamedPipeTarget, null, serverCert, null, policyErrors,
                chain, out ServerCertificateVerdict verdict);

            // Assert
            // The pre-existing exception is neither caught nor changed.
            Assert.NotNull(capturing);
            Assert.Equal(expected.GetType(), capturing!.GetType());
            Assert.Equal(expected.Message, capturing.Message);

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
        /// Capturing the verdict must not change the outcome or the message of validation. The
        /// expected outcome is an independent restatement of the validation rules that predate
        /// capture, so a change shared by both overloads is still detected.
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

            // Arrange
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
            string? expectedMessage = ExpectedMessage(targetServerName, hostNameInCertificate, cert,
                pinKind, policyErrors);

            // Act
            Exception? capturing = Validate(targetServerName, hostNameInCertificate, cert,
                pinFile?.Path, policyErrors, chain, out ServerCertificateVerdict verdict);

            // Assert
            Assert.Equal(expectSuccess, expectedMessage is null);
            if (expectedMessage is null)
            {
                Assert.Null(capturing);
            }
            else
            {
                Assert.IsType<AuthenticationException>(capturing);
                Assert.Equal(expectedMessage, capturing!.Message);
            }

            if (capturing is not null && cert is not null)
            {
                ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(cert, verdict);
                AssertNoLeak(capture.RawCertificate!, capturing.Message);
                AssertNoLeak(capture.RawCertificate!, capturing.ToString());
            }
        }

        /// <summary>
        /// The capture holds its own copy of the DER encoding of the presented certificate,
        /// alongside the verdict it was created with.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_CapturesDer()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            ServerCertificateVerdict verdict = new(
                SqlServerCertificateValidationErrors.None, X509ChainStatusFlags.NoError);

            // Act
            ServerCertificateCapture capture =
                SniCommon.CreateServerCertificateCapture(serverCert, verdict);

            // Assert
            Assert.NotNull(capture.RawCertificate);
            Assert.Equal(serverCert.Export(X509ContentType.Cert), capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.None, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, capture.ChainStatus);

            // The capture owns its copy; it is not the certificate's internal buffer.
            capture.RawCertificate![0] ^= 0xFF;
            Assert.NotEqual(capture.RawCertificate, serverCert.RawData);
        }

        /// <summary>
        /// A certificate larger than the capture size cap is not captured, while the verdict
        /// captured with it is kept.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_OverSizeCap_CertificateUnavailable()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName,
                ServerCertificateCapture.MaxRawCertificateSize);
            Assert.True(serverCert.RawData.Length > ServerCertificateCapture.MaxRawCertificateSize);
            ServerCertificateVerdict verdict = new(
                SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot);

            // Act
            ServerCertificateCapture capture =
                SniCommon.CreateServerCertificateCapture(serverCert, verdict);

            // Assert
            // Only the certificate is dropped; the other captured values are unaffected.
            Assert.Null(capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, capture.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, capture.ChainStatus);
        }

        /// <summary>
        /// A certificate within the capture size cap is captured.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_AtSizeCap_CertificateCaptured()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName,
                ServerCertificateCapture.MaxRawCertificateSize / 2);
            Assert.InRange(serverCert.RawData.Length, 1,
                ServerCertificateCapture.MaxRawCertificateSize);

            // Act
            byte[]? rawCertificate = SniCommon.GetRawCertificate(serverCert);

            // Assert
            Assert.Equal(serverCert.RawData, rawCertificate);
        }

        /// <summary>
        /// The first certificate presented in an attempt is kept, while the verdict is the most
        /// recent one. A previous capture without a certificate does not block a later one.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_FirstCertificateWins()
        {
            // Arrange
            using X509Certificate2 first = CreateSelfSignedCertificate(ServerName);
            using X509Certificate2 second = CreateSelfSignedCertificate("other.contoso.com");
            ServerCertificateCapture previous = SniCommon.CreateServerCertificateCapture(first, default);
            ServerCertificateCapture empty = SniCommon.CreateServerCertificateCapture(null!, default);
            ServerCertificateVerdict verdict = new(
                SqlServerCertificateValidationErrors.HostNameMismatch, X509ChainStatusFlags.NoError);

            // Act
            ServerCertificateCapture next = SniCommon.CreateServerCertificateCapture(
                second, verdict, sender: null, previous: previous);
            ServerCertificateCapture afterEmpty = SniCommon.CreateServerCertificateCapture(
                second, default, previous: empty);

            // Assert
            Assert.Equal(first.RawData, next.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.HostNameMismatch, next.ValidationErrors);
            Assert.Equal(second.RawData, afterEmpty.RawCertificate);
        }

        /// <summary>
        /// With TrustServerCertificate, validation never runs, so only the certificate is
        /// captured and the verdict stays unavailable.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_Bypass_CertificateOnly()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            // Act
            ServerCertificateCapture capture =
                SniCommon.CreateServerCertificateCapture(serverCert, default);

            // Assert
            Assert.Equal(serverCert.RawData, capture.RawCertificate);
            Assert.Null(capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
            Assert.Null(capture.Protocol);
        }

        /// <summary>
        /// When the callback sender is not an <see cref="SslStream"/>, the protocol and cipher
        /// suite are unavailable.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_SenderNotSslStream_ProtocolUnavailable()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);

            // Act
            ServerCertificateCapture capture =
                SniCommon.CreateServerCertificateCapture(serverCert, default, new object());

            // Assert
            Assert.Null(capture.Protocol);
            Assert.Null(capture.CipherSuite);
        }

        /// <summary>
        /// Reading the cipher suite without a stream, or before any handshake, neither throws nor
        /// reports the null cipher suite (0).
        /// </summary>
        [Fact]
        public void GetNegotiatedCipherSuite_NoStreamOrNoHandshake_Unavailable()
        {
            // Arrange
            using SslStream sslStream = new(new MemoryStream());

            // Act
            int? noStream = SniCommon.GetNegotiatedCipherSuite(null!);
            int? noHandshake = SniCommon.GetNegotiatedCipherSuite(sslStream);

            // Assert
            Assert.Null(noStream);
            Assert.Null(noHandshake);
        }

        /// <summary>
        /// A cipher suite carried by a certificate capture is adopted only when the attempt has
        /// not already recorded one.
        /// </summary>
        [Fact]
        public void RecordServerCertificate_CipherSuite_AdoptedOnlyWhenNotAlreadyRecorded()
        {
            // Arrange
            EncryptionInfoCapture capture = new();
            EncryptionInfoCapture alreadyKnown = new();
            alreadyKnown.RecordCipherSuite(0x1301);
            ServerCertificateCapture fromCallback = new(null, null, null, cipherSuite: 0xC030);

            // Act
            capture.RecordServerCertificate(fromCallback);
            alreadyKnown.RecordServerCertificate(fromCallback);

            // Assert
            Assert.Equal(0xC030, capture.CipherSuite);
            Assert.Equal(0x1301, alreadyKnown.CipherSuite);
        }

        /// <summary>
        /// When no certificate is presented, the certificate is unavailable and the verdict is
        /// kept.
        /// </summary>
        [Fact]
        public void CreateServerCertificateCapture_NoCertificate_CertificateUnavailable()
        {
            // Arrange
            ServerCertificateVerdict verdict = new(
                SqlServerCertificateValidationErrors.ServerCertificateMissing, null);

            // Act
            ServerCertificateCapture capture = SniCommon.CreateServerCertificateCapture(null!, verdict);

            // Assert
            Assert.Null(capture.RawCertificate);
            Assert.Equal(SqlServerCertificateValidationErrors.ServerCertificateMissing,
                capture.ValidationErrors);
            Assert.Null(capture.ChainStatus);
        }

        /// <summary>
        /// Every <see cref="X509ChainStatusFlags"/> value other than
        /// <see cref="X509ChainStatusFlags.NoError"/>.
        /// </summary>
        public static TheoryData<X509ChainStatusFlags> AllChainStatusFlags()
        {
            TheoryData<X509ChainStatusFlags> data = new();
            foreach (X509ChainStatusFlags flag in Enum.GetValues<X509ChainStatusFlags>())
            {
                if (flag != X509ChainStatusFlags.NoError)
                {
                    data.Add(flag);
                }
            }
            return data;
        }

        /// <summary>
        /// Each chain status flag is preserved when combining the callback chain status.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllChainStatusFlags))]
        public void CombineChainStatus_PreservesEachFlag(X509ChainStatusFlags flag)
        {
            // Arrange
            X509ChainStatus[] statuses = { new() { Status = flag } };

            // Act
            X509ChainStatusFlags? combined = SniCommon.CombineChainStatus(statuses,
                SslPolicyErrors.RemoteCertificateChainErrors);

            // Assert
            Assert.Equal(flag, combined);
        }

        /// <summary>
        /// All chain status flags are preserved together when combining the callback chain status.
        /// </summary>
        [Fact]
        public void CombineChainStatus_PreservesFullFlagSet()
        {
            // Arrange
            List<X509ChainStatus> statuses = new();
            X509ChainStatusFlags all = X509ChainStatusFlags.NoError;
            foreach (X509ChainStatusFlags flag in Enum.GetValues<X509ChainStatusFlags>())
            {
                statuses.Add(new X509ChainStatus { Status = flag });
                all |= flag;
            }

            // Act
            X509ChainStatusFlags? combined = SniCommon.CombineChainStatus(statuses.ToArray(),
                SslPolicyErrors.RemoteCertificateChainErrors);

            // Assert
            Assert.Equal(all, combined);
        }

        /// <summary>
        /// A chain without status entries is reported as clean unless the platform reported chain
        /// errors, which the empty chain cannot describe.
        /// </summary>
        [Fact]
        public void CombineChainStatus_NoEntries_NoErrorUnlessChainErrorsReported()
        {
            // Act
            X509ChainStatusFlags? none = SniCommon.CombineChainStatus(
                Array.Empty<X509ChainStatus>(), SslPolicyErrors.None);
            X509ChainStatusFlags? nameMismatch = SniCommon.CombineChainStatus(
                Array.Empty<X509ChainStatus>(), SslPolicyErrors.RemoteCertificateNameMismatch);
            X509ChainStatusFlags? chainErrors = SniCommon.CombineChainStatus(
                Array.Empty<X509ChainStatus>(), SslPolicyErrors.RemoteCertificateChainErrors);

            // Assert
            Assert.Equal(X509ChainStatusFlags.NoError, none);
            Assert.Equal(X509ChainStatusFlags.NoError, nameMismatch);
            Assert.Null(chainErrors);
        }

        /// <summary>
        /// Without a callback chain, the chain status is unavailable.
        /// </summary>
        [Fact]
        public void GetChainStatus_NullChain_Unavailable()
        {
            // Act
            X509ChainStatusFlags? chainErrors =
                SniCommon.GetChainStatus(null!, SslPolicyErrors.RemoteCertificateChainErrors);
            X509ChainStatusFlags? none = SniCommon.GetChainStatus(null!, SslPolicyErrors.None);

            // Assert
            Assert.Null(chainErrors);
            Assert.Null(none);
        }

        /// <summary>
        /// Capture reports the chain exactly as supplied to the callback; it does not build or
        /// re-evaluate it. A chain that was never built carries no status, so the chain errors
        /// reported by the platform cannot be described and the status is unavailable.
        /// </summary>
        [Fact]
        public void GetChainStatus_DoesNotBuildChain()
        {
            // Arrange
            using X509Chain unbuilt = new();

            // Act
            X509ChainStatusFlags? status =
                SniCommon.GetChainStatus(unbuilt, SslPolicyErrors.RemoteCertificateChainErrors);

            // Assert
            Assert.Null(status);
            Assert.Empty(unbuilt.ChainElements);
        }

        #region EncryptionInfoCapture

        /// <summary>
        /// A recorded certificate capture is published in the snapshot, round-trips through
        /// <see cref="SqlConnectionEncryptionInfo.GetServerCertificate"/>, and is cleared by
        /// <see cref="EncryptionInfoCapture.Reset"/>.
        /// </summary>
        [Fact]
        public void EncryptionInfoCapture_RecordServerCertificate_PublishesInSnapshot()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            EncryptionInfoCapture capture = new();

            // Act
            capture.RecordServerCertificate(new ServerCertificateCapture(
                SniCommon.GetRawCertificate(serverCert),
                SqlServerCertificateValidationErrors.ChainError,
                X509ChainStatusFlags.UntrustedRoot));
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

            // Assert
            Assert.True(capture.HasValues);
            Assert.Equal(serverCert.RawData, capture.ServerCertificateRawData);
            Assert.False(snapshot.IsEmpty);
            Assert.Null(snapshot.NegotiatedEncryptionMode);
            Assert.Null(snapshot.NegotiatedEncryptionProtocol);
            Assert.Equal(serverCert.RawData,
                snapshot.PresentedServerCertificateRawData!.Value.ToArray());
            using (X509Certificate2? roundTripped = snapshot.GetServerCertificate())
            {
                Assert.NotNull(roundTripped);
                Assert.Equal(serverCert.RawData, roundTripped.RawData);
                Assert.False(roundTripped.HasPrivateKey);
            }
            Assert.Equal(SqlServerCertificateValidationErrors.ChainError, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.UntrustedRoot, snapshot.ChainStatus);

            // Reset clears the attempt.
            capture.Reset();
            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        /// <summary>
        /// Zero values (no errors, a clean chain) are available values, distinct from null.
        /// </summary>
        [Fact]
        public void EncryptionInfoCapture_ZeroValuesAreAvailable()
        {
            // Arrange
            EncryptionInfoCapture capture = new();

            // Act
            capture.RecordServerCertificate(new ServerCertificateCapture(null,
                SqlServerCertificateValidationErrors.None, X509ChainStatusFlags.NoError));
            SqlConnectionEncryptionInfo snapshot = capture.ToSnapshot();

            // Assert
            Assert.True(capture.HasValues);
            Assert.Equal(SqlServerCertificateValidationErrors.None, snapshot.ValidationErrors);
            Assert.Equal(X509ChainStatusFlags.NoError, snapshot.ChainStatus);
            Assert.Null(snapshot.PresentedServerCertificateRawData);
            Assert.Null(snapshot.GetServerCertificate());
        }

        /// <summary>
        /// Recording a null capture leaves the certificate fields unavailable.
        /// </summary>
        [Fact]
        public void EncryptionInfoCapture_NullCapture_LeavesCertificateFieldsUnavailable()
        {
            // Arrange
            EncryptionInfoCapture capture = new();

            // Act
            capture.RecordServerCertificate(null);

            // Assert
            Assert.False(capture.HasValues);
            Assert.Same(SqlConnectionEncryptionInfo.Empty, capture.ToSnapshot());
        }

        /// <summary>
        /// The snapshot's text never contains the certificate, any encoding of it, or its
        /// thumbprint.
        /// </summary>
        [Fact]
        public void EncryptionInfoCapture_CertificateNotInSnapshotText()
        {
            // Arrange
            using X509Certificate2 serverCert = CreateSelfSignedCertificate(ServerName);
            EncryptionInfoCapture capture = new();
            capture.RecordServerCertificate(new ServerCertificateCapture(serverCert.RawData,
                SqlServerCertificateValidationErrors.ChainError, X509ChainStatusFlags.UntrustedRoot));

            // Act
            string text = capture.ToSnapshot().ToString() ?? string.Empty;

            // Assert
            AssertNoLeak(serverCert.RawData, text);
            Assert.DoesNotContain(serverCert.Thumbprint, text, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Runs the capturing validation overload and returns the exception it throws, if any.
        /// </summary>
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
                SniCommon.ValidateSslServerCertificate(Guid.NewGuid(), targetServerName,
                    hostNameInCertificate, serverCert, pinPath, policyErrors, callbackChain,
                    ref verdict);
                return null;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        /// <summary>
        /// Computes the validation failure message that managed SNI produced before encryption
        /// info capture was added, or <see langword="null"/> when validation succeeds. This
        /// restates those rules independently of the product code so that a change to the
        /// outcome or the message is detected.
        /// </summary>
        private static string? ExpectedMessage(
            string targetServerName,
            string? hostNameInCertificate,
            X509Certificate2? serverCert,
            PinKind pinKind,
            SslPolicyErrors policyErrors)
        {
            bool hasHostNameInCertificate = !string.IsNullOrEmpty(hostNameInCertificate);
            bool certificateMissing = serverCert is null
                || policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable);

            // A pin replaces the platform verdict with an exact comparison.
            if (pinKind != PinKind.None)
            {
                Assert.NotEqual(PinKind.Unreadable, pinKind);
                if (certificateMissing)
                {
                    return Strings.SQL_ServerCertificateNotAvailable;
                }
                return pinKind == PinKind.Match
                    ? null
                    : Strings.SQL_RemoteCertificateDoesNotMatchServerCertificate;
            }

            if (!hasHostNameInCertificate && policyErrors == SslPolicyErrors.None)
            {
                return null;
            }

            StringBuilder message = new();
            if (certificateMissing)
            {
                message.AppendLine(Strings.SQL_RemoteCertificateNotAvailable);
            }

            if (serverCert is not null
                && policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
            {
                // The message describes a chain rebuilt with offline revocation checking.
                using X509Chain chain = new();
                chain.ChainPolicy.RevocationMode = X509RevocationMode.Offline;
                StringBuilder chainStatusInformation = new();
                if (!chain.Build(serverCert))
                {
                    foreach (X509ChainStatus chainStatus in chain.ChainStatus)
                    {
                        chainStatusInformation.Append(
                            $"{chainStatus.StatusInformation}, [Status: {chainStatus.Status}]");
                        chainStatusInformation.AppendLine();
                    }
                }
                message.AppendFormat(Strings.SQL_RemoteCertificateChainErrors,
                    chainStatusInformation);
                message.AppendLine();
            }

            if (serverCert is not null
                && (hasHostNameInCertificate
                    || policyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                && !serverCert.MatchesHostname(
                    hasHostNameInCertificate ? hostNameInCertificate! : targetServerName))
            {
                message.AppendLine(Strings.SQL_RemoteCertificateNameMismatch);
            }

            return message.Length > 0 ? message.ToString() : null;
        }

        /// <summary>
        /// Creates the chain that a test passes to the validation callback, built against the
        /// certificate so that its status reflects <paramref name="kind"/>.
        /// </summary>
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

        /// <summary>
        /// Asserts that the text contains no hexadecimal, Base64, or hash encoding of the
        /// certificate.
        /// </summary>
        private static void AssertNoLeak(byte[] certificate, string text)
        {
            Assert.DoesNotContain(Convert.ToHexString(certificate), text,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToBase64String(certificate), text, StringComparison.Ordinal);
            Assert.DoesNotContain(BitConverter.ToString(certificate), text,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Convert.ToHexString(SHA256.HashData(certificate)), text,
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a self-signed certificate for <paramref name="subjectCommonName"/>.
        /// </summary>
        /// <param name="subjectCommonName">The subject and DNS SAN.</param>
        /// <param name="paddingBytes">
        /// When non-zero, adds a private, non-critical extension of this many bytes to grow the DER
        /// encoding (used to test the capture size cap).
        /// </param>
        private static X509Certificate2 CreateSelfSignedCertificate(string subjectCommonName,
            int paddingBytes = 0)
        {
            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new($"CN={subjectCommonName}", rsa,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName(subjectCommonName);
            request.CertificateExtensions.Add(san.Build());
            if (paddingBytes > 0)
            {
                // A DER OCTET STRING keeps the extension value well-formed.
                System.Formats.Asn1.AsnWriter writer = new(System.Formats.Asn1.AsnEncodingRules.DER);
                writer.WriteOctetString(new byte[paddingBytes]);
                request.CertificateExtensions.Add(
                    new X509Extension("1.3.6.1.4.1.311.999.1", writer.Encode(), critical: false));
            }

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            return request.CreateSelfSigned(notBefore, notBefore.AddHours(1));
        }

        /// <summary>
        /// A certificate exported to a temporary file for use as a <c>ServerCertificate</c> pin,
        /// deleted on dispose.
        /// </summary>
        private sealed class TempCertFile : IDisposable
        {
            /// <summary>
            /// Exports <paramref name="cert"/> to a new temporary file.
            /// </summary>
            public TempCertFile(X509Certificate2 cert)
            {
                Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                    "SqlClient_CapturePin_" + Guid.NewGuid().ToString("N") + ".cer");
                File.WriteAllBytes(Path, cert.Export(X509ContentType.Cert));
            }

            /// <summary>
            /// The path of the temporary file.
            /// </summary>
            public string Path { get; }

            /// <summary>
            /// Deletes the temporary file, ignoring failures.
            /// </summary>
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
