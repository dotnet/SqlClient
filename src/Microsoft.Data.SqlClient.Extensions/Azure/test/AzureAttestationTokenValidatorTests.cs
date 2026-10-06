// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Data.SqlClient.Extensions.Azure.Test;

/// <summary>
/// Tests for the Azure Attestation token validator that Microsoft.Data.SqlClient's Azure
/// Attestation enclave provider binds to.
/// </summary>
public class AzureAttestationTokenValidatorTests
{
    private const string Issuer = "https://attest.unit.test";

    /// <summary>
    /// Microsoft.Data.SqlClient binds to the validator by name. This verifies that the binding
    /// succeeds, i.e. that the validator's member names and signatures match what SqlClient expects.
    /// </summary>
    [Fact]
    public void SqlClient_BindsToValidator()
    {
        Type? binding = typeof(SqlConnection).Assembly.GetType("Microsoft.Data.SqlClient.AzureAttestationTokenValidatorBinding");
        Assert.NotNull(binding);

        PropertyInfo? instance = binding.GetProperty("Instance", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(instance);
        Assert.NotNull(instance.GetValue(null));
    }

    /// <summary>
    /// A token signed by a known key, from the expected issuer and within its lifetime, is
    /// accepted with no error. This is the happy path every Azure Attestation session relies on.
    /// </summary>
    [Fact]
    public void ValidateTokenSignature_ValidToken_ReturnsValid()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Issuer, DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out Exception? error);

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, result);
        Assert.Null(error);
    }

    /// <summary>
    /// A token whose issuer spells out the default HTTPS port is accepted for an attestation url
    /// without it. Guards the issuer list the validator builds, which lets either form match.
    /// </summary>
    [Fact]
    public void ValidateTokenSignature_IssuerWithDefaultPort_ReturnsValid()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Issuer + ":443", DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out _);

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, result);
    }

    /// <summary>
    /// An expired token is reported as expired, not as retryable. SqlClient fails attestation
    /// immediately on expiry instead of refreshing the signing keys, which would not help.
    /// </summary>
    [Fact]
    public void ValidateTokenSignature_ExpiredToken_ReturnsExpired()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Issuer, DateTime.UtcNow.AddHours(-1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out Exception? error);

        Assert.Equal(AzureAttestationTokenValidator.TokenExpired, result);
        Assert.IsAssignableFrom<SecurityTokenExpiredException>(error);
    }

    /// <summary>
    /// A token signed with a key that isn't among the cached signing keys must be reported as
    /// retryable, so that the provider refreshes the signing keys.
    /// </summary>
    [Fact]
    public void ValidateTokenSignature_UnknownSigningKey_ReturnsValidationFailed()
    {
        using RSA signingRsa = CreateRsa();
        using RSA otherRsa = CreateRsa();
        string token = CreateToken(signingRsa, Issuer, DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(otherRsa), out Exception? error);

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, result);
        Assert.IsAssignableFrom<SecurityTokenValidationException>(error);
    }

    /// <summary>
    /// A token from an unexpected issuer fails validation. Guards against accepting tokens issued
    /// by a different attestation instance.
    /// </summary>
    [Fact]
    public void ValidateTokenSignature_WrongIssuer_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, "https://other.unit.test", DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out _);

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, result);
    }

    /// <summary>
    /// A string that isn't a JWT is reported as invalid rather than retryable, so SqlClient fails
    /// attestation without downloading the signing keys again.
    /// </summary>
    [Fact]
    public void ValidateTokenSignature_MalformedToken_ReturnsInvalid()
    {
        using RSA rsa = CreateRsa();

        int result = AzureAttestationTokenValidator.ValidateTokenSignature("not a token", Issuer, SigningKeys(rsa), out Exception? error);

        Assert.Equal(AzureAttestationTokenValidator.TokenInvalid, result);
        Assert.NotNull(error);
    }

    /// <summary>
    /// The claims are returned keyed by claim type. SqlClient compares the aas-ehd and rp_data
    /// claims against the enclave key and nonce, so their names and values must come through as-is.
    /// </summary>
    [Fact]
    public void ReadClaims_ReturnsTokenClaims()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Issuer, DateTime.UtcNow.AddHours(1));

        Dictionary<string, string> claims = AzureAttestationTokenValidator.ReadClaims(token);

        Assert.Equal("ehd-value", claims["aas-ehd"]);
        Assert.Equal("nonce-value", claims["rp_data"]);
        Assert.Equal(Issuer, claims["iss"]);
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
    /// Builds the signing keys handle that the validator expects, holding only the public part of
    /// the given key, as the keys downloaded from an attestation instance would.
    /// </summary>
    /// <param name="rsa">The key whose public part to use.</param>
    /// <returns>The signing keys, as the opaque object the validator takes.</returns>
    private static object SigningKeys(RSA rsa)
    {
        return new List<SecurityKey> { new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false)) };
    }

    /// <summary>
    /// Creates a signed JWT carrying the aas-ehd and rp_data claims an attestation token has.
    /// </summary>
    /// <param name="rsa">The key to sign with.</param>
    /// <param name="issuer">The token issuer.</param>
    /// <param name="expires">The expiry; issued-at and not-before are set two hours earlier.</param>
    /// <returns>The serialized token.</returns>
    private static string CreateToken(RSA rsa, string issuer, DateTime expires)
    {
        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            IssuedAt = expires.AddHours(-2),
            NotBefore = expires.AddHours(-2),
            Expires = expires,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("aas-ehd", "ehd-value"),
                new Claim("rp_data", "nonce-value"),
            }),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
