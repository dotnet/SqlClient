// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient.Tests.Common;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Provides unit tests for <see cref="UdtAssemblyPolicy"/>, the deny-by-default
/// policy that governs which assemblies the driver is willing to load while
/// resolving a server-supplied UDT assembly-qualified name.
/// </summary>
[Collection(AppContextSwitchTestCollection.Name)]
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

    /// <summary>
    /// Asks the policy for a decision, discarding the resolved assembly.  Most
    /// tests care only whether the reference was permitted.
    /// </summary>
    private static bool IsAllowed(AssemblyName asmRef, Version? typeSystemAssemblyVersion) =>
        UdtAssemblyPolicy.TryResolve(asmRef, typeSystemAssemblyVersion, out _);

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

        public PolicyScope(bool legacy = false)
        {
            _switches = new LocalAppContextSwitchesHelper();
            _originalAllowList =
                AppContext.GetData(UdtAssemblyPolicy.AllowListAppContextDataName);

            _switches.UseLegacyUdtAssemblyLoad = legacy;

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

    #region Enforcement

    /// <summary>
    /// Verifies that the policy enforces by default, and that there is exactly
    /// one enforcing behavior: the only alternative is the legacy escape hatch.
    /// </summary>
    [Fact]
    public void Policy_EnforcesByDefault()
    {
        using PolicyScope scope = new();

        Assert.False(UdtAssemblyPolicy.LegacyBehaviorEnabled);
    }

    /// <summary>
    /// Verifies that the legacy switch disables the policy entirely.
    /// </summary>
    [Fact]
    public void Policy_LegacySwitch_DisablesEnforcement()
    {
        using PolicyScope scope = new(legacy: true);

        Assert.True(UdtAssemblyPolicy.LegacyBehaviorEnabled);
    }

    #endregion

    #region SqlServerTypes

    /// <summary>
    /// Verifies that the built-in SQL Server CLR types assembly is recognized
    /// case-insensitively and is permitted in every non-legacy mode.
    /// </summary>
    [Fact]
    public void IsAllowed_SqlServerTypes_IsPermitted()
    {
        using PolicyScope scope = new();

        Assert.True(UdtAssemblyPolicy.IsSqlServerTypesAssembly(
            new AssemblyName("microsoft.sqlserver.types")));
        Assert.True(IsAllowed(
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
        using PolicyScope scope = new();

        // A reference as an attacker-controlled server might send it: the right
        // simple name, but a bogus version and no strong-name identity.
        AssemblyName asmRef = new("Microsoft.SqlServer.Types")
        {
            Version = new Version(1, 2, 3, 4),
        };

        Assert.True(IsAllowed(asmRef, new Version(14, 0, 0, 0)));

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
        using PolicyScope scope = new();

        AssemblyName asmRef = new(
            "Microsoft.SqlServer.Types, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef");

        Assert.True(IsAllowed(asmRef, new Version(11, 0, 0, 0)));

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
        using PolicyScope scope = new();

        AssemblyName asmRef = new("Microsoft.SqlServer.Types, Version=1.0.0.0");

        Assert.True(IsAllowed(asmRef, null));

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
    [Fact]
    public void IsAllowed_UnknownAssembly_IsDenied()
    {
        using PolicyScope scope = new();

        Assert.False(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that legacy mode permits everything, restoring the behavior that
    /// predates the policy.
    /// </summary>
    [Fact]
    public void IsAllowed_LegacyMode_PermitsEverything()
    {
        using PolicyScope scope = new(legacy: true);

        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    #endregion

    #region Loaded assemblies

    /// <summary>
    /// Verifies that an assembly already loaded into the process is permitted.
    /// Re-resolving a loaded assembly cannot bring anything new into the
    /// process, so this tier costs nothing.
    /// </summary>
    [Fact]
    public void Resolve_LoadedAssembly_IsPermitted()
    {
        // This test assembly is, by definition, loaded.
        Assembly self = typeof(UdtAssemblyPolicyTest).Assembly;

        using PolicyScope scope = new();

        Assert.True(UdtAssemblyPolicy.TryResolve(
            new AssemblyName(self.GetName().Name!), null, out Assembly? resolved));
        Assert.Same(self, resolved);
    }

    /// <summary>
    /// Verifies that a reference permitted because the process already holds
    /// that simple name resolves to the loaded instance, and that the
    /// server-supplied version and public key token are discarded.
    /// </summary>
    /// <remarks>
    /// Matching on the simple name and then handing the server's full reference
    /// to the loader would let a server name a loaded assembly with a different
    /// identity and still cause a genuinely new load, which is precisely what
    /// this tier must not permit.
    /// </remarks>
    [Fact]
    public void Resolve_LoadedAssembly_IgnoresServerSuppliedIdentity()
    {
        Assembly self = typeof(UdtAssemblyPolicyTest).Assembly;
        string simpleName = self.GetName().Name!;

        using PolicyScope scope = new();

        AssemblyName hostile = new(
            $"{simpleName}, Version=9.9.9.9, Culture=neutral, PublicKeyToken=0123456789abcdef");

        Assert.True(UdtAssemblyPolicy.TryResolve(hostile, null, out Assembly? resolved));
        Assert.Same(self, resolved);
        Assert.NotEqual(new Version(9, 9, 9, 9), resolved!.GetName().Version);
    }

    /// <summary>
    /// Verifies that an assembly which is merely statically referenced by a
    /// loaded assembly, but is not itself loaded, is denied.
    /// </summary>
    /// <remarks>
    /// Loading a referenced-but-unloaded assembly is a genuinely new load, and
    /// keeping new loads under the application's control rather than the
    /// server's is the entire point of this policy.  An application whose custom
    /// UDT assembly is not yet loaded must name it on the allow list.
    /// </remarks>
    [Fact]
    public void Resolve_ReferencedButUnloadedAssembly_IsDenied()
    {
        HashSet<string> loaded = new(
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic)
                .Select(a => a.GetName().Name!),
            StringComparer.OrdinalIgnoreCase);

        AssemblyName? referencedNotLoaded = typeof(UdtAssemblyPolicyTest).Assembly
            .GetReferencedAssemblies()
            .FirstOrDefault(r => !loaded.Contains(r.Name!));

        if (referencedNotLoaded is null)
        {
            // Every referenced assembly happens to be loaded in this run, so
            // there is nothing here to distinguish. The companion test
            // Resolve_UnknownAssembly_IsDenied covers the general deny path.
            return;
        }

        using PolicyScope scope = new();

        Assert.False(IsAllowed(new AssemblyName(referencedNotLoaded.Name!), null));
    }

    #endregion

    #region Allow list

    /// <summary>
    /// Verifies that a simple-name allow list entry permits the assembly in
    /// every enforcing mode, and that it does so regardless of the version,
    /// culture, and public key token the server supplies.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowListSimpleName_Permits()
    {
        using PolicyScope scope = new();
        PolicyScope.SetAllowList(UnknownAssemblyName);

        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
        Assert.True(IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=9.9.9.9, Culture=neutral, PublicKeyToken=0123456789abcdef"), null));
    }

    /// <summary>
    /// Verifies that allow list matching is case-insensitive on the simple name
    /// and tolerates surrounding whitespace and empty entries.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowList_IgnoresCaseAndWhitespace()
    {
        using PolicyScope scope = new();
        PolicyScope.SetAllowList($" ; {UnknownAssemblyName.ToUpperInvariant()} ; ");

        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that a fully-qualified allow list entry is matched on every
    /// component it specifies, so an assembly that merely borrows the simple
    /// name is still denied.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowListFullName_MatchesAllSpecifiedComponents()
    {
        using PolicyScope scope = new();
        PolicyScope.SetAllowList(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef");

        // Exact match.
        Assert.True(IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef"), null));

        // Wrong version.
        Assert.False(IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=2.0.0.0, Culture=neutral, PublicKeyToken=0123456789abcdef"), null));

        // Wrong public key token.
        Assert.False(IsAllowed(new AssemblyName(
            $"{UnknownAssemblyName}, Version=1.0.0.0, Culture=neutral, PublicKeyToken=fedcba9876543210"), null));

        // No public key token at all.
        Assert.False(IsAllowed(new AssemblyName(
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
        using PolicyScope scope = new();
        PolicyScope.SetAllowList($", , Version=bogus ; {UnknownAssemblyName}");

        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
        Assert.False(IsAllowed(new AssemblyName("Some.Other.Assembly"), null));
    }

    /// <summary>
    /// Verifies that changing the allow list at runtime takes effect, i.e. that
    /// the cached parse is keyed on the source string.
    /// </summary>
    [Fact]
    public void IsAllowed_AllowListChange_IsObserved()
    {
        using PolicyScope scope = new();

        Assert.False(IsAllowed(new AssemblyName(UnknownAssemblyName), null));

        AppDomain.CurrentDomain.SetData(
            UdtAssemblyPolicy.AllowListAppContextDataName,
            UnknownAssemblyName);

        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that an assembly reference with no simple name is denied rather
    /// than falling through to a load attempt.
    /// </summary>
    [Fact]
    public void IsAllowed_EmptySimpleName_IsDenied()
    {
        using PolicyScope scope = new();

        Assert.False(IsAllowed(new AssemblyName(), null));
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
