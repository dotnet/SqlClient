// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Provides platform detection flags for OS-specific code paths.
/// </summary>
/// <remarks>
/// These constants are computed at runtime and cached as static readonly fields.  This design
/// allows the JIT compiler to elide branches in hot paths based on whether the OS flags are known
/// constants at JIT compilation time.
/// </remarks>
internal static class OsConstants
{
    /// <summary>
    /// Gets a value indicating whether the runtime is executing on Windows.
    /// </summary>
    internal static readonly bool IsWindows;

    /// <summary>
    /// Gets a value indicating whether the runtime is executing on Linux.
    /// </summary>
    internal static readonly bool IsLinux;

    /// <summary>
    /// Gets a value indicating whether the runtime is executing on macOS.
    /// </summary>
    internal static readonly bool IsMacOS;

    #if NET
    /// <summary>
    /// Gets a value indicating whether the runtime is executing on FreeBSD.
    /// </summary>
    /// <remarks>
    /// FreeBSD support is only available in .NET 5+ and later. This field will be
    /// <c>false</c> on .NET Framework or if the runtime does not support FreeBSD detection.
    /// </remarks>
    internal static readonly bool IsFreeBSD;
    #endif

    /// <summary>
    /// Initializes platform detection flags by querying <see cref="RuntimeInformation"/>.
    /// </summary>
    /// <remarks>
    /// We use a static constructor instead of a module initializer ([ModuleInitializer]) to avoid
    /// the CA2255 security concern. Module initializers can be problematic because: 1. They run in
    /// an unpredictable order relative to other initialization code.  2. They run before the app
    /// initialization sequence, potentially before security policies are set.  3. They can
    /// complicate debugging and profiling.
    ///
    /// Using a static constructor ensures initialization happens in a well-defined, type-safe
    /// manner that is compatible with the CLR's type loading guarantees.
    ///
    /// The trade-off is that the OS flags won't be initialized until the OsConstants type is first
    /// accessed, which may cause a slight delay in a hot path, but only once.
    /// </remarks>
    static OsConstants()
    {
        IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        IsLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        IsMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        #if NET
        IsFreeBSD = RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);
        #endif
    }
}
