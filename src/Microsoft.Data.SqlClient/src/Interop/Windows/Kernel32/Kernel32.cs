// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.
// // See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Interop_TEMP.Windows.Kernel32
{
    internal static class Kernel32
    {
        internal const uint SEM_FAILCRITICALERRORS = 1;
        private const string DllName = "kernel32.dll";

        [DllImport(Interop.Libraries.Kernel32, SetLastError = true, ExactSpelling = true)]
        internal static extern bool SetThreadErrorMode(uint dwNewMode, out uint lpOldMode);
    }
}
