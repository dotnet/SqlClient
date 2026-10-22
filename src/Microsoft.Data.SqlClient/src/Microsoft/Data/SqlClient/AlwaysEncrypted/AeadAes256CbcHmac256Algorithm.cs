// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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

            EncryptedValue value = EncryptedValue.Parse(cipherText, _columnEncryptionKey.MacKey);

            // Try to get a provider from the pool.
            // If no provider is available, create a new one.
            if (!_cryptoProviderPool.TryDequeue(out Aes aesAlg))
            {
                aesAlg = Aes.Create();

                try
                {
                    // Set various algorithm properties
                    aesAlg.Key = _columnEncryptionKey.EncryptionKey;
                    #if NETFRAMEWORK
                    aesAlg.Mode = _cipherMode;
                    aesAlg.Padding = _paddingMode;
                    #endif
                }
                catch (Exception)
                {
                    aesAlg?.Dispose();

                    throw;
                }
            }

            try
            {
                #if NET
                // @TODO: this performs a temporary copy to an ArrayPool-backed array in order to
                //        ensure that the returned array is the correct size. If the TdsParser write
                //        path supports ReadOnlyMemory<byte> in the future, we can eliminate this
                //        copy and return the decryption output directly.
                return aesAlg.DecryptCbc(value.CiphertextSpan, value.IvSpan, _paddingMode);
                #else
                // Always set the IV since it changes from cell to cell.
                aesAlg.IV = value.IvSpan.ToArray();

                // Create a decryptor to perform the transformation.
                using ICryptoTransform decryptor = aesAlg.CreateDecryptor();

                // Decrypt the secret message and get the plain text data
                value.GetCiphertext(out byte[] cipherTextBuffer, out int start, out int length);
                return decryptor.TransformFinalBlock(cipherTextBuffer, start, length);
                #endif
            }
            finally
            {
                // Return the provider to the pool.
                _cryptoProviderPool.Enqueue(aesAlg);
            }
        }
    }
}
