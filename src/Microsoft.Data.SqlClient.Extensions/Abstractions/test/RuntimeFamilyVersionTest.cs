// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Reflection;
using System.Reflection.Emit;

namespace Microsoft.Data.SqlClient.Extensions.Abstractions.Test;

/// <summary>Checks exact loaded-family versions independently of assembly binding versions.</summary>
public class RuntimeFamilyVersionTest
{
    /// <summary>The owning assembly is always accepted as the expected family version.</summary>
    [Fact]
    public void MatchingRuntimeVersionSucceeds() =>
        SqlAuthenticationProviderRegistry.ValidateFamilyVersion(typeof(SqlAuthenticationProvider).Assembly);

    /// <summary>Source-control build metadata does not change an otherwise identical package version.</summary>
    [Fact]
    public void MatchingVersionWithDifferentBuildMetadataSucceeds()
    {
        string expected = typeof(SqlAuthenticationProvider).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
        SqlAuthenticationProviderRegistry.ValidateFamilyVersion(CreateAssembly(expected + "+different.commit"));
    }

    /// <summary>Different stable or prerelease versions fail with exact versions and upgrade guidance.</summary>
    [Theory]
    [InlineData("7.0.1")]
    [InlineData("1.0.0")]
    [InlineData("8.0.0-different-preview")]
    public void MixedRuntimeVersionThrows(string actual)
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => SqlAuthenticationProviderRegistry.ValidateFamilyVersion(CreateAssembly(actual)));
        Assert.Contains("version mismatch", error.Message);
        Assert.Contains(actual, error.Message);
        Assert.Contains("Microsoft.Data.SqlClient.Extensions.Abstractions", error.Message);
        Assert.Contains("Upgrade all SqlClient family packages together", error.Message);
    }

    /// <summary>Assemblies lacking version evidence are rejected rather than silently accepted.</summary>
    [Fact]
    public void MissingInformationalVersionThrows()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => SqlAuthenticationProviderRegistry.ValidateFamilyVersion(CreateAssembly(null)));
        Assert.Contains("Cannot determine the exact SqlClient family version", error.Message);
    }

    /// <summary>Creates an in-memory assembly with controlled version metadata and no filesystem side effects.</summary>
    private static Assembly CreateAssembly(string? version)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("RuntimeVersionFixture"), AssemblyBuilderAccess.Run);
        if (version is not null)
        {
            ConstructorInfo constructor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [version]));
        }
        return assembly;
    }
}
