// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Data.SqlClient.UnitTests.Fixtures.AlwaysEncrypted;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted;

/// <summary>
/// Provides unit tests which verify that a final cell produced by SQL Server's native Always Encrypted code
/// can be decrypted to a known value.
/// </summary>
[SkipOnPlatform(TestPlatforms.OSX | TestPlatforms.MacCatalyst | TestPlatforms.iOS | TestPlatforms.tvOS,
    "The supplied native column encryption key is a certificate which is incompatible with MacOS.")]
public class NativeColumnEncryptionKeyBaseline : IClassFixture<NativeColumnEncryptionKeyCertificateBaselineFixture>
{
    private readonly string _certificateThumbprint;

    public NativeColumnEncryptionKeyBaseline(NativeColumnEncryptionKeyCertificateBaselineFixture fixture)
    {
        _certificateThumbprint = fixture.Thumbprint;
    }

    /// <summary>
    /// Verifies that a final cell produced by SQL Server's native Always Encrypted code can be decrypted to a known value.
    /// </summary>
    /// <param name="certificatePath">The path to the certificate (installed in the class fixture.)</param>
    /// <param name="finalCell">The native final cell value.</param>
    /// <param name="expectedPlaintext">The plaintext value we expect to decrypt to.</param>
    [Theory]
    [MemberData(nameof(NativeColumnEncryptionKeyCertificateBaselineFixture.NativeCEKBaselineData),
        MemberType = typeof(NativeColumnEncryptionKeyCertificateBaselineFixture))]
    public void Baseline_FinalCell_Decrypts_To_Known_Plaintext(string certificatePath, byte[] finalCell, byte[] expectedPlaintext)
    {
        SqlColumnEncryptionCertificateStoreProvider rsaProvider = new();

        // Decrypt the supplied final cell CEK, and ensure that the plaintext CEK value matches the native code baseline.
        byte[] plaintext = rsaProvider.DecryptColumnEncryptionKey(certificatePath, "RSA_OAEP", finalCell);

        Assert.Equal($"CurrentUser/My/{_certificateThumbprint}", certificatePath, ignoreCase: true);
        Assert.Equal(expectedPlaintext, plaintext);
    }
}
