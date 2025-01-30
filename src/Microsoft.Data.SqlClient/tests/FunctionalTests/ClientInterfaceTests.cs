using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Xunit;
using Xunit.Abstractions;

#nullable enable

namespace Microsoft.Data.SqlClient.Tests
{
    public sealed class ClientInterfaceTests
    {
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

            // Find the Trunc() function.
            var truncFunction =
                clientInterfaceType.GetMethod(
                    "Trunc",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(truncFunction);
            _truncFunction = truncFunction;
            _output.WriteLine($"Trunc function: {_truncFunction}");
        }

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
            // MS-MDS|{OS Name}|{Arch}|{OS Info}|{Framework Info}
            //
            var parts = name.Split('|');
            Assert.Equal(5, parts.Length);
            Assert.Equal("MS-MDS", parts[0]);
            
            // Check the OS name against the guaranteed values.
            var osName = parts[1];
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Equal("Windows", osName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Assert.Equal("Linux", osName);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Assert.Equal("MacOS", osName);
            }
#if NET
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
            {
                Assert.Equal("FreeBSD", osName);
            }
#endif // NET
            else
            {
                Assert.Equal("Unknown", osName);
            }

            // The remaining parts have no guaranteed format/content, but they
            // must all be "Unknown" or non-empty.
            Assert.True(parts[2] == "Unknown" || parts[2].Length > 0);
            Assert.True(parts[3] == "Unknown" || parts[3].Length > 0);
            Assert.True(parts[4] == "Unknown" || parts[4].Length > 0);
        }

        // Test the Build() function.
        [Fact]
        public void Build()
        {
        }

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
                "Q_R_S_T_U_V_W_X_Y_Z_a_b_c_d_e_f_g_h_i_j_k_l_m_n_o",
                DoClean("Q^R_S`T~U(V)W*X+Y-Z_a.b,c/d:e<f>g'h\"i[j]k{l}m|n\\o"));

            // All disallowed characters are replaced with underscore.
            for (char c = (char)0u; c <= 0xffff; c++)
            {
                Assert.Equal(
                    AllPermitted.Contains(c) ? c.ToString() : "_",
                    DoClean(c.ToString()));
            }
        }

        // Test the Trunc() function.
        [Fact]
        public void Trunc()
        {
            // Max length of 0.
            Assert.Equal("", DoTrunc("", 0));
            Assert.Equal("", DoTrunc(" ", 0));
            Assert.Equal("", DoTrunc("A", 0));
            Assert.Equal("", DoTrunc("ABCDE FGHIJ", 0));
            
            // Max length of 1.
            Assert.Equal("", DoTrunc("", 1));
            Assert.Equal(" ", DoTrunc(" ", 1));
            Assert.Equal("A", DoTrunc("A", 1));
            Assert.Equal("A", DoTrunc("ABCDE FGHIJ", 1));
            
            // Max length of 5.
            Assert.Equal("", DoTrunc("", 5));
            Assert.Equal(" ", DoTrunc(" ", 5));
            Assert.Equal("A", DoTrunc("A", 5));
            Assert.Equal("ABCDE", DoTrunc("ABCDE FGHIJ", 5));
            
            // Max length of 100.
            Assert.Equal("", DoTrunc("", 100));
            Assert.Equal(" ", DoTrunc(" ", 100));
            Assert.Equal("A", DoTrunc("A", 100));
            Assert.Equal("ABCDE", DoTrunc("ABCDE FGHIJ", 100));
        }

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
        
        // Convenience helper to call the Trunc() function.
        private string DoTrunc(string value, ushort maxLen)
        {
            var result =
                _truncFunction.Invoke(
                    null, new object?[] { value, maxLen }) as string;
            Assert.NotNull(result);
            return result;
        }

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
        
        // The ClientInterface.Trunc() function.
        private readonly MethodInfo _truncFunction;
    }
}
