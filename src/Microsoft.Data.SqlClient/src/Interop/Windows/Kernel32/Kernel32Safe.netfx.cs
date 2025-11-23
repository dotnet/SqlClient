// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK

using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;

namespace Interop.Windows.Kernel32
{
    /// <remarks>
    /// Be insanely careful when using methods from this class. They are configured to skip
    /// runtime security checks.
    /// </remarks>
    [SuppressUnmanagedCodeSecurity]
    internal static class Kernel32Safe
    {
        private const string Kernel32 = "kernel32.dll";

        [DllImport(Kernel32, CharSet = CharSet.Auto)]
        [ResourceExposure(ResourceScope.Process)]
        internal static extern int GetCurrentProcessId();

        [DllImport(Kernel32, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        internal static extern IntPtr GetProcAddress(IntPtr HModule, [MarshalAs(UnmanagedType.LPStr), In] string funcName);
    }
}

#endif
