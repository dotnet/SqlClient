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
        /// Block size in bytes. AES uses 16 byte blocks.
        /// </summary>
        private const int BlockSizeInBytes = 16;

        #if NETFRAMEWORK
        /// <summary>
        /// Cipher Mode. For this algorithm, we only use CBC mode.
        /// </summary>
        private const CipherMode CipherMode = CipherMode.CBC;
        #endif

        /// <summary>
        /// Padding mode. This algorithm uses PKCS7.
        /// </summary>
        private const PaddingMode PaddingMode = PaddingMode.PKCS7;

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

            int valueBufferSize = EncryptedValue.GetEncryptedValueLength(plainText.Length);
            byte[] valueBuffer = new byte[valueBufferSize];

            EncryptedValue value;
            bool initializedValueWrapper = _isDeterministic
                ? EncryptedValue.TryInitializeDeterministic(
                    valueBuffer,
                    plainText,
                    _columnEncryptionKey.IvKey,
                    out value)
                : EncryptedValue.TryInitializeRandomized(
                    valueBuffer,
                    out value);

            Debug.Assert(initializedValueWrapper);

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
                    aesAlg.Mode = CipherMode;
                    aesAlg.Padding = PaddingMode;
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
                int bytesWritten = aesAlg.EncryptCbc(plainText, value.IvSpan, value.CiphertextSpan, PaddingMode);

                Debug.Assert(bytesWritten == value.CiphertextSpan.Length);
                #else
                // Always set the IV since it changes from cell to cell.
                aesAlg.IV = value.IvSpan.ToArray();

                value.GetCiphertext(out byte[] cipherTextBuffer, out int start, out int length);

                // Create an encryptor to perform the transformation.
                using ICryptoTransform encryptor = aesAlg.CreateEncryptor();

                Debug.Assert(encryptor.CanTransformMultipleBlocks, "AES Encryptor can transform multiple blocks");
                // There will always be at least one block, even when encrypting <BlockSizeInBytes of plaintext
                int numBlocks = plainText.Length / BlockSizeInBytes + 1;
                int plainTextCount = 0;
                int finalBlockStart = start;

                // If dealing with multiple blocks, use TransformBlock to write each block except for
                // the last one directly to the destination array.
                if (numBlocks > 1)
                {
                    plainTextCount = (numBlocks - 1) * BlockSizeInBytes;

                    int bytesWritten = encryptor.TransformBlock(plainText, 0, plainTextCount, cipherTextBuffer, start);

                    Debug.Assert(bytesWritten == plainTextCount);
                    Debug.Assert(bytesWritten < length);
                    finalBlockStart += bytesWritten;
                }

                // The final (or only) block may include padding. It can't be written directly, so
                // write to a staging array and copy to its final destination.
                byte[] finalBlock = encryptor.TransformFinalBlock(plainText, plainTextCount, plainText.Length - plainTextCount);

                Debug.Assert(finalBlock.Length == BlockSizeInBytes);
                Debug.Assert(finalBlockStart + finalBlock.Length == start + length);
                finalBlock.AsSpan().CopyTo(cipherTextBuffer.AsSpan(finalBlockStart, finalBlock.Length));
                #endif

                return value.Seal(_columnEncryptionKey.MacKey);
            }
            finally
            {
                // Return the provider to the pool.
                _cryptoProviderPool.Enqueue(aesAlg);
            }
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
                    aesAlg.Mode = CipherMode;
                    aesAlg.Padding = PaddingMode;
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
                return aesAlg.DecryptCbc(value.CiphertextSpan, value.IvSpan, PaddingMode);
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
