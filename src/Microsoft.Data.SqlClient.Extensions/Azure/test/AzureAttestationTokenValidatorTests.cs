// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

// TODO: Not covered here, because they need infrastructure these unit tests don't have:
// - Trimmed and NativeAOT publishing: that the binding and the bound methods work when the app
//   includes this extension, and that apps without it still work. This was verified manually
//   with a trimmed publish of a console app; NativeAOT needs the C++ build tools.
// - End-to-end attestation: establishing a real SGX or VBS enclave session through this
//   validator needs SQL Server with an enclave and an attestation instance.

/// <summary>
/// Tests for the Azure Attestation token validator that Microsoft.Data.SqlClient's Azure
/// Attestation enclave provider binds to: token validation, signing-key retrieval and caching,
/// claim reading, and the binding itself.
/// </summary>
public class AzureAttestationTokenValidatorTests
{
    private const string Instance = "https://attest.unit.test";
    private const string OtherInstance = "https://other.unit.test";

    #region Binding

    /// <summary>
    /// Microsoft.Data.SqlClient binds to the validator by name. This verifies that the binding
    /// succeeds, i.e. that the validator's member names and signatures match what SqlClient expects.
    /// </summary>
    [Fact]
    public void SqlClient_BindsToValidator()
    {
        Assert.NotNull(GetSqlClientBinding());
    }

    /// <summary>
    /// The bound ReadClaims delegate reaches the validator. Guards against a binding that
    /// succeeds but points at the wrong method.
    /// </summary>
    [Fact]
    public void SqlClient_BoundReadClaims_ReadsClaims()
    {
        using RSA rsa = CreateRsa();
        Delegate readClaims = GetBoundDelegate("ReadClaims");

        var claims = (Dictionary<string, string>)readClaims.DynamicInvoke(CreateToken(rsa, Instance))!;

        Assert.Equal("ehd-value", claims["aas-ehd"]);
    }

    /// <summary>
    /// The bound ValidateToken delegate reaches the default validator, which downloads signing
    /// keys over HTTPS. Nothing listens on the loopback port used, so the download fails fast and
    /// is reported as a status rather than thrown, without needing network access.
    /// </summary>
    [Fact]
    public void SqlClient_BoundValidateToken_ReportsUnreachableInstance()
    {
        Delegate validateToken = GetBoundDelegate("ValidateToken");
        object?[] args = { "token", "https://127.0.0.1:1", false, null };

        object? status = validateToken.DynamicInvoke(args);

        Assert.Equal(AzureAttestationTokenValidator.SigningKeysUnavailable, status);
        Assert.NotNull(args[3]);
    }

    /// <summary>
    /// Asynchronous counterpart of <see cref="SqlClient_BoundValidateToken_ReportsUnreachableInstance"/>.
    /// </summary>
    [Fact]
    public async Task SqlClient_BoundValidateTokenAsync_ReportsUnreachableInstance()
    {
        Delegate validateTokenAsync = GetBoundDelegate("ValidateTokenAsync");

        var task = (Task<Tuple<int, Exception?>>)validateTokenAsync.DynamicInvoke("token", "https://127.0.0.1:1", false, CancellationToken.None)!;
        Tuple<int, Exception?> result = await task;

        Assert.Equal(AzureAttestationTokenValidator.SigningKeysUnavailable, result.Item1);
        Assert.NotNull(result.Item2);
    }

    #endregion

    #region Token validation

    /// <summary>
    /// A token signed by a known key, from the expected issuer and within its lifetime, is
    /// accepted with no error. This is the happy path every Azure Attestation session relies on.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_ValidToken_ReturnsValid(bool async)
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(async, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, status);
        Assert.Null(error);
    }

    /// <summary>
    /// A token whose issuer spells out the default HTTPS port is accepted for an attestation url
    /// without it. Guards the issuer list the validator builds, which lets either form match.
    /// </summary>
    [Fact]
    public async Task Validate_IssuerWithDefaultPort_ReturnsValid()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(rsa, Instance + ":443"));

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, status);
        Assert.Null(error);
    }

    /// <summary>
    /// An expired token is reported as expired, with the expiry exception as the documented
    /// error. SqlClient fails attestation immediately on expiry instead of refreshing the signing
    /// keys, which would not help.
    /// </summary>
    [Fact]
    public async Task Validate_ExpiredToken_ReturnsExpired()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(rsa, Instance, expires: DateTime.UtcNow.AddHours(-1)));

        Assert.Equal(AzureAttestationTokenValidator.TokenExpired, status);
        Assert.IsAssignableFrom<SecurityTokenExpiredException>(error);
    }

    /// <summary>
    /// A token signed with a key that isn't among the signing keys is reported as retryable, with
    /// the validation exception as the documented error, so that SqlClient refreshes the keys.
    /// </summary>
    [Fact]
    public async Task Validate_UnknownSigningKey_ReturnsValidationFailed()
    {
        using RSA signingRsa = CreateRsa();
        using RSA publishedRsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, publishedRsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(signingRsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// A token from an unexpected issuer fails validation. Guards against accepting tokens issued
    /// by a different attestation instance.
    /// </summary>
    [Fact]
    public async Task Validate_WrongIssuer_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(rsa, OtherInstance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// A token whose signature was altered is rejected even though its key is known. Guards the
    /// core property of attestation: the claims are only trusted if the signature verifies.
    /// </summary>
    [Fact]
    public async Task Validate_TamperedSignature_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);
        string token = CreateToken(rsa, Instance);
        string tampered = token.Substring(0, token.Length - 4) + (token.EndsWith("AAAA", StringComparison.Ordinal) ? "BBBB" : "AAAA");

        (int status, Exception? error) = await validator.Validate(false, tampered);

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// An unsigned token is rejected, because signed tokens are required.
    /// </summary>
    [Fact]
    public async Task Validate_UnsignedToken_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(signingRsa: null, Instance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// A token without an expiry is rejected, because an expiration time is required.
    /// </summary>
    [Fact]
    public async Task Validate_TokenWithoutExpiry_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(rsa, Instance, withExpiry: false));

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// A token whose lifetime hasn't started is rejected.
    /// </summary>
    [Fact]
    public async Task Validate_TokenNotYetValid_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, CreateToken(rsa, Instance, notBefore: DateTime.UtcNow.AddHours(1), expires: DateTime.UtcNow.AddHours(2)));

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// A string that isn't a JWT is reported as invalid rather than retryable, so SqlClient fails
    /// attestation without downloading the signing keys again.
    /// </summary>
    [Fact]
    public async Task Validate_MalformedToken_ReturnsInvalid()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, Exception? error) = await validator.Validate(false, "not a token");

        Assert.Equal(AzureAttestationTokenValidator.TokenInvalid, status);
        Assert.NotNull(error);
    }

    /// <summary>
    /// Invalid arguments throw instead of being reported as a status, in both paths. They are
    /// programming errors in the caller, not attestation failures.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Validate_InvalidArguments_Throw(bool async)
    {
        var validator = new ValidatorFixture();

        await Assert.ThrowsAsync<ArgumentNullException>(() => validator.Validate(async, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => validator.Validate(async, "token", instanceUrl: null!));
        await Assert.ThrowsAsync<ArgumentException>(() => validator.Validate(async, "token", instanceUrl: "not/absolute"));
        Assert.Equal(0, validator.Retriever.RequestCount);
    }

    #endregion

    #region Signing keys

    /// <summary>
    /// The signing keys are downloaded from the instance's OpenID configuration, through its
    /// jwks_uri, on first use. Guards the retrieval path in both the sync and async validators.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningKeys_AreDownloadedOnFirstUse(bool async)
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        (int status, _) = await validator.Validate(async, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, status);
        Assert.Equal(1, validator.Retriever.MetadataRequests(Instance));
        Assert.Equal(1, validator.Retriever.KeysRequests(Instance));
    }

    /// <summary>
    /// Repeated validations against the same instance reuse the cached keys. The cache exists to
    /// avoid hammering the attestation instance on every connection.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningKeys_AreCached(bool async)
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        await validator.Validate(async, CreateToken(rsa, Instance));
        (int status, _) = await validator.Validate(async, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, status);
        Assert.Equal(1, validator.Retriever.MetadataRequests(Instance));
    }

    /// <summary>
    /// Keys downloaded by one path are reused by the other, in both directions. SqlClient mixes
    /// sync and async opens, so they must share one cache.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningKeys_AreSharedBetweenSyncAndAsync(bool firstAsync)
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        await validator.Validate(firstAsync, CreateToken(rsa, Instance));
        (int status, _) = await validator.Validate(!firstAsync, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, status);
        Assert.Equal(1, validator.Retriever.MetadataRequests(Instance));
    }

    /// <summary>
    /// Keys are cached per instance: another instance's keys are neither reused nor accepted.
    /// Guards against validating one instance's token with another's keys.
    /// </summary>
    [Fact]
    public async Task SigningKeys_AreCachedPerInstance()
    {
        using RSA rsa = CreateRsa();
        using RSA otherRsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa).AddInstance(OtherInstance, otherRsa);

        await validator.Validate(false, CreateToken(rsa, Instance));
        (int status, _) = await validator.Validate(false, CreateToken(rsa, OtherInstance), OtherInstance);

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, status);
        Assert.Equal(1, validator.Retriever.MetadataRequests(Instance));
        Assert.Equal(1, validator.Retriever.MetadataRequests(OtherInstance));
    }

    /// <summary>
    /// Cached keys are used for one day and downloaded again after that, so rotated keys are
    /// eventually picked up even without a validation failure.
    /// </summary>
    [Fact]
    public async Task SigningKeys_ExpireAfterOneDay()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);

        await validator.Validate(false, CreateToken(rsa, Instance));
        validator.Clock.Advance(TimeSpan.FromHours(23));
        await validator.Validate(false, CreateToken(rsa, Instance));
        Assert.Equal(1, validator.Retriever.MetadataRequests(Instance));

        validator.Clock.Advance(TimeSpan.FromHours(2));
        await validator.Validate(false, CreateToken(rsa, Instance));
        Assert.Equal(2, validator.Retriever.MetadataRequests(Instance));
    }

    /// <summary>
    /// After the instance rotates its key, the cached keys reject new tokens as retryable, and a
    /// forced refresh downloads the rotated key and accepts them. This is the recovery SqlClient
    /// performs on a validation failure.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningKeys_ForcedRefresh_PicksUpRotatedKey(bool async)
    {
        using RSA oldRsa = CreateRsa();
        using RSA newRsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, oldRsa);
        await validator.Validate(async, CreateToken(oldRsa, Instance));

        validator.AddInstance(Instance, newRsa);
        (int cachedStatus, _) = await validator.Validate(async, CreateToken(newRsa, Instance));
        (int refreshedStatus, _) = await validator.Validate(async, CreateToken(newRsa, Instance), forceRefresh: true);

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, cachedStatus);
        Assert.Equal(AzureAttestationTokenValidator.TokenValid, refreshedStatus);
        Assert.Equal(2, validator.Retriever.MetadataRequests(Instance));
    }

    /// <summary>
    /// Canceling while the download is in progress throws promptly instead of waiting for it, and
    /// isn't reported as a retrieval failure, so SqlClient surfaces the cancellation rather than
    /// an attestation error. The download doesn't observe the token, so the validator must stop
    /// waiting on its own.
    /// </summary>
    [Fact]
    public async Task SigningKeys_AsyncCancellation_Throws()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture().AddInstance(Instance, rsa);
        validator.Retriever.Gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => validator.Validator.ValidateAsync(CreateToken(rsa, Instance), Instance, false, cancellation.Token));
        }
        finally
        {
            validator.Retriever.Gate.SetResult(true);
        }
    }

    /// <summary>
    /// A failed download is reported as unavailable signing keys, with the failure as the error.
    /// SqlClient turns that into its "failed to get signing keys" attestation error.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningKeys_DownloadFailure_ReturnsSigningKeysUnavailable(bool async)
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture();

        (int status, Exception? error) = await validator.Validate(async, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.SigningKeysUnavailable, status);
        Assert.NotNull(error);
    }

    /// <summary>
    /// Metadata that isn't valid JSON is reported as unavailable signing keys, not thrown.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SigningKeys_MalformedMetadata_ReturnsSigningKeysUnavailable(bool async)
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture();
        validator.Retriever.Documents[Instance + "/.well-known/openid-configuration"] = "{ not json";

        (int status, Exception? error) = await validator.Validate(async, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.SigningKeysUnavailable, status);
        Assert.NotNull(error);
    }

    /// <summary>
    /// A failed download caches nothing, so the next validation downloads again and succeeds.
    /// Guards against a transient outage disabling attestation for the cache lifetime.
    /// </summary>
    [Fact]
    public async Task SigningKeys_FailedDownload_IsNotCached()
    {
        using RSA rsa = CreateRsa();
        var validator = new ValidatorFixture();
        (int failedStatus, _) = await validator.Validate(false, CreateToken(rsa, Instance));

        validator.AddInstance(Instance, rsa);
        (int status, _) = await validator.Validate(false, CreateToken(rsa, Instance));

        Assert.Equal(AzureAttestationTokenValidator.SigningKeysUnavailable, failedStatus);
        Assert.Equal(AzureAttestationTokenValidator.TokenValid, status);
    }

    #endregion

    #region Claims

    /// <summary>
    /// The claims are returned keyed by claim type. SqlClient compares the aas-ehd and rp_data
    /// claims against the enclave key and nonce, so their names and values must come through as-is.
    /// </summary>
    [Fact]
    public void ReadClaims_ReturnsTokenClaims()
    {
        using RSA rsa = CreateRsa();

        Dictionary<string, string> claims = AzureAttestationTokenValidator.ReadClaims(CreateToken(rsa, Instance));

        Assert.Equal("ehd-value", claims["aas-ehd"]);
        Assert.Equal("nonce-value", claims["rp_data"]);
        Assert.Equal(Instance, claims["iss"]);
    }

    /// <summary>
    /// SqlClient translates an ArgumentException from ReadClaims into an attestation failure.
    /// </summary>
    [Fact]
    public void ReadClaims_MalformedToken_ThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() => AzureAttestationTokenValidator.ReadClaims("not a token"));
    }

    /// <summary>
    /// A claim type with more than one value can't be represented in the dictionary and throws
    /// ArgumentException, which SqlClient reports as an unparsable token. Attestation tokens
    /// don't carry multi-valued claims; this pins the behavior if one ever does.
    /// </summary>
    [Fact]
    public void ReadClaims_MultiValuedClaim_ThrowsArgumentException()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Instance, extraClaims: new[] { new Claim("aas-ehd", "second-value") });

        Assert.ThrowsAny<ArgumentException>(() => AzureAttestationTokenValidator.ReadClaims(token));
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Gets SqlClient's binding to this validator through reflection, as the binding is internal
    /// to SqlClient.
    /// </summary>
    /// <returns>The binding instance, or null if SqlClient couldn't bind.</returns>
    private static object? GetSqlClientBinding()
    {
        Type? binding = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.AzureAttestationTokenValidatorBinding");
        Assert.NotNull(binding);

        PropertyInfo? instance = binding.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(instance);
        return instance.GetValue(null);
    }

    /// <summary>
    /// Gets one of the delegates SqlClient's binding holds.
    /// </summary>
    /// <param name="name">The binding property name.</param>
    /// <returns>The bound delegate.</returns>
    private static Delegate GetBoundDelegate(string name)
    {
        object? binding = GetSqlClientBinding();
        Assert.NotNull(binding);
        PropertyInfo? property = binding.GetType().GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(property);
        return (Delegate)property.GetValue(binding)!;
    }

    /// <summary>
    /// Creates a 2048-bit RSA key. RSA.Create(int) is not available on .NET Framework.
    /// </summary>
    /// <returns>A new RSA key, which the caller disposes.</returns>
    private static RSA CreateRsa()
    {
        RSA rsa = RSA.Create();
        rsa.KeySize = 2048;
        return rsa;
    }

    /// <summary>
    /// Gets a stable key id for an RSA key, so that published keys and tokens can be matched.
    /// </summary>
    /// <param name="rsa">The key.</param>
    /// <returns>The key id.</returns>
    private static string KeyId(RSA rsa)
    {
        return Base64UrlEncoder.Encode(rsa.ExportParameters(includePrivateParameters: false).Modulus!).Substring(0, 16);
    }

    /// <summary>
    /// Creates a JWT carrying the aas-ehd and rp_data claims an attestation token has.
    /// </summary>
    /// <param name="signingRsa">The key to sign with, or null for an unsigned token.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="notBefore">The start of the lifetime; defaults to two hours before the expiry.</param>
    /// <param name="expires">The expiry; defaults to an hour from now.</param>
    /// <param name="withExpiry">False to omit the expiry claim.</param>
    /// <param name="extraClaims">Additional claims.</param>
    /// <returns>The serialized token.</returns>
    private static string CreateToken(
        RSA? signingRsa,
        string issuer,
        DateTime? notBefore = null,
        DateTime? expires = null,
        bool withExpiry = true,
        Claim[]? extraClaims = null)
    {
        DateTime expiry = expires ?? DateTime.UtcNow.AddHours(1);
        DateTime start = notBefore ?? expiry.AddHours(-2);
        var claims = new List<Claim>
        {
            new Claim("aas-ehd", "ehd-value"),
            new Claim("rp_data", "nonce-value"),
        };
        claims.AddRange(extraClaims ?? Array.Empty<Claim>());

        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            IssuedAt = start,
            NotBefore = start,
            Expires = withExpiry ? expiry : null,
            Subject = new ClaimsIdentity(claims),
        };

        if (signingRsa is not null)
        {
            descriptor.SigningCredentials = new SigningCredentials(new RsaSecurityKey(signingRsa) { KeyId = KeyId(signingRsa) }, SecurityAlgorithms.RsaSha256);
        }

        JsonWebTokenHandler handler = new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = withExpiry };
        return handler.CreateToken(descriptor);
    }

    /// <summary>
    /// A validator wired to a fake document retriever and a cache with a controllable clock, so
    /// tests run without network access and don't share signing keys with each other.
    /// </summary>
    private sealed class ValidatorFixture
    {
        /// <summary>
        /// Creates the validator, its retriever and its cache.
        /// </summary>
        public ValidatorFixture()
        {
            Validator = new AzureAttestationTokenValidator(Retriever, new MemoryCache(new MemoryCacheOptions { Clock = Clock }));
        }

        public FakeDocumentRetriever Retriever { get; } = new FakeDocumentRetriever();

        public FakeClock Clock { get; } = new FakeClock();

        public AzureAttestationTokenValidator Validator { get; }

        /// <summary>
        /// Publishes an instance's OpenID configuration with the public part of the given key,
        /// replacing anything published for that instance before.
        /// </summary>
        /// <param name="instanceUrl">The attestation instance url.</param>
        /// <param name="rsa">The signing key to publish.</param>
        /// <returns>This fixture.</returns>
        public ValidatorFixture AddInstance(string instanceUrl, RSA rsa)
        {
            RSAParameters key = rsa.ExportParameters(includePrivateParameters: false);
            string keysUrl = instanceUrl + "/certs";
            Retriever.Documents[instanceUrl + "/.well-known/openid-configuration"] =
                $"{{\"issuer\":\"{instanceUrl}\",\"jwks_uri\":\"{keysUrl}\"}}";
            Retriever.Documents[keysUrl] =
                $"{{\"keys\":[{{\"kty\":\"RSA\",\"use\":\"sig\",\"kid\":\"{KeyId(rsa)}\",\"n\":\"{Base64UrlEncoder.Encode(key.Modulus!)}\",\"e\":\"{Base64UrlEncoder.Encode(key.Exponent!)}\"}}]}}";
            return this;
        }

        /// <summary>
        /// Validates a token through the sync or async path, returning the status and error.
        /// </summary>
        /// <param name="async">True for the async path.</param>
        /// <param name="token">The token.</param>
        /// <param name="instanceUrl">The attestation instance url.</param>
        /// <param name="forceRefresh">True to force a signing keys download.</param>
        /// <returns>The status and error.</returns>
        public async Task<(int Status, Exception? Error)> Validate(bool async, string token, string instanceUrl = Instance, bool forceRefresh = false)
        {
            if (async)
            {
                Tuple<int, Exception?> result = await Validator.ValidateAsync(token, instanceUrl, forceRefresh, CancellationToken.None);
                return (result.Item1, result.Item2);
            }

            int status = Validator.Validate(token, instanceUrl, forceRefresh, out Exception? error);
            return (status, error);
        }
    }

    /// <summary>
    /// Serves documents from memory and counts requests per address. Missing documents fail
    /// like an HTTP 404 would.
    /// </summary>
    private sealed class FakeDocumentRetriever : IDocumentRetriever
    {
        private readonly List<string> _requests = new List<string>();

        public Dictionary<string, string> Documents { get; } = new Dictionary<string, string>();

        /// <summary>
        /// When set, requests wait for it to complete, ignoring their cancellation token, as the
        /// configuration manager passes them none.
        /// </summary>
        public TaskCompletionSource<bool>? Gate { get; set; }

        public int RequestCount
        {
            get { lock (_requests) { return _requests.Count; } }
        }

        public int MetadataRequests(string instanceUrl) => Count(instanceUrl + "/.well-known/openid-configuration");

        public int KeysRequests(string instanceUrl) => Count(instanceUrl + "/certs");

        public async Task<string> GetDocumentAsync(string address, CancellationToken cancel)
        {
            lock (_requests)
            {
                _requests.Add(address);
            }

            if (Gate is not null)
            {
                await Gate.Task;
            }

            return Documents.TryGetValue(address, out string? document)
                ? document
                : throw new IOException($"404 Not Found: {address}");
        }

        private int Count(string address)
        {
            lock (_requests)
            {
                return _requests.Count(request => request == address);
            }
        }
    }

    /// <summary>
    /// A clock the cache uses to decide expiry, advanced by tests.
    /// </summary>
    private sealed class FakeClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = DateTimeOffset.UtcNow;

        public void Advance(TimeSpan time) => UtcNow += time;
    }

    #endregion
}
