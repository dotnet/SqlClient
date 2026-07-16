// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace PackageValidator.Tests;

/// <summary>
/// Tests for <see cref="BinaryClassifier"/>, which maps a DLL's package path to the role it plays
/// (implementation, reference, satellite, or other).
/// </summary>
public class BinaryClassifierTests
{
    /// <summary>
    /// Verifies the path-to-kind mapping for the common NuGet layouts: <c>lib/</c> and
    /// <c>runtimes/.../lib/</c> are implementation, <c>ref/</c> is reference, culture
    /// <c>*.resources.dll</c> are satellite, and anything else is other.
    /// </summary>
    /// <param name="path">The DLL path within the package.</param>
    /// <param name="expected">The expected <see cref="BinaryKind"/> name.</param>
    [Theory]
    [InlineData("lib/net8.0/Foo.dll", "Implementation")]
    [InlineData("runtimes/win/lib/net462/Foo.dll", "Implementation")]
    [InlineData("ref/net8.0/Foo.dll", "Reference")]
    [InlineData("lib/net8.0/de/Foo.resources.dll", "Satellite")]
    [InlineData("lib/net8.0/fr/Foo.resources.dll", "Satellite")]
    [InlineData("build/Foo.dll", "Other")]
    [InlineData("build/lib/Foo.dll", "Other")]     // nested lib/ outside runtimes/ is not implementation
    [InlineData("tools/Foo.dll", "Other")]
    public void Classify_maps_paths_to_kinds(string path, string expected)
    {
        // Compare by name (the enum is internal, so the public test signature cannot expose it).
        Assert.Equal(expected, BinaryClassifier.Classify(path).ToString());
    }

    /// <summary>
    /// Verifies that a satellite resource assembly is classified as <see cref="BinaryKind.Satellite"/>
    /// even though it also lives under <c>lib/</c>, since the <c>.resources.dll</c> suffix is the
    /// stronger signal.
    /// </summary>
    [Fact]
    public void Classify_satellite_takes_precedence_over_lib()
    {
        // The path is under lib/ but the name ends in .resources.dll, which must win.
        Assert.Equal("Satellite", BinaryClassifier.Classify("lib/net8.0/es/Foo.resources.dll").ToString());
    }
}
