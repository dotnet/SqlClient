// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
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
        UdtAssemblyPolicy.IsPermitted(asmRef, typeSystemAssemblyVersion, out _);

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
                AppDomain.CurrentDomain.GetData(UdtAssemblyPolicy.AllowListAppContextDataName);

            _switches.UseLegacyUdtAssemblyLoad = legacy;

            SetAllowList(null);
        }

        /// <summary>
        /// Enters an enforcing scope with the given allow list already applied.
        /// </summary>
        public PolicyScope(string? allowList)
            : this(legacy: false)
        {
            SetAllowList(allowList);
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

        Assert.True(UdtAssemblyPolicy.IsPermitted(
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

        Assert.True(UdtAssemblyPolicy.IsPermitted(hostile, null, out Assembly? resolved));
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

        // The driver references a number of assemblies that a unit test run
        // never causes to be loaded (the identity and Azure stacks, for
        // example), which makes this a far more reliable source of a
        // referenced-but-unloaded assembly than the test assembly's own
        // references.
        AssemblyName? referencedNotLoaded = typeof(SqlConnection).Assembly
            .GetReferencedAssemblies()
            .FirstOrDefault(r => !loaded.Contains(r.Name!));

        // Assert the precondition rather than returning quietly. If every
        // referenced assembly is loaded then this test proves nothing, and that
        // should be visible rather than counted as a pass.
        Assert.True(
            referencedNotLoaded is not null,
            "Expected the driver to reference at least one assembly that is not loaded, " +
            "so that the referenced-is-not-trusted rule can be exercised.");

        using PolicyScope scope = new();

        Assert.False(IsAllowed(new AssemblyName(referencedNotLoaded!.Name!), null));
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

    #region Identity enforcement

    /// <summary>
    /// Verifies that an assembly whose simple name differs from the one the
    /// policy permitted is refused, even when the allow list entry constrained
    /// nothing else.
    /// </summary>
    /// <remarks>
    /// Every basis for permitting a load rests on the simple name, so a
    /// resolver that answers the request with an unrelated assembly would
    /// otherwise have it inherit a permission that was never granted to it.
    /// A simple-name entry is the weakest case: it constrains no version,
    /// culture or token, so the name is the only thing left to verify.
    /// </remarks>
    [Fact]
    public void TryLoad_ResolverReturnsDifferentAssembly_IsRefused()
    {
        using PolicyScope scope = new(UnknownAssemblyName);

        Assembly substitute = typeof(string).Assembly;

        Assert.NotEqual(
            UnknownAssemblyName,
            substitute.GetName().Name,
            StringComparer.OrdinalIgnoreCase);

        ResolveEventHandler handler = (_, args) =>
            new AssemblyName(args.Name).Name == UnknownAssemblyName ? substitute : null;

        AppDomain.CurrentDomain.AssemblyResolve += handler;

        try
        {
            bool loaded = UdtAssemblyPolicy.TryLoad(
                new AssemblyName(UnknownAssemblyName),
                null,
                out Assembly? assembly);

            Assert.False(loaded);
            Assert.Null(assembly);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }
    }

    /// <summary>
    /// Verifies that an assembly which first arrives in the process during a
    /// policy-triggered load is not subsequently permitted on the strength of
    /// being "already loaded".
    /// </summary>
    /// <remarks>
    /// The already-loaded tier is meant to reflect only what the application
    /// brought in of its own accord.  The loaded-assembly map is built lazily,
    /// so if the first policy call is permitted by the allow list, the load it
    /// performs happens before the map exists and its dependencies would be
    /// captured by the later snapshot, silently inheriting that permission.
    /// This is the transitive trust the policy documents as denied.
    /// </remarks>
    [Fact]
    public void AlreadyLoaded_AssemblyArrivingDuringPolicyLoad_IsNotPermitted()
    {
        // An assembly that ships with the framework but is not loaded in this
        // process, standing in for a dependency pulled in by a permitted load.
        string? dependencyPath = FindUnloadedFrameworkAssembly(out string? dependencyName);

        Assert.NotNull(dependencyPath);
        Assert.NotNull(dependencyName);

        using PolicyScope scope = new(UnknownAssemblyName);

        // Drop the map so this is the first policy call, which is the ordering
        // the bug depended on.
        UdtAssemblyPolicy.ResetCache();

        ResolveEventHandler handler = (_, args) =>
        {
            if (new AssemblyName(args.Name).Name == UnknownAssemblyName)
            {
                // Bring the stand-in dependency into the process during the
                // policy's own load, then decline to satisfy the request.
                Assembly.LoadFrom(dependencyPath!);
            }

            return null;
        };

        AppDomain.CurrentDomain.AssemblyResolve += handler;

        try
        {
            UdtAssemblyPolicy.TryLoad(new AssemblyName(UnknownAssemblyName), null, out _);
        }
        catch (FileNotFoundException)
        {
            // Expected: the handler declines to satisfy the request, so the
            // load fails.  Production callers catch this the same way.  What
            // matters is the side effect it had on the loaded-assembly map.
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= handler;
        }

        // The dependency is now loaded, but the application never asked for it,
        // so a server naming it must still be refused.
        Assert.False(IsAllowed(new AssemblyName(dependencyName!), null));
    }

    /// <summary>
    /// Locates a framework assembly that is present on disk but not loaded in
    /// this process, to stand in for a dependency arriving during a load.
    /// </summary>
    private static string? FindUnloadedFrameworkAssembly(out string? simpleName)
    {
        HashSet<string> loaded = new(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                string? name = assembly.GetName().Name;

                if (name is not null)
                {
                    loaded.Add(name);
                }
            }
            catch
            {
                // An assembly whose name cannot be read cannot collide with the
                // candidate either, so it is simply skipped.
            }
        }

        string directory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;

        foreach (string path in Directory.GetFiles(directory, "System.*.dll"))
        {
            string candidate = Path.GetFileNameWithoutExtension(path);

            if (!loaded.Contains(candidate))
            {
                simpleName = candidate;

                return path;
            }
        }

        simpleName = null;

        return null;
    }

    /// <summary>
    /// Verifies that an allow list entry which explicitly requires an unsigned
    /// assembly (<c>PublicKeyToken=null</c>) is not satisfied by a signed one.
    /// </summary>
    /// <remarks>
    /// AssemblyName represents an omitted public key token as null and an
    /// explicit <c>PublicKeyToken=null</c> as a zero-length array.  Treating the
    /// two alike would silently widen an entry that was written to pin an
    /// unsigned assembly into one that accepts any identity.
    /// </remarks>
    [Fact]
    public void AllowList_ExplicitNullToken_DoesNotPermitSignedAssembly()
    {
        using PolicyScope scope = new($"{UnknownAssemblyName}, PublicKeyToken=null");

        // The entry is satisfied by an unsigned candidate.
        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));

        // ... but not by one that carries a strong name token.
        AssemblyName signed = new(UnknownAssemblyName);
        signed.SetPublicKeyToken(new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a });

        Assert.False(IsAllowed(signed, null));
    }

    /// <summary>
    /// Verifies that an allow list entry which omits the public key token still
    /// permits any identity, which is the documented simple-name behavior.
    /// </summary>
    [Fact]
    public void AllowList_OmittedToken_PermitsAnyIdentity()
    {
        using PolicyScope scope = new(UnknownAssemblyName);

        AssemblyName signed = new(UnknownAssemblyName);
        signed.SetPublicKeyToken(new byte[] { 0xb0, 0x3f, 0x5f, 0x7f, 0x11, 0xd5, 0x0a, 0x3a });

        Assert.True(IsAllowed(signed, null));
        Assert.True(IsAllowed(new AssemblyName(UnknownAssemblyName), null));
    }

    /// <summary>
    /// Verifies that the culture of the built-in SQL CLR types assembly is
    /// pinned to neutral rather than left under the server's control.
    /// </summary>
    /// <remarks>
    /// The shipped assembly is culture neutral.  Leaving a server-supplied
    /// <c>Culture=</c> in place would let the reference steer the bind towards a
    /// name the real assembly never uses.
    /// </remarks>
    [Fact]
    public void SqlServerTypes_PinsCultureToNeutral()
    {
        using PolicyScope scope = new();

        AssemblyName asmRef = new(
            $"{UdtAssemblyPolicy.SqlServerTypesAssemblyName}, Version=14.0.0.0, Culture=en-US");

        Assert.True(IsAllowed(asmRef, new Version(14, 0, 0, 0)));

        // AssemblyName represents the neutral culture as the empty string.
        Assert.Equal(string.Empty, asmRef.CultureName);
    }

    /// <summary>
    /// Verifies that an assembly the loader returns with an identity that does
    /// not match what the policy required is refused.
    /// </summary>
    /// <remarks>
    /// The two supported runtimes refuse at different points, and this test
    /// accepts either, because both are the same outcome: the assembly is not
    /// used.
    ///
    /// On .NET the loader ignores the public key token in an AssemblyName, so
    /// it returns the real assembly and the policy's own post-load check is what
    /// rejects it. On .NET Framework the loader enforces the strong name during
    /// binding and throws instead, which is the same distinction
    /// SqlAuthenticationProviderManager documents. Asserting only the .NET shape
    /// would fail on net462, and asserting only the net462 shape would let the
    /// post-load check regress unnoticed on .NET.
    ///
    /// The subject is a framework assembly rather than the test assembly so the
    /// test does not depend on whether the build is strong-name signed.
    /// </remarks>
    [Fact]
    public void TryLoad_AssemblyWithWrongToken_IsRefused()
    {
        // A framework assembly is certain to exist and to carry a strong name
        // token that is not the fabricated one below.
        AssemblyName subject = typeof(object).Assembly.GetName();

        byte[] wrongToken = { 0xde, 0xad, 0xbe, 0xef, 0xde, 0xad, 0xbe, 0xef };

        Assert.False(
            wrongToken.AsSpan().SequenceEqual((subject.GetPublicKeyToken() ?? Array.Empty<byte>()).AsSpan()),
            "The fabricated token must differ from the real one for this test to mean anything.");

        using PolicyScope scope = new($"{subject.Name}, PublicKeyToken={ToHex(wrongToken)}");

        // The server names the assembly with the very token the allow list
        // requires, so the entry matches and the load proceeds. Without a
        // post-load check the assembly would then be accepted on the strength of
        // a token it does not actually carry.
        AssemblyName serverSupplied = new(subject.Name!);
        serverSupplied.SetPublicKeyToken((byte[])wrongToken.Clone());

        bool permitted;
        Assembly? loaded = null;

        try
        {
            permitted = UdtAssemblyPolicy.TryLoad(serverSupplied, null, out loaded);
        }
        catch (Exception e) when (e is FileLoadException or FileNotFoundException or BadImageFormatException)
        {
            // .NET Framework: the loader refused the bind outright.
            return;
        }

        // .NET: the loader returned the real assembly and the policy rejected it.
        Assert.False(permitted);
        Assert.Null(loaded);
    }

    /// <summary>
    /// Verifies that a permitted, genuinely loadable assembly is returned by the
    /// load path, so that the identity checks above are not simply refusing
    /// everything.
    /// </summary>
    [Fact]
    public void TryLoad_AllowListedAssembly_IsLoaded()
    {
        AssemblyName self = typeof(UdtAssemblyPolicyTest).Assembly.GetName();

        using PolicyScope scope = new(self.Name!);

        Assert.True(UdtAssemblyPolicy.TryLoad(new AssemblyName(self.Name!), null, out Assembly? loaded));
        Assert.NotNull(loaded);
        Assert.Equal(self.Name, loaded!.GetName().Name);
    }

    /// <summary>
    /// Verifies that a version constraint the policy relied on is confirmed
    /// against the assembly that was actually loaded.
    /// </summary>
    /// <remarks>
    /// A binding redirect on .NET Framework, or a custom resolver on .NET, can
    /// satisfy a request with a different version than the one asked for. An
    /// allow list entry that pinned a version must therefore not be satisfied by
    /// whatever the loader chose to substitute.
    /// </remarks>
    [Fact]
    public void TryLoad_AssemblyWithWrongVersion_IsRefused()
    {
        AssemblyName subject = typeof(object).Assembly.GetName();

        Version wrongVersion = new(subject.Version!.Major + 100, 0, 0, 0);

        using PolicyScope scope = new(
            $"{subject.Name}, Version={wrongVersion}, PublicKeyToken={ToHex(subject.GetPublicKeyToken())}");

        AssemblyName serverSupplied = new(subject.Name!) { Version = wrongVersion };
        serverSupplied.SetPublicKeyToken(subject.GetPublicKeyToken());

        bool permitted;
        Assembly? loaded = null;

        try
        {
            permitted = UdtAssemblyPolicy.TryLoad(serverSupplied, null, out loaded);
        }
        catch (Exception e) when (e is FileLoadException or FileNotFoundException or BadImageFormatException)
        {
            return;
        }

        Assert.False(permitted);
        Assert.Null(loaded);
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
