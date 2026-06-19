// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Interop.Windows.Kernel32;
using Interop.Windows.User32;

namespace Microsoft.Data.SqlClient
{
    /// <summary>
    /// Windows-only portion of <see cref="ActiveDirectoryAuthenticationProvider"/>.
    /// </summary>
    public sealed partial class ActiveDirectoryAuthenticationProvider
    {
        /// <summary>
        /// Resolves the parent window handle used to parent MSAL UI (WAM broker dialog on Windows,
        /// or the embedded WebView on .NET Framework).
        /// </summary>
        private IntPtr GetParentWindow()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return IntPtr.Zero;
            }

            // If the user has provided a custom parent activity/window function, use it.
            // Exceptions from the user-supplied callback escape to MSAL by design.
            if (_parentActivityOrWindowFunc != null)
            {
                object parentWindow = _parentActivityOrWindowFunc();
                if (parentWindow is IntPtr hwnd)
                {
                    return hwnd;
                }
#if NETFRAMEWORK
                if (parentWindow is System.Windows.Forms.IWin32Window win32Window)
                {
                    return win32Window.Handle;
                }
#endif
                if (parentWindow != null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(SetParentActivityOrWindowFunc)} expects the callback to return an " +
                        "IntPtr window handle" +
#if NETFRAMEWORK
                        " (or an IWin32Window on .NET Framework)" +
#endif
                        $"; got {parentWindow.GetType().FullName}.");
                }
            }

            // Fall back to finding the console window, then getting its root owner.  The Win32
            // calls are documented to return NULL on failure rather than throwing, so we treat any
            // IntPtr.Zero return as "no console window available".
            IntPtr consoleHandle = Kernel32.GetConsoleWindow();
            if (consoleHandle != IntPtr.Zero)
            {
                IntPtr rootOwner = User32.GetRootOwner(consoleHandle);
                if (rootOwner != IntPtr.Zero)
                {
                    return rootOwner;
                }
                return consoleHandle;
            }

            return IntPtr.Zero;
        }
    }
}
