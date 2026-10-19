// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Windows-only portion of <see cref="ActiveDirectoryAuthenticationProvider"/>. Kept in a
/// separate file so the Windows-specific P/Invoke helpers (console-window discovery, root-owner
/// lookup) live next to the broker plumbing they support. The other half of the partial class
/// lives in <c>ActiveDirectoryAuthenticationProvider.cs</c> and is fully cross-platform.
/// </summary>
/// <remarks>
/// Responsibilities of this part:
/// <list type="bullet">
///   <item><description>
///     Resolve the parent window handle handed to MSAL's WAM broker (and to the embedded WebView
///     on .NET Framework) via <c>WithParentActivityOrWindow</c>.
///   </description></item>
///   <item><description>
///     Honor the caller-supplied <c>SetParentActivityOrWindowFunc</c> callback when present, and
///     fall back to the process console window (resolved via Win32 interop) otherwise.
///   </description></item>
/// </list>
/// </remarks>
public sealed partial class ActiveDirectoryAuthenticationProvider
{
    /// <summary>
    /// Resolves the parent window handle used to parent MSAL UI (WAM broker dialog on Windows,
    /// or the embedded WebView on .NET Framework). The caller boxes the handle for
    /// MSAL's <c>WithParentActivityOrWindow</c> overload.
    /// </summary>
    /// <param name="parentActivityOrWindowFunc">The current caller-supplied UI callback, if any.</param>
    /// <returns>
    /// The parent window handle, or <see cref="IntPtr.Zero"/> when we are not on Windows or
    /// when neither a caller-supplied callback nor a discoverable console window is available.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Exception behavior:
    /// <list type="bullet">
    ///   <item><description>
    ///     Exceptions thrown by the caller-supplied callback installed via
    ///     <c>SetParentActivityOrWindowFunc</c> are intentionally not caught here; they propagate
    ///     up through MSAL into the caller of <c>AcquireTokenAsync</c> so that bugs in the
    ///     callback surface where they are most diagnosable.
    ///   </description></item>
    ///   <item><description>
    ///     The Win32 P/Invokes used to discover the console window (<c>GetConsoleWindow</c>,
    ///     <c>GetAncestor</c>) are documented by Windows not to throw — they return
    ///     <c>NULL</c>/<see cref="IntPtr.Zero"/> on failure, which this method treats as
    ///     "no parent window available".
    ///   </description></item>
    /// </list>
    /// </para>
    /// </remarks>
    private static IntPtr GetParentWindow(Func<object>? parentActivityOrWindowFunc)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return IntPtr.Zero;
        }

        // If the user has provided a custom parent activity/window function, use it. Exceptions
        // from the user-supplied callback escape to MSAL by design — see method remarks above.
        if (parentActivityOrWindowFunc is not null)
        {
            object parentWindow = parentActivityOrWindowFunc();
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
            if (parentWindow is not null)
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

        // Fall back to finding the console window, then getting its root owner. The Win32 calls
        // are documented to return NULL on failure rather than throwing, so we treat any
        // IntPtr.Zero return as "no console window available".
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

    /// <summary>Holds live Windows UI callback state independently of provider-owned logging state.</summary>
    private sealed class ParentWindowProvider
    {
        /// <summary>Gets or sets the caller's current UI callback, including explicit clearing.</summary>
        internal Func<object>? Callback { get; set; }

        /// <summary>Resolves and boxes the parent handle without capturing the authentication provider.</summary>
        /// <returns>The current parent handle or the console-window fallback.</returns>
        internal object GetParentWindow() => ActiveDirectoryAuthenticationProvider.GetParentWindow(Callback);
    }
}
