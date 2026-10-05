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

    [Fact]
    public void ValidateTokenSignature_ValidToken_ReturnsValid()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Issuer, DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out Exception? error);

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, result);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateTokenSignature_IssuerWithDefaultPort_ReturnsValid()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, Issuer + ":443", DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out _);

        Assert.Equal(AzureAttestationTokenValidator.TokenValid, result);
    }

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

    [Fact]
    public void ValidateTokenSignature_WrongIssuer_ReturnsValidationFailed()
    {
        using RSA rsa = CreateRsa();
        string token = CreateToken(rsa, "https://other.unit.test", DateTime.UtcNow.AddHours(1));

        int result = AzureAttestationTokenValidator.ValidateTokenSignature(token, Issuer, SigningKeys(rsa), out _);

        Assert.Equal(AzureAttestationTokenValidator.TokenValidationFailed, result);
    }

    [Fact]
    public void ValidateTokenSignature_MalformedToken_ReturnsInvalid()
    {
        using RSA rsa = CreateRsa();

        int result = AzureAttestationTokenValidator.ValidateTokenSignature("not a token", Issuer, SigningKeys(rsa), out Exception? error);

        Assert.Equal(AzureAttestationTokenValidator.TokenInvalid, result);
        Assert.NotNull(error);
    }

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

    // RSA.Create(int) is not available on .NET Framework.
    private static RSA CreateRsa()
    {
        RSA rsa = RSA.Create();
        rsa.KeySize = 2048;
        return rsa;
    }

    private static object SigningKeys(RSA rsa)
    {
        return new List<SecurityKey> { new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: false)) };
    }

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
