// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

#if !NETFRAMEWORK
using Interop_TEMP.Windows.Handles;
#endif

namespace Interop_TEMP.Windows.Kernel32
{
    internal static class Kernel32
    {
        internal const uint SEM_FAILCRITICALERRORS = 1;

        #if !NETFRAMEWORK
        internal const int LOAD_LIBRARY_AS_DATAFILE = 0x00000002;
        internal const int LOAD_LIBRARY_SEARCH_SYSTEM32 = 0x00000800;
        #endif

        private const string DllName = "kernel32.dll";

        [DllImport(DllName, SetLastError = true, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern int GetFullPathName(
            string path,
            int numBufferChars,
            StringBuilder buffer,
            IntPtr lpFilePartOrNull);

        #if !NETFRAMEWORK
        [DllImport(DllName, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        #endif

        #if !NETFRAMEWORK
        [DllImport(DllName, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr GetProcAddress(SafeLibraryHandle hModule, string lpProcName);
        #endif

        #if !NETFRAMEWORK
        [DllImport(DllName, ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeLibraryHandle LoadLibraryExW([In] string lpwLibFileName, [In] IntPtr hFile, [In] uint dwFlags);
        #endif

        [DllImport(DllName, SetLastError = true, ExactSpelling = true)]
        internal static extern bool SetThreadErrorMode(uint dwNewMode, out uint lpOldMode);
    }
}
