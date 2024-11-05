// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using System.Threading;

namespace Microsoft.Data.Common
{

    [SuppressUnmanagedCodeSecurityAttribute()]
    internal static class SafeNativeMethods
    {
        private const string Kernel32 = "kernel32.dll";

        // http://msdn.microsoft.com/library/default.asp?url=/library/en-us/sysinfo/base/getcomputernameex.asp
        [DllImport(Kernel32, CharSet = CharSet.Unicode, EntryPoint = "GetComputerNameExW", SetLastError = true)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern int GetComputerNameEx(int nameType, StringBuilder nameBuffer, ref int bufferSize);

        [DllImport(Kernel32, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        [ResourceExposure(ResourceScope.Process)]
        static internal extern int GetCurrentProcessId();

        [DllImport(Kernel32, CharSet = CharSet.Auto, BestFitMapping = false, ThrowOnUnmappableChar = true)]
        //        [DllImport(ExternDll.Kernel32, CharSet=CharSet.Auto)]
        [ResourceExposure(ResourceScope.Process)]
        static internal extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPTStr), In] string moduleName/*lpctstr*/);

        [DllImport(Kernel32, CharSet = CharSet.Ansi, BestFitMapping = false, ThrowOnUnmappableChar = true, SetLastError = true)]
        //        [DllImport(ExternDll.Kernel32, CharSet=CharSet.Ansi)]
        [ResourceExposure(ResourceScope.None)]
        static internal extern IntPtr GetProcAddress(IntPtr HModule, [MarshalAs(UnmanagedType.LPStr), In] string funcName/*lpcstr*/);

    }
}
