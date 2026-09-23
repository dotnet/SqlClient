// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
#if NET
using System.Runtime.Loader;
#endif
using Microsoft.Data.Common;
using Microsoft.Data.SqlClient.Internal;

#nullable enable

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Decides whether the driver may load an assembly named by a server-supplied
/// UDT assembly-qualified name.
///
/// A TDS response describing a UDT column or output parameter carries an
/// <c>AssemblyQualifiedName</c> that the driver must resolve to a CLR
/// <see cref="Type"/>.  A server (or an on-path attacker against a connection
/// that has opted out of certificate validation) therefore gets to choose which
/// assembly the client process loads unless the driver constrains the choice,
/// which is what this class does.
///
/// There is a single enforcing behavior.  An assembly may be loaded when it is
/// the built-in <c>Microsoft.SqlServer.Types</c> assembly with its identity
/// pinned, when the application has named it on the allow list, or when it is
/// already loaded into the process.  Everything else is refused.
///
/// The already-loaded case is free: re-loading an assembly that the process has
/// already loaded returns the existing instance and introduces nothing new.
/// Assemblies that are merely statically referenced are deliberately *not*
/// permitted, because loading one is a genuinely new load, which is the thing
/// this policy exists to keep under the application's control rather than the
/// server's.  An application whose custom UDT assembly is not loaded at the time
/// its first UDT value arrives must name it on the allow list.
///
/// Note that loading an assembly is not by itself the point at which foreign
/// code runs: on CoreCLR neither <see cref="Assembly.Load(AssemblyName)"/>, nor
/// resolving a type from it, nor reading that type's custom attributes executes
/// anything from the target assembly; a module initializer runs on first real
/// access to a member.  That final gate is
/// <c>SqlConnection.CheckGetExtendedUDTInfo</c>, which requires
/// <c>SqlUserDefinedTypeAttribute</c> before <c>GetUdtValue</c> may invoke
/// anything.  This class is the layer in front of it, limiting which assemblies
/// a server can cause to be pulled into the process at all.
///
/// The evaluation is deliberately cheap: apart from a one-time subscription to
/// <see cref="AppDomain.AssemblyLoad"/>, a decision is a dictionary lookup.  The
/// map of loaded assemblies is maintained incrementally, so a hostile server
/// that streams a large number of distinct assembly names cannot force repeated
/// enumeration or disk probing.
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

    #if NET
    /// <summary>
    /// The load context that the driver's own <see cref="Assembly.Load(AssemblyName)"/>
    /// calls resolve into, which is the one that loaded the driver itself.
    /// </summary>
    private static readonly AssemblyLoadContext? s_driverLoadContext =
        AssemblyLoadContext.GetLoadContext(typeof(UdtAssemblyPolicy).Assembly);
    #endif

    #endregion

    #region Fields

    /// <summary>
    /// Guards the cached allow list and known-assembly-name set.
    /// </summary>
    private static readonly object s_lock = new();

    /// <summary>
    /// Set to true once the <see cref="AppDomain.AssemblyLoad"/> handler has
    /// been attached.  The handler is attached lazily so that applications that
    /// never read a UDT value never pay for it.
    /// </summary>
    private static bool s_assemblyLoadHandlerAttached;

    /// <summary>
    /// Maps the simple name of every assembly loaded into the process to the
    /// loaded instance.  Null when it has not been built yet.
    ///
    /// The instance is retained, not just the name, so that a reference which
    /// is permitted because the process has already loaded that simple name is
    /// satisfied with the assembly the process actually holds.  Binding the
    /// server-supplied version, culture and public key token instead would let
    /// a server name a loaded simple name with a different identity and thereby
    /// still trigger a new load, which is exactly what this tier must not do.
    ///
    /// The reference is weak.  A strong one would keep every assembly in the
    /// map alive for the life of the process, which would prevent a collectible
    /// <c>AssemblyLoadContext</c> from ever unloading: a server could force the
    /// map to be built with a single denied UDT and thereby pin unrelated
    /// plugin assemblies.  A dead entry simply drops out and the reference is
    /// re-evaluated as if the assembly had never been loaded.
    ///
    /// When several assemblies share a simple name, the first one seen wins.
    /// All of them are already in the process, so the choice cannot widen the
    /// policy; at worst the subsequent type lookup fails.
    /// </summary>
    private static Dictionary<string, WeakReference<Assembly>>? s_loadedAssemblies;

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
    /// True when the policy has been disabled entirely in favor of the
    /// pre-policy behavior, in which any assembly the server names may be
    /// loaded and no user-defined type check is performed.
    /// </summary>
    internal static bool LegacyBehaviorEnabled =>
        LocalAppContextSwitches.UseLegacyUdtAssemblyLoad;

    #endregion

    #region Methods

    /// <summary>
    /// Determines whether <paramref name="asmRef"/> names the built-in SQL
    /// Server CLR types assembly.
    /// </summary>
    internal static bool IsSqlServerTypesAssembly(AssemblyName asmRef) =>
        string.Equals(asmRef.Name, SqlServerTypesAssemblyName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Decides whether the driver may load the assembly named by
    /// <paramref name="asmRef"/> and, when it may, produces the assembly.
    ///
    /// The decision and the load are deliberately performed by a single call.
    /// Deciding separately from loading was unsafe: on .NET the loader ignores
    /// the public key token in an <see cref="AssemblyName"/>, so a caller that
    /// consulted the policy and then called <see cref="Assembly.Load(AssemblyName)"/>
    /// itself could still be handed a same-named assembly with the wrong
    /// identity.  Pinning the reference is therefore not an enforcement
    /// boundary; verifying what actually came back is, and doing both here
    /// means no caller can forget.  This mirrors what
    /// <c>SqlAuthenticationProviderManager</c> does for the Azure extension
    /// assembly.
    /// </summary>
    /// <param name="asmRef">
    /// The server-supplied assembly reference.  It is normalized in place when
    /// it names the built-in SQL Server CLR types assembly.
    /// </param>
    /// <param name="typeSystemAssemblyVersion">
    /// The type system assembly version negotiated for the connection, used to
    /// pin the version of the built-in SQL Server CLR types assembly.  Null
    /// when no connection context is available, in which case the version is
    /// left to the loader and only the culture and public key token are pinned.
    /// </param>
    /// <param name="assembly">
    /// The assembly the caller must use, or null when the policy refused.
    /// </param>
    /// <returns>True when the assembly may be used.</returns>
    internal static bool TryLoad(
        AssemblyName asmRef,
        Version? typeSystemAssemblyVersion,
        out Assembly? assembly)
    {
        assembly = null;

        if (LegacyBehaviorEnabled)
        {
            assembly = Assembly.Load(asmRef);
            return true;
        }

        if (!TryDecide(asmRef, typeSystemAssemblyVersion, out Decision decision))
        {
            return false;
        }

        // The process already holds this assembly, so there is nothing to load
        // and nothing to verify: the instance is the one the application itself
        // brought in, whatever identity the server claimed for it.
        if (decision.Loaded is not null)
        {
            assembly = decision.Loaded;
            return true;
        }

        Assembly loaded = Assembly.Load(asmRef);

        if (loaded is null)
        {
            return false;
        }

        if (!SatisfiesRequiredIdentity(loaded, decision))
        {
            SqlClientEventSource.Log.TryTraceEvent(
                "UdtAssemblyPolicy.TryLoad | ERR | Assembly '{0}' was loaded but has an unexpected identity '{1}' and will not be used.",
                asmRef.Name,
                loaded.FullName);

            return false;
        }

        assembly = loaded;

        return true;
    }

    /// <summary>
    /// The outcome of evaluating the policy: whether the reference is permitted
    /// and, if so, what must be true of the assembly that the loader returns.
    /// </summary>
    private readonly struct Decision
    {
        /// <summary>
        /// The assembly the process already holds, when the reference was
        /// permitted on that basis.  Null when the caller must load it.
        /// </summary>
        internal Assembly? Loaded { get; init; }

        /// <summary>
        /// The public key token the loaded assembly must carry, or null when
        /// the basis for permitting it placed no constraint on the token.
        /// </summary>
        internal byte[]? RequiredPublicKeyToken { get; init; }

        /// <summary>
        /// True when the loaded assembly must carry no public key token at all,
        /// because the allow list entry explicitly said <c>PublicKeyToken=null</c>.
        /// </summary>
        internal bool RequireUnsigned { get; init; }
    }

    /// <summary>
    /// Evaluates the policy without loading anything.  This is the decision
    /// half of <see cref="TryLoad"/>, exposed so that tests can observe the
    /// decision for assembly names that do not exist on disk.  Production code
    /// must use <see cref="TryLoad"/>, because a decision alone does not
    /// enforce the identity of what the loader returns.
    /// </summary>
    /// <param name="alreadyLoaded">
    /// The instance the process already holds, when that was the basis for
    /// permitting the reference; otherwise null.
    /// </param>
    /// <param name="asmRef">The server-supplied assembly reference.</param>
    /// <param name="typeSystemAssemblyVersion">
    /// The type system assembly version negotiated for the connection, or null.
    /// </param>
    internal static bool IsPermitted(
        AssemblyName asmRef,
        Version? typeSystemAssemblyVersion,
        out Assembly? alreadyLoaded)
    {
        if (LegacyBehaviorEnabled)
        {
            alreadyLoaded = null;

            return true;
        }

        bool permitted = TryDecide(asmRef, typeSystemAssemblyVersion, out Decision decision);

        alreadyLoaded = decision.Loaded;

        return permitted;
    }

    /// <summary>
    /// Evaluates the policy without loading anything.
    /// </summary>
    private static bool TryDecide(
        AssemblyName asmRef,
        Version? typeSystemAssemblyVersion,
        out Decision decision)
    {
        decision = default;

        string? simpleName = asmRef.Name;
        if (string.IsNullOrEmpty(simpleName))
        {
            return false;
        }

        // The built-in types assembly is always permitted, but only once its
        // identity has been pinned, so the exemption cannot be satisfied by an
        // arbitrary assembly that borrows the name.
        if (IsSqlServerTypesAssembly(asmRef))
        {
            PinSqlServerTypesIdentity(asmRef, typeSystemAssemblyVersion);

            decision = new Decision { RequiredPublicKeyToken = s_sqlServerTypesPublicKeyToken };

            return true;
        }

        // The allow list is the application stating which assemblies it is
        // willing to have loaded on a server's say-so, so the reference is
        // handed to the loader as given.  Whatever identity the matched entry
        // specified is carried forward and enforced against the result.
        if (TryMatchAllowList(asmRef, out AssemblyName? matched))
        {
            byte[]? allowedToken = matched!.GetPublicKeyToken();

            decision = new Decision
            {
                // A null token means the entry did not mention one, an empty
                // token means it explicitly required an unsigned assembly.
                RequiredPublicKeyToken = allowedToken is { Length: > 0 } ? allowedToken : null,
                RequireUnsigned = allowedToken is { Length: 0 },
            };

            return true;
        }

        // Otherwise the only remaining basis is that the process already holds
        // an assembly by this simple name, in which case that instance is used
        // and the server-supplied identity is discarded.
        if (TryGetLoadedAssembly(simpleName!, out Assembly? loaded))
        {
            decision = new Decision { Loaded = loaded };

            return true;
        }

        return false;
    }

    /// <summary>
    /// Verifies that an assembly the loader returned actually carries the
    /// identity that the policy required of it.
    /// </summary>
    private static bool SatisfiesRequiredIdentity(Assembly loaded, Decision decision)
    {
        if (decision.RequiredPublicKeyToken is null && !decision.RequireUnsigned)
        {
            return true;
        }

        byte[]? actualToken;

        try
        {
            actualToken = loaded.GetName().GetPublicKeyToken();
        }
        catch (Exception e) when (ADP.IsCatchableExceptionType(e))
        {
            // If the identity cannot be read it cannot be confirmed, so the
            // assembly is refused.
            return false;
        }

        if (decision.RequireUnsigned)
        {
            return actualToken is null || actualToken.Length == 0;
        }

        return actualToken is not null &&
               actualToken.AsSpan().SequenceEqual(decision.RequiredPublicKeyToken.AsSpan());
    }

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
    ///
    /// The culture is normalized to neutral.  The shipped assembly is culture
    /// neutral, so leaving the culture server-controlled would let a reference
    /// carrying <c>Culture=xx-YY</c> steer the bind towards a satellite-shaped
    /// name that the real assembly never uses.
    ///
    /// Normalizing the reference is necessary but not sufficient, because on
    /// .NET the loader ignores the requested token.  <see cref="TryLoad"/>
    /// verifies the identity of whatever the loader actually returns.
    /// </summary>
    /// <param name="asmRef">The assembly reference to normalize, in place.</param>
    /// <param name="typeSystemAssemblyVersion">
    /// The type system assembly version negotiated for the connection, or null
    /// to leave the version unconstrained.
    /// </param>
    private static void PinSqlServerTypesIdentity(AssemblyName asmRef, Version? typeSystemAssemblyVersion)
    {
        if (typeSystemAssemblyVersion is not null)
        {
            asmRef.Version = typeSystemAssemblyVersion;
        }

        asmRef.CultureInfo = CultureInfo.InvariantCulture;
        asmRef.SetPublicKeyToken((byte[])s_sqlServerTypesPublicKeyToken.Clone());
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
            s_loadedAssemblies = null;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Finds the allow list entry that <paramref name="asmRef"/> satisfies, if
    /// any.  The matched entry is returned so that the identity it specified
    /// can be enforced against the assembly the loader returns.
    /// </summary>
    private static bool TryMatchAllowList(AssemblyName asmRef, out AssemblyName? matched)
    {
        List<AssemblyName> allowList = GetAllowList();

        for (int i = 0; i < allowList.Count; i++)
        {
            if (Matches(allowList[i], asmRef))
            {
                matched = allowList[i];

                return true;
            }
        }

        matched = null;

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

        // AssemblyName distinguishes an omitted public key token (null) from an
        // explicitly unsigned one (PublicKeyToken=null, which parses to an empty
        // array).  Treating the latter as "unconstrained" would let an entry
        // that deliberately named an unsigned assembly be satisfied by a signed
        // one, so the two cases are kept apart.
        byte[]? allowedToken = allowed.GetPublicKeyToken();

        if (allowedToken is not null)
        {
            byte[]? candidateToken = candidate.GetPublicKeyToken();

            if (allowedToken.Length == 0)
            {
                // The entry requires an unsigned assembly.
                return candidateToken is null || candidateToken.Length == 0;
            }

            if (candidateToken is null ||
                !candidateToken.AsSpan().SequenceEqual(allowedToken.AsSpan()))
            {
                return false;
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
    /// Looks up an assembly that the process has already loaded under the given
    /// simple name.
    /// </summary>
    private static bool TryGetLoadedAssembly(string simpleName, out Assembly? assembly)
    {
        lock (s_lock)
        {
            Dictionary<string, WeakReference<Assembly>> loaded = GetLoadedAssemblies();

            if (loaded.TryGetValue(simpleName, out WeakReference<Assembly>? reference) &&
                reference.TryGetTarget(out Assembly? target))
            {
                assembly = target;

                return true;
            }

            // The assembly has been collected, which means its load context was
            // unloaded.  Drop the entry so the name is no longer permitted on
            // the strength of a load that no longer exists.
            if (reference is not null)
            {
                loaded.Remove(simpleName);
            }

            assembly = null;

            return false;
        }
    }

    /// <summary>
    /// Returns the map of loaded assembly simple names to instances, building it
    /// on first use and thereafter relying on the
    /// <see cref="AppDomain.AssemblyLoad"/> handler to keep it current.
    /// </summary>
    /// <remarks>
    /// Callers must hold <see cref="s_lock"/>.
    /// </remarks>
    private static Dictionary<string, WeakReference<Assembly>> GetLoadedAssemblies()
    {
        EnsureAssemblyLoadHandlerAttached();

        if (s_loadedAssemblies is not null)
        {
            return s_loadedAssemblies;
        }

        Dictionary<string, WeakReference<Assembly>> loaded = new(StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Remember(loaded, assembly);
        }

        s_loadedAssemblies = loaded;

        return loaded;
    }

    /// <summary>
    /// Records <paramref name="assembly"/> under its simple name, keeping the
    /// first assembly seen for a given name.
    /// </summary>
    private static void Remember(Dictionary<string, WeakReference<Assembly>> loaded, Assembly assembly)
    {
        if (assembly.IsDynamic)
        {
            // A dynamic assembly cannot be the target of Assembly.Load by name,
            // and asking one for its name can throw.
            return;
        }

        if (!IsInDriverLoadContext(assembly))
        {
            // Only assemblies in the load context that Assembly.Load would
            // resolve into can satisfy this tier.  Recording one from another
            // context would permit a reference that the loader would then
            // resolve to a different assembly, or to none at all.
            return;
        }

        try
        {
            string? name = assembly.GetName().Name;

            if (!string.IsNullOrEmpty(name) &&
                (!loaded.TryGetValue(name!, out WeakReference<Assembly>? existing) ||
                 !existing.TryGetTarget(out _)))
            {
                loaded[name!] = new WeakReference<Assembly>(assembly);
            }
        }
        catch (Exception e) when (ADP.IsCatchableExceptionType(e))
        {
            // Reading the name can fail for assemblies loaded from a byte array
            // or produced by a trimmer.  Losing one only makes the policy
            // stricter.
            SqlClientEventSource.Log.TryTraceEvent(
                "UdtAssemblyPolicy.Remember | INFO | Unable to read the name of a loaded assembly.");
        }
    }

    /// <summary>
    /// Determines whether an assembly lives in the load context that the
    /// driver's own <see cref="Assembly.Load(AssemblyName)"/> calls resolve
    /// into.
    /// </summary>
    /// <remarks>
    /// On .NET, <see cref="Assembly.Load(AssemblyName)"/> resolves against the
    /// load context of the calling assembly, which is the driver's.  Assemblies
    /// held by other contexts are therefore not reachable by name from here,
    /// and an application that loads its UDT assembly into a separate
    /// collectible context must name it on the allow list.
    /// </remarks>
    private static bool IsInDriverLoadContext(Assembly assembly)
    {
        #if NET
        return AssemblyLoadContext.GetLoadContext(assembly) == s_driverLoadContext;
        #else
        // .NET Framework has a single load context per AppDomain for this
        // purpose, so every loaded assembly qualifies.
        return true;
        #endif
    }

    /// <summary>
    /// Attaches the assembly load handler that keeps the cached map of loaded
    /// assemblies current, if it has not been attached already.
    /// </summary>
    /// <remarks>
    /// Callers must hold <see cref="s_lock"/>.
    /// </remarks>
    private static void EnsureAssemblyLoadHandlerAttached()
    {
        if (s_assemblyLoadHandlerAttached)
        {
            return;
        }

        AppDomain.CurrentDomain.AssemblyLoad += static (_, args) =>
        {
            lock (s_lock)
            {
                // Nothing to update if the map has not been built yet; it will
                // pick the assembly up when it is.
                if (s_loadedAssemblies is not null)
                {
                    Remember(s_loadedAssemblies, args.LoadedAssembly);
                }
            }
        };

        s_assemblyLoadHandlerAttached = true;
    }

    #endregion
}
