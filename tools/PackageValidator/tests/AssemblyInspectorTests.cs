using System.Security.Cryptography;
using Xunit;

namespace PackageValidator.Tests;

public class AssemblyInspectorTests
{
    [Fact]
    public void ComputePublicKeyToken_returns_null_for_empty_key()
    {
        Assert.Null(AssemblyInspector.ComputePublicKeyToken([]));
        Assert.Null(AssemblyInspector.ComputePublicKeyToken(null!));
    }

    [Fact]
    public void ComputePublicKeyToken_is_reversed_sha1_tail()
    {
        // The token is the low 8 bytes of SHA-1(publicKey), emitted in reverse order, as lowercase hex.
        byte[] key = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        byte[] hash = SHA1.HashData(key);
        var expected = new byte[8];
        for (int i = 0; i < 8; i++)
        {
            expected[i] = hash[hash.Length - 1 - i];
        }

        string expectedHex = Convert.ToHexStringLower(expected);
        Assert.Equal(expectedHex, AssemblyInspector.ComputePublicKeyToken(key));
    }
}

public class PortablePdbTests
{
    [Fact]
    public void IsWindowsPdb_detects_msf_signature()
    {
        byte[] windows = System.Text.Encoding.ASCII.GetBytes("Microsoft C/C++ MSF 7.00\r\n");
        Assert.True(PortablePdb.IsWindowsPdb(windows));
    }

    [Fact]
    public void IsWindowsPdb_rejects_other_content()
    {
        byte[] portableSignature = [0x42, 0x53, 0x4A, 0x42]; // "BSJB"
        Assert.False(PortablePdb.IsWindowsPdb(portableSignature));
        Assert.False(PortablePdb.IsWindowsPdb([]));
    }

    [Fact]
    public void TryReadGuid_returns_null_for_non_pdb()
    {
        Assert.Null(PortablePdb.TryReadGuid([0, 1, 2, 3]));
    }
}
