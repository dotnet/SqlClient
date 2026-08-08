// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Provides platform detection flags for OS-specific code paths.
/// </summary>
/// <remarks>
/// This type exists to keep OS detection terse and in one place: callers write
/// <c>OsConstants.IsWindows</c> instead of repeating <c>RuntimeInformation.IsOSPlatform(...)</c>
/// throughout the codebase.  Centralizing it also gives us a single seam to adjust if a specific
/// OS needs special handling (for example, a different detection mechanism, a finer-grained
/// flag, or an override for testing) without touching every call site.
/// </remarks>
/// <remarks>
/// Each flag is exposed as a property that directly returns an OS check.  On .NET, that check is
/// <c>OperatingSystem.Is*()</c>, which the JIT treats as an intrinsic and constant-folds (and which
/// the IL trimmer also recognizes).  On .NET Framework — where the <see cref="System.OperatingSystem"/>
/// platform-check helpers do not exist — the check falls back to
/// <see cref="RuntimeInformation.IsOSPlatform"/>.  Either way, returning the check directly is
/// deliberate: caching the results in static fields (for example, via a static constructor) would
/// defeat both the JIT folding and the trimmer — the trimmer cannot analyze a static constructor,
/// so it would be unable to prove which branches are dead and would keep OS-specific code (including
/// Windows-only native dependencies) in apps published for other platforms.
/// </remarks>
/// <remarks>
/// <para>
/// IL trimming note: when a downstream app is published for a specific OS (a RID-specific publish),
/// the IL trimmer substitutes the underlying <see cref="RuntimeInformation.IsOSPlatform"/> /
/// <c>OperatingSystem.Is*</c> checks with constant <c>true</c>/<c>false</c> for the target OS and
/// then removes the dead branch (along with everything it transitively references, such as
/// Windows-only native entry points).  This is what lets a single cross-platform assembly trim
/// away the OS-specific code that does not apply to the published target.
/// </para>
/// <para>
/// For this to work, each guard must be used <b>inline</b> in the same method as the OS-specific
/// code it gates.  The trimmer's constant folding is shallow: it reasons within the method that
/// contains the guard, so a guard hidden behind a helper traps the constant inside that helper and
/// leaves the protected code reachable (and therefore not trimmed).
/// </para>
/// <para>
/// Do — guard inline, so the trimmer can drop the branch and its dependencies:
/// <code>
/// if (OsConstants.IsWindows)
/// {
///     WindowsOnlyNativeCall();   // becomes `if (false) { ... }` off-Windows, then removed
/// }
/// </code>
/// </para>
/// <para>
/// Don't — hide the guard in a throw helper; the trimmer cannot prove the call below is dead:
/// <code>
/// static void ThrowIfNotWindows()
/// {
///     if (!OsConstants.IsWindows) throw new PlatformNotSupportedException();
/// }
/// // caller:
/// ThrowIfNotWindows();
/// WindowsOnlyNativeCall();   // still reachable to the trimmer; kept even off-Windows
/// </code>
/// </para>
/// </remarks>
internal static class OsConstants
{
    /// <summary>
    /// Gets a value indicating whether the runtime is executing on Windows.
    /// </summary>
    #if NET
    internal static bool IsWindows => OperatingSystem.IsWindows();
    #else
    internal static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    #endif

    /// <summary>
    /// Gets a value indicating whether the runtime is executing on Linux.
    /// </summary>
    #if NET
    internal static bool IsLinux => OperatingSystem.IsLinux();
    #else
    internal static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
    #endif

    /// <summary>
    /// Gets a value indicating whether the runtime is executing on macOS.
    /// </summary>
    #if NET
    internal static bool IsMacOS => OperatingSystem.IsMacOS();
    #else
    internal static bool IsMacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    #endif

    #if NET
    /// <summary>
    /// Gets a value indicating whether the runtime is executing on FreeBSD.
    /// </summary>
    /// <remarks>
    /// FreeBSD support is only available in .NET 5+ and later. This property will be
    /// <c>false</c> on .NET Framework or if the runtime does not support FreeBSD detection.
    /// </remarks>
    internal static bool IsFreeBSD => OperatingSystem.IsFreeBSD();
    #endif
}
