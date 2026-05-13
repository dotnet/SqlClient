// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

internal static partial class Interop
{
    internal static partial class User32
    {
        private const uint GA_ROOTOWNER = 3;

        /// <summary>
        /// Retrieves the handle to the ancestor of the specified window.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

        /// <summary>
        /// Gets the root owner window of the specified window handle.
        /// </summary>
        internal static IntPtr GetRootOwner(IntPtr hwnd)
        {
            return GetAncestor(hwnd, GA_ROOTOWNER);
        }
    }
}
