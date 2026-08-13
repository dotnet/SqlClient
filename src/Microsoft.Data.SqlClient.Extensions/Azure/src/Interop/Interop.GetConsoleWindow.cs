// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Win32 P/Invoke wrappers used by <see cref="ActiveDirectoryAuthenticationProvider" /> for
/// console-window discovery. Follows the .NET runtime's <c>Interop</c> convention: one Win32
/// import per file, grouped into a nested <c>Interop.&lt;module&gt;</c> static class that mirrors
/// the Win32 DLL it targets.
/// </summary>
internal static partial class Interop
{
    internal static partial class Kernel32
    {
        /// <summary>
        /// Raw <c>kernel32!GetConsoleWindow</c> P/Invoke. Documented by Windows to return
        /// <see cref="IntPtr.Zero" /> when the calling process is not attached to a console
        /// (and to never throw).
        /// </summary>
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetConsoleWindow();
    }
}
