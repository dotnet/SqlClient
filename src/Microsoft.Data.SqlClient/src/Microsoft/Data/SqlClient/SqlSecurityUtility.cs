// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Common;

namespace Microsoft.Data.SqlClient
{

    internal static class SqlSecurityUtility
    {

        static readonly ColumnMasterKeyMetadataSignatureVerificationCache ColumnMasterKeyMetadataSignatureVerificationCache = ColumnMasterKeyMetadataSignatureVerificationCache.Instance;

        /// <summary>
        /// Computes a keyed hash of a given text and returns. It fills the buffer "hash" with computed hash value.
        /// </summary>
        /// <param name="plainText">Plain text bytes whose hash has to be computed.</param>
        /// <param name="key">key used for the HMAC</param>
        /// <param name="hash">Output buffer where the computed hash value is stored. If its less that 64 bytes, the hash is truncated</param>
        /// <returns>HMAC value</returns>
        internal static void GetHMACWithSHA256(byte[] plainText, byte[] key, byte[] hash)
        {
            const int MaxSHA256HashBytes = 32;

            Debug.Assert(key != null && plainText != null);
            Debug.Assert(hash.Length != 0 && hash.Length <= MaxSHA256HashBytes);

            using (HMACSHA256 hmac = new HMACSHA256(key))
            {
                byte[] computedHash = hmac.ComputeHash(plainText);

                // Truncate the hash if needed
                Buffer.BlockCopy(computedHash, 0, hash, 0, hash.Length);
            }
        }

        /// <summary>
        /// Computes SHA256 hash of a given input
        /// </summary>
        /// <param name="input">input byte array which needs to be hashed</param>
        /// <returns>Returns SHA256 hash in a string form</returns>
        internal static string GetSHA256Hash(byte[] input)
        {
            Debug.Assert(input != null);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashValue = sha256.ComputeHash(input);
                return GetHexString(hashValue);
            }
        }

        /// <summary>
        /// Generates cryptographically random bytes
        /// </summary>
        /// <param name="randomBytes">No of cryptographically random bytes to be generated</param>
        /// <returns>A byte array containing cryptographically generated random bytes</returns>
        internal static void GenerateRandomBytes(byte[] randomBytes)
        {
            using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
        }

        /// <summary>
        /// Compares two byte arrays and returns true if all bytes are equal
        /// </summary>
        /// <param name="buffer1">input buffer</param>
        /// <param name="buffer2">another buffer to be compared against</param>
        /// <param name="buffer2Index"></param>
        /// <param name="lengthToCompare"></param>
        /// <returns>returns true if both the arrays have the same byte values else returns false</returns>
        internal static bool CompareBytes(byte[] buffer1, byte[] buffer2, int buffer2Index, int lengthToCompare)
        {
            if (null == buffer1 || null == buffer2)
            {
                return false;
            }

            Debug.Assert(buffer2Index > -1 && buffer2Index < buffer2.Length, "invalid index");// bounds on buffer2Index
            if ((buffer2.Length - buffer2Index) < lengthToCompare)
            {
                return false;
            }

            for (int index = 0; index < buffer1.Length && index < lengthToCompare; ++index)
            {
                if (buffer1[index] != buffer2[buffer2Index + index])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets hex representation of byte array.
        /// <param name="input">input byte array</param>
        /// </summary>
        internal static string GetHexString(byte[] input)
        {
            Debug.Assert(input != null);

            StringBuilder str = new();
            foreach (byte b in input)
            {
                str.AppendFormat(b.ToString(@"X2"));
            }

            return str.ToString();
        }

        /// <summary>
        /// Returns the caller's function name in the format of [ClassName].[FunctionName]
        /// </summary>
        internal static string GetCurrentFunctionName()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame stackFrame = stackTrace.GetFrame(1);
            MethodBase methodBase = stackFrame.GetMethod();
            return string.Format(@"{0}.{1}", methodBase.DeclaringType.Name, methodBase.Name);
        }

        /// <summary>
        /// Return the algorithm name mapped to an Id.
        /// </summary>
        /// <param name="cipherAlgorithmId"></param>
        /// <param name="cipherAlgorithmName"></param>
        /// <returns></returns>
        private static string ValidateAndGetEncryptionAlgorithmName(byte cipherAlgorithmId, string cipherAlgorithmName)
        {
            if (TdsEnums.CustomCipherAlgorithmId == cipherAlgorithmId)
            {
                if (null == cipherAlgorithmName)
                {
                    throw SQL.NullColumnEncryptionAlgorithm(SqlClientEncryptionAlgorithmFactoryList.GetInstance().GetRegisteredCipherAlgorithmNames());
                }

                return cipherAlgorithmName;
            }
            else if (TdsEnums.AEAD_AES_256_CBC_HMAC_SHA256 == cipherAlgorithmId)
            {
                return SqlAeadAes256CbcHmac256Algorithm.AlgorithmName;
            }
            else
            {
                throw SQL.UnknownColumnEncryptionAlgorithmId(cipherAlgorithmId, GetRegisteredCipherAlgorithmIds());
            }
        }

        /// <summary>
        /// Retrieves a string with comma separated list of registered algorithm Ids (enclosed in quotes).
        /// </summary>
        private static string GetRegisteredCipherAlgorithmIds()
        {
            return @"'1', '2'";
        }

        /// <summary>
        /// Encrypts the plaintext.
        /// </summary>
        internal static byte[] EncryptWithKey(byte[] plainText, SqlCipherMetadata md, SqlConnection connection, SqlCommand command)
        {
            // Initialize cipherAlgo if not already done.
            if (!md.IsAlgorithmInitialized())
            {
                DecryptSymmetricKey(md, connection, command);
            }

            Debug.Assert(md.IsAlgorithmInitialized(), "Encryption Algorithm is not initialized");
            byte[] cipherText = md.CipherAlgorithm.EncryptData(plainText); // this call succeeds or throws.
            if (null == cipherText || 0 == cipherText.Length)
            {
                throw SQL.NullCipherText();
            }

            return cipherText;
        }

        /// <summary>
        /// Gets a string with first/last 10 bytes in the buff (useful for exception handling).
        /// </summary>
        internal static string GetBytesAsString(byte[] buff, bool fLast, int countOfBytes)
        {
            int count = (buff.Length > countOfBytes) ? countOfBytes : buff.Length;
            int startIndex = 0;
            if (fLast)
            {
                startIndex = buff.Length - count;
                Debug.Assert(startIndex >= 0);
            }

            return BitConverter.ToString(buff, startIndex, count);
        }

        /// <summary>
        /// Decrypts the ciphertext.
        /// </summary>
        internal static byte[] DecryptWithKey(byte[] cipherText, SqlCipherMetadata md, SqlConnection connection, SqlCommand command)
        {
            // Initialize cipherAlgo if not already done.
            if (!md.IsAlgorithmInitialized())
            {
                DecryptSymmetricKey(md, connection, command);
            }

            Debug.Assert(md.IsAlgorithmInitialized(), "Decryption Algorithm is not initialized");
            try
            {
                byte[] plainText = md.CipherAlgorithm.DecryptData(cipherText); // this call succeeds or throws.
                if (null == plainText)
                {
                    throw SQL.NullPlainText();
                }

                return plainText;
            }
            catch (Exception e)
            {
                // compute the strings to pass
                string keyStr = GetBytesAsString(md.EncryptionKeyInfo.encryptedKey, fLast: true, countOfBytes: 10);
                string valStr = GetBytesAsString(cipherText, fLast: false, countOfBytes: 10);
                throw SQL.ThrowDecryptionFailed(keyStr, valStr, e);
            }
        }

        /// <summary>
        /// <para> Decrypts the symmetric key and saves it in metadata. In addition, initializes
        /// the SqlClientEncryptionAlgorithm for rapid decryption.</para>
        /// </summary>
        internal static void DecryptSymmetricKey(SqlCipherMetadata md, SqlConnection connection, SqlCommand command)
        {
            Debug.Assert(md is not null, "md should not be null in DecryptSymmetricKey.");

            SqlClientSymmetricKey symKey = null;
            SqlEncryptionKeyInfo encryptionkeyInfoChosen = null;

            DecryptSymmetricKey(md.EncryptionInfo, out symKey, out encryptionkeyInfoChosen, connection, command);

            // Given the symmetric key instantiate a SqlClientEncryptionAlgorithm object and cache it in metadata
            md.CipherAlgorithm = null;
            SqlClientEncryptionAlgorithm cipherAlgorithm = null;
            string algorithmName = ValidateAndGetEncryptionAlgorithmName(md.CipherAlgorithmId, md.CipherAlgorithmName); // may throw
            SqlClientEncryptionAlgorithmFactoryList.GetInstance().GetAlgorithm(symKey, md.EncryptionType, algorithmName, out cipherAlgorithm); // will validate algorithm name and type
            Debug.Assert(cipherAlgorithm is not null);
            md.CipherAlgorithm = cipherAlgorithm;
            md.EncryptionKeyInfo = encryptionkeyInfoChosen;
            return;
        }

        /// <summary>
        /// Decrypts the symmetric key and saves it in metadata.
        /// </summary>
        internal static void DecryptSymmetricKey(SqlTceCipherInfoEntry sqlTceCipherInfoEntry, out SqlClientSymmetricKey sqlClientSymmetricKey, out SqlEncryptionKeyInfo encryptionkeyInfoChosen, SqlConnection connection, SqlCommand command)
        {
            Debug.Assert(sqlTceCipherInfoEntry is not null, "sqlTceCipherInfoEntry should not be null in DecryptSymmetricKey.");
            Debug.Assert(sqlTceCipherInfoEntry.ColumnEncryptionKeyValues is not null,
                    "sqlTceCipherInfoEntry.ColumnEncryptionKeyValues should not be null in DecryptSymmetricKey.");

            sqlClientSymmetricKey = null;
            encryptionkeyInfoChosen = null;
            Exception lastException = null;
            SqlSymmetricKeyCache globalCekCache = SqlSymmetricKeyCache.GetInstance();

            foreach (SqlEncryptionKeyInfo keyInfo in sqlTceCipherInfoEntry.ColumnEncryptionKeyValues)
            {
                try
                {
                    sqlClientSymmetricKey = ShouldUseInstanceLevelProviderFlow(keyInfo.keyStoreName, connection, command) ?
                        GetKeyFromLocalProviders(keyInfo, connection, command) :
                        globalCekCache.GetKey(keyInfo, connection, command);
                    encryptionkeyInfoChosen = keyInfo;
                    break;
                }
                catch (Exception e)
                {
                    lastException = e;
                }
            }

            if (sqlClientSymmetricKey is null)
            {
                Debug.Assert(lastException is not null, "CEK decryption failed without raising exceptions");
                throw lastException;
            }

            Debug.Assert(encryptionkeyInfoChosen is not null, "encryptionkeyInfoChosen must have a value.");
        }

        private static SqlClientSymmetricKey GetKeyFromLocalProviders(SqlEncryptionKeyInfo keyInfo, SqlConnection connection, SqlCommand command)
        {
            string serverName = connection.DataSource;
            Debug.Assert(serverName is not null, @"serverName should not be null.");

            Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null, @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");

            ThrowIfKeyPathIsNotTrustedForServer(serverName, keyInfo.keyPath);
            if (!TryGetColumnEncryptionKeyStoreProvider(keyInfo.keyStoreName, out SqlColumnEncryptionKeyStoreProvider provider, connection, command))
            {
                throw SQL.UnrecognizedKeyStoreProviderName(keyInfo.keyStoreName,
                    SqlConnection.GetColumnEncryptionSystemKeyStoreProvidersNames(),
                    GetListOfProviderNamesThatWereSearched(connection, command));
            }

            // Decrypt the CEK
            // We will simply bubble up the exception from the DecryptColumnEncryptionKey function.
            byte[] plaintextKey;
            try
            {
                plaintextKey = provider.DecryptColumnEncryptionKey(keyInfo.keyPath, keyInfo.algorithmName, keyInfo.encryptedKey);
            }
            catch (Exception e)
            {
                // Generate a new exception and throw.
                string keyHex = GetBytesAsString(keyInfo.encryptedKey, fLast: true, countOfBytes: 10);
                throw SQL.KeyDecryptionFailed(keyInfo.keyStoreName, keyHex, e);
            }

            return new SqlClientSymmetricKey(plaintextKey);
        }

        /// <summary>
        /// Calculates the length of the Base64 string used to represent a byte[] with the specified length.
        /// </summary>
        /// <param name="byteLength"></param>
        /// <returns></returns>
        internal static int GetBase64LengthFromByteLength(int byteLength)
        {
            Debug.Assert(byteLength <= UInt16.MaxValue, @"Encrypted column encryption key cannot be larger than 65536 bytes");

            // Base64 encoding uses 1 character to encode 6 bits which means 4 characters for 3 bytes and pads to 4 byte multiples.
            return (int)((double)byteLength * 4 / 3) + 4;
        }

        /// <summary>
        /// Verifies Column Master Key Signature.
        /// </summary>
        internal static void VerifyColumnMasterKeySignature(string keyStoreName, string keyPath, bool isEnclaveEnabled, byte[] CMKSignature, SqlConnection connection, SqlCommand command)
        {
            bool isValidSignature = false;

            try
            {
                Debug.Assert(SqlConnection.ColumnEncryptionTrustedMasterKeyPaths is not null,
                        @"SqlConnection.ColumnEncryptionTrustedMasterKeyPaths should not be null");

                if (CMKSignature is null || CMKSignature.Length == 0)
                {
                    throw SQL.ColumnMasterKeySignatureNotFound(keyPath);
                }

                ThrowIfKeyPathIsNotTrustedForServer(connection.DataSource, keyPath);

                // Attempt to look up the provider and verify CMK Signature
                if (!TryGetColumnEncryptionKeyStoreProvider(keyStoreName, out SqlColumnEncryptionKeyStoreProvider provider, connection, command))
                {
                    throw SQL.InvalidKeyStoreProviderName(keyStoreName,
                        SqlConnection.GetColumnEncryptionSystemKeyStoreProvidersNames(),
                        GetListOfProviderNamesThatWereSearched(connection, command));
                }

                if (ShouldUseInstanceLevelProviderFlow(keyStoreName, connection, command))
                {
                    isValidSignature = provider.VerifyColumnMasterKeyMetadata(keyPath, isEnclaveEnabled, CMKSignature);
                }
                else
                {
                    bool? signatureVerificationResult = ColumnMasterKeyMetadataSignatureVerificationCache.GetSignatureVerificationResult(keyStoreName, keyPath, isEnclaveEnabled, CMKSignature);
                    if (signatureVerificationResult is null)
                    {
                        // We will simply bubble up the exception from VerifyColumnMasterKeyMetadata function.
                        isValidSignature = provider.VerifyColumnMasterKeyMetadata(keyPath, isEnclaveEnabled,
                                CMKSignature);

                        ColumnMasterKeyMetadataSignatureVerificationCache.AddSignatureVerificationResult(keyStoreName, keyPath, isEnclaveEnabled, CMKSignature, isValidSignature);
                    }
                    else
                    {
                        isValidSignature = signatureVerificationResult.Value;
                    }
                }
            }
            catch (Exception e)
            {
                throw SQL.UnableToVerifyColumnMasterKeySignature(e);
            }

            if (!isValidSignature)
            {
                throw SQL.ColumnMasterKeySignatureVerificationFailed(keyPath);
            }
        }

        // Instance-level providers will be used if at least one is registered on a connection or command and
        // the required provider is not a system provider. System providers are pre-registered globally and
        // must use the global provider flow
        private static bool ShouldUseInstanceLevelProviderFlow(string keyStoreName, SqlConnection connection, SqlCommand command)
        {
            return InstanceLevelProvidersAreRegistered(connection, command) &&
                !keyStoreName.StartsWith(ADP.ColumnEncryptionSystemProviderNamePrefix);
        }

        private static bool InstanceLevelProvidersAreRegistered(SqlConnection connection, SqlCommand command) =>
            connection.HasColumnEncryptionKeyStoreProvidersRegistered ||
            (command is not null && command.HasColumnEncryptionKeyStoreProvidersRegistered);

        internal static void ThrowIfKeyPathIsNotTrustedForServer(string serverName, string keyPath)
        {
            // Check against the trusted key paths
            // Get the List corresponding to the connected server
            if (SqlConnection.ColumnEncryptionTrustedMasterKeyPaths.TryGetValue(serverName, out IList<string> trustedKeyPaths))
            {
                // If the list is null or is empty or if the keyPath doesn't exist in the trusted key paths, then throw an exception.

                bool pathIsKnown = false;
                if (trustedKeyPaths != null)
                {
                    foreach (string candidate in trustedKeyPaths)
                    {
                        if (string.Equals(keyPath, candidate, StringComparison.InvariantCultureIgnoreCase))
                        {
                            pathIsKnown = true;
                            break;
                        }
                    }
                }

                if (!pathIsKnown)
                {
                    // throw an exception since the key path is not in the trusted key paths list for this server
                    throw SQL.UntrustedKeyPath(keyPath, serverName);
                }
            }
        }

        internal static bool TryGetColumnEncryptionKeyStoreProvider(string keyStoreName, out SqlColumnEncryptionKeyStoreProvider provider, SqlConnection connection, SqlCommand command)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(keyStoreName), "Provider name is invalid");

            if (SqlConnection.TryGetSystemColumnEncryptionKeyStoreProvider(keyStoreName, out provider))
            {
                return true;
            }

            // command may be null because some callers do not have a command object, eg SqlBulkCopy
            if (command is not null && command.HasColumnEncryptionKeyStoreProvidersRegistered)
            {
                return command.TryGetColumnEncryptionKeyStoreProvider(keyStoreName, out provider);
            }

            return connection.TryGetColumnEncryptionKeyStoreProvider(keyStoreName, out provider);
        }

        internal static List<string> GetListOfProviderNamesThatWereSearched(SqlConnection connection, SqlCommand command)
        {
            if (command is not null && command.HasColumnEncryptionKeyStoreProvidersRegistered)
            {
                return command.GetColumnEncryptionCustomKeyStoreProvidersNames();
            }

            return connection.GetColumnEncryptionCustomKeyStoreProvidersNames();
        }
    }
}
