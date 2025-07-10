// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text;
using System.Threading;
using Azure.Core;
using Azure.Security.KeyVault.Keys.Cryptography;
using static Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider.Validator;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    /// <summary>
    /// Implementation of column master key store provider that allows client applications to access data when a 
    /// column master key is stored in Microsoft Azure Key Vault. 
    ///
    /// For more information on Always Encrypted, please refer to: https://aka.ms/AlwaysEncrypted.
    ///
    /// A Column Encryption Key encrypted with certificate store provider should be decryptable by this provider and vice versa.
    /// 
    /// Envelope Format for the encrypted column encryption key :
    ///           version + keyPathLength + ciphertextLength + keyPath + ciphertext + signature
    /// 
    /// - version: A single byte indicating the format version.
    /// - keyPathLength: Length of the keyPath.
    /// - ciphertextLength: ciphertext length
    /// - keyPath: keyPath used to encrypt the column encryption key. This is only used for troubleshooting purposes and is not verified during decryption.
    /// - ciphertext: Encrypted column encryption key
    /// - signature: Signature of the entire byte array. Signature is validated before decrypting the column encryption key.
    /// </summary>
    /// <remarks>
	///	    <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// For more information, see: [Using the Azure Key Vault Provider](/sql/connect/ado-net/sql/sqlclient-support-always-encrypted#using-the-azure-key-vault-provider) 
    /// ]]></format>
    /// </remarks>
    public class SqlColumnEncryptionAzureKeyVaultProvider : SqlColumnEncryptionKeyStoreProvider
    {
        #region Properties

        /// <summary>
        /// Column Encryption Key Store Provider string
        /// </summary>
        public const string ProviderName = "AZURE_KEY_VAULT";

        /// <summary>
        /// Key storage and cryptography client
        /// </summary>
        private AzureSqlKeyCryptographer KeyCryptographer { get; set; }

        /// <summary>
        /// Algorithm version
        /// </summary>
        private readonly static byte[] s_firstVersion = new byte[] { 0x01 };

        private readonly static KeyWrapAlgorithm s_keyWrapAlgorithm = KeyWrapAlgorithm.RsaOaep;

        private SemaphoreSlim _cacheSemaphore = new(1, 1);

        /// <summary>
        /// List of Trusted Endpoints
        /// 
        /// </summary>
        public readonly string[] TrustedEndPoints;

        /// <summary>
        /// A cache of column encryption keys (once they are decrypted). This is useful for rapidly decrypting multiple data values.
        /// </summary>
        private readonly LocalCache<string, byte[]> _columnEncryptionKeyCache = new() { TimeToLive = TimeSpan.FromHours(2) };

        /// <summary>
        /// A cache for storing the results of signature verification of column master key metadata.
        /// </summary>
        private readonly LocalCache<Tuple<string, bool, string>, bool> _columnMasterKeyMetadataSignatureVerificationCache =
            new(maxSizeLimit: 2000) { TimeToLive = TimeSpan.FromDays(10) };

        /// <summary>
        /// Gets or sets the lifespan of the decrypted column encryption key in the cache.
        /// Once the timespan has elapsed, the decrypted column encryption key is discarded
        /// and must be revalidated.
        /// </summary>
        /// <remarks>
        /// Internally, there is a cache of column encryption keys (once they are decrypted).
        /// This is useful for rapidly decrypting multiple data values. The default value is 2 hours.
        /// Setting the <see cref="ColumnEncryptionKeyCacheTtl"/> to zero disables caching.
        /// </remarks>
        public override TimeSpan? ColumnEncryptionKeyCacheTtl
        {
            get => _columnEncryptionKeyCache.TimeToLive;
            set => _columnEncryptionKeyCache.TimeToLive = value;
        }

        #endregion

        #region Constructors
        /// <summary>
        /// Constructor that takes an implementation of Token Credential that is capable of providing an OAuth Token.
        /// </summary>
        /// <param name="tokenCredential"></param>
        public SqlColumnEncryptionAzureKeyVaultProvider(TokenCredential tokenCredential) :
            this(tokenCredential, Constants.AzureKeyVaultPublicDomainNames)
        { }

        /// <summary>
        /// Constructor that takes an implementation of Token Credential that is capable of providing an OAuth Token and a trusted endpoint. 
        /// </summary>
        /// <param name="tokenCredential">Instance of an implementation of Token Credential that is capable of providing an OAuth Token.</param>
        /// <param name="trustedEndPoint">TrustedEndpoint is used to validate the master key path.</param>
        public SqlColumnEncryptionAzureKeyVaultProvider(TokenCredential tokenCredential, string trustedEndPoint) :
            this(tokenCredential, new[] { trustedEndPoint })
        { }

        /// <summary>
        /// Constructor that takes an instance of an implementation of Token Credential that is capable of providing an OAuth Token 
        /// and an array of trusted endpoints.
        /// </summary>
        /// <param name="tokenCredential">Instance of an implementation of Token Credential that is capable of providing an OAuth Token</param>
        /// <param name="trustedEndpoints">TrustedEndpoints are used to validate the master key path</param>
        public SqlColumnEncryptionAzureKeyVaultProvider(TokenCredential tokenCredential, string[] trustedEndpoints)
        {
            using var _ = AKVScope.Create();
            ValidateNotNull(tokenCredential, nameof(tokenCredential));
            ValidateNotNull(trustedEndpoints, nameof(trustedEndpoints));
            ValidateNotEmpty(trustedEndpoints, nameof(trustedEndpoints));
            ValidateNotNullOrWhitespaceForEach(trustedEndpoints, nameof(trustedEndpoints));

            KeyCryptographer = new AzureSqlKeyCryptographer(tokenCredential);
            TrustedEndPoints = trustedEndpoints;
        }
        #endregion

        #region Public methods

        /// <summary>
        /// Uses an asymmetric key identified by the key path to sign the master key metadata consisting of (masterKeyPath, allowEnclaveComputations bit, providerName).
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to a trusted enclave</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
        {
            using var _ = AKVScope.Create();
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: false);

            // Also validates key is of RSA type.
            KeyCryptographer.AddKey(masterKeyPath);
            byte[] message = CompileMasterKeyMetadata(masterKeyPath, allowEnclaveComputations);
            return KeyCryptographer.SignData(message, masterKeyPath);
        }

        /// <summary>
        /// Uses an asymmetric key identified by the key path to verify the master key metadata consisting of (masterKeyPath, allowEnclaveComputations bit, providerName).
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <param name="signature">Signature for the master key metadata</param>
        /// <returns>Boolean indicating whether the master key metadata can be verified based on the provided signature</returns>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            using var _ = AKVScope.Create();
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);

            var key = Tuple.Create(masterKeyPath, allowEnclaveComputations, ToHexString(signature));
            return GetOrCreateSignatureVerificationResult(key, VerifyColumnMasterKeyMetadata);

            bool VerifyColumnMasterKeyMetadata()
            {
                // Also validates key is of RSA type.
                KeyCryptographer.AddKey(masterKeyPath);
                byte[] message = CompileMasterKeyMetadata(masterKeyPath, allowEnclaveComputations);
                return KeyCryptographer.VerifyData(message, signature, masterKeyPath);
            }
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and decrypts an encrypted CEK with RSA encryption algorithm.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in Azure Key Vault</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="encryptedColumnEncryptionKey">Encrypted Column Encryption Key</param>
        /// <returns>Plain text column encryption key</returns>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
        {
            using var _ = AKVScope.Create();
            // Validate the input parameters
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: true);
            ValidateNotNull(encryptedColumnEncryptionKey, nameof(encryptedColumnEncryptionKey));
            ValidateNotEmpty(encryptedColumnEncryptionKey, nameof(encryptedColumnEncryptionKey));
            ValidateVersionByte(encryptedColumnEncryptionKey[0], s_firstVersion[0]);

            return GetOrCreateColumnEncryptionKey(ToHexString(encryptedColumnEncryptionKey), DecryptEncryptionKey);

            byte[] DecryptEncryptionKey()
            {
                // Also validates whether the key is RSA one or not and then get the key size
                KeyCryptographer.AddKey(masterKeyPath);

                int keySizeInBytes = KeyCryptographer.GetKeySize(masterKeyPath);

                // Get key path length
                int currentIndex = s_firstVersion.Length;
                ushort keyPathLength = BitConverter.ToUInt16(encryptedColumnEncryptionKey, currentIndex);
                currentIndex += sizeof(ushort);

                // Get ciphertext length
                ushort cipherTextLength = BitConverter.ToUInt16(encryptedColumnEncryptionKey, currentIndex);
                currentIndex += sizeof(ushort);

                // Skip KeyPath
                // KeyPath exists only for troubleshooting purposes and doesnt need validation.
                currentIndex += keyPathLength;

                // validate the ciphertext length
                if (cipherTextLength != keySizeInBytes)
                {
                    AKVEventSource.Log.TryTraceEvent("Cipher Text length: {0}", cipherTextLength);
                    AKVEventSource.Log.TryTraceEvent("keySizeInBytes: {0}", keySizeInBytes);
                    throw ADP.InvalidCipherTextLength(cipherTextLength, keySizeInBytes, masterKeyPath);
                }

                // Validate the signature length
                int signatureLength = encryptedColumnEncryptionKey.Length - currentIndex - cipherTextLength;
                if (signatureLength != keySizeInBytes)
                {
                    AKVEventSource.Log.TryTraceEvent("Signature length: {0}", signatureLength);
                    AKVEventSource.Log.TryTraceEvent("keySizeInBytes: {0}", keySizeInBytes);
                    throw ADP.InvalidSignatureLengthTemplate(signatureLength, keySizeInBytes, masterKeyPath);
                }

                // Get ciphertext
                byte[] cipherText = new byte[cipherTextLength];
                Array.Copy(encryptedColumnEncryptionKey, currentIndex, cipherText, 0, cipherTextLength);

                currentIndex += cipherTextLength;

                // Get signature
                byte[] signature = new byte[signatureLength];
                Buffer.BlockCopy(encryptedColumnEncryptionKey, currentIndex, signature, 0, signatureLength);

                // Compute the message to validate the signature
                byte[] message = new byte[encryptedColumnEncryptionKey.Length - signatureLength];
                Buffer.BlockCopy(encryptedColumnEncryptionKey, 0, message, 0, encryptedColumnEncryptionKey.Length - signatureLength);

                if (message == null)
                {
                    throw ADP.NullHashFound();
                }

                if (!KeyCryptographer.VerifyData(message, signature, masterKeyPath))
                {
                    AKVEventSource.Log.TryTraceEvent("Signature could not be verified.");
                    throw ADP.InvalidSignatureTemplate(masterKeyPath);
                }
                return KeyCryptographer.UnwrapKey(s_keyWrapAlgorithm, cipherText, masterKeyPath);
            }
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and encrypts CEK with RSA encryption algorithm.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in Azure Key Vault</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="columnEncryptionKey">The plaintext column encryption key.</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
        {
            using var _ = AKVScope.Create();
            // Validate the input parameters
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: true);
            ValidateNotNull(columnEncryptionKey, nameof(columnEncryptionKey));
            ValidateNotEmpty(columnEncryptionKey, nameof(columnEncryptionKey));

            // Also validates whether the key is RSA one or not and then get the key size
            KeyCryptographer.AddKey(masterKeyPath);
            int keySizeInBytes = KeyCryptographer.GetKeySize(masterKeyPath);

            // Construct the encryptedColumnEncryptionKey
            // Format is 
            //          s_firstVersion + keyPathLength + ciphertextLength + keyPath + ciphertext + signature

            // Get the Unicode encoded bytes of cultureinvariant lower case masterKeyPath
            byte[] masterKeyPathBytes = Encoding.Unicode.GetBytes(masterKeyPath.ToLowerInvariant());
            byte[] keyPathLength = BitConverter.GetBytes((short)masterKeyPathBytes.Length);

            // Encrypt the plain text
            byte[] cipherText = KeyCryptographer.WrapKey(s_keyWrapAlgorithm, columnEncryptionKey, masterKeyPath);
            byte[] cipherTextLength = BitConverter.GetBytes((short)cipherText.Length);

            if (cipherText.Length != keySizeInBytes)
            {
                AKVEventSource.Log.TryTraceEvent("Cipher Text length: {0}", cipherText.Length);
                AKVEventSource.Log.TryTraceEvent("keySizeInBytes: {0}", keySizeInBytes);
                throw ADP.CipherTextLengthMismatch();
            }

            // Compute message
            // SHA-2-256(version + keyPathLength + ciphertextLength + keyPath + ciphertext) 
            int messageLength = s_firstVersion.Length + keyPathLength.Length + cipherTextLength.Length + masterKeyPathBytes.Length + cipherText.Length;
            byte[] message = new byte[messageLength];
            int position = 0;

            Buffer.BlockCopy(s_firstVersion, 0, message, position, s_firstVersion.Length);
            position += s_firstVersion.Length;

            Buffer.BlockCopy(keyPathLength, 0, message, position, keyPathLength.Length);
            position += keyPathLength.Length;

            Buffer.BlockCopy(cipherTextLength, 0, message, position, cipherTextLength.Length);
            position += cipherTextLength.Length;

            Buffer.BlockCopy(masterKeyPathBytes, 0, message, position, masterKeyPathBytes.Length);
            position += masterKeyPathBytes.Length;

            Buffer.BlockCopy(cipherText, 0, message, position, cipherText.Length);
            position += cipherText.Length;

            // Sign the message
            byte[] signature = KeyCryptographer.SignData(message, masterKeyPath);

            if (signature.Length != keySizeInBytes)
            {
                throw ADP.HashLengthMismatch();
            }

            ValidateSignature(masterKeyPath, message, signature);

            byte[] retval = new byte[message.Length + signature.Length];
            Buffer.BlockCopy(message, 0, retval, 0, message.Length);
            Buffer.BlockCopy(signature, 0, retval, message.Length, signature.Length);

            return retval;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Checks if the Azure Key Vault key path is Empty or Null (and raises exception if they are).
        /// </summary>
        internal void ValidateNonEmptyAKVPath(string masterKeyPath, bool isSystemOp)
        {
            // throw appropriate error if masterKeyPath is null or empty
            if (string.IsNullOrWhiteSpace(masterKeyPath))
            {
                AKVEventSource.Log.TryTraceEvent("Azure Key Vault URI found null or empty.");
                throw ADP.InvalidAKVPath(masterKeyPath, isSystemOp);
            }

            if (!Uri.TryCreate(masterKeyPath, UriKind.Absolute, out Uri parsedUri) || parsedUri.Segments.Length < 3)
            {
                // Return an error indicating that the AKV url is invalid.
                AKVEventSource.Log.TryTraceEvent("URI could not be created with provided master key path: {0}", masterKeyPath);
                throw ADP.InvalidAKVUrl(masterKeyPath);
            }

            // A valid URI.
            // Check if it is pointing to trusted endpoint.
            foreach (string trustedEndPoint in TrustedEndPoints)
            {
                if (parsedUri.Host.EndsWith(trustedEndPoint, StringComparison.OrdinalIgnoreCase))
                {
                    AKVEventSource.Log.TryTraceEvent("Azure Key Vault URI validated successfully.");
                    return;
                }
            }

            // Return an error indicating that the AKV url is invalid.
            AKVEventSource.Log.TryTraceEvent("Master Key Path could not be validated as it does not end with trusted endpoints: {0}", masterKeyPath);
            throw ADP.InvalidAKVUrlTrustedEndpoints(masterKeyPath, string.Join(", ", TrustedEndPoints));
        }

        private void ValidateSignature(string masterKeyPath, byte[] message, byte[] signature)
        {
            if (!KeyCryptographer.VerifyData(message, signature, masterKeyPath))
            {
                AKVEventSource.Log.TryTraceEvent("Signature could not be verified.");
                throw ADP.InvalidSignature();
            }
            AKVEventSource.Log.TryTraceEvent("Signature verified successfully.");
        }

        private byte[] CompileMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
        {
            string masterkeyMetadata = ProviderName + masterKeyPath + allowEnclaveComputations;
            return Encoding.Unicode.GetBytes(masterkeyMetadata.ToLowerInvariant());
        }

        /// <summary>
        /// Converts the numeric value of each element of a specified array of bytes to its equivalent hexadecimal string representation.
        /// </summary>
        /// <param name="source">An array of bytes to convert.</param>
        /// <returns>A string of hexadecimal characters</returns>
        /// <remarks>
        /// Produces a string of hexadecimal character pairs preceded with "0x", where each pair represents the corresponding element in value; for example, "0x7F2C4A00".
        /// </remarks>
        private string ToHexString(byte[] source)
            => source is null ? null : "0x" + BitConverter.ToString(source).Replace("-", "");

        /// <summary>
        /// Returns the cached decrypted column encryption key, or unwraps the encrypted column encryption key if not present.
        /// </summary>
        /// <param name="encryptedColumnEncryptionKey">Encrypted Column Encryption Key</param>
        /// <param name="createItem">The delegate function that will decrypt the encrypted column encryption key.</param>
        /// <returns>The decrypted column encryption key.</returns>
        /// <remarks>
        ///
        /// </remarks>
        private byte[] GetOrCreateColumnEncryptionKey(string encryptedColumnEncryptionKey, Func<byte[]> createItem)
        {
            try
            {
                // Allow only one thread to access the cache at a time.
                _cacheSemaphore.Wait();
                return _columnEncryptionKeyCache.GetOrCreate(encryptedColumnEncryptionKey, createItem);
            }
            finally
            {
                // Release the semaphore to allow other threads to access the cache.
                _cacheSemaphore.Release();
            }
        }

        /// <summary>
        /// Returns the cached signature verification result, or proceeds to verify if not present.
        /// </summary>
        /// <param name="keyInformation">The encryptionKeyId, allowEnclaveComputations and hexadecimal signature.</param>
        /// <param name="createItem">The delegate function that will perform the verification.</param>
        /// <returns></returns>
        private bool GetOrCreateSignatureVerificationResult(Tuple<string, bool, string> keyInformation, Func<bool> createItem)
            => _columnMasterKeyMetadataSignatureVerificationCache.GetOrCreate(keyInformation, createItem);

        #endregion
    }
}
