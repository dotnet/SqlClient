// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.Data.SqlClient;

/// <summary>
/// Objective-C runtime P/Invoke wrappers used by <see cref="ActiveDirectoryAuthenticationProvider" />
/// to read the host process' bundle identifier on macOS. Follows the .NET runtime's <c>Interop</c>
/// convention: imports grouped into a nested <c>Interop.&lt;module&gt;</c> static class that
/// mirrors the native library it targets.
/// </summary>
internal static partial class Interop
{
    internal static partial class NSBundle
    {
        private const string LibObjcLib = "/usr/lib/libobjc.dylib";

        // The main bundle identifier is fixed for the lifetime of the process, and -[NSString
        // UTF8String] hands back an autoreleased buffer that nothing drains on a .NET thread
        // pool thread. Resolve it once so repeated connections don't accumulate those buffers.
        private static readonly Lazy<string?> s_mainBundleIdentifier =
            new(ResolveMainBundleIdentifier);

        /// <summary>
        /// Equivalent to the Objective-C expression <c>[[NSBundle mainBundle] bundleIdentifier]</c>.
        /// </summary>
        /// <returns>
        /// The bundle identifier of the host application (for example <c>com.contoso.myapp</c>),
        /// or <see langword="null"/> when the process is not a bundled application. Loose
        /// executables and scripts launched from a terminal have a main bundle whose
        /// <c>bundleIdentifier</c> is <c>nil</c>.
        /// </returns>
        /// <remarks>
        /// The macOS broker derives the expected redirect URI from the host application's bundle
        /// identifier, so this determines which redirect URI MSAL must be configured with. Any
        /// failure to resolve the class or selectors is treated as "not bundled", which selects
        /// the redirect URI registered for unsigned, non-bundled executables.
        /// </remarks>
        internal static string? TryGetMainBundleIdentifier() => s_mainBundleIdentifier.Value;

        private static string? ResolveMainBundleIdentifier()
        {
            try
            {
                IntPtr bundleClass = objc_getClass("NSBundle");
                if (bundleClass == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr mainBundle = SendMessage(bundleClass, "mainBundle");
                if (mainBundle == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr bundleIdentifier = SendMessage(mainBundle, "bundleIdentifier");
                if (bundleIdentifier == IntPtr.Zero)
                {
                    return null;
                }

                IntPtr utf8 = SendMessage(bundleIdentifier, "UTF8String");
                if (utf8 == IntPtr.Zero)
                {
                    return null;
                }

                // Bundle identifiers are reverse-DNS strings, so ANSI marshalling is lossless
                // here. Marshal.PtrToStringUTF8 is not available on netstandard2.0.
                string? identifier = Marshal.PtrToStringAnsi(utf8);

                return string.IsNullOrEmpty(identifier) ? null : identifier;
            }
            catch (Exception)
            {
                // DllNotFoundException / EntryPointNotFoundException on a runtime without the
                // Objective-C runtime, or any other interop failure.
                return null;
            }
        }

        private static IntPtr SendMessage(IntPtr receiver, string selectorName)
        {
            IntPtr selector = sel_registerName(selectorName);

            return selector == IntPtr.Zero ? IntPtr.Zero : objc_msgSend(receiver, selector);
        }

        [DllImport(LibObjcLib, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_getClass(string name);

        [DllImport(LibObjcLib, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr sel_registerName(string name);

        [DllImport(LibObjcLib, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);
    }
}
