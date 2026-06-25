using Xunit;

namespace PackageValidator.Tests;

public class BinaryClassifierTests
{
    [Theory]
    [InlineData("lib/net8.0/Foo.dll", "Implementation")]
    [InlineData("runtimes/win/lib/net462/Foo.dll", "Implementation")]
    [InlineData("ref/net8.0/Foo.dll", "Reference")]
    [InlineData("lib/net8.0/de/Foo.resources.dll", "Satellite")]
    [InlineData("lib/net8.0/fr/Foo.resources.dll", "Satellite")]
    [InlineData("build/Foo.dll", "Other")]
    [InlineData("tools/Foo.dll", "Other")]
    public void Classify_maps_paths_to_kinds(string path, string expected)
    {
        Assert.Equal(expected, BinaryClassifier.Classify(path).ToString());
    }

    [Fact]
    public void Classify_satellite_takes_precedence_over_lib()
    {
        Assert.Equal("Satellite", BinaryClassifier.Classify("lib/net8.0/es/Foo.resources.dll").ToString());
    }
}
