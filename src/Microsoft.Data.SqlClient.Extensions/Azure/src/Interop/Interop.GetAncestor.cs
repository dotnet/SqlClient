// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Win32 P/Invoke wrappers used by <see cref="ActiveDirectoryAuthenticationProvider" /> for
/// console-window discovery. Follows the .NET runtime's <c>Interop</c> convention: one Win32
/// import per file, grouped into a nested <c>Interop.&lt;module&gt;</c> static class that mirrors
/// the Win32 DLL it targets. Only the <c>internal</c> helper is exposed; the raw
/// <c>DllImport</c> stays private.
/// </summary>
internal static partial class Interop
{
    internal static partial class User32
    {
        /// <summary>
        /// <c>GA_ROOTOWNER</c> flag value for <c>GetAncestor</c> — "Retrieves the owned root
        /// window by walking the chain of parent and owner windows returned by <c>GetParent</c>."
        /// </summary>
        private const uint GA_ROOTOWNER = 3;

        /// <summary>
        /// Raw <c>user32!GetAncestor</c> P/Invoke. Documented by Windows to return
        /// <see cref="IntPtr.Zero" /> rather than throw when the input handle is invalid.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        /// <summary>
        /// Walks the parent/owner chain of <paramref name="hwnd" /> and returns the root owner
        /// window, or <see cref="IntPtr.Zero" /> when none can be found.
        /// </summary>
        internal static IntPtr GetRootOwner(IntPtr hwnd)
        {
            return GetAncestor(hwnd, GA_ROOTOWNER);
        }
    }
}
