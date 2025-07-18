// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal static class Validator
    {
        internal static void ValidateNotNull(object parameter, string name)
        {
            if (parameter == null)
            {
                throw ADP.NullArgument(name);
            }
        }

        internal static void ValidateNotEmpty(IList parameter, string name)
        {
            if (parameter.Count == 0)
            {
                throw ADP.EmptyArgument(name);
            }
        }

        internal static void ValidateNotNullOrWhitespaceForEach(string[] parameters, string name)
        {
            if (parameters != null && parameters.Length > 0)
            {
                for (int index = 0; index < parameters.Length; index++)
                {
                    if (string.IsNullOrWhiteSpace(parameters[index]))
                    {
                        throw ADP.NullOrWhitespaceForEach(name);
                    }
                }
            }
        }

        internal static void ValidateEncryptionAlgorithm(string encryptionAlgorithm, bool isSystemOp)
        {
            // This validates that the encryption algorithm is RSA_OAEP
            if (encryptionAlgorithm == null)
            {
                throw ADP.NullAlgorithm(isSystemOp);
            }

            if (!encryptionAlgorithm.Equals("RSA_OAEP", StringComparison.OrdinalIgnoreCase)
                && !encryptionAlgorithm.Equals("RSA-OAEP", StringComparison.OrdinalIgnoreCase))
            {
                throw ADP.InvalidKeyAlgorithm(encryptionAlgorithm);
            }
        }

        internal static void ValidateVersionByte(byte encryptedByte, byte firstVersionByte)
        {
            // Validate and decrypt the EncryptedColumnEncryptionKey
            // Format is 
            //           version + keyPathLength + ciphertextLength + keyPath + ciphertext +  signature
            //
            // keyPath is present in the encrypted column encryption key for identifying the original source of the asymmetric key pair and 
            // we will not validate it against the data contained in the CMK metadata (masterKeyPath).

            // Validate the version byte
            if (encryptedByte != firstVersionByte)
            {
                throw ADP.InvalidAlgorithmVersion(encryptedByte.ToString(@"X2"), firstVersionByte.ToString("X2"));
            }
        }
    }

    internal static class ADP
    {
        internal static ArgumentNullException NullArgument(string name) =>
            new(name);

        internal static ArgumentException EmptyArgument(string name) =>
            new(string.Format(Strings.EmptyArgumentInternal, name));

        internal static ArgumentException NullOrWhitespaceForEach(string name) =>
            new(string.Format(Strings.NullOrWhitespaceForEach, name));

        internal static KeyNotFoundException MasterKeyNotFound(string masterKeyPath) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.MasterKeyNotFound, masterKeyPath));

        internal static KeyNotFoundException GetKeyFailed(string masterKeyPath) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.GetKeyFailed, masterKeyPath));

        internal static FormatException NonRsaKeyFormat(string keyType) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.NonRsaKeyTemplate, keyType));

        internal static ArgumentException InvalidCipherTextLength(ushort cipherTextLength, int keySizeInBytes, string masterKeyPath) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.InvalidCiphertextLengthTemplate,
                cipherTextLength, keySizeInBytes, masterKeyPath), Constants.AeParamEncryptedCek);

        internal static ArgumentNullException NullAlgorithm(bool isSystemOp) =>
            new(Constants.AeParamEncryptionAlgorithm, (isSystemOp ? Strings.NullAlgorithmInternal : Strings.NullAlgorithm));

        internal static ArgumentException InvalidKeyAlgorithm(string encryptionAlgorithm) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.InvalidKeyAlgorithm, encryptionAlgorithm,
                "RSA_OAEP' or 'RSA-OAEP")/* For supporting both algorithm formats.*/, Constants.AeParamEncryptionAlgorithm);

        internal static ArgumentException InvalidSignatureLengthTemplate(int signatureLength, int keySizeInBytes, string masterKeyPath) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.InvalidSignatureLengthTemplate,
                signatureLength, keySizeInBytes, masterKeyPath), Constants.AeParamEncryptedCek);

        internal static Exception InvalidAlgorithmVersion(string encryptedBytes, string firstVersionBytes) =>
            new ArgumentException(string.Format(CultureInfo.InvariantCulture, Strings.InvalidAlgorithmVersionTemplate,
                encryptedBytes, firstVersionBytes), Constants.AeParamEncryptedCek);

        internal static ArgumentException InvalidSignatureTemplate(string masterKeyPath) =>
            new ArgumentException(string.Format(CultureInfo.InvariantCulture, Strings.InvalidSignatureTemplate, masterKeyPath),
                Constants.AeParamEncryptedCek);

        internal static CryptographicException InvalidSignature() => new(Strings.InvalidSignature);

        internal static CryptographicException NullHashFound() => new(Strings.NullHash);

        internal static CryptographicException CipherTextLengthMismatch() => new(Strings.CipherTextLengthMismatch);

        internal static CryptographicException HashLengthMismatch() => new(Strings.HashLengthMismatch);

        internal static ArgumentException InvalidAKVPath(string masterKeyPath, bool isSystemOp)
        {
            string errorMessage = masterKeyPath == null
                ? Strings.NullAkvPath
                : string.Format(CultureInfo.InvariantCulture, Strings.InvalidAkvPathTemplate, masterKeyPath);
            if (isSystemOp)
            {
                return new ArgumentNullException(Constants.AeParamMasterKeyPath, errorMessage);
            }

            return new ArgumentException(errorMessage, Constants.AeParamMasterKeyPath);
        }

        internal static ArgumentException InvalidAKVUrl(string masterKeyPath) =>
            new(string.Format(CultureInfo.InvariantCulture, Strings.InvalidAkvUrlTemplate, masterKeyPath), Constants.AeParamMasterKeyPath);

        internal static ArgumentException InvalidAKVUrlTrustedEndpoints(string masterKeyPath, string endpoints) =>
            new ArgumentException(string.Format(CultureInfo.InvariantCulture, Strings.InvalidAkvKeyPathTrustedTemplate, masterKeyPath, endpoints),
                Constants.AeParamMasterKeyPath);
    }
}
