//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.Caching;
using System.Runtime.Serialization.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    /// <summary>
    /// Implementation of an Enclave provider (both for Sgx and Vsm) with Azure Attestation
    /// </summary>
    public class AzureAttestationEnclaveProvider : EnclaveProviderBase
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

        #region Public methods
        /// <summary>
        /// When overridden in a derived class, looks up an existing enclave session information in the enclave session cache.
        /// If the enclave provider doesn't implement enclave session caching, this method is expected to return null in the sqlEnclaveSession parameter.
        /// </summary>
        /// <param name="servername">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="attestationUrl">The endpoint of an attestation service, SqlClient contacts to attest the enclave.</param>
        /// <param name="sqlEnclaveSession">When this method returns, the requested enclave session or null if the provider doesn't implement session caching. This parameter is treated as uninitialized.</param>
        /// <param name="counter">A counter that the enclave provider is expected to increment each time SqlClient retrieves the session from the cache. The purpose of this field is to prevent replay attacks.</param>
        public override void GetEnclaveSession(string servername, string attestationUrl, out SqlEnclaveSession sqlEnclaveSession, out long counter)
        {
            GetEnclaveSessionHelper(servername, attestationUrl, true, out sqlEnclaveSession, out counter);
        }

        /// <summary>
        /// Gets the information that SqlClient subsequently uses to initiate the process of attesting the enclave and to establish a secure session with the enclave.
        /// </summary>
        /// <returns>The information SqlClient subsequently uses to initiate the process of attesting the enclave and to establish a secure session with the enclave.</returns>
        public override SqlEnclaveAttestationParameters GetAttestationParameters()
        {
            ECDiffieHellmanCng clientDHKey = new ECDiffieHellmanCng(DiffieHellmanKeySize);
            clientDHKey.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
            clientDHKey.HashAlgorithm = CngAlgorithm.Sha256;
            byte[] attestationParam = PrepareAttestationParameters();
            return new SqlEnclaveAttestationParameters(AzureBasedAttestationProtocolId, attestationParam, clientDHKey);
        }

        /// <summary>
        /// When overridden in a derived class, performs enclave attestation, generates a symmetric key for the session, creates a an enclave session and stores the session information in the cache.
        /// </summary>
        /// <param name="attestationInfo">The information the provider uses to attest the enclave and generate a symmetric key for the session. The format of this information is specific to the enclave attestation protocol.</param>
        /// <param name="clientDHKey">A Diffie-Hellman algorithm object that encapsulates a client-side key pair.</param>
        /// <param name="attestationUrl">The endpoint of an attestation service for attesting the enclave.</param>
        /// <param name="servername">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="sqlEnclaveSession">The requested enclave session or null if the provider doesn't implement session caching.</param>
        /// <param name="counter">A counter that the enclave provider is expected to increment each time SqlClient retrieves the session from the cache. The purpose of this field is to prevent replay attacks.</param>
        public override void CreateEnclaveSession(byte[] attestationInfo, ECDiffieHellmanCng clientDHKey, string attestationUrl, string servername, out SqlEnclaveSession sqlEnclaveSession, out long counter)
        {
            sqlEnclaveSession = null;
            counter = 0;
            try
            {
                AttestationInfoCacheItem attestationInfoCacheItem = AttestationInfoCache.Remove(Thread.CurrentThread.ManagedThreadId.ToString()) as AttestationInfoCacheItem;
                sqlEnclaveSession = GetEnclaveSessionFromCache(servername, attestationUrl, out counter);
                if (sqlEnclaveSession == null)
                {
                    if (attestationInfoCacheItem != null)
                    {
                        byte[] nonce = attestationInfoCacheItem.AttestNonce;

                        IdentityModelEventSource.ShowPII = true;

                        // Deserialize the payload
                        AzureAttestationInfo attestInfo = new AzureAttestationInfo(attestationInfo);

                        // Validate the attestation info
                        VerifyAzureAttestationInfo(attestationUrl, attestInfo.EnclaveType, attestInfo.AttestationToken.AttestationToken, attestInfo.Identity, nonce);

                        // Set up shared secret and validate signature
                        byte[] sharedSecret = GetSharedSecret(attestInfo.Identity, nonce, attestInfo.EnclaveType, attestInfo.EnclaveDHInfo, clientDHKey);

                        // add session to cache
                        sqlEnclaveSession = AddEnclaveSessionToCache(attestationUrl, servername, sharedSecret, attestInfo.SessionId, out counter);
                    }
                    else
                    {
                        throw new AlwaysEncryptedAttestationException(SR.FailToCreateEnclaveSession);
                    }
                }
            }
            finally
            {
                // As per current design, we want to minimize the number of create session calls. To acheive this we block all the GetEnclaveSession calls until the first call to
                // GetEnclaveSession -> GetAttestationParameters -> CreateEnclaveSession completes or the event timeout happen.
                // Case 1: When the first request successfully creates the session, then all outstanding GetEnclaveSession will use the current session.
                // Case 2: When the first request unable to create the encalve session (may be due to some error or the first request doesn't require enclave computation) then in those case we set the event timeout to 0.
                UpdateEnclaveSessionLockStatus(sqlEnclaveSession);
            }
        }

        /// <summary>
        /// When overridden in a derived class, looks up and evicts an enclave session from the enclave session cache, if the provider implements session caching.
        /// </summary>
        /// <param name="serverName">The name of the SQL Server instance containing the enclave.</param>
        /// <param name="enclaveAttestationUrl">The endpoint of an attestation service, SqlClient contacts to attest the enclave.</param>
        /// <param name="enclaveSessionToInvalidate">The session to be invalidated.</param>
        public override void InvalidateEnclaveSession(string serverName, string enclaveAttestationUrl, SqlEnclaveSession enclaveSessionToInvalidate)
        {
            InvalidateEnclaveSessionHelper(serverName, enclaveAttestationUrl, enclaveSessionToInvalidate);
        }
        #endregion

        #region Internal Class
        /// <summary>
        /// A model class respresenting the deserialization of the byte payload the client
        /// receives from SQL Server while setting up a session.
        /// Protocol format:
        /// 1. Total Size of the attestation blob as UINT
        /// 2. Size of Enclave RSA public key as UINT
        /// 3. Size of Attestation token as UINT
        /// 4. Enclave Type as UINT
        /// 5. Enclave RSA public key (raw key, of length #2)
        /// 6. Attestation token (of length #3)
        /// 7. Size of Session Id was UINT
        /// 8. Session id value
        /// 9. Size of enclave ECDH public key
        /// 10. Enclave ECDH public key (of length #9)
        /// </summary>
        internal class AzureAttestationInfo
        {
            public uint TotalSize { get; set; }

            /// <summary>
            /// The enclave's RSA Public Key.
            /// Needed to establish trust of the enclave.
            /// Used to verify the enclave's DiffieHellman info.
            /// </summary>
            public EnclavePublicKey Identity { get; set; }

            /// <summary>
            /// The enclave report from the SQL Server host's enclave.
            /// </summary>
            public AzureAttestationToken AttestationToken { get; set; }

            /// <summary>
            /// The id of the current session.
            /// Needed to set up a secure session between the client and enclave.
            /// </summary>
            public long SessionId { get; set; }

            public EnclaveType EnclaveType { get; set; }

            /// <summary>
            /// The DiffieHellman public key and signature of SQL Server host's enclave.
            /// Needed to set up a secure session between the client and enclave.
            /// </summary>
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
                    throw new AlwaysEncryptedAttestationException(String.Format(SR.FailToParseAttestationInfo, exception.Message));
                }
            }
        }

        /// <summary>
        /// A managed model representing the output of EnclaveGetAttestationReport
        /// https://msdn.microsoft.com/en-us/library/windows/desktop/mt844233(v=vs.85).aspx
        /// </summary>
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
        /// <summary>
        /// Prepare the attestation data in following format
        /// Attestation Url length
        /// Attestation Url
        /// Size of nonce
        /// Nonce value
        /// </summary>
        internal byte[] PrepareAttestationParameters()
        {
            AttestationInfoCacheItem attestationInfoCacheItem = AttestationInfoCache[Thread.CurrentThread.ManagedThreadId.ToString()] as AttestationInfoCacheItem;
            if (attestationInfoCacheItem != null)
            {
                // In c# strings are not null terminated, so adding the null termination before serializing it
                string attestationUrlLocal = attestationInfoCacheItem.AttestationUrl + char.MinValue;
                byte[] serializedAttestationUrl = Encoding.Unicode.GetBytes(attestationUrlLocal);
                byte[] serializedAttestationUrlLength = BitConverter.GetBytes(serializedAttestationUrl.Length);

                // serializing nonce
                byte[] serializedNonce = attestationInfoCacheItem.AttestNonce;
                byte[] serializedNonceLength = BitConverter.GetBytes(attestationInfoCacheItem.AttestNonce.Length);

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
                throw new AlwaysEncryptedAttestationException(SR.FailToCreateEnclaveSession);
            }
        }

        /// <summary>
        /// Performs Attestation per the protocol used by Azure Attestation Service
        /// </summary>
        /// <param name="attestationUrl">Url of the attestation service</param>
        /// <param name="enclaveType">Type of Enclave which we are attesting</param>
        /// <param name="attestationToken">The Azure enclave attestation token about the SQL Server host's enclave</param>
        /// <param name="enclavePublicKey">The enclave's RSA public key</param>
        /// <param name="nonce">Nonce value send by client during attestation</param>
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
                throw new AlwaysEncryptedAttestationException(String.Format(SR.AttestationTokenSignatureValidationFailed, exceptionMessage));
            }

            // Validate claims in the token
            ValidateAttestationClaims(enclaveType, attestationToken, enclavePublicKey, nonce);
        }

        /// <summary>
        /// Returns the innermost exception value
        /// </summary>
        /// <param name="exception">Current exception value</param>
        /// <returns></returns>
        private static string GetInnerMostExceptionMessage(Exception exception)
        {
            Exception exLocal = exception;
            while (exLocal.InnerException != null)
            {
                exLocal = exLocal.InnerException;
            }

            return exLocal.Message;
        }

        /// <summary>
        /// For the given attestation url it downloads the token signing keys from the well-known openid configuration end point.
        /// It also caches that information for 1 day to avoid DDOS attacks.
        /// </summary>
        /// <param name="url">Url of attestation service</param>
        /// <param name="forceUpdate">Re-download the signing keys irrespective of caching</param>
        /// <returns>OpenIdConnectConfiguration object for the signing keys</returns>
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
                    throw new AlwaysEncryptedAttestationException(String.Format(SR.GetAttestationTokenSigningKeysFailed, GetInnerMostExceptionMessage(exception)), exception);
                }

                OpenIdConnectConfigurationCache.Add(url, openIdConnectConfig, DateTime.UtcNow.AddDays(1));
            }

            return openIdConnectConfig;
        }

        /// <summary>
        /// Return the attestation instance url for given attestation url
        /// such as for https://sql.azure.attest.com/attest/SgxEnclave?api-version=2017-11-01
        /// It will return https://sql.azure.attest.com
        /// </summary>
        /// <param name="attestationUrl">Url of the attestation service</param>
        /// <returns>altered url</returns>
        private string GetAttestationInstanceUrl(string attestationUrl)
        {
            Uri attestationUri = new Uri(attestationUrl);
            return attestationUri.GetLeftPart(UriPartial.Authority);
        }

        /// <summary>
        /// Generate the list of valid issuer Url's (in case if tokenIssuerUrl is using default port)
        /// </summary>
        /// <param name="tokenIssuerUrl">Attestation token issuer url</param>
        /// <returns>List of valid issuer urls (can't be null/empty)</returns>
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

        /// <summary>
        /// Verifies the attestation token is signed by correct signing keys.
        /// </summary>
        /// <param name="attestationToken">Complete attestation token</param>
        /// <param name="tokenIssuerUrl">Attestation token issuer url</param>
        /// <param name="issuerSigningKeys">List of attestation token issuer signing keys</param>
        /// <param name="isKeySigningExpired">return if token signing key is expired</param>
        /// <param name="exceptionMessage">returns exception message to the caller</param>
        /// <returns></returns>
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
                throw new AlwaysEncryptedAttestationException(SR.ExpiredAttestationToken, securityException);
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
                throw new AlwaysEncryptedAttestationException(String.Format(SR.InvalidAttestationToken, GetInnerMostExceptionMessage(exception)));
            }

            return isSignatureValid;
        }

        /// <summary>
        /// Computes the SHA256 hash of the byte array
        /// </summary>
        /// <param name="data">Input for SHA256</param>
        /// <returns>SHA256 of the input data</returns>
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
                throw new AlwaysEncryptedAttestationException(SR.InvalidArgumentToSHA256, argumentException);
            }
            return result;
        }

        /// <summary>
        /// Validate the claims in the attestation token
        /// </summary>
        /// <param name="enclaveType">Type of Enclave which we are attesting</param>
        /// <param name="attestationToken">The Azure enclave attestation token about the SQL Server host's enclave</param>
        /// <param name="enclavePublicKey">The enclave's RSA public key</param>
        /// <param name="nonce">Nonce value send by client during attestation</param>
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
                throw new AlwaysEncryptedAttestationException(String.Format(SR.FailToParseAttestationToken, argumentException.Message));
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

        /// <summary>
        /// Validate the claim value against the actual data
        /// </summary>
        /// <param name="claims">Collection of all the claims in the JWT</param>
        /// <param name="claimName">Claim to validate</param>
        /// <param name="actualData">Data to validate against the claim</param>
        private void ValidateClaim(Dictionary<string, string> claims, string claimName, byte[] actualData)
        {
            // Get required claim data
            string claimData;
            bool hasClaim = claims.TryGetValue(claimName, out claimData);
            if (!hasClaim)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(SR.MissingClaimInAttestationToken, claimName));
            }

            // Get the Base64Url of the actual data and compare it with claim
            string encodedActualData = string.Empty;
            try
            {
                encodedActualData = Base64UrlEncoder.Encode(actualData);
            }
            catch (Exception)
            {
                throw new AlwaysEncryptedAttestationException(SR.InvalidArgumentToBase64UrlDecoder);
            }

            bool hasValidClaim = String.Equals(encodedActualData, claimData, StringComparison.InvariantCultureIgnoreCase);
            if (!hasValidClaim)
            {
                throw new AlwaysEncryptedAttestationException(String.Format(SR.InvalidClaimInAttestationToken, claimName, claimData));
            }
        }

        /// <summary>
        /// Derives the shared secret between the client and enclave.
        /// </summary>
        /// <param name="enclavePublicKey">The enclave's RSA public key</param>
        /// <param name="nonce">Nonce value send by client during attestation</param>
        /// <param name="enclaveType">Type of Enclave which we are attesting</param>
        /// <param name="enclaveDHInfo">The enclave's DiffieHellman key and signature info</param>
        /// <param name="clientDHKey">The client's DiffieHellman key info</param>
        /// <returns>A byte buffer containing the shared secret</returns>
        private byte[] GetSharedSecret(EnclavePublicKey enclavePublicKey, byte[] nonce, EnclaveType enclaveType, EnclaveDiffieHellmanInfo enclaveDHInfo, ECDiffieHellmanCng clientDHKey)
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
            CngKey cngkey = CngKey.Import(enclaveRsaPublicKey, CngKeyBlobFormat.GenericPublicBlob);
            using (RSACng rsacng = new RSACng(cngkey))
            {
                if (!rsacng.VerifyData(enclaveDHInfo.PublicKey, enclaveDHInfo.PublicKeySignature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                {
                    throw new ArgumentException(SR.GetSharedSecretFailed);
                }
            }

            CngKey key = CngKey.Import(enclaveDHInfo.PublicKey, CngKeyBlobFormat.GenericPublicBlob);
            return clientDHKey.DeriveKeyMaterial(key);
        }
        #endregion
    }
}
