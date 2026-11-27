// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Security.Cryptography;

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted;

internal partial class AeadAes256CbcHmac256Algorithm
{
    /// <summary>
    /// Wraps a byte buffer which has been (or is in the process of being) encrypted using this
    /// algorithm.
    /// </summary>
    /// <remarks>
    /// This buffer has a fixed structure, and a fixed minimum size.
    /// <list type="number">
    /// <item>
    /// Version: 1 byte, always 0x01
    /// </item>
    /// <item>
    /// Authentication tag: 32 bytes, calculated as <c>HMAC-SHA-256(mac_key, Version + IV + Ciphertext
    /// + Version length)</c>, where the version length is also 0x01 (the size of a single <see cref="byte"/>.)
    /// </item>
    /// <item>
    /// IV: 16 bytes. If the encryption is randomized, this is random. If not, this is calculated as
    /// the first 16 bytes of <c>HMAC-SHA-256(iv_key, Plaintext)</c>.
    /// </item>
    /// <item>
    /// Ciphertext: variable length, contains a minimum of one 16-byte block. Calculated as
    /// <c>AES-CBC-256(enc_key, IV, Plaintext)</c>, with PKCS7 padding to 16-byte block boundaries.
    /// </item>
    /// </list>
    /// </remarks>
    /// <see href="https://learn.microsoft.com/en-us/sql/relational-databases/security/encryption/always-encrypted-cryptography"/>
    private readonly ref struct EncryptedValue
    {
        private const int VersionOffset = 0;
        private const int VersionLength = sizeof(byte);
        private const int AuthenticationTagOffset = VersionOffset + VersionLength;
        #if NET
        private const int AuthenticationTagLength = HMACSHA256.HashSizeInBytes;
        #else
        private const int AuthenticationTagLength = 32;
        #endif
        private const int IvOffset = AuthenticationTagOffset + AuthenticationTagLength;
        private const int IvLength = 16;
        private const int CiphertextOffset = IvOffset + IvLength;
        private const int MinimumEncryptedValueSize = CiphertextOffset + BlockSizeInBytes;

        #if NET
        private static ReadOnlySpan<byte> VersionLengthFieldBuffer => [VersionLength];
        #else
        #pragma warning disable IDE1006 // Naming prefix violation: missing prefix 's_'
        private static readonly byte[] VersionLengthFieldBuffer = [VersionLength];
        #pragma warning restore IDE1006
        #endif

        private readonly byte[] _valueBuffer;

        private int CiphertextLength => _valueBuffer.Length - CiphertextOffset;

        /// <summary>
        /// Determines whether the buffer and the buffer size is valid for cryptographic operations.
        /// For the buffer to be valid, it must be non-null. It must also be large enough to
        /// contain the version byte, authentication tag, IV and at least one block of ciphertext.
        /// </summary>
        public bool IsBufferSizeValid =>
            _valueBuffer is not null
            && _valueBuffer.Length >= MinimumEncryptedValueSize
            && (CiphertextLength % BlockSizeInBytes) == 0;

        /// <summary>
        /// Determines whether the value's version is the value expected (<see cref="CurrentVersion"/>.)
        /// </summary>
        public bool IsVersionValid =>
            Version == CurrentVersion;

        /// <summary>
        /// Version of the algorithm used to generate this encrypted value.
        /// </summary>
        public byte Version => _valueBuffer[VersionOffset];

        /// <summary>
        /// The IV to be used to populate the ciphertext.
        /// </summary>
        public Span<byte> IvSpan => _valueBuffer.AsSpan(IvOffset, IvLength);

        /// <summary>
        /// The ciphertext to be populated.
        /// </summary>
        public Span<byte> CiphertextSpan => _valueBuffer.AsSpan(CiphertextOffset);

        private EncryptedValue(byte[] buffer)
        {
            _valueBuffer = buffer;
        }

        /// <summary>
        /// Determines the size of the backing buffer needed to store <paramref name="plaintextLength"/>
        /// bytes of plaintext in an encrypted value.
        /// </summary>
        /// <param name="plaintextLength">Number of bytes of plaintext to store. Must not be negative.</param>
        /// <returns>A backing buffer size.</returns>
        public static int GetEncryptedValueLength(int plaintextLength)
        {
            Debug.Assert(plaintextLength >= 0);

            return checked(CiphertextOffset + ((plaintextLength / BlockSizeInBytes + 1) * BlockSizeInBytes));
        }

        /// <summary>
        /// Wraps an existing encrypted value backing buffer in an <see cref="EncryptedValue"/>
        /// instance, validating its size and structure.
        /// </summary>
        /// <param name="encryptedBuffer">The unparsed encrypted value.</param>
        /// <param name="key">The key containing the HMAC used to verify the authentication tag.</param>
        /// <returns>A valid <see cref="EncryptedValue"/>.</returns>
        public static EncryptedValue Parse(byte[] encryptedBuffer, AeadAes256CbcHmac256EncryptionKey key)
        {
            EncryptedValue ev = new(encryptedBuffer);

            if (!ev.IsBufferSizeValid)
            {
                throw SQL.InvalidCipherTextSize(encryptedBuffer.Length, MinimumEncryptedValueSize);
            }

            if (!ev.IsVersionValid)
            {
                throw SQL.InvalidAlgorithmVersion(ev.Version, CurrentVersion);
            }

            if (!ev.IsAuthenticationTagValid(key))
            {
                throw SQL.InvalidAuthenticationTag();
            }

            return ev;
        }

        /// <summary>
        /// Wraps the <paramref name="encryptedBuffer"/> backing buffer in an <see cref="EncryptedValue"/>,
        /// populating its version and setting its IV ready for deterministic encryption.
        /// </summary>
        /// <param name="encryptedBuffer">The backing buffer to populate.</param>
        /// <param name="plaintext">The plaintext value to use for the IV generation.</param>
        /// <param name="ivKey">The IV key.</param>
        /// <param name="encryptedValue">
        /// If this method returns true, a <see cref="EncryptedValue"/> which is ready for further
        /// operations.
        /// </param>
        /// <returns>true if the backing buffer passes the structural size checks, false otherwise.</returns>
        /// <remarks>
        /// A backing buffer must be exactly the length returned by <see cref="GetEncryptedValueLength"/>
        /// for the corresponding plaintext.
        /// </remarks>
        public static bool TryInitializeDeterministic(byte[] encryptedBuffer, byte[] plaintext, byte[] ivKey, out EncryptedValue encryptedValue)
        {
            EncryptedValue ev = new(encryptedBuffer);

            if (!ev.IsBufferSizeValid)
            {
                encryptedValue = default;
                return false;
            }

            ev._valueBuffer[0] = CurrentVersion;
            SqlSecurityUtility.GetHMACWithSHA256(plaintext, ivKey, ev.IvSpan);

            encryptedValue = ev;
            return true;
        }

        /// <summary>
        /// Wraps the <paramref name="encryptedBuffer"/> backing buffer in an <see cref="EncryptedValue"/>,
        /// populating its version and setting its IV ready for randomised encryption.
        /// </summary>
        /// <param name="encryptedBuffer">The backing buffer to populate.</param>
        /// <param name="encryptedValue">
        /// If this method returns true, a <see cref="EncryptedValue"/> which is ready for further
        /// operations.
        /// </param>
        /// <returns>true if the backing buffer passes the structural size checks, false otherwise.</returns>
        /// <remarks>
        /// A backing buffer must be exactly the length returned by <see cref="GetEncryptedValueLength"/>
        /// for the corresponding plaintext.
        /// </remarks>
        public static bool TryInitializeRandomized(byte[] encryptedBuffer, out EncryptedValue encryptedValue)
        {
            EncryptedValue ev = new(encryptedBuffer);

            if (!ev.IsBufferSizeValid)
            {
                encryptedValue = default;
                return false;
            }

            ev._valueBuffer[0] = CurrentVersion;
            #if NET
            RandomNumberGenerator.Fill(ev.IvSpan);
            #else
            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(ev._valueBuffer, IvOffset, IvLength);
            #endif

            encryptedValue = ev;
            return true;
        }

        /// <summary>
        /// Finalises the initialization of the backing buffer by writing its authentication tag,
        /// then returns the buffer.
        /// </summary>
        /// <param name="key">The key containing the HMAC used to calculate the authentication tag.</param>
        /// <returns>The completed <see cref="EncryptedValue"/>'s backing buffer.</returns>
        /// <remarks>
        /// This is intended to be the last operation on the <see cref="EncryptedValue"/> instance.
        /// The ciphertext must be fully populated before this method is called, and data in this
        /// instance's backing buffer must not be changed following this operation.
        /// </remarks>
        public byte[] Seal(AeadAes256CbcHmac256EncryptionKey key)
        {
            WriteAuthenticationTag(key, _valueBuffer.AsSpan(AuthenticationTagOffset, AuthenticationTagLength));

            return _valueBuffer;
        }

        #if NETFRAMEWORK
        /// <summary>
        /// On netfx, exposes a reference to a buffer containing the ciphertext along with its
        /// position within that buffer.
        /// </summary>
        /// <param name="ciphertextBuffer">The buffer containing the ciphertext.</param>
        /// <param name="offset">First byte of the ciphertext within <paramref name="ciphertextBuffer"/>.</param>
        /// <param name="length">Length of the ciphertext within <paramref name="ciphertextBuffer"/>.</param>
        /// <remarks>
        /// This should be used to perform cryptographic operations on the ciphertext. Both reading
        /// and writing to the ciphertext are permitted.
        /// </remarks>
        public void GetCiphertext(out byte[] ciphertextBuffer, out int offset, out int length)
        {
            ciphertextBuffer = _valueBuffer;
            offset = CiphertextOffset;
            length = CiphertextLength;
        }
        #endif

        private bool IsAuthenticationTagValid(AeadAes256CbcHmac256EncryptionKey key)
        {
            Debug.Assert(IsBufferSizeValid);

            ReadOnlySpan<byte> actualAuthTag = _valueBuffer.AsSpan(AuthenticationTagOffset, AuthenticationTagLength);
            Span<byte> expectedAuthTag = stackalloc byte[AuthenticationTagLength];

            WriteAuthenticationTag(key, expectedAuthTag);
            return actualAuthTag.SequenceEqual(expectedAuthTag);
        }

        private void WriteAuthenticationTag(AeadAes256CbcHmac256EncryptionKey key, Span<byte> buffer)
        {
            Debug.Assert(IsBufferSizeValid);

            using AeadAes256CbcHmac256EncryptionKey.HmacScope hmac = key.GetHmac();

            hmac.AppendData(_valueBuffer, VersionOffset, VersionLength);
            // The IV and the ciphertext are consecutive, so can be appended in a single call
            hmac.AppendData(_valueBuffer, IvOffset, IvLength + CiphertextLength);
            hmac.AppendData(VersionLengthFieldBuffer);

            Debug.Assert(buffer.Length == AuthenticationTagLength);
            #if NET
            Debug.Assert(hmac.HashLengthInBytes == AuthenticationTagLength);

            hmac.GetHashAndReset(buffer);
            #else
            byte[] copiedHash = hmac.GetHashAndReset();

            Debug.Assert(copiedHash.Length == AuthenticationTagLength);
            copiedHash.AsSpan().CopyTo(buffer);
            #endif
        }
    }
}
