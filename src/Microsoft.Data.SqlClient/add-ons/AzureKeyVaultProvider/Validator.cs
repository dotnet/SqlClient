// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted.AzureKeyVaultProvider
{
    internal static class Validator
    {
        internal static void ValidateNotNull(object parameter, string name)
        {
            if (null == parameter)
            {
                throw new ArgumentNullException(name);
            }
        }

        internal static void ValidateNotNullOrWhitespace(string parameter, string name)
        {
            if (string.IsNullOrWhiteSpace(parameter))
            {
                throw new ArgumentException(string.Format(Strings.NullOrWhitespaceArgument, name));
            }
        }

        internal static void ValidateNotEmpty(IList parameter, string name)
        {
            if (parameter.Count == 0)
            {
                throw new ArgumentException(string.Format(Strings.EmptyArgumentInternal, name));
            }
        }

        internal static void ValidateNotNullOrWhitespaceForEach(string[] parameters, string name)
        {
            foreach (var parameter in parameters)
            {
                if (null == parameter)
                {
                    throw new ArgumentException(Strings.InvalidTrustedEndpointTemplate);
                }
            }
        }

        internal static void ValidateEncryptionAlgorithm(string encryptionAlgorithm, bool isSystemOp)
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

            if (!encryptionAlgorithm.Equals("RSA_OAEP", StringComparison.OrdinalIgnoreCase)
                && !encryptionAlgorithm.Equals("RSA-OAEP", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Strings.InvalidKeyAlgorithm,
                                                            encryptionAlgorithm, "RSA_OAEP' or 'RSA-OAEP"), // For supporting both algorithm formats.
                                            Constants.AeParamEncryptionAlgorithm);
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
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, Strings.InvalidAlgorithmVersionTemplate,
                                                           encryptedByte.ToString(@"X2"),
                                                           firstVersionByte.ToString("X2")),
                                           Constants.AeParamEncryptedCek);
            }
        }
    }
}
