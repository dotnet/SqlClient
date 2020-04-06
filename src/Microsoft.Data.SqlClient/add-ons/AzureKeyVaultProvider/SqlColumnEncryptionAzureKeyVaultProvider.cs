// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.WebKey;
using Microsoft.Azure.KeyVault.Models;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    /// <summary>
    /// Implementation of column master key store provider that allows client applications to access data when a 
    /// column master key is stored in Microsoft Azure Key Vault. For more information on Always Encrypted, please refer to: https://aka.ms/AlwaysEncrypted.
    ///
    /// A Column Encryption Key encrypted with certificate store provider should be decryptable by this provider and vice versa.
    /// 
    /// Envelope Format for the encrypted column encryption key  
    ///           version + keyPathLength + ciphertextLength + keyPath + ciphertext +  signature
    /// 
    /// version: A single byte indicating the format version.
    /// keyPathLength: Length of the keyPath.
    /// ciphertextLength: ciphertext length
    /// keyPath: keyPath used to encrypt the column encryption key. This is only used for troubleshooting purposes and is not verified during decryption.
    /// ciphertext: Encrypted column encryption key
    /// signature: Signature of the entire byte array. Signature is validated before decrypting the column encryption key.
    /// </summary>
    /// <remarks>
	///	    <format type="text/markdown"><![CDATA[
    /// ## Remarks
    /// 
    /// **SqlColumnEncryptionAzureKeyVaultProvider** is implemented for Microsoft.Data.SqlClient driver and supports .NET Framework 4.6+ and .NET Core 2.1+.
    /// The provider name identifier for this implementation is "AZURE_KEY_VAULT" and it is not registered in driver by default.
    /// Client applications must call <xref=Microsoft.Data.SqlClient.SqlConnection.RegisterColumnEncryptionKeyStoreProviders> API only once in the lifetime of driver to register this custom provider by implementing a custom Authentication Callback mechanism.
    /// 
    /// Once the provider is registered, it can used to perform Always Encrypted operations by creating Column Master Key using Azure Key Vault Key Identifier URL.
    /// 
    /// ## Example
    /// 
    /// Sample C# applications to demonstrate Always Encrypted use with Azure Key Vault are available at links below:
    /// 
    /// - [Example: Using Azure Key Vault with Always Encrypted](~/connect/ado-net/sql/azure-key-vault-example.md)
    /// - [Example: Using Azure Key Vault with Always Encrypted with enclaves enabled](~/connect/ado-net/sql/azure-key-vault-enclave-example.md)
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
        /// Algorithm version
        /// </summary>
        private readonly byte[] firstVersion = new byte[] { 0x01 };

        /// <summary>
        /// Azure Key Vault Client
        /// </summary>
        public KeyVaultClient KeyVaultClient
        {
            get;
            private set;
        }

        /// <summary>
        /// List of Trusted Endpoints
        /// 
        /// </summary>
        public readonly string[] TrustedEndPoints;

        #endregion

        /// <summary>
        /// Constructor that takes a callback function to authenticate to AAD. This is used by KeyVaultClient at runtime 
        /// to authenticate to Azure Key Vault.
        /// </summary>
        /// <param name="authenticationCallback">Callback function used for authenticating to AAD.</param>
        public SqlColumnEncryptionAzureKeyVaultProvider(KeyVaultClient.AuthenticationCallback authenticationCallback) :
            this(authenticationCallback, Constants.AzureKeyVaultPublicDomainNames)
        { }

        /// <summary>
        /// Constructor that takes a callback function to authenticate to AAD and a trusted endpoint. 
        /// </summary>
        /// <param name="authenticationCallback">Callback function used for authenticating to AAD.</param>
        /// <param name="trustedEndPoint">TrustedEndpoint is used to validate the master key path</param>
        public SqlColumnEncryptionAzureKeyVaultProvider(KeyVaultClient.AuthenticationCallback authenticationCallback, string trustedEndPoint) :
            this(authenticationCallback, new[] { trustedEndPoint })
        { }

        /// <summary>
        /// Constructor that takes a callback function to authenticate to AAD and an array of trusted endpoints. The callback function 
        /// is used by KeyVaultClient at runtime to authenticate to Azure Key Vault.
        /// </summary>
        /// <param name="authenticationCallback">Callback function used for authenticating to AAD.</param>
        /// <param name="trustedEndPoints">TrustedEndpoints are used to validate the master key path</param>
        public SqlColumnEncryptionAzureKeyVaultProvider(KeyVaultClient.AuthenticationCallback authenticationCallback, string[] trustedEndPoints)
        {
            if (authenticationCallback == null)
            {
                throw new ArgumentNullException("authenticationCallback");
            }

            if (trustedEndPoints == null || trustedEndPoints.Length == 0)
            {
                throw new ArgumentException(Strings.InvalidTrustedEndpointsList);
            }

            foreach (string trustedEndPoint in trustedEndPoints)
            {
                if (String.IsNullOrWhiteSpace(trustedEndPoint))
                {
                    throw new ArgumentException(String.Format(Strings.InvalidTrustedEndpointTemplate, trustedEndPoint));
                }
            }

            KeyVaultClient = new KeyVaultClient(authenticationCallback);
            this.TrustedEndPoints = trustedEndPoints;
        }

        #region Public methods

        /// <summary>
        /// Uses an asymmetric key identified by the key path to sign the masterkey metadata consisting of (masterKeyPath, allowEnclaveComputations bit, providerName).
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] SignColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations)
        {
            var hash = ComputeMasterKeyMetadataHash(masterKeyPath, allowEnclaveComputations, isSystemOp: false);
            byte[] signedHash = AzureKeyVaultSignHashedData(hash, masterKeyPath);
            return signedHash;
        }

        /// <summary>
        /// Uses an asymmetric key identified by the key path to verify the masterkey metadata consisting of (masterKeyPath, allowEnclaveComputations bit, providerName).
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key. Path format is specific to a key store provider.</param>
        /// <param name="allowEnclaveComputations">Boolean indicating whether this key can be sent to trusted enclave</param>
        /// <param name="signature">Signature for the master key metadata</param>
        /// <returns>Boolean indicating whether the master key metadata can be verified based on the provided signature</returns>
        public override bool VerifyColumnMasterKeyMetadata(string masterKeyPath, bool allowEnclaveComputations, byte[] signature)
        {
            var hash = ComputeMasterKeyMetadataHash(masterKeyPath, allowEnclaveComputations, isSystemOp: true);
            return AzureKeyVaultVerifySignature(hash, signature, masterKeyPath);
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and decrypts an encrypted CEK with RSA encryption algorithm.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in AKV</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="encryptedColumnEncryptionKey">Encrypted Column Encryption Key</param>
        /// <returns>Plain text column encryption key</returns>
        public override byte[] DecryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
        {
            // Validate the input parameters
            this.ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);

            if (null == encryptedColumnEncryptionKey)
            {
                throw new ArgumentNullException(Constants.AeParamEncryptedCek, Strings.NullCekvInternal);
            }

            if (0 == encryptedColumnEncryptionKey.Length)
            {
                throw new ArgumentException(Strings.EmptyCekvInternal, Constants.AeParamEncryptedCek);
            }

            // Validate encryptionAlgorithm
            this.ValidateEncryptionAlgorithm(ref encryptionAlgorithm, isSystemOp: true);

            // Validate whether the key is RSA one or not and then get the key size
            int keySizeInBytes = GetAKVKeySize(masterKeyPath);

            // Validate and decrypt the EncryptedColumnEncryptionKey
            // Format is 
            //           version + keyPathLength + ciphertextLength + keyPath + ciphertext +  signature
            //
            // keyPath is present in the encrypted column encryption key for identifying the original source of the asymmetric key pair and 
            // we will not validate it against the data contained in the CMK metadata (masterKeyPath).

            // Validate the version byte
            if (encryptedColumnEncryptionKey[0] != firstVersion[0])
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidAlgorithmVersionTemplate,
                                                           encryptedColumnEncryptionKey[0].ToString(@"X2"),
                                                           firstVersion[0].ToString("X2")),
                                           Constants.AeParamEncryptedCek);
            }

            // Get key path length
            int currentIndex = firstVersion.Length;
            UInt16 keyPathLength = BitConverter.ToUInt16(encryptedColumnEncryptionKey, currentIndex);
            currentIndex += sizeof(UInt16);

            // Get ciphertext length
            UInt16 cipherTextLength = BitConverter.ToUInt16(encryptedColumnEncryptionKey, currentIndex);
            currentIndex += sizeof(UInt16);

            // Skip KeyPath
            // KeyPath exists only for troubleshooting purposes and doesnt need validation.
            currentIndex += keyPathLength;

            // validate the ciphertext length
            if (cipherTextLength != keySizeInBytes)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidCiphertextLengthTemplate,
                                                            cipherTextLength,
                                                            keySizeInBytes,
                                                            masterKeyPath),
                                            Constants.AeParamEncryptedCek);
            }

            // Validate the signature length
            int signatureLength = encryptedColumnEncryptionKey.Length - currentIndex - cipherTextLength;
            if (signatureLength != keySizeInBytes)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidSignatureLengthTemplate,
                                                            signatureLength,
                                                            keySizeInBytes,
                                                            masterKeyPath),
                                            Constants.AeParamEncryptedCek);
            }

            // Get ciphertext
            byte[] cipherText = new byte[cipherTextLength];
            Buffer.BlockCopy(encryptedColumnEncryptionKey, currentIndex, cipherText, 0, cipherTextLength);
            currentIndex += cipherTextLength;

            // Get signature
            byte[] signature = new byte[signatureLength];
            Buffer.BlockCopy(encryptedColumnEncryptionKey, currentIndex, signature, 0, signature.Length);

            // Compute the hash to validate the signature
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                sha256.TransformFinalBlock(encryptedColumnEncryptionKey, 0, encryptedColumnEncryptionKey.Length - signature.Length);
                hash = sha256.Hash;
            }

            if (null == hash)
            {
                throw new CryptographicException(Strings.NullHash);
            }

            // Validate the signature
            if (!AzureKeyVaultVerifySignature(hash, signature, masterKeyPath))
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidSignatureTemplate,
                                                            masterKeyPath),
                                            Constants.AeParamEncryptedCek);
            }

            // Decrypt the CEK
            return this.AzureKeyVaultUnWrap(masterKeyPath, encryptionAlgorithm, cipherText);
        }

        /// <summary>
        /// This function uses the asymmetric key specified by the key path
        /// and encrypts CEK with RSA encryption algorithm.
        /// </summary>
        /// <param name="masterKeyPath">Complete path of an asymmetric key in AKV</param>
        /// <param name="encryptionAlgorithm">Asymmetric Key Encryption Algorithm</param>
        /// <param name="columnEncryptionKey">Plain text column encryption key</param>
        /// <returns>Encrypted column encryption key</returns>
        public override byte[] EncryptColumnEncryptionKey(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
        {
            // Validate the input parameters
            this.ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp: true);

            if (null == columnEncryptionKey)
            {
                throw new ArgumentNullException(Constants.AeParamColumnEncryptionKey, Strings.NullCek);
            }

            if (0 == columnEncryptionKey.Length)
            {
                throw new ArgumentException(Strings.EmptyCek, Constants.AeParamColumnEncryptionKey);
            }

            // Validate encryptionAlgorithm
            this.ValidateEncryptionAlgorithm(ref encryptionAlgorithm, isSystemOp: false);

            // Validate whether the key is RSA one or not and then get the key size
            int keySizeInBytes = GetAKVKeySize(masterKeyPath);

            // Construct the encryptedColumnEncryptionKey
            // Format is 
            //          version + keyPathLength + ciphertextLength + ciphertext + keyPath + signature
            //
            // We currently only support one version
            byte[] version = new byte[] { firstVersion[0] };

            // Get the Unicode encoded bytes of cultureinvariant lower case masterKeyPath
            byte[] masterKeyPathBytes = Encoding.Unicode.GetBytes(masterKeyPath.ToLowerInvariant());
            byte[] keyPathLength = BitConverter.GetBytes((Int16)masterKeyPathBytes.Length);

            // Encrypt the plain text
            byte[] cipherText = this.AzureKeyVaultWrap(masterKeyPath, encryptionAlgorithm, columnEncryptionKey);
            byte[] cipherTextLength = BitConverter.GetBytes((Int16)cipherText.Length);

            if (cipherText.Length != keySizeInBytes)
            {
                throw new CryptographicException(Strings.CiphertextLengthMismatch);
            }

            // Compute hash
            // SHA-2-256(version + keyPathLength + ciphertextLength + keyPath + ciphertext) 
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                sha256.TransformBlock(version, 0, version.Length, version, 0);
                sha256.TransformBlock(keyPathLength, 0, keyPathLength.Length, keyPathLength, 0);
                sha256.TransformBlock(cipherTextLength, 0, cipherTextLength.Length, cipherTextLength, 0);
                sha256.TransformBlock(masterKeyPathBytes, 0, masterKeyPathBytes.Length, masterKeyPathBytes, 0);
                sha256.TransformFinalBlock(cipherText, 0, cipherText.Length);
                hash = sha256.Hash;
            }

            // Sign the hash
            byte[] signedHash = AzureKeyVaultSignHashedData(hash, masterKeyPath);

            if (signedHash.Length != keySizeInBytes)
            {
                throw new CryptographicException(Strings.HashLengthMismatch);
            }

            if (!this.AzureKeyVaultVerifySignature(hash, signedHash, masterKeyPath))
            {
                throw new CryptographicException(Strings.InvalidSignature);
            }

            // Construct the encrypted column encryption key
            // EncryptedColumnEncryptionKey = version + keyPathLength + ciphertextLength + keyPath + ciphertext +  signature
            int encryptedColumnEncryptionKeyLength = version.Length + cipherTextLength.Length + keyPathLength.Length + cipherText.Length + masterKeyPathBytes.Length + signedHash.Length;
            byte[] encryptedColumnEncryptionKey = new byte[encryptedColumnEncryptionKeyLength];

            // Copy version byte
            int currentIndex = 0;
            Buffer.BlockCopy(version, 0, encryptedColumnEncryptionKey, currentIndex, version.Length);
            currentIndex += version.Length;

            // Copy key path length
            Buffer.BlockCopy(keyPathLength, 0, encryptedColumnEncryptionKey, currentIndex, keyPathLength.Length);
            currentIndex += keyPathLength.Length;

            // Copy ciphertext length
            Buffer.BlockCopy(cipherTextLength, 0, encryptedColumnEncryptionKey, currentIndex, cipherTextLength.Length);
            currentIndex += cipherTextLength.Length;

            // Copy key path
            Buffer.BlockCopy(masterKeyPathBytes, 0, encryptedColumnEncryptionKey, currentIndex, masterKeyPathBytes.Length);
            currentIndex += masterKeyPathBytes.Length;

            // Copy ciphertext
            Buffer.BlockCopy(cipherText, 0, encryptedColumnEncryptionKey, currentIndex, cipherText.Length);
            currentIndex += cipherText.Length;

            // copy the signature
            Buffer.BlockCopy(signedHash, 0, encryptedColumnEncryptionKey, currentIndex, signedHash.Length);

            return encryptedColumnEncryptionKey;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// This function validates that the encryption algorithm is RSA_OAEP and if it is not,
        /// then throws an exception
        /// </summary>
        /// <param name="encryptionAlgorithm">Asymmetric key encryption algorithm</param>
        /// <param name="isSystemOp">is the operation a system operation</param>
        private void ValidateEncryptionAlgorithm(ref string encryptionAlgorithm, bool isSystemOp)
        {
            // This validates that the encryption algorithm is RSA_OAEP
            if (null == encryptionAlgorithm)
            {
                if (isSystemOp)
                {
                    throw new ArgumentNullException(Constants.AeParamEncryptionAlgorithm, Strings.NullAlgorithmInternal);
                }
                else
                {
                    throw new ArgumentNullException(Constants.AeParamEncryptionAlgorithm, Strings.NullAlgorithm);
                }
            }

            // Transform to standard format (dash instead of underscore) to support both "RSA_OAEP" and "RSA-OAEP"
            if (encryptionAlgorithm.Equals("RSA_OAEP", StringComparison.OrdinalIgnoreCase))
            {
                encryptionAlgorithm = JsonWebKeyEncryptionAlgorithm.RSAOAEP;
            }

            if (String.Equals(encryptionAlgorithm, JsonWebKeyEncryptionAlgorithm.RSAOAEP, StringComparison.OrdinalIgnoreCase) != true)
            {
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidKeyAlgorithm,
                                                            encryptionAlgorithm, "RSA_OAEP' or 'RSA-OAEP"), // For supporting both algorithm formats.
                                            Constants.AeParamEncryptionAlgorithm);
            }
        }

        private byte[] ComputeMasterKeyMetadataHash(string masterKeyPath, bool allowEnclaveComputations, bool isSystemOp)
        {
            // Validate the input parameters
            ValidateNonEmptyAKVPath(masterKeyPath, isSystemOp);

            // Validate whether the key is RSA one or not and then get the key size
            GetAKVKeySize(masterKeyPath);

            string masterkeyMetadata = ProviderName + masterKeyPath + allowEnclaveComputations;
            byte[] masterkeyMetadataBytes = Encoding.Unicode.GetBytes(masterkeyMetadata.ToLowerInvariant());

            // Compute hash
            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                sha256.TransformFinalBlock(masterkeyMetadataBytes, 0, masterkeyMetadataBytes.Length);
                hash = sha256.Hash;
            }
            return hash;
        }

        /// <summary>
        /// Checks if the Azure Key Vault key path is Empty or Null (and raises exception if they are).
        /// </summary>
        internal void ValidateNonEmptyAKVPath(string masterKeyPath, bool isSystemOp)
        {
            // throw appropriate error if masterKeyPath is null or empty
            if (String.IsNullOrWhiteSpace(masterKeyPath))
            {
                string errorMessage = null == masterKeyPath
                                      ? Strings.NullAkvPath
                                      : String.Format(CultureInfo.InvariantCulture, Strings.InvalidAkvPathTemplate, masterKeyPath);

                if (isSystemOp)
                {
                    throw new ArgumentNullException(Constants.AeParamMasterKeyPath, errorMessage);
                }

                throw new ArgumentException(errorMessage, Constants.AeParamMasterKeyPath);
            }

            Uri parsedUri;

            if (!Uri.TryCreate(masterKeyPath, UriKind.Absolute, out parsedUri))
            {
                // Return an error indicating that the AKV url is invalid.
                throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidAkvUrlTemplate, masterKeyPath), Constants.AeParamMasterKeyPath);
            }

            // A valid URI.
            // Check if it is pointing to trusted endpoint.
            foreach (string trustedEndPoint in this.TrustedEndPoints)
            {
                if (parsedUri.Host.EndsWith(trustedEndPoint, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // Return an error indicating that the AKV url is invalid.
            throw new ArgumentException(String.Format(CultureInfo.InvariantCulture, Strings.InvalidAkvKeyPathTrustedTemplate, masterKeyPath, String.Join(", ", this.TrustedEndPoints.ToArray())), Constants.AeParamMasterKeyPath);
        }

        /// <summary>
        /// Encrypt the text using specified Azure Key Vault key.
        /// </summary>
        /// <param name="masterKeyPath">Azure Key Vault key url.</param>
        /// <param name="encryptionAlgorithm">Encryption Algorithm.</param>
        /// <param name="columnEncryptionKey">Plain text Column Encryption Key.</param>
        /// <returns>Returns an encrypted blob or throws an exception if there are any errors.</returns>
        private byte[] AzureKeyVaultWrap(string masterKeyPath, string encryptionAlgorithm, byte[] columnEncryptionKey)
        {
            if (null == columnEncryptionKey)
            {
                throw new ArgumentNullException("columnEncryptionKey");
            }

            var wrappedKey = Task.Run(() => KeyVaultClient.WrapKeyAsync(masterKeyPath, encryptionAlgorithm, columnEncryptionKey)).Result;
            return wrappedKey.Result;
        }

        /// <summary>
        /// Encrypt the text using specified Azure Key Vault key.
        /// </summary>
        /// <param name="masterKeyPath">Azure Key Vault key url.</param>
        /// <param name="encryptionAlgorithm">Encryption Algorithm.</param>
        /// <param name="encryptedColumnEncryptionKey">Encrypted Column Encryption Key.</param>
        /// <returns>Returns the decrypted plaintext Column Encryption Key or throws an exception if there are any errors.</returns>
        private byte[] AzureKeyVaultUnWrap(string masterKeyPath, string encryptionAlgorithm, byte[] encryptedColumnEncryptionKey)
        {
            if (null == encryptedColumnEncryptionKey)
            {
                throw new ArgumentNullException("encryptedColumnEncryptionKey");
            }

            if (0 == encryptedColumnEncryptionKey.Length)
            {
                throw new ArgumentException(Strings.EncryptedCekEmpty);
            }


            var unwrappedKey = Task.Run(() => KeyVaultClient.UnwrapKeyAsync(masterKeyPath, encryptionAlgorithm, encryptedColumnEncryptionKey)).Result;

            return unwrappedKey.Result;
        }

        /// <summary>
        /// Generates signature based on RSA PKCS#v1.5 scheme using a specified Azure Key Vault Key URL. 
        /// </summary>
        /// <param name="dataToSign">Text to sign.</param>
        /// <param name="masterKeyPath">Azure Key Vault key url.</param>
        /// <returns>Signature</returns>
        private byte[] AzureKeyVaultSignHashedData(byte[] dataToSign, string masterKeyPath)
        {
            Debug.Assert((dataToSign != null) && (dataToSign.Length != 0));

            var signedData = Task.Run(() => KeyVaultClient.SignAsync(masterKeyPath, Constants.HashingAlgorithm, dataToSign)).Result;

            return signedData.Result;
        }

        /// <summary>
        /// Verifies the given RSA PKCSv1.5 signature.
        /// </summary>
        /// <param name="dataToVerify"></param>
        /// <param name="signature"></param>
        /// <param name="masterKeyPath">Azure Key Vault key url.</param>
        /// <returns>true if signature is valid, false if it is not valid</returns>
        private bool AzureKeyVaultVerifySignature(byte[] dataToVerify, byte[] signature, string masterKeyPath)
        {
            Debug.Assert((dataToVerify != null) && (dataToVerify.Length != 0));
            Debug.Assert((signature != null) && (signature.Length != 0));

            return Task.Run(() => KeyVaultClient.VerifyAsync(masterKeyPath, Constants.HashingAlgorithm, dataToVerify, signature)).Result;
        }

        /// <summary>
        /// Gets the public Key size in bytes
        /// </summary>
        /// <param name="masterKeyPath">Azure Key Vault Key path</param>
        /// <returns>Key size in bytes</returns>
        private int GetAKVKeySize(string masterKeyPath)
        {
            KeyBundle retrievedKey = Task.Run(() => KeyVaultClient.GetKeyAsync(masterKeyPath)).Result;

            if (!String.Equals(retrievedKey.Key.Kty, JsonWebKeyType.Rsa, StringComparison.InvariantCultureIgnoreCase) &&
                !String.Equals(retrievedKey.Key.Kty, JsonWebKeyType.RsaHsm, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new Exception(String.Format(CultureInfo.InvariantCulture, Strings.NonRsaKeyTemplate, retrievedKey.Key.Kty));
            }

            return retrievedKey.Key.N.Length;
        }

        #endregion
    }
}
