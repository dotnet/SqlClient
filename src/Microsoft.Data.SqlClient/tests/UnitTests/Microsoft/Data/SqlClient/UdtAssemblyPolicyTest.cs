// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests for <see cref="UdtAssemblyPolicy"/>, the deny-by-default
/// policy that governs which assemblies the driver is willing to load while
/// resolving a server-supplied UDT assembly-qualified name.
/// </summary>
public class UdtAssemblyPolicyTest
{
    /// <summary>
    /// The public key token that Microsoft signs Microsoft.SqlServer.Types with.
    /// </summary>
    private const string SqlServerTypesPublicKeyToken = "89845dcd8080cc91";

    /// <summary>
    /// An assembly name that is neither loaded into the test process nor
    /// referenced by anything that is.
    /// </summary>
    private const string UnknownAssemblyName = "Contoso.Totally.Unknown.Assembly";

    #region Scope

    /// <summary>
    /// Acquires the app context switch lock, forces the policy switches to
    /// known values, and clears the allow list and every policy cache.  Disposal
    /// restores the original switch values and allow list, and clears the caches
    /// again so no state leaks into the next test.
    /// </summary>
    private sealed class PolicyScope : IDisposable
    {
        private readonly LocalAppContextSwitchesHelper _switches;
        private readonly object? _originalAllowList;

        public PolicyScope(bool legacy = false, bool strict = false)
        {
            _switches = new LocalAppContextSwitchesHelper();
            _originalAllowList =
                AppContext.GetData(UdtAssemblyPolicy.AllowListAppContextDataName);

            _switches.UseLegacyUdtAssemblyLoad = legacy;
            _switches.UseStrictUdtAssemblyLoad = strict;

            SetAllowList(null);
        }

        public static void SetAllowList(string? value)
        {
            AppDomain.CurrentDomain.SetData(
                UdtAssemblyPolicy.AllowListAppContextDataName,
                value);
            UdtAssemblyPolicy.ResetCache();
        }

        public void Dispose()
        {
            AppDomain.CurrentDomain.SetData(
                UdtAssemblyPolicy.AllowListAppContextDataName,
                _originalAllowList);
            UdtAssemblyPolicy.ResetCache();
            _switches.Dispose();
        }
    }

    #endregion

    #region Mode

    /// <summary>
    /// Verifies that the policy defaults to Restricted when neither switch is
    /// set.
    /// </summary>
    [Fact]
    public void Mode_DefaultsToRestricted()
    {
        using PolicyScope scope = new();

        Assert.Equal(UdtAssemblyLoadMode.Restricted, UdtAssemblyPolicy.Mode);
        Assert.False(UdtAssemblyPolicy.LegacyBehaviorEnabled);
    }

    /// <summary>
    /// Verifies that the strict switch selects Strict mode.
    /// </summary>
    [Fact]
    public void Mode_StrictSwitch_SelectsStrict()
    {
        using PolicyScope scope = new(strict: true);

        Assert.Equal(UdtAssemblyLoadMode.Strict, UdtAssemblyPolicy.Mode);
        Assert.False(UdtAssemblyPolicy.LegacyBehaviorEnabled);
    }

    /// <summary>
    /// Verifies that the legacy switch selects Legacy mode and takes precedence
    /// over the strict switch, so an application that has opted back into the
    /// old behavior gets it unambiguously.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mode_LegacySwitch_WinsOverStrict(bool strict)
    {
        using PolicyScope scope = new(legacy: true, strict: strict);

        Assert.Equal(UdtAssemblyLoadMode.Legacy, UdtAssemblyPolicy.Mode);
        Assert.True(UdtAssemblyPolicy.LegacyBehaviorEnabled);
    }

    #endregion

    #region SqlServerTypes

    /// <summary>
    /// Verifies that the built-in SQL Server CLR types assembly is recognized
    /// case-insensitively and is permitted in every non-legacy mode.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAllowed_SqlServerTypes_IsPermitted(bool strict)
    {
        using PolicyScope scope = new(strict: strict);

        Assert.True(UdtAssemblyPolicy.IsSqlServerTypesAssembly(
            new AssemblyName("microsoft.sqlserver.types")));
        Assert.True(UdtAssemblyPolicy.IsAllowed(
            new AssemblyName("Microsoft.SqlServer.Types"), null));
    }

    /// <summary>
    /// Verifies that permitting the built-in types assembly also normalizes both
    /// its version and its public key token, so a server that omits or forges
    /// the token cannot cause a partial-name bind that an unsigned same-named
    /// assembly could satisfy.  The two must happen together: the exemption is
    /// granted on the simple name alone, so an unpinned reference would let an
    /// arbitrary assembly borrow the name.
    /// </summary>
    [Fact]
    public void IsAllowed_SqlServerTypes_PinsVersionAndPublicKeyToken()
    {
        using PolicyScope scope = new(strict: false);

        // A reference as an attacker-controlled server might send it: the right
        // simple name, but a bogus version and no strong-name identity.
        AssemblyName asmRef = new("Microsoft.SqlServer.Types")
        {
            Version = new Version(1, 2, 3, 4),
        };

        Assert.True(UdtAssemblyPolicy.IsAllowed(asmRef, new Version(14, 0, 0, 0)));

        Assert.Equal(new Version(14, 0, 0, 0), asmRef.Version);
        Assert.Equal(
            SqlServerTypesPublicKeyToken,
            ToHex(asmRef.GetPublicKeyToken()));
    }

    /// <summary>
    /// Verifies that pinning overwrites a public key token supplied by the
    /// server rather than trusting it.
    /// </summary>
    [Fact]
    public void IsAllowed_SqlServerTypes_OverwritesServerSuppliedToken()
    {
        using PolicyScope scope = new(strict: false);

        AssemblyName asmRef = new(
            "Microsoft.SqlServer.Types, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef");

        Assert.True(UdtAssemblyPolicy.IsAllowed(asmRef, new Version(11, 0, 0, 0)));

        Assert.Equal(
            SqlServerTypesPublicKeyToken,
            ToHex(asmRef.GetPublicKeyToken()));
    }

    /// <summary>
    /// Verifies that the public key token is still pinned when no type system
    /// version is available to pin the version to, which is the case for callers
    /// that have no connection context.
    /// </summary>
    [Fact]
    public void IsAllowed_SqlServerTypes_WithoutVersion_StillPinsToken()
    {
        using PolicyScope scope = new(strict: false);

        AssemblyName asmRef = new("Microsoft.SqlServer.Types, Version=1.0.0.0");

        Assert.True(UdtAssemblyPolicy.IsAllowed(asmRef, null));

        Assert.Equal(new Version(1, 0, 0, 0), asmRef.Version);
        Assert.Equal(
            SqlServerTypesPublicKeyToken,
            ToHex(asmRef.GetPublicKeyToken()));
    }

    #endregion

    #region Deny by default

    /// <summary>
    /// Verifies that an assembly the process has never heard of is denied in
    /// both enforcing modes.  This is the reporter's scenario: a server-supplied
    /// name that resolves to a DLL planted on the probing path.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAllowed_UnknownAssembly_IsDenied(bool strict)
    {
        using PolicyScope scope = new(strict: strict);

        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that legacy mode permits everything, restoring the behavior that
    /// predates the policy.
    /// </summary>
    [Fact]
    public void IsAllowed_LegacyMode_PermitsEverything()
    {
        using PolicyScope scope = new(legacy: true);

        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    #endregion

    #region Loaded and referenced assemblies

    /// <summary>
    /// Verifies that an assembly already loaded into the process is permitted in
    /// Restricted mode but denied in Strict mode.
    /// </summary>
    [Fact]
    public void IsAllowed_LoadedAssembly_DependsOnMode()
    {
        // This test assembly is, by definition, loaded.
        string loadedName = typeof(UdtAssemblyPolicyTest).Assembly.GetName().Name!;

        using (PolicyScope restricted = new())
        {
            Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(loadedName), null));
        }

        using (PolicyScope strict = new(strict: true))
        {
            Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(loadedName), null));
        }
    }

    /// <summary>
    /// Verifies that an assembly that is statically referenced by a loaded
    /// assembly, but that may not itself be loaded yet, is permitted in
    /// Restricted mode.  This is what keeps lazily-loaded custom UDT assemblies
    /// working.
    /// </summary>
    [Fact]
    public void IsAllowed_ReferencedAssembly_IsPermittedWhenRestricted()
    {
        AssemblyName[] references =
            typeof(UdtAssemblyPolicyTest).Assembly.GetReferencedAssemblies();
        Assert.NotEmpty(references);

        using PolicyScope scope = new();

        foreach (AssemblyName reference in references)
        {
            Assert.True(
                UdtAssemblyPolicy.IsAllowed(new AssemblyName(reference.Name!), null),
                $"Expected referenced assembly '{reference.Name}' to be permitted.");
        }
    }

    #endregion

    #region Allow list

    /// <summary>
    /// Verifies that a simple-name allow list entry permits the assembly in
    /// every enforcing mode, and that it does so regardless of the version,
    /// culture, and public key token the server supplies.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsAllowed_AllowListSimpleName_Permits(bool strict)
    {
        using PolicyScope scope = new(strict: strict);
        PolicyScope.SetAllowList(UnknownAssemblyName);

        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));
        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=9.9.9.9, Culture=neutral, PublicKeyToken=0123456789abcdef"), null));
    }

    /// <summary>
    /// Verifies that allow list matching is case-insensitive on the simple name
    /// and tolerates surrounding whitespace and empty entries.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowList_IgnoresCaseAndWhitespace()
    {
        using PolicyScope scope = new(strict: true);
        PolicyScope.SetAllowList($" ; {UnknownAssemblyName.ToUpperInvariant()} ; ");

        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that a fully-qualified allow list entry is matched on every
    /// component it specifies, so an assembly that merely borrows the simple
    /// name is still denied.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowListFullName_MatchesAllSpecifiedComponents()
    {
        using PolicyScope scope = new(strict: true);
        PolicyScope.SetAllowList(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef");

        // Exact match.
        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef"), null));

        // Wrong version.
        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef"), null));

        // Wrong public key token.
        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=fedcba9876543210"), null));

        // No public key token at all.
        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral"), null));
    }

    /// <summary>
    /// Verifies that a malformed allow list entry is skipped without throwing
    /// and without widening the policy, while valid entries alongside it still
    /// take effect.
    /// </summary>
    [Fact]
    public void IsAllowed_MalformedAllowListEntry_IsSkipped()
    {
        using PolicyScope scope = new(strict: true);
        PolicyScope.SetAllowList($", , Version=bogus ; {UnknownAssemblyName}");

        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));
        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName("Some.Other.Assembly"), null));
    }

    /// <summary>
    /// Verifies that changing the allow list at runtime takes effect, i.e. that
    /// the cached parse is keyed on the source string.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowListChange_IsObserved()
    {
        using PolicyScope scope = new(strict: true);

        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));

        AppDomain.CurrentDomain.SetData(
            UdtAssemblyPolicy.AllowListAppContextDataName,
            UnknownAssemblyName);

        Assert.True(UdtAssemblyPolicy.IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that an assembly reference with no simple name is denied rather
    /// than falling through to a load attempt.
    /// </summary>
    [Fact]
    public void IsAllowed_EmptySimpleName_IsDenied()
    {
        using PolicyScope scope = new();

        Assert.False(UdtAssemblyPolicy.IsAllowed(new AssemblyName(), null));
    }

    #endregion

    #region Helpers

    private static string? ToHex(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        char[] chars = new char[bytes.Length * 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = GetHexDigit(bytes[i] >> 4);
            chars[(i * 2) + 1] = GetHexDigit(bytes[i] & 0xF);
        }

        return new string(chars);
    }

    private static char GetHexDigit(int value) =>
        (char)(value < 10 ? '0' + value : 'a' + (value - 10));

    #endregion
}
