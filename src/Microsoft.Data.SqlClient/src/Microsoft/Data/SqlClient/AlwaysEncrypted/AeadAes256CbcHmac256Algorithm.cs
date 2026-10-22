// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;

namespace Microsoft.Data.SqlClient.AlwaysEncrypted
{
    /// <summary>
    /// This class implements authenticated encryption algorithm with associated data as described in 
    /// http://tools.ietf.org/html/draft-mcgrew-aead-aes-cbc-hmac-sha2-05. More specifically this implements
    /// AEAD_AES_256_CBC_HMAC_SHA256 algorithm.
    /// </summary>
    internal partial class AeadAes256CbcHmac256Algorithm : EncryptionAlgorithm
    {
        /// <summary>
        /// Algorithm Name
        /// </summary>
        internal const string AlgorithmName = @"AEAD_AES_256_CBC_HMAC_SHA256";

        /// <summary>
        /// Algorithm version
        /// </summary>
        /// <remarks>
        /// For now, only one version exists. In future, this may be derived from the algorithm name.
        /// </remarks>
        public const byte CurrentVersion = 0x01;

        /// <summary>
        /// Authentication tag size in bytes
        /// </summary>
        #if NET
        private const int AuthenticationTagSizeInBytes = HMACSHA256.HashSizeInBytes;
        #else
        private const int AuthenticationTagSizeInBytes = 32;
        #endif

        /// <summary>
        /// IV size in bytes. This algorithm uses an IV size of 16 bytes.
        /// </summary>
        private const int IvSizeInBytes = 16;

        /// <summary>
        /// Block size in bytes. AES uses 16 byte blocks.
        /// </summary>
        private const int BlockSizeInBytes = 16;

        /// <summary>
        /// Minimum Length of cipherText. This value is 1 (version byte) + 32 (authentication tag) + 16 (IV) + 16 (minimum of 1 block of cipher Text)
        /// </summary>
        private const int MinimumCipherTextLengthInBytes = sizeof(byte) + AuthenticationTagSizeInBytes + IvSizeInBytes + BlockSizeInBytes;

        /// <summary>
        /// Cipher Mode. For this algorithm, we only use CBC mode.
        /// </summary>
        private const CipherMode _cipherMode = CipherMode.CBC;

        /// <summary>
        /// Padding mode. This algorithm uses PKCS7.
        /// </summary>
        private const PaddingMode _paddingMode = PaddingMode.PKCS7;

        /// <summary>
        /// Variable indicating whether this algorithm should work in Deterministic mode or Randomized mode.
        /// For deterministic encryption, we derive an IV from the plaintext data.
        /// For randomized encryption, we generate a cryptographically random IV.
        /// </summary>
        private readonly bool _isDeterministic;

        /// <summary>
        /// Column Encryption Key. This has a root key and three derived keys.
        /// </summary>
        private readonly AeadAes256CbcHmac256EncryptionKey _columnEncryptionKey;

        /// <summary>
        /// The pool of crypto providers to use for encrypt/decrypt operations.
        /// </summary>
        private readonly ConcurrentQueue<Aes> _cryptoProviderPool;

        /// <summary>
        /// Byte array with algorithm version used for authentication tag computation.
        /// </summary>
        private static readonly byte[] _version = [ CurrentVersion ];

        /// <summary>
        /// Byte array with algorithm version size used for authentication tag computation.
        /// </summary>
        private static readonly byte[] _versionSize = new byte[] { sizeof(byte) };

        /// <summary>
        /// Initializes a new instance of AeadAes256CbcHmac256Algorithm algorithm with a given key and encryption type
        /// </summary>
        /// <param name="encryptionKey">
        /// Root encryption key from which three other keys will be derived
        /// </param>
        /// <param name="encryptionType">Encryption Type, accepted values are Deterministic and Randomized. 
        /// For Deterministic encryption, a synthetic IV will be generated during encryption
        /// For Randomized encryption, a random IV will be generated during encryption.
        /// </param>
        internal AeadAes256CbcHmac256Algorithm(AeadAes256CbcHmac256EncryptionKey encryptionKey, EncryptionType encryptionType)
        {
            _columnEncryptionKey = encryptionKey;

            Debug.Assert(encryptionKey != null, "Null encryption key detected in AeadAes256CbcHmac256 algorithm");

            // Validate encryption type for this algorithm
            // This algorithm can only provide randomized or deterministic encryption types.
            if (encryptionType == EncryptionType.Deterministic)
            {
                _isDeterministic = true;
            }
            else
            {
                Debug.Assert(EncryptionType.Randomized == encryptionType, "Invalid Encryption Type detected in AeadAes256CbcHmac256Algorithm, this should've been caught in factory class");
            }

            _cryptoProviderPool = new ConcurrentQueue<Aes>();
        }

        /// <summary>
        /// Encryption Algorithm
        /// cell_iv = HMAC_SHA-2-256(iv_key, cell_data) truncated to 128 bits
        /// cell_ciphertext = AES-CBC-256(enc_key, cell_iv, cell_data) with PKCS7 padding.
        /// cell_tag = HMAC_SHA-2-256(mac_key, versionbyte + cell_iv + cell_ciphertext + versionbyte_length)
        /// cell_blob = versionbyte + cell_tag + cell_iv + cell_ciphertext
        /// </summary>
        /// <param name="plainText">Plaintext data to be encrypted</param>
        /// <returns>Returns the ciphertext corresponding to the plaintext.</returns>
        internal override byte[] EncryptData(byte[] plainText)
        {
            // Empty values get encrypted and decrypted properly for both Deterministic and Randomized encryptions.
            Debug.Assert(plainText != null);

            byte[] iv = new byte[IvSizeInBytes];

            // Prepare IV
            // Should be 1 single block (16 bytes)
            if (_isDeterministic)
            {
                SqlSecurityUtility.GetHMACWithSHA256(plainText, _columnEncryptionKey.IvKey, iv);
            }
            else
            {
                SqlSecurityUtility.GenerateRandomBytes(iv);
            }

            int numBlocks = plainText.Length / BlockSizeInBytes + 1;

            // Final blob we return = version + HMAC + iv + cipherText
            const int hmacStartIndex = 1;
            int authenticationTagLen = AuthenticationTagSizeInBytes;
            int ivStartIndex = hmacStartIndex + authenticationTagLen;
            int cipherStartIndex = ivStartIndex + IvSizeInBytes; // this is where hmac starts.

            // Output buffer size = size of VersionByte + Authentication Tag + IV + cipher Text blocks.
            int outputBufSize = sizeof(byte) + authenticationTagLen + iv.Length + (numBlocks * BlockSizeInBytes);
            byte[] outBuffer = new byte[outputBufSize];

            // Store the version and IV rightaway
            outBuffer[0] = CurrentVersion;
            Buffer.BlockCopy(iv, 0, outBuffer, ivStartIndex, iv.Length);


            // Try to get a provider from the pool.
            // If no provider is available, create a new one.
            if (!_cryptoProviderPool.TryDequeue(out Aes aesAlg))
            {
                aesAlg = Aes.Create();

                try
                {
                    // Set various algorithm properties
                    aesAlg.Key = _columnEncryptionKey.EncryptionKey;
                    aesAlg.Mode = _cipherMode;
                    aesAlg.Padding = _paddingMode;
                }
                catch (Exception)
                {
                    if (aesAlg != null)
                    {
                        aesAlg.Dispose();
                    }

                    throw;
                }
            }

            try
            {
                // Always set the IV since it changes from cell to cell.
                aesAlg.IV = iv;

                // Compute CipherText and authentication tag in a single pass
                using (ICryptoTransform encryptor = aesAlg.CreateEncryptor())
                {
                    Debug.Assert(encryptor.CanTransformMultipleBlocks, "AES Encryptor can transform multiple blocks");
                    int count = 0;
                    int cipherIndex = cipherStartIndex; // this is where cipherText starts
                    if (numBlocks > 1)
                    {
                        count = (numBlocks - 1) * BlockSizeInBytes;
                        cipherIndex += encryptor.TransformBlock(plainText, 0, count, outBuffer, cipherIndex);
                    }

                    byte[] buffTmp = encryptor.TransformFinalBlock(plainText, count, plainText.Length - count); // done encrypting
                    Buffer.BlockCopy(buffTmp, 0, outBuffer, cipherIndex, buffTmp.Length);
                    cipherIndex += buffTmp.Length;
                }

                using (HMACSHA256 hmac = new HMACSHA256(_columnEncryptionKey.MacKey))
                {
                    Debug.Assert(hmac.CanTransformMultipleBlocks, "HMAC can't transform multiple blocks");
                    hmac.TransformBlock(_version, 0, _version.Length, _version, 0);
                    hmac.TransformBlock(iv, 0, iv.Length, iv, 0);

                    // Compute HMAC on final block
                    hmac.TransformBlock(outBuffer, cipherStartIndex, numBlocks * BlockSizeInBytes, outBuffer, cipherStartIndex);
                    hmac.TransformFinalBlock(_versionSize, 0, _versionSize.Length);
                    byte[] hash = hmac.Hash;
                    Debug.Assert(hash.Length >= authenticationTagLen, "Unexpected hash size");
                    Buffer.BlockCopy(hash, 0, outBuffer, hmacStartIndex, authenticationTagLen);
                }
            }
            finally
            {
                // Return the provider to the pool.
                _cryptoProviderPool.Enqueue(aesAlg);
            }

            return outBuffer;
        }

        /// <summary>
        /// Decryption steps
        /// 1. Validate version byte
        /// 2. Validate Authentication tag
        /// 3. Decrypt the message
        /// </summary>
        /// <param name="cipherText"></param>
        /// <returns></returns>
        internal override byte[] DecryptData(byte[] cipherText)
        {
            Debug.Assert(cipherText != null);

            byte[] iv = new byte[IvSizeInBytes];

            int minimumCipherTextLength = MinimumCipherTextLengthInBytes;
            if (cipherText.Length < minimumCipherTextLength)
            {
                throw SQL.InvalidCipherTextSize(cipherText.Length, minimumCipherTextLength);
            }

            // Validate the version byte
            int startIndex = 0;
            if (cipherText[startIndex] != CurrentVersion)
            {
                // Cipher text was computed with a different algorithm version than this.
                throw SQL.InvalidAlgorithmVersion(cipherText[startIndex], CurrentVersion);
            }

            startIndex += 1;
            int authenticationTagOffset = 0;

            // Read authentication tag
            authenticationTagOffset = startIndex;
            startIndex += AuthenticationTagSizeInBytes; // authentication tag size is _KeySizeInBytes

            // Read cell IV
            Buffer.BlockCopy(cipherText, startIndex, iv, 0, iv.Length);
            startIndex += iv.Length;

            // Read encrypted text
            int cipherTextOffset = startIndex;
            int cipherTextCount = cipherText.Length - startIndex;

            // Compute authentication tag
            byte[] authenticationTag = PrepareAuthenticationTag(iv, cipherText, cipherTextOffset, cipherTextCount);
            if (!SqlSecurityUtility.CompareBytes(authenticationTag, cipherText, authenticationTagOffset, authenticationTag.Length))
            {
                // Potentially tampered data, throw an exception
                throw SQL.InvalidAuthenticationTag();
            }

            // Decrypt the text and return
            return DecryptData(iv, cipherText, cipherTextOffset, cipherTextCount);
        }

        /// <summary>
        /// Decrypts plain text data using AES in CBC mode
        /// </summary>
        /// <param name="cipherText"> cipher text data to be decrypted</param>
        /// <param name="iv">IV to be used for decryption</param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns>Returns decrypted plain text data</returns>
        private byte[] DecryptData(byte[] iv, byte[] cipherText, int offset, int count)
        {
            Debug.Assert((iv != null) && (cipherText != null));
            Debug.Assert(offset > -1 && count > -1);
            Debug.Assert((count + offset) <= cipherText.Length);

            byte[] plainText;

            // Try to get a provider from the pool.
            // If no provider is available, create a new one.
            if (!_cryptoProviderPool.TryDequeue(out Aes aesAlg))
            {
                aesAlg = Aes.Create();

                try
                {
                    // Set various algorithm properties
                    aesAlg.Key = _columnEncryptionKey.EncryptionKey;
                    aesAlg.Mode = _cipherMode;
                    aesAlg.Padding = _paddingMode;
                }
                catch (Exception)
                {
                    if (aesAlg != null)
                    {
                        aesAlg.Dispose();
                    }

                    throw;
                }
            }

            try
            {
                // Always set the IV since it changes from cell to cell.
                aesAlg.IV = iv;

                // Create the streams used for decryption. 
                using (MemoryStream msDecrypt = new MemoryStream())
                {
                    // Create an encryptor to perform the stream transform.
                    using (ICryptoTransform decryptor = aesAlg.CreateDecryptor())
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Write))
                        {
                            // Decrypt the secret message and get the plain text data
                            csDecrypt.Write(cipherText, offset, count);
                            csDecrypt.FlushFinalBlock();
                            plainText = msDecrypt.ToArray();
                        }
                    }
                }
            }
            finally
            {
                // Return the provider to the pool.
                _cryptoProviderPool.Enqueue(aesAlg);
            }

            return plainText;
        }

        /// <summary>
        /// Prepares an authentication tag.
        /// Authentication Tag = HMAC_SHA-2-256(mac_key, versionbyte + cell_iv + cell_ciphertext + versionbyte_length)
        /// </summary>
        /// <param name="iv"></param>
        /// <param name="cipherText"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        private byte[] PrepareAuthenticationTag(byte[] iv, byte[] cipherText, int offset, int length)
        {
            Debug.Assert(cipherText != null);

            byte[] computedHash;
            byte[] authenticationTag = new byte[AuthenticationTagSizeInBytes];

            // Raw Tag Length:
            //              1 for the version byte
            //              1 block for IV (16 bytes)
            //              cipherText.Length
            //              1 byte for version byte length

            using (HMACSHA256 hmac = new HMACSHA256(_columnEncryptionKey.MacKey))
            {
                int retVal = 0;
                retVal = hmac.TransformBlock(_version, 0, _version.Length, _version, 0);
                Debug.Assert(retVal == _version.Length);
                retVal = hmac.TransformBlock(iv, 0, iv.Length, iv, 0);
                Debug.Assert(retVal == iv.Length);
                retVal = hmac.TransformBlock(cipherText, offset, length, cipherText, offset);
                Debug.Assert(retVal == length);
                hmac.TransformFinalBlock(_versionSize, 0, _versionSize.Length);
                computedHash = hmac.Hash;
            }

            Debug.Assert(computedHash.Length >= authenticationTag.Length);
            Buffer.BlockCopy(computedHash, 0, authenticationTag, 0, authenticationTag.Length);
            return authenticationTag;
        }
    }
}
