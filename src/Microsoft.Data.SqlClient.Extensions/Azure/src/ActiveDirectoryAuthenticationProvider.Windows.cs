// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

public sealed partial class ActiveDirectoryAuthenticationProvider
{
    /// <summary>
    /// Gets the parent window handle to be used for interactive authentication prompts
    /// via the Windows Account Manager (WAM) broker.
    /// </summary>
    /// <returns>
    /// The parent window handle as an <see cref="IntPtr"/>, or <see cref="IntPtr.Zero"/> if
    /// not running on Windows or no window handle is available.
    /// </returns>
    private IntPtr GetParentWindow()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IntPtr.Zero;
        }

        // If the user has provided a custom parent activity/window function, use it.
        if (_parentActivityOrWindowFunc is not null)
        {
            object parentWindow = _parentActivityOrWindowFunc();
            if (parentWindow is IntPtr hwnd)
            {
                return hwnd;
            }
        }

        // Fall back to finding the console window, then getting its root owner.
        IntPtr consoleHandle = Interop.Kernel32.GetConsoleWindow();
        if (consoleHandle != IntPtr.Zero)
        {
            IntPtr rootOwner = Interop.User32.GetRootOwner(consoleHandle);
            if (rootOwner != IntPtr.Zero)
            {
                return rootOwner;
            }
            return consoleHandle;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Gets the parent activity or window object for the broker authentication flow.
    /// On Windows, returns the window handle. On other platforms, returns <see cref="IntPtr.Zero"/>.
    /// </summary>
    private object GetBrokerParentWindow()
    {
        return GetParentWindow();
    }
}
