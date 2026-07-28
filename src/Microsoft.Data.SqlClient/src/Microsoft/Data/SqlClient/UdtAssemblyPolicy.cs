// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
/// The policy modes that govern which assemblies the driver is willing to load
/// while resolving a server-supplied UDT assembly-qualified name.
/// </summary>
internal enum UdtAssemblyLoadMode
{
    /// <summary>
    /// Only the pinned <c>Microsoft.SqlServer.Types</c> assembly and assemblies
    /// named on the application-supplied allow list may be loaded.
    /// </summary>
    Strict,

    /// <summary>
    /// The <see cref="Strict"/> set, plus assemblies that are already loaded
    /// into the process and assemblies that are statically referenced by
    /// already-loaded assemblies.  This is the default.
    /// </summary>
    Restricted,

    /// <summary>
    /// Any assembly named by the server may be loaded.  This restores the
    /// behavior of the driver prior to the introduction of this policy and is
    /// not recommended.
    /// </summary>
    Legacy
}

/// <summary>
/// Decides whether the driver may load an assembly named by a server-supplied
/// UDT assembly-qualified name.
///
/// A TDS response describing a UDT column or output parameter carries an
/// <c>AssemblyQualifiedName</c> that the driver must resolve to a CLR
/// <see cref="Type"/>.  Resolving it involves loading the named assembly, and
/// loading an assembly executes that assembly's module initializer.  A server
/// (or an on-path attacker against a connection that has opted out of
/// certificate validation) therefore gets to choose which assembly the client
/// process loads unless the driver constrains the choice, which is what this
/// class does.
///
/// The evaluation is deliberately cheap: apart from a one-time subscription to
/// <see cref="AppDomain.AssemblyLoad"/>, a decision is a couple of hash-set
/// lookups.  The set of known assembly names is rebuilt only when an assembly
/// is actually loaded into the process, so a hostile server that streams a
/// large number of distinct assembly names cannot force repeated disk probing.
/// </summary>
internal static class UdtAssemblyPolicy
{
    #region Constants

    /// <summary>
    /// The simple name of the assembly that ships the built-in SQL Server CLR
    /// types (geography, geometry, hierarchyid).  It is always permitted, but
    /// only with the identity pinned by
    /// <see cref="s_sqlServerTypesPublicKeyToken"/>.
    /// </summary>
    internal const string SqlServerTypesAssemblyName = "Microsoft.SqlServer.Types";

    /// <summary>
    /// The name of the AppContext data element that holds the application's
    /// UDT assembly allow list.  The value is a string containing one or more
    /// assembly names separated by semicolons.  An entry may be a simple name
    /// (<c>Contoso.Udts</c>), in which case only the simple name is compared,
    /// or a full assembly name
    /// (<c>Contoso.Udts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=...</c>),
    /// in which case every component that the entry specifies must also match.
    /// </summary>
    internal const string AllowListAppContextDataName =
        "Microsoft.Data.SqlClient.UdtAssemblyAllowList";

    /// <summary>
    /// The public key token that every shipped build of
    /// <c>Microsoft.SqlServer.Types</c> is signed with.
    /// </summary>
    private static readonly byte[] s_sqlServerTypesPublicKeyToken =
        { 0x89, 0x84, 0x5d, 0xcd, 0x80, 0x80, 0xcc, 0x91 };

    #endregion

    #region Fields

    /// <summary>
    /// Guards the cached allow list and known-assembly-name set.
    /// </summary>
    private static readonly object s_lock = new();

    /// <summary>
    /// Incremented every time an assembly is loaded into the process.  Used to
    /// invalidate <see cref="s_knownAssemblyNames"/>.  Written with
    /// <see cref="Interlocked"/> from the <see cref="AppDomain.AssemblyLoad"/>
    /// callback and only read while <see cref="s_lock"/> is held.
    /// </summary>
    private static int s_assemblyLoadVersion;

    /// <summary>
    /// Set to true once the <see cref="AppDomain.AssemblyLoad"/> handler has
    /// been attached.  The handler is attached lazily so that applications that
    /// never read a UDT value never pay for it.
    /// </summary>
    private static bool s_assemblyLoadHandlerAttached;

    /// <summary>
    /// The simple names of every assembly that is loaded into the process, plus
    /// the simple names of every assembly they statically reference.  Null when
    /// it has not been built yet.
    /// </summary>
    private static HashSet<string>? s_knownAssemblyNames;

    /// <summary>
    /// The value of <see cref="s_assemblyLoadVersion"/> at the time
    /// <see cref="s_knownAssemblyNames"/> was built.
    /// </summary>
    private static int s_knownAssemblyNamesVersion = -1;

    /// <summary>
    /// The raw allow list string that <see cref="s_allowList"/> was parsed
    /// from, used to detect that the application has changed it.
    /// </summary>
    private static string? s_allowListSource;

    /// <summary>
    /// The parsed allow list.  Null when it has not been parsed yet.
    /// </summary>
    private static List<AssemblyName>? s_allowList;

    #endregion

    #region Properties

    /// <summary>
    /// The policy mode currently in effect.
    /// </summary>
    internal static UdtAssemblyLoadMode Mode
    {
        get
        {
            // Legacy wins over Strict so that an application that has opted
            // back into the old behavior gets it unambiguously.
            if (LocalAppContextSwitches.UseLegacyUdtAssemblyLoad)
            {
                return UdtAssemblyLoadMode.Legacy;
            }

            return LocalAppContextSwitches.UseStrictUdtAssemblyLoad
                ? UdtAssemblyLoadMode.Strict
                : UdtAssemblyLoadMode.Restricted;
        }
    }

    /// <summary>
    /// True when the policy has been disabled entirely in favor of the
    /// pre-policy behavior.
    /// </summary>
    internal static bool LegacyBehaviorEnabled => Mode == UdtAssemblyLoadMode.Legacy;

    #endregion

    #region Methods

    /// <summary>
    /// Determines whether <paramref name="asmRef"/> names the built-in SQL
    /// Server CLR types assembly.
    /// </summary>
    internal static bool IsSqlServerTypesAssembly(AssemblyName asmRef) =>
        string.Equals(asmRef.Name, SqlServerTypesAssemblyName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Pins the identity of the built-in SQL Server CLR types assembly.
    ///
    /// The version is normalized to the type system version negotiated for the
    /// connection, which is long-standing behavior: the server advertises the
    /// version it holds, and the client instantiates the version it has.
    ///
    /// The public key token is normalized to the token that Microsoft signs the
    /// assembly with.  Without this, a server that omits the token (or supplies
    /// a different one) would cause a partial-name bind that an unsigned
    /// same-named assembly on the probing path could satisfy.
    /// </summary>
    /// <param name="asmRef">The assembly reference to normalize, in place.</param>
    /// <param name="typeSystemAssemblyVersion">
    /// The type system assembly version negotiated for the connection.
    /// </param>
    internal static void PinSqlServerTypesIdentity(AssemblyName asmRef, Version typeSystemAssemblyVersion)
    {
        asmRef.Version = typeSystemAssemblyVersion;
        asmRef.SetPublicKeyToken((byte[])s_sqlServerTypesPublicKeyToken.Clone());
    }

    /// <summary>
    /// Determines whether the driver is permitted to load the assembly named by
    /// <paramref name="asmRef"/>.
    /// </summary>
    /// <param name="asmRef">The server-supplied assembly reference.</param>
    /// <returns>True when the assembly may be loaded.</returns>
    internal static bool IsAllowed(AssemblyName asmRef)
    {
        UdtAssemblyLoadMode mode = Mode;

        if (mode == UdtAssemblyLoadMode.Legacy)
        {
            return true;
        }

        string? simpleName = asmRef.Name;
        if (string.IsNullOrEmpty(simpleName))
        {
            return false;
        }

        // The built-in types assembly is always permitted.  Its identity has
        // already been pinned by the caller, so this cannot be satisfied by an
        // arbitrary assembly that merely borrows the name.
        if (IsSqlServerTypesAssembly(asmRef))
        {
            return true;
        }

        if (MatchesAllowList(asmRef))
        {
            return true;
        }

        if (mode == UdtAssemblyLoadMode.Restricted && IsKnownToProcess(simpleName!))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Discards all cached state.  Intended for use by tests, which need to
    /// observe the effect of changing the allow list or the policy switches.
    /// </summary>
    internal static void ResetCache()
    {
        lock (s_lock)
        {
            s_allowList = null;
            s_allowListSource = null;
            s_knownAssemblyNames = null;
            s_knownAssemblyNamesVersion = -1;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Determines whether <paramref name="asmRef"/> matches an entry on the
    /// application-supplied allow list.
    /// </summary>
    private static bool MatchesAllowList(AssemblyName asmRef)
    {
        List<AssemblyName> allowList = GetAllowList();

        for (int i = 0; i < allowList.Count; i++)
        {
            if (Matches(allowList[i], asmRef))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a server-supplied assembly reference satisfies an
    /// allow list entry.  Only the components that the entry actually specifies
    /// are compared, so a simple-name entry permits any version, culture, and
    /// public key token.
    /// </summary>
    private static bool Matches(AssemblyName allowed, AssemblyName candidate)
    {
        if (!string.Equals(allowed.Name, candidate.Name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (allowed.Version is not null && !allowed.Version.Equals(candidate.Version))
        {
            return false;
        }

        // AssemblyName.CultureName is the empty string for the neutral culture
        // and null when the entry did not specify a culture at all.
        if (allowed.CultureName is not null &&
            !string.Equals(allowed.CultureName, candidate.CultureName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        byte[]? allowedToken = allowed.GetPublicKeyToken();
        if (allowedToken is { Length: > 0 })
        {
            byte[]? candidateToken = candidate.GetPublicKeyToken();
            if (candidateToken is null || candidateToken.Length != allowedToken.Length)
            {
                return false;
            }

            for (int i = 0; i < allowedToken.Length; i++)
            {
                if (allowedToken[i] != candidateToken[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the parsed allow list, re-parsing it if the application has
    /// changed the underlying AppContext data since it was last read.
    /// </summary>
    private static List<AssemblyName> GetAllowList()
    {
        string source = AppContext.GetData(AllowListAppContextDataName) as string ?? string.Empty;

        lock (s_lock)
        {
            if (s_allowList is not null && string.Equals(s_allowListSource, source, StringComparison.Ordinal))
            {
                return s_allowList;
            }

            List<AssemblyName> parsed = new();

            foreach (string entry in source.Split(';'))
            {
                string trimmed = entry.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                try
                {
                    AssemblyName name = new(trimmed);
                    if (!string.IsNullOrEmpty(name.Name))
                    {
                        parsed.Add(name);
                    }
                }
                catch (Exception e) when (ADP.IsCatchableExceptionType(e))
                {
                    // A malformed entry must not take down the application, and
                    // it must not silently widen the policy either, so it is
                    // traced and skipped.
                    SqlClientEventSource.Log.TryTraceEvent(
                        "UdtAssemblyPolicy.GetAllowList | ERR | Ignoring malformed UDT assembly allow list entry '{0}'.",
                        trimmed);
                }
            }

            s_allowList = parsed;
            s_allowListSource = source;

            return parsed;
        }
    }

    /// <summary>
    /// Determines whether an assembly with the given simple name is already
    /// loaded into the process, or is statically referenced by an assembly that
    /// is.
    /// </summary>
    private static bool IsKnownToProcess(string simpleName) =>
        GetKnownAssemblyNames().Contains(simpleName);

    /// <summary>
    /// Returns the set of assembly simple names that are loaded into the
    /// process or referenced by an assembly that is, rebuilding it only if an
    /// assembly has been loaded since it was last built.
    /// </summary>
    private static HashSet<string> GetKnownAssemblyNames()
    {
        EnsureAssemblyLoadHandlerAttached();

        lock (s_lock)
        {
            int version = s_assemblyLoadVersion;

            if (s_knownAssemblyNames is not null && s_knownAssemblyNamesVersion == version)
            {
                return s_knownAssemblyNames;
            }

            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                {
                    // A dynamic assembly has no manifest to read references
                    // from, and it cannot be a target of Assembly.Load by name
                    // anyway.
                    continue;
                }

                string? name = assembly.GetName().Name;
                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name!);
                }

                try
                {
                    foreach (AssemblyName reference in assembly.GetReferencedAssemblies())
                    {
                        if (!string.IsNullOrEmpty(reference.Name))
                        {
                            names.Add(reference.Name!);
                        }
                    }
                }
                catch (Exception e) when (ADP.IsCatchableExceptionType(e))
                {
                    // Reading the reference list can fail for assemblies loaded
                    // from a byte array or produced by a trimmer.  Losing one
                    // assembly's references only makes the policy stricter.
                    SqlClientEventSource.Log.TryTraceEvent(
                        "UdtAssemblyPolicy.GetKnownAssemblyNames | INFO | Unable to read references of '{0}'.",
                        name);
                }
            }

            s_knownAssemblyNames = names;
            s_knownAssemblyNamesVersion = version;

            return names;
        }
    }

    /// <summary>
    /// Attaches the assembly load handler that invalidates the cached
    /// known-assembly-name set, if it has not been attached already.
    /// </summary>
    private static void EnsureAssemblyLoadHandlerAttached()
    {
        lock (s_lock)
        {
            if (s_assemblyLoadHandlerAttached)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyLoad += static (_, _) =>
                Interlocked.Increment(ref s_assemblyLoadVersion);

            s_assemblyLoadHandlerAttached = true;
        }
    }

    #endregion
}
