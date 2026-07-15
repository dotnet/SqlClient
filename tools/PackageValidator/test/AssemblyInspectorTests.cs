using System.Security.Cryptography;
using Xunit;

namespace PackageValidator.Tests;

/// <summary>
/// Tests for <see cref="AssemblyInspector"/>, focused on the public-key-token derivation that turns
/// a strong-name public key into its 8-byte token.
/// </summary>
public class AssemblyInspectorTests
{
    /// <summary>
    /// Verifies that an assembly with no public key (empty or null blob) yields no token, since such
    /// an assembly is not strong-name signed.
    /// </summary>
    [Fact]
    public void ComputePublicKeyToken_returns_null_for_empty_key()
    {
        Assert.Null(AssemblyInspector.ComputePublicKeyToken([]));
        Assert.Null(AssemblyInspector.ComputePublicKeyToken(null!));
    }

    /// <summary>
    /// Verifies the token derivation contract: the low 8 bytes of <c>SHA-1(publicKey)</c>, emitted in
    /// reverse (little-endian) order as lowercase hex.
    /// </summary>
    [Fact]
    public void ComputePublicKeyToken_is_reversed_sha1_tail()
    {
        // Arrange: an arbitrary public-key blob plus the token computed independently from the
        // documented contract (reversed last 8 bytes of its SHA-1).
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        byte[] hash = SHA1.HashData(key);
        var expected = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            expected[i] = hash[hash.Length - 1 - i];
        }
        string expectedHex = Convert.ToHexStringLower(expected);

        // Act + Assert: the inspector must produce that same hex token.
        Assert.Equal(expectedHex, AssemblyInspector.ComputePublicKeyToken(key));
    }
}

/// <summary>
/// Tests for <see cref="PortablePdb"/>, covering portable-PDB GUID reading and the detection of
/// legacy Windows (MSF) PDBs that cannot be matched by GUID.
/// </summary>
public class PortablePdbTests
{
    /// <summary>
    /// Verifies that the legacy Windows PDB magic ("Microsoft C/C++ MSF 7.00") is recognized so such
    /// PDBs can be reported rather than silently treated as portable.
    /// </summary>
    [Fact]
    public void IsWindowsPdb_detects_msf_signature()
    {
        // Arrange: bytes that begin with the Windows PDB MSF signature.
        byte[] windows = System.Text.Encoding.ASCII.GetBytes("Microsoft C/C++ MSF 7.00\r\n");

        // Act + Assert.
        Assert.True(PortablePdb.IsWindowsPdb(windows));
    }

    /// <summary>
    /// Verifies that non-Windows-PDB content (including the portable-PDB "BSJB" signature and empty
    /// input) is not misidentified as a Windows PDB.
    /// </summary>
    [Fact]
    public void IsWindowsPdb_rejects_other_content()
    {
        byte[] portableSignature = [0x42, 0x53, 0x4A, 0x42]; // "BSJB" - the portable PDB / metadata magic
        Assert.False(PortablePdb.IsWindowsPdb(portableSignature));
        Assert.False(PortablePdb.IsWindowsPdb([]));
    }

    /// <summary>
    /// Verifies that bytes which are not a readable portable PDB yield no GUID rather than throwing.
    /// </summary>
    [Fact]
    public void TryReadGuid_returns_null_for_non_pdb()
    {
        Assert.Null(PortablePdb.TryReadGuid([0, 1, 2, 3]));
    }

    /// <summary>
    /// Verifies that a malformed portable-PDB header encoding a negative version length is treated as
    /// unparseable (checksum verification returns <see langword="null"/>) rather than throwing and
    /// failing the whole run.
    /// </summary>
    [Fact]
    public void TryVerifyChecksum_returns_null_for_malformed_version_length()
    {
        // A metadata header ("BSJB" + major/minor/reserved) followed by a negative version length,
        // which would otherwise make the seek throw.
        byte[] malformed =
        [
            0x42, 0x53, 0x4A, 0x42, // signature "BSJB"
            0x00, 0x00,             // major version
            0x00, 0x00,             // minor version
            0x00, 0x00, 0x00, 0x00, // reserved
            0xFF, 0xFF, 0xFF, 0xFF, // version length = -1
        ];
        var checksum = new PdbChecksum { Algorithm = "SHA256", Hash = new byte[32] };

        Assert.Null(PortablePdb.TryVerifyChecksum(malformed, checksum));
    }
}
