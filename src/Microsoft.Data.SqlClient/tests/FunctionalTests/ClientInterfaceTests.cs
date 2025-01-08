using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.Data.SqlClient.Tests
{
    public sealed class ClientInterfaceTests
    {
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
            _nameProperty =
                clientInterfaceType.GetProperty(
                    "Name",
                    BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(_nameProperty);
            _output.WriteLine($"Name property: {_nameProperty}");
        }

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
            // Microsoft SqlClient - {OS Name} {OS Version}, {Runtime} - {Arch}
            //
            Regex regex = new Regex(
                @"^Microsoft SqlClient - " +
                @"([A-Za-z]+) " +
                @"([0-9.]+), " +
                @"(.+) - " +
                @"(.+)$");

            // GOTCHA: This match will likely fail if the name was truncated.
            // This is on purpose, to help us detect strange underlying values
            // as the OSes and runtimes we target evolve.
            Match match = regex.Match(name);
            Assert.True(match.Success);
            // We get 5 capture groups - the full match itself, and the 4
            // explicit captures.
            Assert.Equal(5, match.Groups.Count);
            
            // Check the OS name against the guaranteed values.
            var osName = match.Groups[1].Value;
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
                Assert.Equal("macOS", osName);
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

            // The remaining parts have no guaranteed format/content, so we
            // can't check them further.
        }

        private readonly ITestOutputHelper _output;
        private readonly PropertyInfo _nameProperty;
    }
}
