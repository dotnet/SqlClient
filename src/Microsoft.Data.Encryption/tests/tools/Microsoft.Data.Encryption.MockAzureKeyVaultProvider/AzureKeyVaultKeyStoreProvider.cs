using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using Microsoft.Data.Encryption.Cryptography;
using System;
using System.Linq;
using System.Text;

using static Microsoft.Data.Encryption.MockAzureKeyVaultProvider.AzureKeyVaultProviderTokenCredential;

namespace Microsoft.Data.Encryption.MockAzureKeyVaultProvider
{
    public class AzureKeyVaultKeyStoreProvider : EncryptionKeyStoreProvider
    {
        #region Properties

        /// <summary>
        /// Always Protected Param names for exec handling
        /// </summary>
        private const string providerName = "AZURE_KEY_VAULT";

        /// <summary>
        /// Algorithm version
        /// </summary>
        private readonly byte version = 1;

        /// <summary>
        /// Column Encryption Key Store Provider string
        /// </summary>
        public override string ProviderName { get => providerName; }

        /// <summary>
        /// Key storage and cryptography client
        /// </summary>
        private KeyCryptographer KeyCryptographer { get; set; }

        /// <summary>
        /// List of Trusted Endpoints
        /// </summary>
        private readonly string[] TrustedEndPoints;

        /// <summary>
        /// Azure Key Vault Domain Name
        /// </summary>
        internal static readonly string[] AzureKeyVaultPublicDomainNames = new[] {
            @"vault.azure.net", // Public Cloud
            @"vault.azure.cn", // Azure China
            @"vault.usgovcloudapi.net", // US Government
            @"vault.microsoftazure.de" // Azure Germany
        };

        #endregion

        #region Constructors

        public AzureKeyVaultKeyStoreProvider(TokenCredential tokenCredential) :
            this(tokenCredential, AzureKeyVaultPublicDomainNames)
        { }

        public AzureKeyVaultKeyStoreProvider(AuthenticationCallback authenticationCallback) :
            this(authenticationCallback, AzureKeyVaultPublicDomainNames)
        { }

        public AzureKeyVaultKeyStoreProvider(TokenCredential tokenCredential, string trustedEndPoint) :
            this(tokenCredential, new[] { trustedEndPoint })
        { }

        public AzureKeyVaultKeyStoreProvider(AuthenticationCallback authenticationCallback, string trustedEndPoint) :
            this(authenticationCallback, new[] { trustedEndPoint })
        { }

        public AzureKeyVaultKeyStoreProvider(TokenCredential tokenCredential, string[] trustedEndPoints)
        {
            tokenCredential.ValidateNotNull(nameof(tokenCredential));
            trustedEndPoints.ValidateNotNull(nameof(trustedEndPoints));
            trustedEndPoints.ValidateNotEmpty(nameof(trustedEndPoints));
            trustedEndPoints.ValidateNotNullOrWhitespaceForEach(nameof(trustedEndPoints));

            KeyCryptographer = new KeyCryptographer(tokenCredential);
            TrustedEndPoints = trustedEndPoints;
        }

        public AzureKeyVaultKeyStoreProvider(AuthenticationCallback authenticationCallback, string[] trustedEndPoints)
        {
            authenticationCallback.ValidateNotNull(nameof(authenticationCallback));
            trustedEndPoints.ValidateNotNull(nameof(trustedEndPoints));
            trustedEndPoints.ValidateNotEmpty(nameof(trustedEndPoints));
            trustedEndPoints.ValidateNotNullOrWhitespaceForEach(nameof(trustedEndPoints));

            KeyCryptographer = new KeyCryptographer(authenticationCallback);
            TrustedEndPoints = trustedEndPoints;
        }

        #endregion

        #region Public methods

        /// <summary>
        /// Uses an asymmetric key identified by the key path to sign the masterkey metadata consisting of (masterKeyPath, allowEnclaveComputations bit, providerName).
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] Sign(string masterKeyPath, bool allowEnclaveComputations)
        {
            masterKeyPath.ValidateNotNullOrWhitespace(nameof(masterKeyPath));
            ValidateMasterKeyPathFormat(masterKeyPath);
            ValidateMasterKeyIsTrusted(masterKeyPath, TrustedEndPoints);

            KeyCryptographer.AddKey(masterKeyPath);
            byte[] message = CompileMasterKeyMetadata(masterKeyPath, allowEnclaveComputations);
            return KeyCryptographer.SignData(message, masterKeyPath);
        }

        /// <summary>
        /// Uses an asymmetric key identified by the key path to verify the masterkey metadata consisting of (masterKeyPath, allowEnclaveComputations bit, providerName).
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <param name="signature">Signature for the master key metadata</param>
        /// <returns>Boolean indicating whether the master key metadata can be verified based on the provided signature</returns>
        public override bool Verify(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            masterKeyPath.ValidateNotNullOrWhitespace(nameof(masterKeyPath));
            ValidateMasterKeyPathFormat(masterKeyPath);
            ValidateMasterKeyIsTrusted(masterKeyPath, TrustedEndPoints);

            var key = Tuple.Create(ProviderName, masterKeyPath, allowEnclaveComputations, signature.ToHexString());
            return GetOrCreateSignatureVerificationResult(key, VerifyMasterKeyMetadata);

            bool VerifyMasterKeyMetadata()
            {
                KeyCryptographer.AddKey(masterKeyPath);
                byte[] message = CompileMasterKeyMetadata(masterKeyPath, allowEnclaveComputations);
                return KeyCryptographer.VerifyData(message, signature, masterKeyPath);
            }
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and decrypts an encrypted CEK with RSA encryption algorithm.
        /// Key format is (version + keyPathLength + ciphertextLength + keyPath + ciphertext +  signature)
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in AKV</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="encryptedColumnEncryptionKey">Encrypted Column Encryption Key</param>
        /// <returns>Plain text column encryption key</returns>
        public override byte[] UnwrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
        {
            encryptedColumnEncryptionKey.ValidateNotNull(nameof(encryptedColumnEncryptionKey));
            encryptedColumnEncryptionKey.ValidateNotEmpty(nameof(encryptedColumnEncryptionKey));

            return GetOrCreateDataEncryptionKey(encryptedColumnEncryptionKey.ToHexString(), DecryptEncryptionKey);

            byte[] DecryptEncryptionKey()
            {
                masterKeyPath.ValidateNotNullOrWhitespace(nameof(masterKeyPath));
                ValidateMasterKeyPathFormat(masterKeyPath);
                ValidateMasterKeyIsTrusted(masterKeyPath, TrustedEndPoints);

                KeyCryptographer.AddKey(masterKeyPath);
                KeyWrapAlgorithm keyWrapAlgorithm = KeyWrapAlgorithm.RsaOaep;
                EncryptedColumnEncryptionKey encryptionKey = new EncryptedColumnEncryptionKey(encryptedColumnEncryptionKey);
                ValidateSignature(masterKeyPath, encryptionKey);

                return KeyCryptographer.UnwrapKey(keyWrapAlgorithm, encryptionKey.Ciphertext, masterKeyPath);
            }
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and encrypts CEK with RSA encryption algorithm.
        /// Key format is (version + keyPathLength + ciphertextLength + ciphertext + keyPath + signature)
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in AKV</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="columnEncryptionKey">Plain text column encryption key</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] WrapKey(string masterKeyPath, KeyEncryptionKeyAlgorithm encryptionAlgorithm, byte[] columnEncryptionKey)
        {
            masterKeyPath.ValidateNotNullOrWhitespace(nameof(masterKeyPath));
            ValidateMasterKeyPathFormat(masterKeyPath);
            ValidateMasterKeyIsTrusted(masterKeyPath, TrustedEndPoints);
            columnEncryptionKey.ValidateNotNull(nameof(columnEncryptionKey));
            columnEncryptionKey.ValidateNotEmpty(nameof(columnEncryptionKey));

            KeyCryptographer.AddKey(masterKeyPath);
            KeyWrapAlgorithm keyWrapAlgorithm = KeyWrapAlgorithm.RsaOaep;

            byte[] versionByte = new byte[] { version };
            byte[] masterKeyPathBytes = Encoding.Unicode.GetBytes(masterKeyPath.ToLowerInvariant());
            byte[] keyPathLength = BitConverter.GetBytes((short)masterKeyPathBytes.Length);
            byte[] cipherText = KeyCryptographer.WrapKey(keyWrapAlgorithm, columnEncryptionKey, masterKeyPath);
            byte[] cipherTextLength = BitConverter.GetBytes((short)cipherText.Length);
            byte[] message = versionByte.Concat(keyPathLength).Concat(cipherTextLength).Concat(masterKeyPathBytes).Concat(cipherText).ToArray();
            byte[] signature = KeyCryptographer.SignData(message, masterKeyPath);

            return message.Concat(signature).ToArray();
        }

        #endregion

        #region Private methods

        private void ValidateSignature(string masterKeyPath, EncryptedColumnEncryptionKey key)
        {
            if (!KeyCryptographer.VerifyData(key.Message, key.Signature, masterKeyPath))
            {
                throw new ArgumentException("Invalid signature");
            }
        }

        internal static void ValidateMasterKeyIsTrusted(string masterKeyPath, string[] trustedEndpoints)
        {
            bool isParsedSuccessfully = Uri.TryCreate(masterKeyPath, UriKind.Absolute, out Uri parsedUri);
            bool isTrustedEndpoint = isParsedSuccessfully && trustedEndpoints.Any(e => parsedUri.Host.EndsWith(e, StringComparison.OrdinalIgnoreCase));

            if (!isTrustedEndpoint)
            {
                throw new ArgumentException($"The {nameof(masterKeyPath)} was not found in the accepted trusted endpoints. {trustedEndpoints}");
            }
        }

        internal static void ValidateMasterKeyPathFormat(string masterKeyPath)
        {
            bool isParsedSuccessfully = Uri.TryCreate(masterKeyPath, UriKind.Absolute, out Uri parsedUri);
            bool isValidFormat = isParsedSuccessfully && parsedUri.Segments.Length > 2;

            if (!isValidFormat)
            {
                throw new FormatException($"The {nameof(masterKeyPath)} is of an invalid format.");
            }
        }

        private byte[] CompileMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
        {
            string masterkeyMetadata = ProviderName + masterKeyPath + allowEnclaveComputations;
            return Encoding.Unicode.GetBytes(masterkeyMetadata.ToLowerInvariant());
        }

        #endregion
    }
}
