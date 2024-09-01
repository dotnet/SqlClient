// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Text;

namespace System.IO
{
    /// <summary>Contains internal path helpers that are shared between many projects.</summary>
    internal static partial class PathInternal
    {
        // All paths in Win32 ultimately end up becoming a path to a File object in the Windows object manager. Passed in paths get mapped through
        // DosDevice symbolic links in the object tree to actual File objects under \Devices. To illustrate, this is what happens with a typical
        // path "Foo" passed as a filename to any Win32 API:
        //
        //  1. "Foo" is recognized as a relative path and is appended to the current directory (say, "C:\" in our example)
        //  2. "C:\Foo" is prepended with the DosDevice namespace "\??\"
        //  3. CreateFile tries to create an object handle to the requested file "\??\C:\Foo"
        //  4. The Object Manager recognizes the DosDevices prefix and looks
        //      a. First in the current session DosDevices ("\Sessions\1\DosDevices\" for example, mapped network drives go here)
        //      b. If not found in the session, it looks in the Global DosDevices ("\GLOBAL??\")
        //  5. "C:" is found in DosDevices (in our case "\GLOBAL??\C:", which is a symbolic link to "\Device\HarddiskVolume6")
        //  6. The full path is now "\Device\HarddiskVolume6\Foo", "\Device\HarddiskVolume6" is a File object and parsing is handed off
        //      to the registered parsing method for Files
        //  7. The registered open method for File objects is invoked to create the file handle which is then returned
        //
        // There are multiple ways to directly specify a DosDevices path. The final format of "\??\" is one way. It can also be specified
        // as "\\.\" (the most commonly documented way) and "\\?\". If the question mark syntax is used the path will skip normalization
        // (essentially GetFullPathName()) and path length checks.

        // Windows Kernel-Mode Object Manager
        // https://msdn.microsoft.com/en-us/library/windows/hardware/ff565763.aspx
        // https://channel9.msdn.com/Shows/Going+Deep/Windows-NT-Object-Manager
        //
        // Introduction to MS-DOS Device Names
        // https://msdn.microsoft.com/en-us/library/windows/hardware/ff548088.aspx
        //
        // Local and Global MS-DOS Device Names
        // https://msdn.microsoft.com/en-us/library/windows/hardware/ff554302.aspx

        internal const char DirectorySeparatorChar = '\\';
        internal const char AltDirectorySeparatorChar = '/';

        // \\?\, \\.\, \??\
        internal const int DevicePrefixLength = 4;
        // \\?\UNC\, \\.\UNC\
        internal const int UncExtendedPrefixLength = 8;

        /// <summary>
        /// Returns true if the path uses any of the DOS device path syntaxes. ("\\.\", "\\?\", or "\??\")
        /// </summary>
        internal static bool IsDevice(ReadOnlySpan<char> path)
        {
            // If the path begins with any two separators is will be recognized and normalized and prepped with
            // "\??\" for internal usage correctly. "\??\" is recognized and handled, "/??/" is not.
            return IsExtended(path)
                ||
                (
                    path.Length >= DevicePrefixLength
                    && IsDirectorySeparator(path[0])
                    && IsDirectorySeparator(path[1])
                    && (path[2] == '.' || path[2] == '?')
                    && IsDirectorySeparator(path[3])
                );
        }

        /// <summary>
        /// Returns true if the path is a device UNC (\\?\UNC\, \\.\UNC\)
        /// </summary>
        internal static bool IsDeviceUNC(ReadOnlySpan<char> path)
        {
            return path.Length >= UncExtendedPrefixLength
                && IsDevice(path)
                && IsDirectorySeparator(path[7])
                && path[4] == 'U'
                && path[5] == 'N'
                && path[6] == 'C';
        }

        /// <summary>
        /// Returns true if the path uses the canonical form of extended syntax ("\\?\" or "\??\"). If the
        /// path matches exactly (cannot use alternate directory separators) Windows will skip normalization
        /// and path length checks.
        /// </summary>
        internal static bool IsExtended(ReadOnlySpan<char> path)
        {
            // While paths like "//?/C:/" will work, they're treated the same as "\\.\" paths.
            // Skipping of normalization will *only* occur if back slashes ('\') are used.
            return path.Length >= DevicePrefixLength
                && path[0] == '\\'
                && (path[1] == '\\' || path[1] == '?')
                && path[2] == '?'
                && path[3] == '\\';
        }

        /// <summary>
        /// True if the given character is a directory separator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDirectorySeparator(char c)
        {
            return c == DirectorySeparatorChar || c == AltDirectorySeparatorChar;
        }
    }
}
