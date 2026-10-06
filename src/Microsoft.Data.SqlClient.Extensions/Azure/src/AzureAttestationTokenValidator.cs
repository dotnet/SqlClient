// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Data.SqlClient.Extensions.Azure;

/// <summary>
/// Validates Microsoft Azure Attestation tokens on behalf of the Azure Attestation enclave
/// provider in Microsoft.Data.SqlClient.
/// </summary>
/// <remarks>
/// Microsoft.Data.SqlClient binds to the static ValidateToken, ValidateTokenAsync and ReadClaims
/// methods by name via reflection, so that the Microsoft.IdentityModel dependencies only need to
/// be present when Azure Attestation is used. Their signatures use only BCL types. Do not rename
/// or change them without updating AzureAttestationTokenValidatorBinding in
/// Microsoft.Data.SqlClient, which also duplicates the status constants below.
/// </remarks>
internal sealed class AzureAttestationTokenValidator
{
    /// <summary>
    /// The token is valid: signed by one of the attestation instance's signing keys, issued by
    /// that instance, and within its lifetime. The error is null.
    /// </summary>
    internal const int TokenValid = 0;

    /// <summary>
    /// The token has expired. The error is the <see cref="SecurityTokenExpiredException"/>.
    /// Retrying won't help.
    /// </summary>
    internal const int TokenExpired = 1;

    /// <summary>
    /// The token failed validation for a reason other than expiry, such as a signature that
    /// doesn't match the signing keys, an unexpected issuer, or a lifetime that hasn't started.
    /// The error is the <see cref="SecurityTokenValidationException"/>. The signing keys may be
    /// stale, so the caller should retry once with refreshed keys.
    /// </summary>
    internal const int TokenValidationFailed = 2;

    /// <summary>
    /// The token couldn't be validated at all, for example because it isn't a JWT. The error is
    /// the exception the token handler threw. Retrying won't help.
    /// </summary>
    internal const int TokenInvalid = 3;

    /// <summary>
    /// The signing keys couldn't be retrieved from the attestation instance. The error is the
    /// exception the retrieval threw, of any type, for example an HTTP or parsing failure.
    /// </summary>
    internal const int SigningKeysUnavailable = 4;

    // This is the metadata endpoint for AAS provided by the Windows team,
    // i.e. https://<attestation_instance>/.well-known/openid-configuration
    // such as https://sql.azure.attest.com/.well-known/openid-configuration
    private const string AttestationUrlSuffix = @"/.well-known/openid-configuration";

    // Signing keys are cached for 1 day to avoid DDOS attacks on the attestation instance.
    private static readonly TimeSpan s_signingKeysCacheTimeout = TimeSpan.FromDays(1);

    // The validator SqlClient uses, through the static methods it binds to.
    private static readonly AzureAttestationTokenValidator s_default =
        new(new HttpDocumentRetriever(), new MemoryCache(new MemoryCacheOptions()));

    // Retrieves the OpenID configuration and signing keys documents.
    private readonly IDocumentRetriever _documentRetriever;

    // Signing keys, keyed by attestation instance url.
    private readonly IMemoryCache _signingKeysCache;

    static AzureAttestationTokenValidator()
    {
        // Include token details in IdentityModel's exception messages, which SqlClient surfaces
        // in its attestation errors. This was set the same way before the validator moved here.
        IdentityModelEventSource.ShowPII = true;
    }

    /// <summary>
    /// Creates a validator. SqlClient uses a single default instance; tests create their own.
    /// </summary>
    /// <param name="documentRetriever">Retrieves the OpenID configuration and signing keys.</param>
    /// <param name="signingKeysCache">Caches the signing keys per attestation instance.</param>
    internal AzureAttestationTokenValidator(IDocumentRetriever documentRetriever, IMemoryCache signingKeysCache)
    {
        _documentRetriever = documentRetriever;
        _signingKeysCache = signingKeysCache;
    }

    /// <summary>
    /// Validates an attestation token with the default validator. Bound by SqlClient.
    /// </summary>
    /// <inheritdoc cref="Validate"/>
    internal static int ValidateToken(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, out Exception? error)
    {
        return s_default.Validate(attestationToken, attestationInstanceUrl, forceRefreshSigningKeys, out error);
    }

    /// <summary>
    /// Validates an attestation token with the default validator. Bound by SqlClient.
    /// </summary>
    /// <inheritdoc cref="ValidateAsync"/>
    internal static Task<Tuple<int, Exception?>> ValidateTokenAsync(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, CancellationToken cancellationToken)
    {
        return s_default.ValidateAsync(attestationToken, attestationInstanceUrl, forceRefreshSigningKeys, cancellationToken);
    }

    /// <summary>
    /// Validates an attestation token against the signing keys of the attestation instance that
    /// issued it, downloading the keys if they aren't cached.
    /// </summary>
    /// <remarks>
    /// Validation and retrieval failures are reported through the return value and
    /// <paramref name="error"/>, never thrown. Callers should branch only on the status; the error
    /// is for messages and as an inner exception.
    /// </remarks>
    /// <param name="attestationToken">The attestation token (JWT).</param>
    /// <param name="attestationInstanceUrl">
    /// The attestation instance url, without a path, such as https://sql.azure.attest.com. It is
    /// also the expected token issuer.
    /// </param>
    /// <param name="forceRefreshSigningKeys">True to download the signing keys even if cached.</param>
    /// <param name="error">The failure, as described by the status returned; null when valid.</param>
    /// <returns>One of the status constants.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The attestation instance url isn't an absolute URI.</exception>
    internal int Validate(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, out Exception? error)
    {
        CheckArguments(attestationToken, attestationInstanceUrl);

        ICollection<SecurityKey> signingKeys;
        try
        {
            signingKeys = GetSigningKeys(attestationInstanceUrl, forceRefreshSigningKeys);
        }
        catch (Exception exception)
        {
            error = exception;
            return SigningKeysUnavailable;
        }

        return ValidateSignature(attestationToken, attestationInstanceUrl, signingKeys, out error);
    }

    /// <summary>
    /// Asynchronous counterpart of <see cref="Validate"/>.
    /// </summary>
    /// <param name="attestationToken">The attestation token (JWT).</param>
    /// <param name="attestationInstanceUrl">
    /// The attestation instance url, without a path. It is also the expected token issuer.
    /// </param>
    /// <param name="forceRefreshSigningKeys">True to download the signing keys even if cached.</param>
    /// <param name="cancellationToken">Cancels the signing keys download.</param>
    /// <returns>The status, and the failure as described by that status; null when valid.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The attestation instance url isn't an absolute URI.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    internal async Task<Tuple<int, Exception?>> ValidateAsync(string attestationToken, string attestationInstanceUrl, bool forceRefreshSigningKeys, CancellationToken cancellationToken)
    {
        CheckArguments(attestationToken, attestationInstanceUrl);

        ICollection<SecurityKey> signingKeys;
        try
        {
            signingKeys = await GetSigningKeysAsync(attestationInstanceUrl, forceRefreshSigningKeys, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            return Tuple.Create<int, Exception?>(SigningKeysUnavailable, exception);
        }

        int status = ValidateSignature(attestationToken, attestationInstanceUrl, signingKeys, out Exception? error);
        return Tuple.Create(status, error);
    }

    /// <summary>
    /// Reads the claims of the attestation token, without validating it. Bound by SqlClient.
    /// </summary>
    /// <param name="attestationToken">The attestation token (JWT).</param>
    /// <returns>The claims, keyed by claim type.</returns>
    /// <exception cref="ArgumentException">
    /// The token can't be parsed, or has a claim type with more than one value.
    /// </exception>
    internal static Dictionary<string, string> ReadClaims(string attestationToken)
    {
        JsonWebTokenHandler tokenHandler = new JsonWebTokenHandler();
        JsonWebToken token = tokenHandler.ReadJsonWebToken(attestationToken);

        Dictionary<string, string> claims = new Dictionary<string, string>();
        foreach (Claim claim in token.Claims)
        {
            claims.Add(claim.Type, claim.Value);
        }

        return claims;
    }

    /// <summary>
    /// Throws if the arguments common to <see cref="Validate"/> and <see cref="ValidateAsync"/>
    /// are invalid.
    /// </summary>
    /// <param name="attestationToken">The attestation token.</param>
    /// <param name="attestationInstanceUrl">The attestation instance url.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The attestation instance url isn't an absolute URI.</exception>
    private static void CheckArguments(string attestationToken, string attestationInstanceUrl)
    {
        if (attestationToken is null)
        {
            throw new ArgumentNullException(nameof(attestationToken));
        }

        if (attestationInstanceUrl is null)
        {
            throw new ArgumentNullException(nameof(attestationInstanceUrl));
        }

        if (!Uri.TryCreate(attestationInstanceUrl, UriKind.Absolute, out _))
        {
            throw new ArgumentException("The attestation instance url must be an absolute URI.", nameof(attestationInstanceUrl));
        }
    }

    /// <summary>
    /// Returns the signing keys for an attestation instance, from the cache unless
    /// <paramref name="forceUpdate"/> is set, downloading and caching them otherwise.
    /// </summary>
    /// <param name="attestationInstanceUrl">The attestation instance url.</param>
    /// <param name="forceUpdate">True to download the keys even if cached.</param>
    /// <returns>The signing keys.</returns>
    /// <exception cref="Exception">
    /// Any exception from retrieving or parsing the OpenID configuration or signing keys, wrapped
    /// in an <see cref="AggregateException"/>. Nothing is cached then.
    /// </exception>
    private ICollection<SecurityKey> GetSigningKeys(string attestationInstanceUrl, bool forceUpdate)
    {
        if (!forceUpdate && _signingKeysCache.TryGetValue(attestationInstanceUrl, out ICollection<SecurityKey>? signingKeys) && signingKeys is not null)
        {
            return signingKeys;
        }

        OpenIdConnectConfiguration openIdConnectConfig = CreateConfigurationManager(attestationInstanceUrl)
            .GetConfigurationAsync(CancellationToken.None).Result;

        return CacheSigningKeys(attestationInstanceUrl, openIdConnectConfig);
    }

    /// <summary>
    /// Asynchronous counterpart of <see cref="GetSigningKeys"/>.
    /// </summary>
    /// <param name="attestationInstanceUrl">The attestation instance url.</param>
    /// <param name="forceUpdate">True to download the keys even if cached.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The signing keys.</returns>
    /// <exception cref="Exception">
    /// Any exception from retrieving or parsing the OpenID configuration or signing keys,
    /// including <see cref="OperationCanceledException"/>. Nothing is cached then.
    /// </exception>
    private async Task<ICollection<SecurityKey>> GetSigningKeysAsync(string attestationInstanceUrl, bool forceUpdate, CancellationToken cancellationToken)
    {
        if (!forceUpdate && _signingKeysCache.TryGetValue(attestationInstanceUrl, out ICollection<SecurityKey>? signingKeys) && signingKeys is not null)
        {
            return signingKeys;
        }

        OpenIdConnectConfiguration openIdConnectConfig = await WithCancellation(
            CreateConfigurationManager(attestationInstanceUrl).GetConfigurationAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return CacheSigningKeys(attestationInstanceUrl, openIdConnectConfig);
    }

    /// <summary>
    /// Waits for a task, but stops waiting when the cancellation token is canceled. The
    /// configuration manager doesn't pass its cancellation token on to the download, so without
    /// this a canceled open would keep waiting for it.
    /// </summary>
    /// <typeparam name="T">The task result type.</typeparam>
    /// <param name="task">The task to wait for. If abandoned, it runs to completion in the
    /// background and its result, or failure, is discarded.</param>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The task's result.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled first.</exception>
    private static async Task<T> WithCancellation<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (cancellationToken.CanBeCanceled && !task.IsCompleted)
        {
            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state!).TrySetResult(true), canceled))
            {
                if (await Task.WhenAny(task, canceled.Task).ConfigureAwait(false) != task)
                {
                    // Observe the abandoned task's failure, so it isn't reported as unobserved.
                    _ = task.ContinueWith(t => _ = t.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        return await task.ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a configuration manager for the attestation instance's OpenID configuration.
    /// </summary>
    /// <param name="attestationInstanceUrl">The attestation instance url.</param>
    /// <returns>A new configuration manager, so that each call downloads afresh.</returns>
    private ConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(string attestationInstanceUrl)
    {
        string openIdMetadataEndpoint = attestationInstanceUrl + AttestationUrlSuffix;
        return new ConfigurationManager<OpenIdConnectConfiguration>(openIdMetadataEndpoint, new OpenIdConnectConfigurationRetriever(), _documentRetriever);
    }

    /// <summary>
    /// Caches the signing keys of a downloaded OpenID configuration.
    /// </summary>
    /// <param name="attestationInstanceUrl">The attestation instance url, used as the cache key.</param>
    /// <param name="openIdConnectConfig">The downloaded configuration.</param>
    /// <returns>The configuration's signing keys.</returns>
    private ICollection<SecurityKey> CacheSigningKeys(string attestationInstanceUrl, OpenIdConnectConfiguration openIdConnectConfig)
    {
        ICollection<SecurityKey> signingKeys = openIdConnectConfig.SigningKeys;
        _signingKeysCache.Set(attestationInstanceUrl, signingKeys, absoluteExpirationRelativeToNow: s_signingKeysCacheTimeout);
        return signingKeys;
    }

    /// <summary>
    /// Validates the token's signature, issuer and lifetime against the given signing keys.
    /// </summary>
    /// <param name="attestationToken">The attestation token.</param>
    /// <param name="tokenIssuerUrl">The expected issuer.</param>
    /// <param name="signingKeys">The attestation instance's signing keys.</param>
    /// <param name="error">The failure, as described by the status returned; null when valid.</param>
    /// <returns>One of the Token* status constants.</returns>
    private static int ValidateSignature(string attestationToken, string tokenIssuerUrl, ICollection<SecurityKey> signingKeys, out Exception? error)
    {
        error = null;

        TokenValidationParameters validationParameters =
            new TokenValidationParameters
            {
                RequireExpirationTime = true,
                ValidateLifetime = true,
                ValidateIssuer = true,
                ValidateAudience = false, // CodeQL [SM04387] Required for an external standard: Microsoft Azure Attestation does not support the audience claim.
                RequireSignedTokens = true,
                ValidIssuers = GenerateListOfIssuers(tokenIssuerUrl),
                IssuerSigningKeys = signingKeys
            };

        try
        {
            JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(attestationToken, validationParameters, out _);
            return TokenValid;
        }
        catch (SecurityTokenExpiredException securityException)
        {
            error = securityException;
            return TokenExpired;
        }
        catch (SecurityTokenValidationException securityTokenException)
        {
            error = securityTokenException;
            return TokenValidationFailed;
        }
        catch (Exception exception)
        {
            error = exception;
            return TokenInvalid;
        }
    }

    /// <summary>
    /// Generates the list of valid issuer urls: the url's authority, and, when it uses the
    /// default port, the authority with that port spelled out.
    /// </summary>
    /// <param name="tokenIssuerUrl">The expected issuer; must be an absolute URI.</param>
    /// <returns>The valid issuer urls.</returns>
    private static ICollection<string> GenerateListOfIssuers(string tokenIssuerUrl)
    {
        List<string> issuerUrls = new List<string>();

        Uri tokenIssuerUri = new Uri(tokenIssuerUrl);
        int port = tokenIssuerUri.Port;
        bool isDefaultPort = tokenIssuerUri.IsDefaultPort;

        string issuerUrl = tokenIssuerUri.GetLeftPart(UriPartial.Authority);
        issuerUrls.Add(issuerUrl);

        if (isDefaultPort)
        {
            issuerUrls.Add(string.Concat(issuerUrl, ":", port.ToString()));
        }

        return issuerUrls;
    }
}
