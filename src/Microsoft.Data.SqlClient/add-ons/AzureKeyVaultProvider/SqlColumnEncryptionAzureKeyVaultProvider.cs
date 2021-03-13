// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        /// <summary>
        /// List of Trusted Endpoints
        /// 
        /// </summary>
        public readonly string[] TrustedEndPoints;

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
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);

            // Also validates key is of RSA type.
            KeyCryptographer.AddKey(masterKeyPath);
            byte[] message = CompileMasterKeyMetadata(masterKeyPath, allowEnclaveComputations);
            return KeyCryptographer.VerifyData(message, signature, masterKeyPath);
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
            // Validate the input parameters
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);
            ValidateEncryptionAlgorithm(encryptionAlgorithm, isSystemOp: true);
            ValidateNotNull(encryptedColumnEncryptionKey, nameof(encryptedColumnEncryptionKey));
            ValidateNotEmpty(encryptedColumnEncryptionKey, nameof(encryptedColumnEncryptionKey));
            ValidateVersionByte(encryptedColumnEncryptionKey[0], s_firstVersion[0]);

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
                throw ADP.InvalidCipherTextLength(cipherTextLength, keySizeInBytes, masterKeyPath);
            }

            // Validate the signature length
            int signatureLength = encryptedColumnEncryptionKey.Length - currentIndex - cipherTextLength;
            if (signatureLength != keySizeInBytes)
            {
                throw ADP.InvalidSignatureLengthTemplate(signatureLength, keySizeInBytes, masterKeyPath);
            }

            // Get ciphertext
            byte[] cipherText = encryptedColumnEncryptionKey.Skip(currentIndex).Take(cipherTextLength).ToArray();
            currentIndex += cipherTextLength;

            // Get signature
            byte[] signature = encryptedColumnEncryptionKey.Skip(currentIndex).Take(signatureLength).ToArray();

            // Compute the message to validate the signature
            byte[] message = encryptedColumnEncryptionKey.Take(encryptedColumnEncryptionKey.Length - signatureLength).ToArray();

            if (null == message)
            {
                throw ADP.NullHashFound();
            }

            if (!KeyCryptographer.VerifyData(message, signature, masterKeyPath))
            {
                throw ADP.InvalidSignatureTemplate(masterKeyPath);
            }

            return KeyCryptographer.UnwrapKey(s_keyWrapAlgorithm, cipherText, masterKeyPath);
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
                throw ADP.CipherTextLengthMismatch();
            }

            // Compute message
            // SHA-2-256(version + keyPathLength + ciphertextLength + keyPath + ciphertext) 
            byte[] message = s_firstVersion.Concat(keyPathLength).Concat(cipherTextLength).Concat(masterKeyPathBytes).Concat(cipherText).ToArray();

            // Sign the message
            byte[] signature = KeyCryptographer.SignData(message, masterKeyPath);

            if (signature.Length != keySizeInBytes)
            {
                throw ADP.HashLengthMismatch();
            }

            ValidateSignature(masterKeyPath, message, signature);

            return message.Concat(signature).ToArray();
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
                throw ADP.InvalidAKVPath(masterKeyPath, isSystemOp);
            }

            if (!Uri.TryCreate(masterKeyPath, UriKind.Absolute, out Uri parsedUri) || parsedUri.Segments.Length < 3)
            {
                // Return an error indicating that the AKV url is invalid.
                throw ADP.InvalidAKVUrl(masterKeyPath);
            }

            // A valid URI.
            // Check if it is pointing to trusted endpoint.
            foreach (string trustedEndPoint in TrustedEndPoints)
            {
                if (parsedUri.Host.EndsWith(trustedEndPoint, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // Return an error indicating that the AKV url is invalid.
            throw ADP.InvalidAKVUrlTrustedEndpoints(masterKeyPath, string.Join(", ", TrustedEndPoints.ToArray()));
        }

        private void ValidateSignature(string masterKeyPath, byte[] message, byte[] signature)
        {
            if (!KeyCryptographer.VerifyData(message, signature, masterKeyPath))
            {
                throw ADP.InvalidSignature();
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
