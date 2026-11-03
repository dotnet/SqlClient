// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// The enclave provider hierarchy is internal and nullable-oblivious. Nullable analysis is
// disabled for this file so the tests can mirror those signatures exactly.
#nullable disable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted
{
    /// <summary>
    /// Tests for <see cref="AzureAttestationEnclaveProvider"/>'s token verification: how it
    /// interprets each validator result, retries once with refreshed signing keys, translates
    /// failures into SqlClient exceptions, and checks the enclave claims. The validator is a fake
    /// injected through the provider's internal constructor, so these run without the Azure
    /// extension or an attestation instance.
    /// </summary>
    public class AzureAttestationEnclaveProviderShould
    {
        private const string AttestationUrl = "https://attest.unit.test/attest/VbsEnclave?api-version=2020-10-01";
        private const string InstanceUrl = "https://attest.unit.test";
        private const string Token = "token";

        private static readonly byte[] s_enclavePublicKey = { 1, 2, 3, 4, 5 };
        private static readonly byte[] s_nonce = { 6, 7, 8, 9 };

        #region Validation results

        /// <summary>
        /// A valid token is validated once, against the attestation instance (not the full
        /// attestation url), without forcing a signing keys refresh.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Verify_ValidToken_ValidatesOnceWithoutRefresh(bool async)
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid);

            await Verify(validator, async);

            Assert.Equal(new[] { false }, validator.ForceRefreshCalls);
            Assert.Equal(InstanceUrl, validator.LastInstanceUrl);
        }

        /// <summary>
        /// A retryable failure is retried exactly once, with refreshed signing keys. This is how
        /// SqlClient recovers when the attestation instance has rotated its keys.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Verify_StaleSigningKeys_RetriesOnceWithRefreshedKeys(bool async)
        {
            var validator = new FakeValidator(
                AzureAttestationTokenValidatorBinding.TokenValidationFailed,
                AzureAttestationTokenValidatorBinding.TokenValid);

            await Verify(validator, async);

            Assert.Equal(new[] { false, true }, validator.ForceRefreshCalls);
        }

        /// <summary>
        /// A token that still fails after the refresh fails attestation with the signature
        /// validation message, carrying the innermost message of the last failure. There is no
        /// second retry.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Verify_StillFailingAfterRefresh_Throws(bool async)
        {
            var validator = new FakeValidator(
                AzureAttestationTokenValidatorBinding.TokenValidationFailed,
                AzureAttestationTokenValidatorBinding.TokenValidationFailed);

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async));

            Assert.Equal(string.Format(Strings.AttestationTokenSignatureValidationFailed, "inner failure"), exception.Message);
            Assert.Equal(new[] { false, true }, validator.ForceRefreshCalls);
        }

        /// <summary>
        /// An expired token fails attestation without a retry, keeping the validator's error as
        /// the inner exception.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Verify_ExpiredToken_ThrowsWithoutRetry(bool async)
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenExpired);

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async));

            Assert.Equal(Strings.ExpiredAttestationToken, exception.Message);
            Assert.Same(validator.Error, exception.InnerException);
            Assert.Single(validator.ForceRefreshCalls);
        }

        /// <summary>
        /// Unavailable signing keys fail attestation without a retry, with the "failed to get
        /// signing keys" message carrying the innermost failure, and the failure as the inner
        /// exception. This was SqlClient's behavior before the download moved to the extension.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Verify_SigningKeysUnavailable_ThrowsWithoutRetry(bool async)
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.SigningKeysUnavailable);

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async));

            Assert.Equal(string.Format(Strings.GetAttestationTokenSigningKeysFailed, "inner failure"), exception.Message);
            Assert.Same(validator.Error, exception.InnerException);
            Assert.Single(validator.ForceRefreshCalls);
        }

        /// <summary>
        /// An invalid token, or a status this version of SqlClient doesn't know, fails
        /// attestation as an invalid token without a retry. Unknown statuses can come from a newer
        /// Azure extension.
        /// </summary>
        [Theory]
        [InlineData(false, AzureAttestationTokenValidatorBinding.TokenInvalid)]
        [InlineData(true, AzureAttestationTokenValidatorBinding.TokenInvalid)]
        [InlineData(false, 99)]
        [InlineData(true, 99)]
        public async Task Verify_InvalidToken_ThrowsWithoutRetry(bool async, int status)
        {
            var validator = new FakeValidator(status);

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async));

            Assert.Equal(string.Format(Strings.InvalidAttestationToken, "inner failure"), exception.Message);
            Assert.Single(validator.ForceRefreshCalls);
        }

        /// <summary>
        /// Cancellation from the async validator propagates as is, not as an attestation failure.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_ValidatorCanceled_PropagatesCancellation()
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid)
            {
                AsyncFailure = new OperationCanceledException(),
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Verify(validator, async: true));
        }

        /// <summary>
        /// The wait before the retry honors cancellation on the async path, so a canceled open
        /// doesn't sit out the retry delay.
        /// </summary>
        [Fact]
        public async Task VerifyAsync_CanceledDuringRetryDelay_Throws()
        {
            var validator = new FakeValidator(
                AzureAttestationTokenValidatorBinding.TokenValidationFailed,
                AzureAttestationTokenValidatorBinding.TokenValid);
            var provider = new AzureAttestationEnclaveProvider(validator.Binding, TimeSpan.FromMinutes(1));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => provider.VerifyAzureAttestationInfoAsync(AttestationUrl, EnclaveType.Vbs, Token, new EnclavePublicKey(s_enclavePublicKey), s_nonce, cancellation.Token));
            Assert.Single(validator.ForceRefreshCalls);
        }

        #endregion

        #region Claims

        /// <summary>
        /// A token without the enclave held data claim fails attestation, for both enclave types.
        /// The claim binds the token to the enclave's public key.
        /// </summary>
        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public async Task Verify_MissingEnclaveHeldDataClaim_Throws(bool async, bool sgx)
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid);
            validator.Claims.Remove("aas-ehd");

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async, sgx ? EnclaveType.Sgx : EnclaveType.Vbs));

            Assert.Equal(string.Format(Strings.MissingClaimInAttestationToken, "aas-ehd"), exception.Message);
        }

        /// <summary>
        /// A VBS token without the nonce claim fails attestation. The nonce guards against
        /// replaying an old token.
        /// </summary>
        [Fact]
        public async Task Verify_VbsTokenMissingNonceClaim_Throws()
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid);
            validator.Claims.Remove("rp_data");

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async: false, EnclaveType.Vbs));

            Assert.Equal(string.Format(Strings.MissingClaimInAttestationToken, "rp_data"), exception.Message);
        }

        /// <summary>
        /// An SGX token doesn't need the nonce claim: for SGX, the nonce is mixed into the
        /// enclave's public key instead.
        /// </summary>
        [Fact]
        public async Task Verify_SgxTokenWithoutNonceClaim_Succeeds()
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid);
            validator.Claims.Remove("rp_data");

            await Verify(validator, async: false, EnclaveType.Sgx);
        }

        /// <summary>
        /// A claim that doesn't match the enclave's public key or the nonce fails attestation.
        /// Guards against a valid token issued for a different enclave or session.
        /// </summary>
        [Theory]
        [InlineData("aas-ehd")]
        [InlineData("rp_data")]
        public async Task Verify_MismatchedClaim_Throws(string claimName)
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid);
            validator.Claims[claimName] = "AAAA";

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async: false, EnclaveType.Vbs));

            Assert.Equal(string.Format(Strings.InvalidClaimInAttestationToken, claimName, "AAAA"), exception.Message);
        }

        /// <summary>
        /// A token the validator can't parse into claims fails attestation with the parse message.
        /// </summary>
        [Fact]
        public async Task Verify_UnparsableClaims_Throws()
        {
            var validator = new FakeValidator(AzureAttestationTokenValidatorBinding.TokenValid)
            {
                ReadClaimsFailure = new ArgumentException("bad token"),
            };

            SqlException exception = await Assert.ThrowsAsync<SqlException>(() => Verify(validator, async: false));

            Assert.Equal(string.Format(Strings.FailToParseAttestationToken, "bad token"), exception.Message);
        }

        #endregion

        #region Base64Url

        /// <summary>
        /// Verifies that claim values are encoded as unpadded base64url, as JWT claims are.
        /// </summary>
        [Theory]
        [InlineData(new byte[] { }, "")]
        [InlineData(new byte[] { 0x66 }, "Zg")]
        [InlineData(new byte[] { 0x66, 0x6f }, "Zm8")]
        [InlineData(new byte[] { 0x66, 0x6f, 0x6f }, "Zm9v")]
        [InlineData(new byte[] { 0xfb, 0xff, 0xbf }, "-_-_")]
        [InlineData(new byte[] { 0x3e, 0x3f }, "Pj8")]
        public void Base64UrlEncode_ProducesUnpaddedBase64Url(byte[] data, string expected)
        {
            Assert.Equal(expected, AzureAttestationEnclaveProvider.Base64UrlEncode(data));
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Verifies the test token through the sync or async path of a provider bound to the
        /// given fake validator, with no retry delay.
        /// </summary>
        /// <param name="validator">The fake validator.</param>
        /// <param name="async">True for the async path.</param>
        /// <param name="enclaveType">The enclave type.</param>
        /// <returns>A task that faults with the verification failure, if any.</returns>
        private static async Task Verify(FakeValidator validator, bool async, EnclaveType enclaveType = EnclaveType.Vbs)
        {
            var provider = new AzureAttestationEnclaveProvider(validator.Binding, TimeSpan.Zero);
            var publicKey = new EnclavePublicKey(s_enclavePublicKey);

            if (async)
            {
                await provider.VerifyAzureAttestationInfoAsync(AttestationUrl, enclaveType, Token, publicKey, s_nonce, CancellationToken.None);
            }
            else
            {
                provider.VerifyAzureAttestationInfo(AttestationUrl, enclaveType, Token, publicKey, s_nonce);
            }
        }

        /// <summary>
        /// A fake validator that returns a scripted sequence of statuses, records each call, and
        /// returns claims matching the test enclave key and nonce unless a test changes them.
        /// </summary>
        private sealed class FakeValidator
        {
            private readonly Queue<int> _statuses;

            /// <summary>
            /// Creates the fake with the statuses to return, one per validation.
            /// </summary>
            /// <param name="statuses">The statuses, in order.</param>
            public FakeValidator(params int[] statuses)
            {
                _statuses = new Queue<int>(statuses);
                Binding = new AzureAttestationTokenValidatorBinding(ValidateToken, ValidateTokenAsync, ReadClaims);
            }

            public AzureAttestationTokenValidatorBinding Binding { get; }

            /// <summary>
            /// The error returned with every failure status; its innermost message is "inner failure".
            /// </summary>
            public Exception Error { get; } = new InvalidOperationException("outer failure", new Exception("inner failure"));

            public List<bool> ForceRefreshCalls { get; } = new List<bool>();

            public string LastInstanceUrl { get; private set; }

            public Dictionary<string, string> Claims { get; } = new Dictionary<string, string>
            {
                ["aas-ehd"] = AzureAttestationEnclaveProvider.Base64UrlEncode(s_enclavePublicKey),
                ["rp_data"] = AzureAttestationEnclaveProvider.Base64UrlEncode(s_nonce),
            };

            public Exception ReadClaimsFailure { get; set; }

            public Exception AsyncFailure { get; set; }

            private int ValidateToken(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, out Exception error)
            {
                ForceRefreshCalls.Add(forceRefreshSigningKeys);
                LastInstanceUrl = attestationInstanceUrl;
                int status = _statuses.Dequeue();
                error = status == AzureAttestationTokenValidatorBinding.TokenValid ? null : Error;
                return status;
            }

            private Task<Tuple<int, Exception>> ValidateTokenAsync(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, CancellationToken cancellationToken)
            {
                if (AsyncFailure is not null)
                {
                    return Task.FromException<Tuple<int, Exception>>(AsyncFailure);
                }

                int status = ValidateToken(attestationToken, attestationInstanceUrl, forceRefreshSigningKeys, out Exception error);
                return Task.FromResult(Tuple.Create(status, error));
            }

            private Dictionary<string, string> ReadClaims(string attestationToken)
            {
                if (ReadClaimsFailure is not null)
                {
                    throw ReadClaimsFailure;
                }

                return new Dictionary<string, string>(Claims);
            }
        }

        #endregion
    }
}
