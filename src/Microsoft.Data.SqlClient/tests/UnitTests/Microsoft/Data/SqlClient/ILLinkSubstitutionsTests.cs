// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies that the correct ILLink substitution files are embedded in the
/// assembly for the current platform. This catches regressions in the csproj
/// conditional embedding logic.
/// </summary>
public class ILLinkSubstitutionsTests
{
    private static readonly string[] s_resourceNames =
        typeof(SqlConnection).Assembly.GetManifestResourceNames();

#if NETFRAMEWORK
    /// <summary>
    /// On .NET Framework the trimmer is not supported, so the cross-platform
    /// substitution file must NOT be embedded.
    /// </summary>
    [Fact]
    public void Assembly_DoesNotContainCrossPlatformSubstitutions()
    {
        Assert.DoesNotContain("ILLink.Substitutions.xml", s_resourceNames);
    }

    /// <summary>
    /// On .NET Framework the trimmer is not supported, so the Windows-only
    /// substitution file must NOT be embedded.
    /// </summary>
    [Fact]
    public void Assembly_DoesNotContainWindowsSubstitutions()
    {
        Assert.DoesNotContain("ILLink.Substitutions.Windows.xml", s_resourceNames);
    }
#else
    /// <summary>
    /// The cross-platform substitution file (auth provider feature switch) must
    /// always be present on .NET (non-Framework) builds.
    /// </summary>
    [Fact]
    public void Assembly_ContainsCrossPlatformSubstitutions()
    {
        Assert.Contains("ILLink.Substitutions.xml", s_resourceNames);
    }

    /// <summary>
    /// The Windows-only substitution file (UseManagedNetworkingOnWindows) must be
    /// present on Windows and absent on Unix. Embedding it on Unix would be a
    /// breaking change for cross-platform consumers who set the switch to false.
    /// </summary>
    [Fact]
    public void Assembly_ContainsWindowsSubstitutionsOnlyOnWindows()
    {
        if (System.OperatingSystem.IsWindows())
        {
            Assert.Contains("ILLink.Substitutions.Windows.xml", s_resourceNames);
        }
        else
        {
            Assert.DoesNotContain("ILLink.Substitutions.Windows.xml", s_resourceNames);
        }
    }
#endif
}
