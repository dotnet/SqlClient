// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted
{
    /// <summary>
    /// Encryption key class containing 4 keys. This class is used by AeadAes256CbcHmac256Algorithm
    /// 1) root key - Main key that is used to derive the keys used in the encryption algorithm
    /// 2) encryption key - A derived key that is used to encrypt the plain text and generate cipher text
    /// 3) mac_key - A derived key that is used to compute HMAC of the cipher text
    /// 4) iv_key - A derived key that is used to generate a synthetic IV from plain text data.
    /// </summary>
    internal sealed class AeadAes256CbcHmac256EncryptionKey : SymmetricKey
    {
        /// <summary>
        /// Key size in bits.
        /// </summary>
        public const int KeySizeInBits = 256;

        /// <summary>
        /// Key size in bytes.
        /// </summary>
        public const int KeySizeInBytes = KeySizeInBits / 8;

        /// <summary>
        /// <see cref="KeySizeInBits"/> as a string, for use in the salt formats below.
        /// </summary>
        private const string KeySizeInBitsString = "256";

        /// <summary>
        /// Encryption Key Salt. This is used to derive the encryption key from the root key.
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver17#step-2-computing-aes_256_cbc-ciphertext"/>
        private const string EncryptionKeySaltString = $"Microsoft SQL Server cell encryption key with encryption algorithm:{AeadAes256CbcHmac256Algorithm.AlgorithmName} and key length:{KeySizeInBitsString}";

        /// <summary>
        /// MAC Key Salt. This is used to derive the MAC key from the root key.
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver17#step-3-computing-mac"/>
        private const string MacKeySaltString = $"Microsoft SQL Server cell MAC key with encryption algorithm:{AeadAes256CbcHmac256Algorithm.AlgorithmName} and key length:{KeySizeInBitsString}";

        /// <summary>
        /// IV Key Salt. This is used to derive the IV key from the root key. This is only used for Deterministic encryption.
        /// </summary>
        /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography?view=sql-server-ver17#step-1-generating-the-initialization-vector-iv"/>
        private const string IvKeySaltString = $"Microsoft SQL Server cell IV key with encryption algorithm:{AeadAes256CbcHmac256Algorithm.AlgorithmName} and key length:{KeySizeInBitsString}";

        private static readonly byte[] s_encryptionKeySalt = Encoding.Unicode.GetBytes(EncryptionKeySaltString);
        private static readonly byte[] s_macKeySalt = Encoding.Unicode.GetBytes(MacKeySaltString);
        private static readonly byte[] s_ivKeySalt = Encoding.Unicode.GetBytes(IvKeySaltString);

        // Cached IncrementalHash, populated on demand when an Always Encrypted operation uses
        // this key to generate its authentication tag.
        // IncrementalHash is disposable, but never disposed. This doesn't mean that we leak this
        // instance - the keys are permanently held in memory by AeadAes256CbcHmac256Factory, so
        // don't have a lifecycle.
        private IncrementalHash? _cachedIncrementalHash;

#if DEBUG
        static AeadAes256CbcHmac256EncryptionKey()
        {
            Debug.Assert(KeySizeInBitsString == KeySizeInBits.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
#endif

        /// <summary>
        /// Derives all the required keys from the given root key
        /// </summary>
        /// <param name="rootKey">Root key used to derive all the required derived keys</param>
        public AeadAes256CbcHmac256EncryptionKey(byte[] rootKey) : base(rootKey)
        {
            // Key validation
            if (rootKey.Length != KeySizeInBytes)
            {
                throw SQL.InvalidKeySize(AeadAes256CbcHmac256Algorithm.AlgorithmName,
                                         rootKey.Length,
                                         KeySizeInBytes);
            }

            // Derive keys from the root key
            //
            // Derive encryption key
            byte[] buff1 = new byte[KeySizeInBytes];
            SqlSecurityUtility.GetHMACWithSHA256(s_encryptionKeySalt, RootKey, buff1);
            EncryptionKey = buff1;

            // Derive MAC key
            byte[] buff2 = new byte[KeySizeInBytes];
            SqlSecurityUtility.GetHMACWithSHA256(s_macKeySalt, RootKey, buff2);
            MacKey = buff2;

            // Derive IV key
            byte[] buff3 = new byte[KeySizeInBytes];
            SqlSecurityUtility.GetHMACWithSHA256(s_ivKeySalt, RootKey, buff3);
            IvKey = buff3;
        }

        /// <summary>
        /// Encryption key should be used for encryption and decryption
        /// </summary>
        public byte[] EncryptionKey { get; }

        /// <summary>
        /// MAC key should be used to compute and validate HMAC
        /// </summary>
        public byte[] MacKey { get; }

        /// <summary>
        /// IV key should be used to compute synthetic IV from a given plain text
        /// </summary>
        public byte[] IvKey { get; }

        /// <summary>
        /// Supplies a disposable scope to the caller which can be used to efficiently compute a HMAC.
        /// </summary>
        /// <returns>A disposable scope to be used for HMAC computation.</returns>
        public HmacScope GetHmac() => new(this);

        /// <summary>
        /// Provides a scope for incremental HMAC computation using the supplied encryption key.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Ensures proper disposal and/or reuse of cryptographic resources. Not thread-safe and
        /// should not be used after disposal.
        /// </para>
        /// <para>
        /// The AppendData and the GetHashAndReset methods call their corresponding methods on the
        /// IncrementalHash type. See those methods for documentation.
        /// </para>
        /// </remarks>
        public ref struct HmacScope : IDisposable
        {
            private bool _disposed;
            private bool _dirty;
            private readonly AeadAes256CbcHmac256EncryptionKey _sourceKey;
            private readonly IncrementalHash _incrementalHmac;

#if NET
            public readonly int HashLengthInBytes => _incrementalHmac.HashLengthInBytes;
#endif

            internal HmacScope(AeadAes256CbcHmac256EncryptionKey sourceKey)
            {
                IncrementalHash? cachedHmac = Interlocked.Exchange(ref sourceKey._cachedIncrementalHash, null);

                _incrementalHmac = cachedHmac ?? IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256, sourceKey.MacKey);
                _sourceKey = sourceKey;
                _dirty = false;
            }

            public void AppendData(byte[] data, int offset, int count)
            {
                _dirty = true;
                _incrementalHmac.AppendData(data, offset, count);
            }

#if NET
            public void AppendData(ReadOnlySpan<byte> data)
            {
                _dirty = true;
                _incrementalHmac.AppendData(data);
            }

            public int GetHashAndReset(Span<byte> destination)
            {
                int bytesWritten = _incrementalHmac.GetHashAndReset(destination);

                _dirty = false;
                return bytesWritten;
            }
#else
            public void AppendData(byte[] data)
            {
                _dirty = true;
                _incrementalHmac.AppendData(data);
            }

            public byte[] GetHashAndReset()
            {
                byte[] hash = _incrementalHmac.GetHashAndReset();

                _dirty = false;
                return hash;
            }
#endif

            /// <summary>
            /// Cleans this <see cref="HmacScope"/>.
            /// </summary>
            /// <remarks>
            /// Calling this method returns the backing IncrementalHash to the underlying encryption
            /// key if possible. If another thread has already filled the key's IncrementalHash slot,
            /// the backing IncrementalHash will simply be disposed. The backing IncrementalHash will
            /// also be disposed if the caller has not called <see cref="GetHashAndReset"/>.
            /// </remarks>
            public void Dispose()
            {
                if (!_disposed)
                {
                    if (!_dirty)
                    {
                        IncrementalHash? originalHmac = Interlocked.CompareExchange(ref _sourceKey._cachedIncrementalHash, _incrementalHmac, null);

                        if (originalHmac is not null)
                        {
                            _incrementalHmac.Dispose();
                        }
                    }
                    else
                    {
                        _incrementalHmac.Dispose();
                    }

                    _disposed = true;
                }
            }
        }
    }
}
