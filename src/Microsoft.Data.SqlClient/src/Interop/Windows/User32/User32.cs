// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.User32
{
    /// <summary>
    /// Win32 P/Invoke wrappers from <c>user32.dll</c> used by
    /// <see cref="Microsoft.Data.SqlClient.ActiveDirectoryAuthenticationProvider"/> for
    /// console/window owner discovery when parenting MSAL UI on Windows.
    /// </summary>
    internal static class User32
    {
        private const string DllName = "user32.dll";

        /// <summary>
        /// <c>GA_ROOTOWNER</c> flag value for <c>GetAncestor</c> — "Retrieves the owned root
        /// window by walking the chain of parent and owner windows returned by GetParent."
        /// </summary>
        private const uint GA_ROOTOWNER = 3;

        /// <summary>
        /// Raw <c>user32!GetAncestor</c> P/Invoke. Documented by Windows to return
        /// <see cref="IntPtr.Zero"/> rather than throw when the input handle is invalid.
        /// </summary>
        [DllImport(DllName)]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        /// <summary>
        /// Walks the parent/owner chain of <paramref name="hwnd"/> and returns the root owner
        /// window, or <see cref="IntPtr.Zero"/> when none can be found.
        /// </summary>
        internal static IntPtr GetRootOwner(IntPtr hwnd)
        {
            return GetAncestor(hwnd, GA_ROOTOWNER);
        }
    }
}
