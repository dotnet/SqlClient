using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.AlwaysEncrypted;

/// <summary>
/// Provides unit tests which verify that a final cell produced by SQL Server's native Always Encrypted
/// using AEAD_AES_256_CBC_HMAC_SHA256 can be decrypted to a known plaintext, and that encrypting a known
/// plaintext using this algorithm will produce a known final cell.
/// </summary>
public class NativeAeadBaseline
{
    /// <summary>
    /// The deterministically encrypted values produced by SQL Server's native Always Encrypted implementation.
    /// As a result of the deterministic encryption, we can test the encryption of known plaintexts with known keys.
    /// </summary>
    /// <remarks>
    /// This is a subset of <see cref="NativeEncryptionBaseline"/>.
    /// Parameter 1: the plaintext value to be encrypted.
    /// Parameter 2: the column encryption key (CEK) used for encryption.
    /// Parameter 3: the expected final cell value as produced by SQL Server's native code.
    /// </remarks>
    public static TheoryData<byte[], byte[], byte[]> DeterministicEncryptedValues =>
        new()
        {
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText01, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey01, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell01 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText02, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey02, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell02 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText03, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey03, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell03 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText04, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey04, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell04 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText05, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey05, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell05 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText06, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey06, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell06 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText07, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey07, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell07 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText08, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey08, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell08 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText09, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey09, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell09 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText10, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey10, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell10 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText11, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey11, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell11 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText12, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey12, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell12 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText13, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey13, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell13 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText14, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey14, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell14 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText15, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey15, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell15 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText16, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey16, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell16 }
        };

    /// <summary>
    /// The master set of native encryption baseline data, encrypted using both deterministic and randomized encryption.
    /// </summary>
    /// <remarks>
    /// Parameter 1: the plaintext value to be encrypted.
    /// Parameter 2: the column encryption key (CEK) used for encryption.
    /// Parameter 3: the expected final cell value as produced by SQL Server's native code.
    /// </remarks>
    public static TheoryData<byte[], byte[], byte[]> NativeEncryptionBaseline =>
        new()
        {
            // Encrypted using deterministic encryption
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText01, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey01, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell01 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText02, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey02, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell02 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText03, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey03, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell03 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText04, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey04, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell04 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText05, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey05, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell05 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText06, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey06, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell06 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText07, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey07, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell07 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText08, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey08, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell08 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText09, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey09, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell09 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText10, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey10, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell10 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText11, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey11, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell11 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText12, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey12, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell12 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText13, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey13, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell13 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText14, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey14, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell14 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText15, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey15, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell15 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText16, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey16, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell16 },

            // Encrypted using randomized encryption
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText17, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey17, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell17 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText18, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey18, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell18 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText19, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey19, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell19 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText20, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey20, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell20 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText21, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey21, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell21 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText22, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey22, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell22 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText23, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey23, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell23 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText24, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey24, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell24 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText25, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey25, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell25 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText26, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey26, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell26 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText27, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey27, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell27 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText28, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey28, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell28 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText29, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey29, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell29 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText30, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey30, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell30 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText31, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey31, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell31 },
            { Resources.AlwaysEncrypted_NativeAeadBaseline_PlainText32, Resources.AlwaysEncrypted_NativeAeadBaseline_RootKey32, Resources.AlwaysEncrypted_NativeAeadBaseline_FinalCell32 },
        };

    /// <summary>
    /// Verifies that a deterministically encrypted plaintext matches a known final cell value produced by SQL Server's
    /// native Always Encrypted implementation.
    /// </summary>
    /// <param name="plainText">The plaintext to encrypt.</param>
    /// <param name="rootKey">The column encryption key.</param>
    /// <param name="expectedFinalCell">The expected encrypted value.</param>
    [Theory]
    [MemberData(nameof(DeterministicEncryptedValues))]
    public void Known_Plaintext_Encrypts_To_Known_FinalCell(byte[] plainText, byte[] rootKey, byte[] expectedFinalCell)
    {
        SqlClientSymmetricKey cek = new(rootKey);
        SqlAeadAes256CbcHmac256Factory aeadFactory = new();
        SqlClientEncryptionAlgorithm aeadAlgorithm = aeadFactory.Create(cek, SqlClientEncryptionType.Deterministic, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);

        byte[] encryptedData = aeadAlgorithm.EncryptData(plainText);
        Assert.Equal(expectedFinalCell, encryptedData);
    }

    /// <summary>
    /// Verifies that a final cell produced by SQL Server's native Always Encrypted implementation can be decrypted to a
    /// known plaintext.
    /// </summary>
    /// <param name="expectedPlaintext">The expected plaintext.</param>
    /// <param name="rootKey">The column encryption key.</param>
    /// <param name="finalCell">The encrypted value.</param>
    [Theory]
    [MemberData(nameof(NativeEncryptionBaseline))]
    public void Known_FinalCell_Decrypts_To_Known_Plaintext(byte[] expectedPlaintext, byte[] rootKey, byte[] finalCell)
    {
        SqlClientSymmetricKey cek = new(rootKey);
        SqlAeadAes256CbcHmac256Factory aeadFactory = new();
        SqlClientEncryptionAlgorithm aeadAlgorithm = aeadFactory.Create(cek, SqlClientEncryptionType.Deterministic, SqlAeadAes256CbcHmac256Algorithm.AlgorithmName);

        byte[] decryptedData = aeadAlgorithm.DecryptData(finalCell);
        Assert.Equal(expectedPlaintext, decryptedData);
    }
}
