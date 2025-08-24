// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

#nullable enable

namespace Microsoft.Data.SqlClient.AlwaysEncrypted;

/// <summary>
/// Represents metadata about the column master key, to be signed or verified by an enclave.
/// </summary>
/// <remarks>
/// This metadata is a lower-case string which is laid out in the following format:
/// <list type="number">
/// <item>
/// Provider name. This always <see cref="SqlColumnEncryptionCertificateStoreProvider.ProviderName"/>.
/// </item>
/// <item>
/// Master key path. This will be in the format [LocalMachine|CurrentUser]/My/[SHA1 thumbprint].
/// </item>
/// <item>
/// Boolean to indicate whether the CMK supports enclave computations. This is either <c>true</c> or <c>false</c>.
/// </item>
/// </list>
/// <para>
/// This takes ownership of the RSA instance supplied to it, disposing of it when Dispose is called.
/// </para>
/// </remarks>
internal readonly ref struct ColumnMasterKeyMetadata // : IDisposable
{
    private static readonly HashAlgorithmName s_hashAlgorithm = HashAlgorithmName.SHA256;

#if NET
    [InlineArray(SHA256.HashSizeInBytes)]
    private struct Sha256Hash
    {
        private byte _elementTemplate;
    }

    private readonly Sha256Hash _hash;
#else
    private readonly byte[] _hash;
#endif
    private readonly RSA _rsa;

    // @TODO: SqlColumnEncryptionCertificateStoreProvider.SignMasterKeyMetadata and .VerifyMasterKeyMetadata should use this type.
    public ColumnMasterKeyMetadata(RSA rsa, string masterKeyPath, string providerName, bool allowEnclaveComputations)
    {
        // Lay the column master key metadata out in memory. Then, calculate the hash of this metadata ready for signature or verification.
        // .NET Core supports Spans in more places, allowing us to allocate on the stack for better performance. It also supports the
        // SHA256.HashData method, which saves allocations compared to instantiating a SHA256 object and calling TransformFinalBlock.

        // By this point, we know that we have a valid certificate, so the path is valid. The longest valid masterKeyPath is in one of the formats:
        // * [LocalMachine|CurrentUser]/My/[40 character SHA1 thumbprint]
        // * My/[40 character SHA1 thumbprint]
        // * [40 character SHA1 thumbprint]
        // ProviderName is a constant string of length 23 characters, and allowEnclaveComputations' longest value is 5 characters long. This
        // implies a maximum length of 84 characters for the masterKeyMetadata string - and by extension, 168 bytes for the Unicode-encoded
        // byte array. This is small enough to allocate on the stack, but we fall back to allocating a new char/byte array in case those assumptions fail.
        // It also implies that when masterKeyPath is converted to its invariant lowercase value, it will be the same length (because it's
        // an ASCII string.)
        Debug.Assert(masterKeyPath.Length == masterKeyPath.ToLowerInvariant().Length);

        ReadOnlySpan<char> enclaveComputationSpan = (allowEnclaveComputations ? bool.TrueString : bool.FalseString).AsSpan();
        int masterKeyMetadataLength = providerName.Length + masterKeyPath.Length + enclaveComputationSpan.Length;
        int byteCount;

#if NET
        const int CharStackAllocationThreshold = 128;
        const int ByteStackAllocationThreshold = CharStackAllocationThreshold * sizeof(char);

        Span<char> masterKeyMetadata = masterKeyMetadataLength <= CharStackAllocationThreshold
            ? stackalloc char[CharStackAllocationThreshold].Slice(0, masterKeyMetadataLength)
            : new char[masterKeyMetadataLength];
        Span<char> masterKeyMetadataSpan = masterKeyMetadata;
#else
        char[] masterKeyMetadata = new char[masterKeyMetadataLength];
        Span<char> masterKeyMetadataSpan = masterKeyMetadata.AsSpan();
#endif

        providerName.AsSpan().ToLowerInvariant(masterKeyMetadataSpan);
        masterKeyPath.AsSpan().ToLowerInvariant(masterKeyMetadataSpan.Slice(providerName.Length));
        enclaveComputationSpan.ToLowerInvariant(masterKeyMetadataSpan.Slice(providerName.Length + masterKeyPath.Length));
        byteCount = Encoding.Unicode.GetByteCount(masterKeyMetadata);

#if NET
        Span<byte> masterKeyMetadataBytes = byteCount <= ByteStackAllocationThreshold
            ? stackalloc byte[ByteStackAllocationThreshold].Slice(0, byteCount)
            : new byte[byteCount];

        Encoding.Unicode.GetBytes(masterKeyMetadata, masterKeyMetadataBytes);

        // Compute hash
        SHA256.HashData(masterKeyMetadataBytes, _hash);
#else
        byte[] masterKeyMetadataBytes = Encoding.Unicode.GetBytes(masterKeyMetadata);
        using SHA256 sha256 = SHA256.Create();

        // Compute hash
        sha256.TransformFinalBlock(masterKeyMetadataBytes, 0, masterKeyMetadataBytes.Length);
        _hash = sha256.Hash;
#endif

        _rsa = rsa;
    }

    public byte[] Sign() =>
        _rsa.SignHash(_hash, s_hashAlgorithm, RSASignaturePadding.Pkcs1);

    public bool Verify(byte[] signature) =>
        _rsa.VerifyHash(_hash, signature, s_hashAlgorithm, RSASignaturePadding.Pkcs1);

    public void Dispose() =>
        _rsa.Dispose();
}
