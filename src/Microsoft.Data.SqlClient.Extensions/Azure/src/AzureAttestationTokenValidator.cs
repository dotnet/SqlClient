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
/// Microsoft.Data.SqlClient binds to these members by name via reflection, so that the
/// Microsoft.IdentityModel dependencies only need to be present when Azure Attestation is used.
/// Every signature uses only BCL types. Do not rename or change these members without updating
/// AzureAttestationTokenValidator in Microsoft.Data.SqlClient.
/// </remarks>
internal static class AzureAttestationTokenValidator
{
    /// <summary>
    /// The token signature is valid.
    /// </summary>
    internal const int TokenValid = 0;

    /// <summary>
    /// The token has expired.
    /// </summary>
    internal const int TokenExpired = 1;

    /// <summary>
    /// The token failed validation, possibly because the cached signing keys are stale. The caller
    /// should refresh the signing keys and retry.
    /// </summary>
    internal const int TokenValidationFailed = 2;

    /// <summary>
    /// The token is invalid and retrying will not help.
    /// </summary>
    internal const int TokenInvalid = 3;

    // This is the metadata endpoint for AAS provided by the Windows team,
    // i.e. https://<attestation_instance>/.well-known/openid-configuration
    // such as https://sql.azure.attest.com/.well-known/openid-configuration
    private const string AttestationUrlSuffix = @"/.well-known/openid-configuration";

    private static readonly MemoryCache s_signingKeysCache = new(new MemoryCacheOptions());
    private static readonly TimeSpan s_signingKeysCacheTimeout = TimeSpan.FromDays(1);

    /// <summary>
    /// Downloads the token signing keys for the given attestation instance from its well-known
    /// OpenID configuration endpoint. The keys are cached for 1 day to avoid DDOS attacks.
    /// </summary>
    /// <param name="attestationInstanceUrl">The attestation instance url, without a path.</param>
    /// <param name="forceUpdate">True to bypass the cache and download the keys again.</param>
    /// <returns>An opaque handle to the signing keys, to pass to ValidateTokenSignature.</returns>
    internal static object GetSigningKeys(string attestationInstanceUrl, bool forceUpdate)
    {
        IdentityModelEventSource.ShowPII = true;

        if (!forceUpdate && s_signingKeysCache.TryGetValue(attestationInstanceUrl, out ICollection<SecurityKey>? signingKeys) && signingKeys is not null)
        {
            return signingKeys;
        }

        OpenIdConnectConfiguration openIdConnectConfig = CreateConfigurationManager(attestationInstanceUrl)
            .GetConfigurationAsync(CancellationToken.None).Result;

        return CacheSigningKeys(attestationInstanceUrl, openIdConnectConfig);
    }

    /// <summary>
    /// Asynchronous counterpart of GetSigningKeys.
    /// </summary>
    /// <param name="attestationInstanceUrl">The attestation instance url, without a path.</param>
    /// <param name="forceUpdate">True to bypass the cache and download the keys again.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An opaque handle to the signing keys, to pass to ValidateTokenSignature.</returns>
    internal static async Task<object> GetSigningKeysAsync(string attestationInstanceUrl, bool forceUpdate, CancellationToken cancellationToken)
    {
        IdentityModelEventSource.ShowPII = true;

        if (!forceUpdate && s_signingKeysCache.TryGetValue(attestationInstanceUrl, out ICollection<SecurityKey>? signingKeys) && signingKeys is not null)
        {
            return signingKeys;
        }

        OpenIdConnectConfiguration openIdConnectConfig = await CreateConfigurationManager(attestationInstanceUrl)
            .GetConfigurationAsync(cancellationToken).ConfigureAwait(false);

        return CacheSigningKeys(attestationInstanceUrl, openIdConnectConfig);
    }

    /// <summary>
    /// Verifies that the attestation token is signed by the given signing keys, has not expired,
    /// and was issued by the given issuer.
    /// </summary>
    /// <param name="attestationToken">The attestation token (JWT).</param>
    /// <param name="tokenIssuerUrl">The expected issuer.</param>
    /// <param name="signingKeys">The handle returned by GetSigningKeys or GetSigningKeysAsync.</param>
    /// <param name="error">The validation exception, when the token is not valid.</param>
    /// <returns>One of the Token* status constants.</returns>
    internal static int ValidateTokenSignature(string attestationToken, string tokenIssuerUrl, object signingKeys, out Exception? error)
    {
        IdentityModelEventSource.ShowPII = true;
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
                IssuerSigningKeys = (ICollection<SecurityKey>)signingKeys
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
    /// Reads the claims of the attestation token, without validating it.
    /// </summary>
    /// <param name="attestationToken">The attestation token (JWT).</param>
    /// <returns>The claims, keyed by claim type.</returns>
    /// <exception cref="ArgumentException">The token cannot be parsed.</exception>
    internal static Dictionary<string, string> ReadClaims(string attestationToken)
    {
        IdentityModelEventSource.ShowPII = true;

        JsonWebTokenHandler tokenHandler = new JsonWebTokenHandler();
        JsonWebToken token = tokenHandler.ReadJsonWebToken(attestationToken);

        Dictionary<string, string> claims = new Dictionary<string, string>();
        foreach (Claim claim in token.Claims)
        {
            claims.Add(claim.Type, claim.Value);
        }

        return claims;
    }

    private static ConfigurationManager<OpenIdConnectConfiguration> CreateConfigurationManager(string attestationInstanceUrl)
    {
        string openIdMetadataEndpoint = attestationInstanceUrl + AttestationUrlSuffix;
        return new ConfigurationManager<OpenIdConnectConfiguration>(openIdMetadataEndpoint, new OpenIdConnectConfigurationRetriever());
    }

    private static ICollection<SecurityKey> CacheSigningKeys(string attestationInstanceUrl, OpenIdConnectConfiguration openIdConnectConfig)
    {
        ICollection<SecurityKey> signingKeys = openIdConnectConfig.SigningKeys;
        s_signingKeysCache.Set(attestationInstanceUrl, signingKeys, absoluteExpirationRelativeToNow: s_signingKeysCacheTimeout);
        return signingKeys;
    }

    // Generate the list of valid issuer urls (in case the token issuer url is using the default port)
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
