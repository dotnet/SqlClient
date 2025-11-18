// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.Tests.Common.Fixtures;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.Fixtures.AlwaysEncrypted;

/// <summary>
/// Provides a fixture for managing the certificate used by SQL Server's native code
/// to encrypt the column encryption keys.
/// </summary>
public sealed class NativeColumnEncryptionKeyCertificateBaselineFixture : CertificateFixtureBase
{
    private const string NativeCertificatePassword = "P@zzw0rD!SqlvN3x+";

    /// <summary>
    /// The native column encryption key baseline data.
    /// </summary>
    /// <remarks>
    /// Parameter 1: The key path to the certificate used to encrypt the column encryption key.
    /// Parameter 2: The encrypted column encryption key as produced by SQL Server's native code.
    /// Parameter 3: The expected plaintext column encryption key.
    /// </remarks>
    /// <seealso cref="UnitTests.AlwaysEncrypted.NativeColumnEncryptionKeyBaseline.Baseline_FinalCell_Decrypts_To_Known_Plaintext" />
    public static TheoryData<string, byte[], byte[]> NativeCEKBaselineData =>
        new()
        {
            {
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_CertificatePath1,
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_FinalCell1,
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_Plaintext1
            },
            {
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_CertificatePath2,
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_FinalCell2,
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_Plaintext2
            },
            {
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_CertificatePath3,
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_FinalCell3,
                Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_Plaintext3
            }
        };

    public string Thumbprint { get; }

    public NativeColumnEncryptionKeyCertificateBaselineFixture()
        : base()
    {
        byte[] nativeCertificateBaseline = Resources.AlwaysEncrypted_NativeColumnEncryptionKeyBaseline_Certificate;
#if NET9_0_OR_GREATER
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(nativeCertificateBaseline, NativeCertificatePassword,
            keyStorageFlags: X509KeyStorageFlags.PersistKeySet);
#else
        using X509Certificate2 certificate = new(nativeCertificateBaseline, NativeCertificatePassword,
            X509KeyStorageFlags.PersistKeySet);
#endif

        Thumbprint = certificate.Thumbprint;
        AddToStore(certificate, StoreLocation.CurrentUser, StoreName.My);
    }
}
