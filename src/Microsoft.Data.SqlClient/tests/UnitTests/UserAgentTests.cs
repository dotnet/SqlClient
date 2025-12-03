// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.Tests;

public sealed class UserAgentTests
{
    #region Test Setup

    // Setup to test by saving the xUnit output helper.
    public UserAgentTests(ITestOutputHelper output)
    {
        // Use --logger option to see the output for successful test runs:
        //
        //   dotnet test --logger "console;verbosity=detailed"
        //
        // The output will appear by default if a test fails.
        //
        _output = output;
    }

    #endregion Test Setup

    #region Tests

    // Test the Value property when actual runtime information is used.
    //
    // This test assumes that values returned by the runtime used to construct
    // the Value property will all fit within the max length (currently 256
    // characters).
    //
    // If this test fails, then either the max length has changed or the runtime
    // values have changed in a meaningful way.
    //
    [Fact]
    public void Value_Runtime_Parts()
    {
        string value = UserAgent.Value;

        _output.WriteLine($"UserAgent.Value: {value}");

        // Check the basic properties of the value.
        Assert.NotNull(value);
        Assert.True(value.Length > 0);
        Assert.True(value.Length <= 128);

        // Ensure we can split it into the expected parts.
        //
        // The format should be:
        //
        // 1|MS-MDS|{Driver Version}|{OS Type}|{Arch}|{OS Info}|{Runtime Info}
        //
        var parts = value.Split('|');
        Assert.Equal(7, parts.Length);
        Assert.Equal("1", parts[0]);
        Assert.Equal("MS-MDS", parts[1]);
        Assert.Equal(System.ThisAssembly.NuGetPackageVersion, parts[2]);
        
        // Check the OS Type against the guaranteed values.
        var osType = parts[3];
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Equal("Windows", osType);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Equal("Linux", osType);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.Equal("macOS", osType);
        }
        #if NET
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            Assert.Equal("FreeBSD", osType);
        }
        #endif
        else
        {
            Assert.Equal("Unknown", osType);
        }

        // Architecture must be non-empty and 10 characters or less.
        Assert.True(parts[4] == "Unknown" || parts[4].Length > 0);
        Assert.True(parts[4].Length <= 10);

        // OS Info must be non-empty and 44 characters or less.
        Assert.True(parts[5] == "Unknown" || parts[5].Length > 0);
        Assert.True(parts[5].Length <= 44);
        
        // Runtime Info must be non-empty and 44 characters or less.
        Assert.True(parts[6] == "Unknown" || parts[6].Length > 0);
        Assert.True(parts[6].Length <= 44);
    }

    // Test the Ucs2Bytes property when actual runtime information is used.
    [Fact]
    public void Ucs2Bytes_Runtime_Parts()
    {
        var bytes = UserAgent.Ucs2Bytes;

        _output.WriteLine(
            $"UserAgent.Ucs2Bytes: 0x{Convert.ToHexString(bytes.Span)}");

        // Check the basic properties of the byte array.
        Assert.True(bytes.Length > 0);
        Assert.True(bytes.Length <= 256 * 2); // USC2 uses 2 bytes per char.

        // Ensure we can convert the bytes back to the original string.
        string value = Encoding.Unicode.GetString(bytes.Span);
        Assert.Equal(UserAgent.Value, value);
    }

    // Test the Build() function when it truncates the overall length.
    [Theory]
    [InlineData(0, "")]
    [InlineData(1, "2")]
    [InlineData(2, "2|")]
    [InlineData(3, "2|A")]
    [InlineData(4, "2|A|")]
    [InlineData(5, "2|A|B")]
    [InlineData(6, "2|A|B|")]
    [InlineData(7, "2|A|B|C")]
    [InlineData(8, "2|A|B|C|")]
    [InlineData(9, "2|A|B|C|X")]
    [InlineData(10, "2|A|B|C|X6")]
    [InlineData(11, "2|A|B|C|X64")]
    [InlineData(12, "2|A|B|C|X64|")]
    [InlineData(13, "2|A|B|C|X64|D")]
    [InlineData(14, "2|A|B|C|X64|D|")]
    [InlineData(15, "2|A|B|C|X64|D|E")]
    public void Build_Truncate_Overall(ushort maxLen, string expected)
    {
        Assert.Equal(
            expected,
            UserAgent.Build(
                maxLen,
                payloadVersion: "2",
                driverName: "A",
                driverVersion: "B",
                osType: "C",
                Architecture.X64,
                osInfo: "D",
                runtimeInfo: "E"));
    }

    // Test the Build() function when it truncates the payload version.
    [Fact]
    public void Build_Truncate_Payload_Version()
    {
        // The payload version is longer than max length.
        Assert.Equal(
            "P",
            UserAgent.Build(
                1, "PV", "A", "B", "C", Architecture.X64, "D", "E"));
        
        // The payload version is longer than its per-field max length of 2.
        Assert.Equal(
            "12|A|B|C|X64|D|E",
            UserAgent.Build(
                128, "1234", "A", "B", "C", Architecture.X64, "D", "E"));
    }

    // Test the Build() function when it truncates the driver name.
    [Fact]
    public void Build_Truncate_Driver_Name()
    {
        // The driver name is longer than max length.
        Assert.Equal(
            "2|DriverNa",
            UserAgent.Build(
                10, "2", "DriverName", "B", "C", Architecture.X64, "D", "E"));
        
        // The driver name is longer than its per-field max length of 12.
        Assert.Equal(
            "2|LongDriverNa|B|C|X64|D|E",
            UserAgent.Build(
                128, "2", "LongDriverName", "B", "C", Architecture.X64,
                "D", "E"));
    }

    // Test the Build() function when it truncates the driver version.
    [Fact]
    public void Build_Truncate_Driver_Version()
    {
        // The driver version is longer than max length.
        Assert.Equal(
            "2|A|DriverVe",
            UserAgent.Build(
                12, "2", "A", "DriverVersion", "C", Architecture.X64, "D",
                "E"));
        
        // The driver version is longer than its per-field max length of 24.
        Assert.Equal(
            "2|A|ReallyLongDriverVersionS|C|X64|D|E",
            UserAgent.Build(
                128, "2", "A", "ReallyLongDriverVersionString", "C",
                Architecture.X64, "D", "E"));
    }

    // Test the Build() function when it truncates the OS Type.
    [Fact]
    public void Build_Truncate_OS_Type()
    {
        // The OS Type puts the overall length over the max.
        Assert.Equal(
            "2|A|B|LongOs",
            UserAgent.Build(
                12, "2", "A", "B", "LongOsName", Architecture.X64, "D", "E"));
        
        // The OS Type is longer than its per-field max length of 10.
        Assert.Equal(
            "2|A|B|VeryLongOs|X64|D|E",
            UserAgent.Build(
                128, "2", "A", "B", "VeryLongOsName", Architecture.X64, "D",
                "E"));
    }

    // Test the Build() function when it truncates the Architecture.
    [Fact]
    public void Build_Truncate_Arch()
    {
        // The Architecture puts the overall length over the max.
        Assert.Equal(
            "2|A|B|C|Arm6",
            UserAgent.Build(
                12, "2", "A", "B", "C", Architecture.Arm64, "D", "E"));

        // There are no Architecture enum values defined in .NET Framework
        // with a length longer than 10, so we can only check truncation
        // in .NET.
        #if NET
        // The Architecture is longer than its per-field max length of 10.
        Assert.Equal(
            "2|A|B|C|LoongArch6|D|E",
            UserAgent.Build(
                128, "2", "A", "B", "C", Architecture.LoongArch64, "D", "E"));
        #endif
    }

    // Test the Build() function when it truncates the OS Info.
    [Fact]
    public void Build_Truncate_OS_Info()
    {
        // The OS Info puts the overall length over the max.
        Assert.Equal(
            "2|A|B|C|X64|LongOsI",
            UserAgent.Build(
                19, "2", "A", "B", "C", Architecture.X64, "LongOsInfo", "E"));
        
        // The OS Type is longer than its per-field max length of 44.
        Assert.Equal(
            "2|A|B|C|X64|01234567890123456789012345678901234567890123|E",
            UserAgent.Build(
                128, "2", "A", "B", "C", Architecture.X64,
                "01234567890123456789012345678901234567890123456789",
                "E"));
    }

    // Test the Build() function when it truncates the Runtime Info.
    [Fact]
    public void Build_Truncate_Runtime_Info()
    {
        // The Runtime Info puts the overall length over the max.
        Assert.Equal(
            "2|A|B|C|X64|D|LongRunt",
            UserAgent.Build(
                22, "2", "A", "B", "C", Architecture.X64, "D",
                "LongRuntimeInfo"));
        
        // The Runtime Type is longer than its per-field max length of 44.
        Assert.Equal(
            "2|A|B|C|X64|D|01234567890123456789012345678901234567890123",
            UserAgent.Build(
                128, "2", "A", "B", "C", Architecture.X64, "D",
                "01234567890123456789012345678901234567890123456789"));
    }

    // Test the Build() function when most of the fields are truncated, and
    // the overall length is still within the max.
    [Fact]
    public void Build_Truncate_Most()
    {
        var name = 
            UserAgent.Build(
                192,
                // Payload version > 2 chars.
                "1234",
                // Driver name > 12 chars.
                "A01234567890123456789",
                // Driver version > 24 chars.
                "B012345678901234567890123456789",
                // OS Type > 10 chars.
                "C01234567890123456789",
                // Architecture isn't truncated (because .NET Framework
                // doesn't have any enum values long enough).
                Architecture.X64,
                // OS Info > 44 chars.
                "D01234567890123456789012345678901234567890123456789",
                // Runtime Info > 44 chars.
                "E01234567890123456789012345678901234567890123456789");
        Assert.Equal(145, name.Length);
        Assert.Equal(
            "12|" +
            "A01234567890|" +
            "B01234567890123456789012|" +
            "C012345678|" +
            "X64|" +
            "D0123456789012345678901234567890123456789012|" +
            "E0123456789012345678901234567890123456789012",
            name);
    }

    #if NET
    // Test the Build() function when all the fields are truncated, and
    // the overall length is still within the max.
    [Fact]
    public void Build_Truncate_All()
    {
        var name = 
            UserAgent.Build(
                192,
                // Payload version > 2 chars.
                "1234",
                // Driver name > 12 chars.
                "A01234567890123456789",
                // Driver version > 24 chars.
                "B012345678901234567890123456789",
                // OS Type > 10 chars.
                "C01234567890123456789",
                // Architecture > 10 chars.
                Architecture.LoongArch64,
                // OS Info > 44 chars.
                "D01234567890123456789012345678901234567890123456789",
                // Runtime Info > 44 chars.
                "E01234567890123456789012345678901234567890123456789");
        Assert.Equal(152, name.Length);
        Assert.Equal(
            "12|" +
            "A01234567890|" +
            "B01234567890123456789012|" +
            "C012345678|" +
            "LoongArch6|" +
            "D0123456789012345678901234567890123456789012|" +
            "E0123456789012345678901234567890123456789012",
            name);
    }
#endif // NET

    // Test the Clean() function.
    [Fact]
    public void Clean()
    {
        // Null becomes "Unknown".
        Assert.Equal("Unknown", UserAgent.Clean(null));

        // Empty string becomes "Unknown".
        Assert.Equal("Unknown", UserAgent.Clean(string.Empty));

        // Whitespace string becomes "Unknown".
        Assert.Equal("Unknown", UserAgent.Clean(" "));
        Assert.Equal("Unknown", UserAgent.Clean("\t"));
        Assert.Equal("Unknown", UserAgent.Clean("\r"));
        Assert.Equal("Unknown", UserAgent.Clean("\n"));
        Assert.Equal("Unknown", UserAgent.Clean(" \t\r\n"));

        // Leading and trailing whitespace are removed.
        Assert.Equal("A", UserAgent.Clean(" A"));
        Assert.Equal("A", UserAgent.Clean("A\t"));
        Assert.Equal("A", UserAgent.Clean("\rA\n"));

        // All permitted characters are preserved.
        const string AllPermitted =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
            "abcdefghijklmnopqrstuvwxyz" +
            "0123456789" +
            " .+_-";
        Assert.Equal(AllPermitted, UserAgent.Clean(AllPermitted));

        // Each disallowed character is replaced with underscore.
        Assert.Equal(
            "A_B_C_D_E_F_G_H_I_J_K_L_M_N_O_P",
            UserAgent.Clean("A|B,C;D:E'F\"G[H{I]J}K\\L/M<N>O?P"));
        Assert.Equal(
            "Q_R_S_T_U_V_W_X+Y-Z_a.b_c_d_e_f_g_h_i_j_k_l_m_n_o",
            UserAgent.Clean("Q^R_S`T~U(V)W*X+Y-Z_a.b,c/d:e<f>g'h\"i[j]k{l}m|n\\o"));

        // All disallowed characters are replaced with underscore.
        for (char c = (char)0u; /* see condition below */ ; ++c)
        {
            var clean = UserAgent.Clean(c.ToString());

            // Whitespace becomes "Unknown".
            if (char.IsWhiteSpace(c))
            {
                Assert.Equal("Unknown", clean);
            }
            else if (
#if NET
                AllPermitted.Contains(c)
#else
                AllPermitted.Contains(c.ToString())
#endif          
            )
            {
                Assert.Equal(c.ToString(), clean);
            }
            else
            {
                Assert.Equal("_", clean);
            }

            // We can't use 'c <= 0xffff' as the terminating condition because
            // incrementing a char past 0xffff overflows back to 0x0000, and the
            // loop will iterate forever.
            //
            // Instead, we check for the terminating condition inside the loop
            // and break out when we reach it.
            //
            if (c == 0xffff)
            {
                break;
            }
        }
    }

    // Test the Trunc() function.
    [Fact]
    public void Trunc()
    {
        // Max length of 0.
        Assert.Equal("", UserAgent.Truncate("", 0));
        Assert.Equal("", UserAgent.Truncate(" ", 0));
        Assert.Equal("", UserAgent.Truncate("A", 0));
        Assert.Equal("", UserAgent.Truncate("ABCDE FGHIJ", 0));
        
        // Max length of 1.
        Assert.Equal("", UserAgent.Truncate("", 1));
        Assert.Equal(" ", UserAgent.Truncate(" ", 1));
        Assert.Equal("A", UserAgent.Truncate("A", 1));
        Assert.Equal("A", UserAgent.Truncate("ABCDE FGHIJ", 1));
        
        // Max length of 5.
        Assert.Equal("", UserAgent.Truncate("", 5));
        Assert.Equal(" ", UserAgent.Truncate(" ", 5));
        Assert.Equal("A", UserAgent.Truncate("A", 5));
        Assert.Equal("ABCDE", UserAgent.Truncate("ABCDE FGHIJ", 5));
        
        // Max length of 100.
        Assert.Equal("", UserAgent.Truncate("", 100));
        Assert.Equal(" ", UserAgent.Truncate(" ", 100));
        Assert.Equal("A", UserAgent.Truncate("A", 100));
        Assert.Equal("ABCDE FGHIJ", UserAgent.Truncate("ABCDE FGHIJ", 100));
    }

    #endregion Tests

    #region Private Fields

    // The xUnit output helper.
    private readonly ITestOutputHelper _output;

    #endregion Private Fields
}
