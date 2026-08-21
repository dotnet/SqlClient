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
    /// On .NET Framework the trimmer is not supported, so the
    /// substitution file must NOT be embedded.
    /// </summary>
    [Fact]
    public void Assembly_DoesNotContainSubstitutions()
    {
        Assert.DoesNotContain("ILLink.Substitutions.xml", s_resourceNames);
    }
#else
    /// <summary>
    /// The cross-platform substitution file (auth provider feature switch and
    /// UseManagedNetworkingOnWindows) must always be present on .NET
    /// (non-Framework) builds regardless of OS.
    /// </summary>
    [Fact]
    public void Assembly_ContainsSubstitutions()
    {
        Assert.Contains("ILLink.Substitutions.xml", s_resourceNames);
    }

    /// <summary>
    /// There should be no separate Windows-only substitution file. All entries
    /// are now in the unified cross-platform ILLink.Substitutions.xml since the
    /// UseManagedNetworking property includes a platform guard.
    /// </summary>
    [Fact]
    public void Assembly_DoesNotContainWindowsSubstitutions()
    {
        Assert.DoesNotContain("ILLink.Substitutions.Windows.xml", s_resourceNames);
    }
#endif
}
