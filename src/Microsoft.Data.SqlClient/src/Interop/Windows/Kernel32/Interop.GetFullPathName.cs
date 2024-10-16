// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.
// // See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Data;
using Microsoft.Data.Common;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [ResourceExposure(ResourceScope.Machine)]
        internal static extern int GetFullPathName
        (
            string path,
            int numBufferChars,
            StringBuilder buffer,
            IntPtr lpFilePartOrNull
        );
    }
}
