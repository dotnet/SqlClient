// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.Caching;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

// Azure Attestation Protocol Flow
// To start the attestation process, Sql Client sends the Protocol Id (i.e. 1), Nonce, Attestation Url and ECDH Public Key
// Sql Server uses attestation Url to attest the enclave and send the JWT to Sql client.
// Along with JWT, Sql server also sends enclave RSA public key, enclave Type, Enclave ECDH Public key.

// To verify the chain of trust here is how it works
// JWT is signed by well-known signing keys which Sql client can download over https (via OpenIdConnect protocol).
// JWT contains the Enclave public key to safeguard against spoofing enclave RSA public key.
// Enclave ECDH public key signed by enclave RSA key

// JWT validation
// To get the signing key for the JWT, we use OpenIdConnect API's. It download the signing keys from the well-known endpoint.
// We validate that JWT is signed, valid (i.e. not expired) and check the Issuer.

// Claim validation:
// Validate the RSA public key send by Sql server matches the value specified in JWT.

// Enclave Specific checks
// VSM
// Validate the nonce send by Sql client during start of attestation is same as that of specified in the JWT

// SGX
// JWT for SGX enclave does not contain nonce claim. To workaround this limitation Sql Server sends the RSA public key XOR with the Nonce.
// In Sql server tempered with the nonce value then both Sql Server and client will not able to compute the same shared secret.

namespace Microsoft.Data.SqlClient
{
    // Implementation of an Enclave provider (both for Sgx and Vsm) with Azure Attestation
    internal class AzureAttestationEnclaveProvider : EnclaveProviderBase
    {
        #region Constants
        private const int DiffieHellmanKeySize = 384;
        private const int AzureBasedAttestationProtocolId = 1;
        private const int SigningKeyRetryInSec = 3;
        #endregion

        #region Members
        // this is meta data endpoint for AAS provided by Windows team
        // i.e. https://<attestation_instance>/.well-known/openid-configuration
        // such as https://sql.azure.attest.com/.well-known/openid-configuration
        private const string AttestationUrlSuffix = @"/.well-known/openid-configuration";

        private static readonly MemoryCache OpenIdConnectConfigurationCache = new MemoryCache("OpenIdConnectConfigurationCache");
        #endregion

        #region Internal methods
        // When overridden in a derived class, looks up an existing enclave session information in the enclave session cache.
        // If the enclave provider doesn't implement enclave session caching, this method is expected to return null in the sqlEnclaveSession parameter.
        internal override void GetEnclaveSession(EnclaveSessionParameters enclaveSessionParameters, bool generateCustomData, out SqlEnclaveSession sqlEnclaveSession, out long counter, out byte[] customData, out int customDataLength)
        {
            GetEnclaveSessionHelper(enclaveSessionParameters, generateCustomData, out sqlEnclaveSession, out counter, out customData, out customDataLength);
        }

        // Gets the information that SqlClient subsequently uses to initiate the process of attesting the enclave and to establish a secure session with the enclave.
        internal override SqlEnclaveAttestationParameters GetAttestationParameters(string attestationUrl, byte[] customData, int customDataLength)
        {
            // The key derivation function and hash algorithm name are specified when key derivation is performed
            ECDiffieHellman clientDHKey = ECDiffieHellman.Create();
            clientDHKey.KeySize = DiffieHellmanKeySize;
            byte[] attestationParam = PrepareAttestationParameters(attestationUrl, customData, customDataLength);
            return new SqlEnclaveAttestationParameters(AzureBasedAttestationProtocolId, attestationParam, clientDHKey);
        }

        // When overridden in a derived class, performs enclave attestation, generates a symmetric key for the session, creates a an enclave session and stores the session information in the cache.
        internal override void CreateEnclaveSession(byte[] attestationInfo, ECDiffieHellman clientDHKey, EnclaveSessionParameters enclaveSessionParameters, byte[] customData, int customDataLength, out SqlEnclaveSession sqlEnclaveSession, out long counter)
        {
            sqlEnclaveSession = null;
            counter = 0;
            try
            {
                ThreadRetryCache.Remove(Thread.CurrentThread.ManagedThreadId.ToString());
                sqlEnclaveSession = GetEnclaveSessionFromCache(enclaveSessionParameters, out counter);
                if (sqlEnclaveSession == null)
                {
                    if (!string.IsNullOrEmpty(enclaveSessionParameters.AttestationUrl) && customData != null && customDataLength > 0)
                    {
                        byte[] nonce = customData;

                        IdentityModelEventSource.ShowPII = true;

                        // Deserialize the payload
                        AzureAttestationInfo attestInfo = new AzureAttestationInfo(attestationInfo);

                        // Validate the attestation info
                        VerifyAzureAttestationInfo(enclaveSessionParameters.AttestationUrl, attestInfo.EnclaveType, attestInfo.AttestationToken.AttestationToken, attestInfo.Identity, nonce);

                        // Set up shared secret and validate signature
                        byte[] sharedSecret = GetSharedSecret(attestInfo.Identity, nonce, attestInfo.EnclaveType, attestInfo.EnclaveDHInfo, clientDHKey);

                        // add session to cache
                        sqlEnclaveSession = AddEnclaveSessionToCache(enclaveSessionParameters, sharedSecret, attestInfo.SessionId, out counter);
                    }
                    else
                    {
                        throw new AlwaysEncryptedAttestationException(Strings.FailToCreateEnclaveSession);
                    }
                }
            }
            finally
            {
                // As per current design, we want to minimize the number of create session calls. To achieve this we block all the GetEnclaveSession calls until the first call to
                // GetEnclaveSession -> GetAttestationParameters -> CreateEnclaveSession completes or the event timeout happen.
                // Case 1: When the first request successfully creates the session, then all outstanding GetEnclaveSession will use the current session.
                // Case 2: When the first request unable to create the enclave session (may be due to some error or the first request doesn't require enclave computation) then in those case we set the event timeout to 0.
                UpdateEnclaveSessionLockStatus(sqlEnclaveSession);
            }
        }

        // When overridden in a derived class, looks up and evicts an enclave session from the enclave session cache, if the provider implements session caching.
        internal override void InvalidateEnclaveSession(EnclaveSessionParameters enclaveSessionParameters, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            InvalidateEnclaveSessionHelper(enclaveSessionParameters, enclaveSessionToInvalidate);
        }
        #endregion

        #region Internal Class

        // A model class representing the deserialization of the byte payload the client
        // receives from SQL Server while setting up a session.
        // Protocol format:
        // 1. Total Size of the attestation blob as UINT
        // 2. Size of Enclave RSA public key as UINT
        // 3. Size of Attestation token as UINT
        // 4. Enclave Type as UINT
        // 5. Enclave RSA public key (raw key, of length #2)
        // 6. Attestation token (of length #3)
        // 7. Size of Session Id was UINT
        // 8. Session id value
        // 9. Size of enclave ECDH public key
        // 10. Enclave ECDH public key (of length #9)
        internal class AzureAttestationInfo
        {
            public uint TotalSize { get; set; }

            // The enclave's RSA Public Key.
            // Needed to establish trust of the enclave.
            // Used to verify the enclave's DiffieHellman info.
            public EnclavePublicKey Identity { get; set; }

            // The enclave report from the SQL Server host's enclave.
            public AzureAttestationToken AttestationToken { get; set; }

            // The id of the current session.
            // Needed to set up a secure session between the client and enclave.
            public long SessionId { get; set; }

            public EnclaveType EnclaveType { get; set; }

            // The DiffieHellman public key and signature of SQL Server host's enclave.
            // Needed to set up a secure session between the client and enclave.
            public EnclaveDiffieHellmanInfo EnclaveDHInfo { get; set; }

            public AzureAttestationInfo(byte[] attestationInfo)
            {
                try
                {
                    int offset = 0;

                    // Total size of the attestation info buffer
                    TotalSize = BitConverter.ToUInt32(attestationInfo, offset);
                    offset += sizeof(uint);

                    // Size of the Enclave public key
                    int identitySize = BitConverter.ToInt32(attestationInfo, offset);
                    offset += sizeof(uint);

                    // Size of the Azure attestation token
                    int attestationTokenSize = BitConverter.ToInt32(attestationInfo, offset);
                    offset += sizeof(uint);

                    // Enclave type
                    int enclaveType = BitConverter.ToInt32(attestationInfo, offset);
                    EnclaveType = (EnclaveType)enclaveType;
                    offset += sizeof(uint);

                    // Get the enclave public key
                    byte[] identityBuffer = attestationInfo.Skip(offset).Take(identitySize).ToArray();
                    Identity = new EnclavePublicKey(identityBuffer);
                    offset += identitySize;

                    // Get Azure attestation token
                    byte[] attestationTokenBuffer = attestationInfo.Skip(offset).Take(attestationTokenSize).ToArray();
                    AttestationToken = new AzureAttestationToken(attestationTokenBuffer);
                    offset += attestationTokenSize;

                    uint secureSessionInfoResponseSize = BitConverter.ToUInt32(attestationInfo, offset);
                    offset += sizeof(uint);

                    SessionId = BitConverter.ToInt64(attestationInfo, offset);
                    offset += sizeof(long);

                    int secureSessionBufferSize = Convert.ToInt32(secureSessionInfoResponseSize) - sizeof(uint);
                    byte[] secureSessionBuffer = attestationInfo.Skip(offset).Take(secureSessionBufferSize).ToArray();
                    EnclaveDHInfo = new EnclaveDiffieHellmanInfo(secureSessionBuffer);
                    offset += Convert.ToInt32(EnclaveDHInfo.Size);
                }
                catch (Exception exception)
                {
                    throw new AlwaysEncryptedAttestationException(String.Format(Strings.FailToParseAttestationInfo, exception.Message));
                }
            }
        }

        // A managed model representing the output of EnclaveGetAttestationReport
        // https://msdn.microsoft.com/en-us/library/windows/desktop/mt844233(v=vs.85).aspx
        internal class AzureAttestationToken
        {
            public string AttestationToken { get; set; }

            public AzureAttestationToken(byte[] payload)
            {
                string jwt = System.Text.Encoding.Default.GetString(payload);
                AttestationToken = jwt.Trim().Trim('"');
            }
        }
        #endregion Internal Class

        #region Private helpers
        // Prepare the attestation data in following format
        // Attestation Url length
        // Attestation Url
        // Size of nonce
        // Nonce value
        internal byte[] PrepareAttestationParameters(string attestationUrl, byte[] attestNonce, int attestNonceLength)
        {
            if (!string.IsNullOrEmpty(attestationUrl) && attestNonce != null && attestNonceLength > 0)
            {
                // In c# strings are not null terminated, so adding the null termination before serializing it
                string attestationUrlLocal = attestationUrl + char.MinValue;
                byte[] serializedAttestationUrl = Encoding.Unicode.GetBytes(attestationUrlLocal);
                byte[] serializedAttestationUrlLength = BitConverter.GetBytes(serializedAttestationUrl.Length);

                // serializing nonce
                byte[] serializedNonce = attestNonce;
                byte[] serializedNonceLength = BitConverter.GetBytes(attestNonceLength);

                // Computing the total length of the data
                int totalDataSize = serializedAttestationUrl.Length + serializedAttestationUrlLength.Length + serializedNonce.Length + serializedNonceLength.Length;

                int dataCopied = 0;
                byte[] attestationParam = new byte[totalDataSize];

                // copy the attestation url and url length
                Buffer.BlockCopy(serializedAttestationUrlLength, 0, attestationParam, dataCopied, serializedAttestationUrlLength.Length);
                dataCopied += serializedAttestationUrlLength.Length;

                Buffer.BlockCopy(serializedAttestationUrl, 0, attestationParam, dataCopied, serializedAttestationUrl.Length);
                dataCopied += serializedAttestationUrl.Length;

                // copy the nonce and nonce length
                Buffer.BlockCopy(serializedNonceLength, 0, attestationParam, dataCopied, serializedNonceLength.Length);
                dataCopied += serializedNonceLength.Length;

                Buffer.BlockCopy(serializedNonce, 0, attestationParam, dataCopied, serializedNonce.Length);
                dataCopied += serializedNonce.Length;

                return attestationParam;
            }
            else
            {
                throw new AlwaysEncryptedAttestationException(Strings.FailToCreateEnclaveSession);
            }
        }

        // Performs Attestation per the protocol used by Azure Attestation Service
        private void VerifyAzureAttestationInfo(string attestationUrl, EnclaveType enclaveType, string attestationToken, EnclavePublicKey enclavePublicKey, byte[] nonce)
        {
            bool shouldForceUpdateSigningKeys = false;
            string attestationInstanceUrl = GetAttestationInstanceUrl(attestationUrl);

            bool shouldRetryValidation;
            bool isSignatureValid;
            string exceptionMessage = string.Empty;
            do
            {
                shouldRetryValidation = false;

                // Get the OpenId config object for the signing keys
                OpenIdConnectConfiguration openIdConfig = GetOpenIdConfigForSigningKeys(attestationInstanceUrl, shouldForceUpdateSigningKeys);

                // Verify the token signature against the signing keys downloaded from meta data end point
                bool isKeySigningExpired;
                isSignatureValid = VerifyTokenSignature(attestationToken, attestationInstanceUrl, openIdConfig.SigningKeys, out isKeySigningExpired, out exceptionMessage);

                // In cases if we fail to validate the token, since we are using the old signing keys
                // let's re-download the signing keys again and re-validate the token signature
                if (!isSignatureValid && isKeySigningExpired && !shouldForceUpdateSigningKeys)
                {
                    shouldForceUpdateSigningKeys = true;
                    shouldRetryValidation = true;
                }
            }
            while (shouldRetryValidation);

            if (!isSignatureValid)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(Strings.AttestationTokenSignatureValidationFailed, exceptionMessage));
            }

            // Validate claims in the token
            ValidateAttestationClaims(enclaveType, attestationToken, enclavePublicKey, nonce);
        }

        // Returns the innermost exception value
        private static string GetInnerMostExceptionMessage(Exception exception)
        {
            Exception exLocal = exception;
            while (exLocal.InnerException != null)
            {
                exLocal = exLocal.InnerException;
            }

            return exLocal.Message;
        }

        // For the given attestation url it downloads the token signing keys from the well-known openid configuration end point.
        // It also caches that information for 1 day to avoid DDOS attacks.
        private OpenIdConnectConfiguration GetOpenIdConfigForSigningKeys(string url, bool forceUpdate)
        {
            OpenIdConnectConfiguration openIdConnectConfig = OpenIdConnectConfigurationCache[url] as OpenIdConnectConfiguration;
            if (forceUpdate || openIdConnectConfig == null)
            {
                // Compute the meta data endpoint
                string openIdMetadataEndpoint = url + AttestationUrlSuffix;

                try
                {
                    IConfigurationManager<OpenIdConnectConfiguration> configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(openIdMetadataEndpoint, new OpenIdConnectConfigurationRetriever());
                    openIdConnectConfig = configurationManager.GetConfigurationAsync(CancellationToken.None).Result;
                }
                catch (Exception exception)
                {
                    throw new AlwaysEncryptedAttestationException(String.Format(Strings.GetAttestationTokenSigningKeysFailed, GetInnerMostExceptionMessage(exception)), exception);
                }

                OpenIdConnectConfigurationCache.Add(url, openIdConnectConfig, DateTime.UtcNow.AddDays(1));
            }

            return openIdConnectConfig;
        }

        // Return the attestation instance url for given attestation url
        // such as for https://sql.azure.attest.com/attest/SgxEnclave?api-version=2017-11-01
        // It will return https://sql.azure.attest.com
        private string GetAttestationInstanceUrl(string attestationUrl)
        {
            Uri attestationUri = new Uri(attestationUrl);
            return attestationUri.GetLeftPart(UriPartial.Authority);
        }

        // Generate the list of valid issuer Url's (in case if tokenIssuerUrl is using default port)
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
                issuerUrls.Add(String.Concat(issuerUrl, ":", port.ToString()));
            }

            return issuerUrls;
        }

        // Verifies the attestation token is signed by correct signing keys.
        private bool VerifyTokenSignature(string attestationToken, string tokenIssuerUrl, ICollection<SecurityKey> issuerSigningKeys, out bool isKeySigningExpired, out string exceptionMessage)
        {
            exceptionMessage = string.Empty;
            bool isSignatureValid = false;
            isKeySigningExpired = false;

            // Configure the TokenValidationParameters
            TokenValidationParameters validationParameters =
                new TokenValidationParameters
                {
                    RequireExpirationTime = true,
                    ValidateLifetime = true,
                    ValidateIssuer = true,
                    ValidateAudience = false,
                    RequireSignedTokens = true,
                    ValidIssuers = GenerateListOfIssuers(tokenIssuerUrl),
                    IssuerSigningKeys = issuerSigningKeys
                };

            try
            {
                SecurityToken validatedToken;
                JwtSecurityTokenHandler handler = new JwtSecurityTokenHandler();
                var token = handler.ValidateToken(attestationToken, validationParameters, out validatedToken);
                isSignatureValid = true;
            }
            catch (SecurityTokenExpiredException securityException)
            {
                throw new AlwaysEncryptedAttestationException(Strings.ExpiredAttestationToken, securityException);
            }
            catch (SecurityTokenValidationException securityTokenException)
            {
                isKeySigningExpired = true;

                // Sleep for SigningKeyRetryInSec sec before retrying to download the signing keys again.
                Thread.Sleep(SigningKeyRetryInSec * 1000);
                exceptionMessage = GetInnerMostExceptionMessage(securityTokenException);
            }
            catch (Exception exception)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(Strings.InvalidAttestationToken, GetInnerMostExceptionMessage(exception)));
            }

            return isSignatureValid;
        }

        // Computes the SHA256 hash of the byte array
        private byte[] ComputeSHA256(byte[] data)
        {
            byte[] result = null;
            try
            {
                using (SHA256 sha256 = SHA256.Create())
                {
                    result = sha256.ComputeHash(data);
                }
            }
            catch (Exception argumentException)
            {
                throw new AlwaysEncryptedAttestationException(Strings.InvalidArgumentToSHA256, argumentException);
            }
            return result;
        }

        // Validate the claims in the attestation token
        private void ValidateAttestationClaims(EnclaveType enclaveType, string attestationToken, EnclavePublicKey enclavePublicKey, byte[] nonce)
        {
            // Read the json token
            JsonWebToken token = null;
            try
            {
                JsonWebTokenHandler tokenHandler = new JsonWebTokenHandler();
                token = tokenHandler.ReadJsonWebToken(attestationToken);
            }
            catch (ArgumentException argumentException)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(Strings.FailToParseAttestationToken, argumentException.Message));
            }

            // Get all the claims from the token
            Dictionary<string, string> claims = new Dictionary<string, string>();
            foreach (Claim claim in token.Claims.ToList())
            {
                claims.Add(claim.Type, claim.Value);
            }

            // Get Enclave held data claim and validate it with the Base64UrlEncode(enclave public key)
            ValidateClaim(claims, "aas-ehd", enclavePublicKey.PublicKey);

            if (enclaveType == EnclaveType.Vbs)
            {
                // Get rp_data claim and validate it with the Base64UrlEncode(nonce)
                ValidateClaim(claims, "rp_data", nonce);
            }
        }

        // Validate the claim value against the actual data
        private void ValidateClaim(Dictionary<string, string> claims, string claimName, byte[] actualData)
        {
            // Get required claim data
            string claimData;
            bool hasClaim = claims.TryGetValue(claimName, out claimData);
            if (!hasClaim)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(Strings.MissingClaimInAttestationToken, claimName));
            }

            // Get the Base64Url of the actual data and compare it with claim
            string encodedActualData = string.Empty;
            try
            {
                encodedActualData = Base64UrlEncoder.Encode(actualData);
            }
            catch (Exception)
            {
                throw new AlwaysEncryptedAttestationException(Strings.InvalidArgumentToBase64UrlDecoder);
            }

            bool hasValidClaim = String.Equals(encodedActualData, claimData, StringComparison.Ordinal);
            if (!hasValidClaim)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(Strings.InvalidClaimInAttestationToken, claimName, claimData));
            }
        }

        // Derives the shared secret between the client and enclave.
        private byte[] GetSharedSecret(EnclavePublicKey enclavePublicKey, byte[] nonce, EnclaveType enclaveType, EnclaveDiffieHellmanInfo enclaveDHInfo, ECDiffieHellman clientDHKey)
        {
            byte[] enclaveRsaPublicKey = enclavePublicKey.PublicKey;

            // For SGX enclave we Sql server sends the enclave public key XOR'ed with Nonce.
            // In case if Sql server replayed old JWT then shared secret will not match and hence client will not able to determine the updated enclave keys.
            if (enclaveType == EnclaveType.Sgx)
            {
                for (int iterator = 0; iterator < enclaveRsaPublicKey.Length; iterator++)
                {
                    enclaveRsaPublicKey[iterator] = (byte)(enclaveRsaPublicKey[iterator] ^ nonce[iterator % nonce.Length]);
                }
            }

            // Perform signature verification. The enclave's DiffieHellman public key was signed by the enclave's RSA public key.
            RSAParameters rsaParams = KeyConverter.RSAPublicKeyBlobToParams(enclaveRsaPublicKey);
            using (RSA rsa = RSA.Create(rsaParams))
            {
                if (!rsa.VerifyData(enclaveDHInfo.PublicKey, enclaveDHInfo.PublicKeySignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    throw new ArgumentException(Strings.GetSharedSecretFailed);
                }
            }

            ECParameters ecParams = KeyConverter.ECCPublicKeyBlobToParams(enclaveDHInfo.PublicKey);
            ECDiffieHellman enclaveDHKey = ECDiffieHellman.Create(ecParams);
            return clientDHKey.DeriveKeyFromHash(enclaveDHKey.PublicKey, HashAlgorithmName.SHA256);
        }
        #endregion
    }
}
