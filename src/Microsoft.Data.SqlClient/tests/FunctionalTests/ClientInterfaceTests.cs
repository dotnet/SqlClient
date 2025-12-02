// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Microsoft.Data.SqlClient.Tests
{
    public sealed class ClientInterfaceTests
    {
        #region Test Setup

        // ====================================================================
        // Test Setup

        // Setup to test by acquiring handles to the internal ClientInterface
        // APIs we need.
        public ClientInterfaceTests(ITestOutputHelper output)
        {
            // Use --logger option to see the output for successful test runs:
            //
            //   dotnet test --logger "console;verbosity=detailed"
            //
            // The output will appear by default if a test fails.
            //
            _output = output;

            // The ClientInterface class is internal, so we need to use
            // reflection to access it.
            //
            // Alternatively, we could use the [assembly:InternalsVisibleTo]
            // attribute in the main project to allow these tests to access
            // internals directly.
            
            // Find the internal ClientInterface class' type.
            var clientInterfaceType =
              // Get the assembly of a public type from the same assembly as
              // ClientInterface, and then use that to find its type.
                typeof(SqlCommand).Assembly
                .GetType("Microsoft.Data.SqlClient.ClientInterface");
            Assert.NotNull(clientInterfaceType);
            _output.WriteLine($"ClientInterface type: {clientInterfaceType}");

            // Find the Name property.
            var nameProperty =
                clientInterfaceType.GetProperty(
                    "Name",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(nameProperty);
            _nameProperty = nameProperty;
            _output.WriteLine($"Name property: {_nameProperty}");

            // Find the Build() function.
            var buildFunction =
                clientInterfaceType.GetMethod(
                    "Build",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(buildFunction);
            _buildFunction = buildFunction;
            _output.WriteLine($"Build function: {_buildFunction}");

            // Find the Clean() function.
            var cleanFunction =
                clientInterfaceType.GetMethod(
                    "Clean",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(cleanFunction);
            _cleanFunction = cleanFunction;
            _output.WriteLine($"Clean function: {_cleanFunction}");

            // Find the Truncate() function.
            var truncateFunction =
                clientInterfaceType.GetMethod(
                    "Truncate",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(truncateFunction);
            _truncateFunction = truncateFunction;
            _output.WriteLine($"Truncate function: {_truncateFunction}");
        }

        #endregion Test Setup

        #region Tests

        // ====================================================================
        // Tests

        // Test the Name property.
        //
        // This test assumes that values returned by the runtime used to
        // construct the Name property will all fit within the
        // TdsEnums.MAXLEN_CLIENTINTERFACE_NAME max length (currently 128).
        //
        // If this test fails, then either the max length has changed or the
        // runtime values have changed in a meaningful way.
        //
        [Fact]
        public void Name()
        {
            var name = _nameProperty.GetValue(null) as string;

            _output.WriteLine($"ClientInterface.Name: {name}");

            // Check the basic properties of the name.
            Assert.NotNull(name);
            Assert.True(name.Length > 0);
            Assert.True(name.Length <= 128);

            // Ensure we can split it into the expected parts.
            //
            // The format should be:
            //
            // MS-MDS|{OS Type}|{Arch}|{OS Info}|{Runtime Info}
            //
            var parts = name.Split('|');
            Assert.Equal(5, parts.Length);
            Assert.Equal("MS-MDS", parts[0]);
            
            // Check the OS Type against the guaranteed values.
            var osType = parts[1];
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
#endif // NET
            else
            {
                Assert.Equal("Unknown", osType);
            }

            // Architecture must be non-empty and 10 characters or less.
            Assert.True(parts[2] == "Unknown" || parts[2].Length > 0);
            Assert.True(parts[2].Length <= 10);

            // OS Info must be non-empty and 44 characters or less.
            Assert.True(parts[3] == "Unknown" || parts[3].Length > 0);
            Assert.True(parts[3].Length <= 44);
            
            // Runtime Info must be non-empty and 44 characters or less.
            Assert.True(parts[4] == "Unknown" || parts[4].Length > 0);
            Assert.True(parts[4].Length <= 44);
        }

        // Test the Build() function when it truncates the overall length.
        [Theory]
        [InlineData(0, "")]
        [InlineData(1, "A")]
        [InlineData(2, "A|")]
        [InlineData(3, "A|B")]
        [InlineData(4, "A|B|")]
        [InlineData(5, "A|B|X")]
        [InlineData(6, "A|B|X6")]
        [InlineData(7, "A|B|X64")]
        [InlineData(8, "A|B|X64|")]
        [InlineData(9, "A|B|X64|C")]
        [InlineData(10, "A|B|X64|C|")]
        [InlineData(11, "A|B|X64|C|D")]
        [InlineData(12, "A|B|X64|C|D")]
        public void Build_Truncate_Overall(ushort maxLen, string expected)
        {
            Assert.Equal(
                expected,
                DoBuild(maxLen, "A", "B", Architecture.X64, "C", "D"));
        }

        // Test the Build() function when it truncates the driver name.
        [Fact]
        public void Build_Truncate_Driver_Name()
        {
            // The driver name is longer than max length.
            Assert.Equal(
                "DriverNa",
                DoBuild(8, "DriverName", "B", Architecture.X64, "C", "D"));
            
            // The driver name is longer than its per-field max length of 16.
            Assert.Equal(
                "ReallyLongDriver|B|X64|C|D",
                DoBuild(
                    128, "ReallyLongDriverName", "B", Architecture.X64, "C",
                    "D"));
        }

        // Test the Build() function when it truncates the OS Type.
        [Fact]
        public void Build_Truncate_OS_Type()
        {
            // The OS Type puts the overall length over the max.
            Assert.Equal(
                "A|LongOs",
                DoBuild(8, "A", "LongOsName", Architecture.X64, "C", "D"));
            
            // The OS Type is longer than its per-field max length of 10.
            Assert.Equal(
                "A|VeryLongOs|X64|C|D",
                DoBuild(
                    128, "A", "VeryLongOsName", Architecture.X64, "C", "D"));
        }

        // Test the Build() function when it truncates the Architecture.
        [Fact]
        public void Build_Truncate_Arch()
        {
            // The Architecture puts the overall length over the max.
            Assert.Equal(
                "A|B|Arm6",
                DoBuild(8, "A", "B", Architecture.Arm64, "C", "D"));

#if NET
            // There are no Architecture enum values defined in .NET Framework
            // with a length longer than 10, so we can only check truncation
            // in .NET.

            // The Architecture is longer than its per-field max length of 10.
            Assert.Equal(
                "A|B|LoongArch6|C|D",
                DoBuild(
                    128, "A", "B", Architecture.LoongArch64, "C", "D"));
#endif // NET
        }

        // Test the Build() function when it truncates the OS Info.
        [Fact]
        public void Build_Truncate_OS_Info()
        {
            // The OS Info puts the overall length over the max.
            Assert.Equal(
                "A|B|X64|LongOsI",
                DoBuild(15, "A", "B", Architecture.X64, "LongOsInfo", "D"));
            
            // The OS Type is longer than its per-field max length of 44.
            Assert.Equal(
                "A|B|X64|01234567890123456789012345678901234567890123|D",
                DoBuild(
                    128, "A", "B", Architecture.X64,
                    "01234567890123456789012345678901234567890123456789",
                    "D"));
        }

        // Test the Build() function when it truncates the Runtime Info.
        [Fact]
        public void Build_Truncate_Runtime_Info()
        {
            // The Runtime Info puts the overall length over the max.
            Assert.Equal(
                "A|B|X64|C|LongRunt",
                DoBuild(18, "A", "B", Architecture.X64, "C",
                "LongRuntimeInfo"));
            
            // The Runtime Type is longer than its per-field max length of 44.
            Assert.Equal(
                "A|B|X64|C|01234567890123456789012345678901234567890123",
                DoBuild(
                    128, "A", "B", Architecture.X64, "C",
                    "01234567890123456789012345678901234567890123456789"));
        }

        // Test the Build() function when most of the fields are truncated.
        [Fact]
        public void Build_Truncate_Most()
        {
            var name = 
                DoBuild(
                    128,
                    // Driver name > 16 chars.
                    "A01234567890123456789",
                    // OS Type > 10 chars.
                    "B01234567890123456789",
                    // Architecture isn't truncated (because .NET Framework
                    // doesn't have any enum values long enough).
                    Architecture.X64,
                    // OS Info > 44 chars.
                    "C01234567890123456789012345678901234567890123456789",
                    // Runtime Info > 44 chars.
                    "D01234567890123456789012345678901234567890123456789");
            Assert.Equal(121, name.Length);
            Assert.Equal(
                "A012345678901234|" +
                "B012345678|" +
                "X64|" +
                "C0123456789012345678901234567890123456789012|" +
                "D0123456789012345678901234567890123456789012",
                name);
        }

#if NET
        // Test the Build() function when all the fields are truncated.
        [Fact]
        public void Build_Truncate_All()
        {
            var name = 
                DoBuild(
                    128,
                    // Driver name > 16 chars.
                    "A01234567890123456789",
                    // OS Type > 10 chars.
                    "B01234567890123456789",
                    // Architecture > 10 chars.
                    Architecture.LoongArch64,
                    // OS Info > 44 chars.
                    "C01234567890123456789012345678901234567890123456789",
                    // Runtime Info > 44 chars.
                    "D01234567890123456789012345678901234567890123456789");
            Assert.Equal(128, name.Length);
            Assert.Equal(
                "A012345678901234|" +
                "B012345678|" +
                "LoongArch6|" +
                "C0123456789012345678901234567890123456789012|" +
                "D0123456789012345678901234567890123456789012",
                name);
        }
#endif // NET

        // Test the Clean() function.
        [Fact]
        public void Clean()
        {
            // Null becomes "Unknown".
            Assert.Equal("Unknown", DoClean(null));

            // Empty string becomes "Unknown".
            Assert.Equal("Unknown", DoClean(string.Empty));

            // Whitespace string becomes "Unknown".
            Assert.Equal("Unknown", DoClean(" "));
            Assert.Equal("Unknown", DoClean("\t"));
            Assert.Equal("Unknown", DoClean("\r"));
            Assert.Equal("Unknown", DoClean("\n"));
            Assert.Equal("Unknown", DoClean(" \t\r\n"));

            // Leading and trailing whitespace are removed.
            Assert.Equal("A", DoClean(" A"));
            Assert.Equal("A", DoClean("A\t"));
            Assert.Equal("A", DoClean("\rA\n"));

            // All permitted characters are preserved.
            const string AllPermitted =
                "ABCDEFGHIJKLMNOPQRSTUVWXYZ" +
                "abcdefghijklmnopqrstuvwxyz" +
                "0123456789" +
                " .+_-";
            Assert.Equal(AllPermitted, DoClean(AllPermitted));

            // Each disallowed character is replaced with underscore.
            Assert.Equal(
                "A_B_C_D_E_F_G_H_I_J_K_L_M_N_O_P",
                DoClean("A|B,C;D:E'F\"G[H{I]J}K\\L/M<N>O?P"));
            Assert.Equal(
                "Q_R_S_T_U_V_W_X+Y-Z_a.b_c_d_e_f_g_h_i_j_k_l_m_n_o",
                DoClean("Q^R_S`T~U(V)W*X+Y-Z_a.b,c/d:e<f>g'h\"i[j]k{l}m|n\\o"));

            // All disallowed characters are replaced with underscore.
            for (char c = (char)0u; /* see condition below */ ; ++c)
            {
                var clean = DoClean(c.ToString());

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

                // We can't check for c <= 0xffff in the for statement because
                // ++c will overflow back to 0x0000 and the loop will iterate
                // forever.
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
            Assert.Equal("", DoTruncate("", 0));
            Assert.Equal("", DoTruncate(" ", 0));
            Assert.Equal("", DoTruncate("A", 0));
            Assert.Equal("", DoTruncate("ABCDE FGHIJ", 0));
            
            // Max length of 1.
            Assert.Equal("", DoTruncate("", 1));
            Assert.Equal(" ", DoTruncate(" ", 1));
            Assert.Equal("A", DoTruncate("A", 1));
            Assert.Equal("A", DoTruncate("ABCDE FGHIJ", 1));
            
            // Max length of 5.
            Assert.Equal("", DoTruncate("", 5));
            Assert.Equal(" ", DoTruncate(" ", 5));
            Assert.Equal("A", DoTruncate("A", 5));
            Assert.Equal("ABCDE", DoTruncate("ABCDE FGHIJ", 5));
            
            // Max length of 100.
            Assert.Equal("", DoTruncate("", 100));
            Assert.Equal(" ", DoTruncate(" ", 100));
            Assert.Equal("A", DoTruncate("A", 100));
            Assert.Equal("ABCDE FGHIJ", DoTruncate("ABCDE FGHIJ", 100));
        }

        #endregion Tests

        #region Private Helpers

        // ====================================================================
        // Private Helpers
        
        // Convenience helper to call the Build() function.
        private string DoBuild(
            ushort maxLen,
            string driverName,
            string osType,
            Architecture arch,
            string osDesc,
            string frameworkDesc)
        {
            var result =
                _buildFunction.Invoke(
                    null,
                    new object[]
                    {
                        maxLen,
                        driverName,
                        osType,
                        arch,
                        osDesc,
                        frameworkDesc
                    }) as string;
            Assert.NotNull(result);
            return result;
        }
        
        // Convenience helper to call the Clean() function.
        private string DoClean(string? value)
        {
            var result =
                _cleanFunction.Invoke(null, new object?[] { value }) as string;
            Assert.NotNull(result);
            return result;
        }
        
        // Convenience helper to call the Truncate() function.
        private string DoTruncate(string value, ushort maxLen)
        {
            var result =
                _truncateFunction.Invoke(
                    null, new object?[] { value, maxLen }) as string;
            Assert.NotNull(result);
            return result;
        }

        #endregion Private Helpers

        #region Private Fields

        // ====================================================================
        // Private Fields
        
        // The xUnit output helper.
        private readonly ITestOutputHelper _output;
        
        // The ClientInterface.Name property.
        private readonly PropertyInfo _nameProperty;
        
        // The ClientInterface.Build() function.
        private readonly MethodInfo _buildFunction;
        
        // The ClientInterface.Clean() function.
        private readonly MethodInfo _cleanFunction;
        
        // The ClientInterface.Truncate() function.
        private readonly MethodInfo _truncateFunction;

        #endregion Private Fields
    }
}
