// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted;

/// <summary>
/// Represents the parameters used to construct an encrypted column encryption key, used to encrypt and decrypt data in SQL Server Always Encrypted columns.
/// </summary>
/// <remarks>
/// An encrypted CEK is a byte array that contains the following structure:
/// <list type="number">
/// <item>
/// Version: 1 byte, always 0x01
/// </item>
/// <item>
/// Key path length: 2 bytes, length of the key path in bytes
/// </item>
/// <item>
/// Ciphertext length: 2 bytes, length of the ciphertext in bytes
/// </item>
/// <item>
/// Key path: variable length, Unicode-encoded string representing the key path
/// </item>
/// <item>
/// Ciphertext: variable length, encrypted data. Length determined by size of the RSA key used for encryption
/// </item>
/// <item>
/// Signature: variable length, digital signature of the encrypted CEK's SHA256 hash. Length determined by the size of the RSA key used for signing
/// </item>
/// </list>
/// <para>
/// This takes ownership of the RSA instance supplied to it, disposing of it when Dispose is called.
/// </para>
/// </remarks>
internal readonly ref struct EncryptedColumnEncryptionKeyParameters // : IDisposable
{
    private const byte AlgorithmVersion = 0x01;

    private const int AlgorithmOffset = 0;
    private const int KeyPathLengthOffset = AlgorithmOffset + sizeof(byte);
    private const int CiphertextLengthOffset = KeyPathLengthOffset + sizeof(ushort);
    private const int KeyPathOffset = CiphertextLengthOffset + sizeof(ushort);

#if NET
    private const int HashSize = SHA256.HashSizeInBytes;
#else
    private const int HashSize = 32;
#endif

    private static readonly HashAlgorithmName s_hashAlgorithm = HashAlgorithmName.SHA256;

    private readonly RSA _rsa;
    private readonly int _rsaKeySize;
    private readonly string _keyPath;
    private readonly string _keyType;
    private readonly string _keyPathReference;

    // @TODO: SqlColumnEncryptionCertificateStoreProvider, SqlColumnEncryptionCngProvider and SqlColumnEncryptionCspProvider should use this type.
    public EncryptedColumnEncryptionKeyParameters(RSA rsa, string keyPath, string keyType, string keyPathReference)
    {
        _rsa = rsa;
        _rsaKeySize = rsa.KeySize / 8;
        _keyPath = keyPath;
        
        Debug.Assert(keyType is SqlColumnEncryptionCertificateStoreProvider.MasterKeyType
            or SqlColumnEncryptionCngProvider.MasterKeyType or SqlColumnEncryptionCspProvider.MasterKeyType);
        Debug.Assert(keyPathReference is SqlColumnEncryptionCertificateStoreProvider.KeyPathReference
            or SqlColumnEncryptionCngProvider.KeyPathReference or SqlColumnEncryptionCspProvider.KeyPathReference);
        _keyType = keyType;
        _keyPathReference = keyPathReference;
    }

    public byte[] Encrypt(byte[] columnEncryptionKey)
    {
        ushort keyPathSize = (ushort)Encoding.Unicode.GetByteCount(_keyPath);
        int cekSize = sizeof(byte) + sizeof(ushort) + sizeof(ushort) + keyPathSize + _rsaKeySize + _rsaKeySize;
        byte[] encryptedColumnEncryptionKey = new byte[cekSize];
        int bytesWritten;
        int cipherTextOffset = KeyPathOffset + keyPathSize;
        int signatureOffset = cipherTextOffset + _rsaKeySize;

        // We currently only support one version
        encryptedColumnEncryptionKey[AlgorithmOffset] = AlgorithmVersion;

        // Write the key path length and the ciphertext length
        BinaryPrimitives.WriteUInt16LittleEndian(encryptedColumnEncryptionKey.AsSpan(KeyPathLengthOffset), keyPathSize);
        BinaryPrimitives.WriteUInt16LittleEndian(encryptedColumnEncryptionKey.AsSpan(CiphertextLengthOffset), (ushort)_rsaKeySize);

        // Write the unicode encoded bytes of the key path
        bytesWritten = Encoding.Unicode.GetBytes(_keyPath, 0, _keyPath.Length, encryptedColumnEncryptionKey, KeyPathOffset);
        Debug.Assert(bytesWritten == keyPathSize, @"Key path length does not match the expected length.");

        // Encrypt the column encryption key using RSA with OAEP padding.
        // In .NET Core, we can encrypt directly into the byte array, while in .NET Framework we need to allocate an intermediary and copy.
#if NET
        // CodeQL [SM03796] Required for an external standard: Always Encrypted only supports encrypting column encryption keys with RSA_OAEP(SHA1) (https://learn.microsoft.com/en-us/sql/t-sql/statements/create-column-encryption-key-transact-sql?view=sql-server-ver16)
        bytesWritten = _rsa.Encrypt(columnEncryptionKey, encryptedColumnEncryptionKey.AsSpan(cipherTextOffset), RSAEncryptionPadding.OaepSHA1);
#else
        // CodeQL [SM03796] Required for an external standard: Always Encrypted only supports encrypting column encryption keys with RSA_OAEP(SHA1) (https://learn.microsoft.com/en-us/sql/t-sql/statements/create-column-encryption-key-transact-sql?view=sql-server-ver16)
        byte[] cipherText = _rsa.Encrypt(columnEncryptionKey, RSAEncryptionPadding.OaepSHA1);
        bytesWritten = cipherText.Length;

        Buffer.BlockCopy(cipherText, 0, encryptedColumnEncryptionKey, cipherTextOffset, bytesWritten);
#endif
        Debug.Assert(bytesWritten == _rsaKeySize, @"Ciphertext length does not match the RSA key size.");

        // Compute the SHA256 hash of the encrypted CEK, (up to this point) then sign it and write the signature
        // In .NET Core, we can use a stack-allocated span for the hash, while in .NET Framework we need to allocate a byte array.
#if NET
        Span<byte> hash = stackalloc byte[HashSize];
        bytesWritten = SHA256.HashData(encryptedColumnEncryptionKey.AsSpan(0, signatureOffset), hash);
        Debug.Assert(bytesWritten == HashSize, @"Hash size does not match the expected size.");

        bytesWritten = _keyType == SqlColumnEncryptionCertificateStoreProvider.MasterKeyType
            ? _rsa.SignHash(hash, encryptedColumnEncryptionKey.AsSpan(signatureOffset), s_hashAlgorithm, RSASignaturePadding.Pkcs1)
            : _rsa.SignData(hash, encryptedColumnEncryptionKey.AsSpan(signatureOffset), s_hashAlgorithm, RSASignaturePadding.Pkcs1);
        Debug.Assert(bytesWritten == _rsaKeySize, @"Signature length does not match the RSA key size.");

#else
        byte[] hash;
        using (SHA256 sha256 = SHA256.Create())
        {
            sha256.TransformFinalBlock(encryptedColumnEncryptionKey, 0, signatureOffset);
            hash = sha256.Hash;
        }
        bytesWritten = hash.Length;
        Debug.Assert(bytesWritten == HashSize, @"Hash size does not match the expected size.");

        byte[] signedHash = _keyType == SqlColumnEncryptionCertificateStoreProvider.MasterKeyType
            ? _rsa.SignHash(hash, s_hashAlgorithm, RSASignaturePadding.Pkcs1)
            : _rsa.SignData(hash, s_hashAlgorithm, RSASignaturePadding.Pkcs1);
        bytesWritten = signedHash.Length;
        Debug.Assert(bytesWritten == _rsaKeySize, @"Signature length does not match the RSA key size.");

        Buffer.BlockCopy(signedHash, 0, encryptedColumnEncryptionKey, signatureOffset, bytesWritten);
#endif

        return encryptedColumnEncryptionKey;
    }

    public byte[] Decrypt(byte[] encryptedCek)
    {
        // Validate the version byte
        if (encryptedCek[0] != AlgorithmVersion)
        {
            throw SQL.InvalidAlgorithmVersionInEncryptedCEK(encryptedCek[0], AlgorithmVersion);
        }

        // Get key path length, but skip reading it. It exists only for troubleshooting purposes and doesn't need validation.
        ushort keyPathLength = BinaryPrimitives.ReadUInt16LittleEndian(encryptedCek.AsSpan(KeyPathLengthOffset));

        // Get ciphertext length, then validate it against the RSA key size
        ushort cipherTextLength = BinaryPrimitives.ReadUInt16LittleEndian(encryptedCek.AsSpan(CiphertextLengthOffset));

        if (cipherTextLength != _rsaKeySize)
        {
            throw SQL.InvalidCiphertextLengthInEncryptedCEK(_keyType, _keyPathReference, cipherTextLength, _rsaKeySize, _keyPath);
        }

        // Validate the signature length
        int cipherTextOffset = KeyPathOffset + keyPathLength;
        int signatureOffset = cipherTextOffset + cipherTextLength;
        int signatureLength = encryptedCek.Length - signatureOffset;

        if (signatureLength != _rsaKeySize)
        {
            throw SQL.InvalidSignatureInEncryptedCEK(_keyType, _keyPathReference, signatureLength, _rsaKeySize, _keyPath);
        }

        // Get the ciphertext and signature, then calculate the hash of the encrypted CEK.
        // In .NET Core most of these operations can be done with spans, while in .NET Framework we need to allocate byte arrays.
#if NET
        Span<byte> cipherText = encryptedCek.AsSpan(cipherTextOffset, cipherTextLength);
        Span<byte> signature = encryptedCek.AsSpan(signatureOffset);

        Span<byte> hash = stackalloc byte[HashSize];
        SHA256.HashData(encryptedCek.AsSpan(0, signatureOffset), hash);
#else
        byte[] cipherText = new byte[cipherTextLength];
        Buffer.BlockCopy(encryptedCek, cipherTextOffset, cipherText, 0, cipherText.Length);

        byte[] signature = new byte[signatureLength];
        Buffer.BlockCopy(encryptedCek, signatureOffset, signature, 0, signature.Length);

        byte[] hash;
        using (SHA256 sha256 = SHA256.Create())
        {
            sha256.TransformFinalBlock(encryptedCek, 0, signatureOffset);
            hash = sha256.Hash;
        }
        Debug.Assert(hash.Length == HashSize, @"hash length should be same as the signature length while decrypting encrypted column encryption key.");
#endif

        bool dataVerified = _keyType == SqlColumnEncryptionCertificateStoreProvider.MasterKeyType
            ? _rsa.VerifyHash(hash, signature, s_hashAlgorithm, RSASignaturePadding.Pkcs1)
            : _rsa.VerifyData(hash, signature, s_hashAlgorithm, RSASignaturePadding.Pkcs1);

        // Validate the signature
        if (!dataVerified)
        {
            throw SQL.InvalidSignature(_keyPath, _keyType);
        }

        // Decrypt the CEK
        // CodeQL [SM03796] Required for an external standard: Always Encrypted only supports encrypting column encryption keys with RSA_OAEP(SHA1) (https://learn.microsoft.com/en-us/sql/t-sql/statements/create-column-encryption-key-transact-sql?view=sql-server-ver16)
        return _rsa.Decrypt(cipherText, RSAEncryptionPadding.OaepSHA1);
    }

    public void Dispose() =>
        _rsa.Dispose();
}
