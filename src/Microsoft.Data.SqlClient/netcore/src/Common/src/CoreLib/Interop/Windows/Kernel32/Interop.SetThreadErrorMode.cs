// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

internal partial class Interop
{
    internal partial class Kernel32
    {
        [DllImport(Libraries.Kernel32, SetLastError = true, ExactSpelling = true)]
#if NETFRAMEWORK
        [ResourceExposure(ResourceScope.None)]
#endif
        internal static extern bool SetThreadErrorMode(uint dwNewMode, out uint lpOldMode);

        internal const uint SEM_FAILCRITICALERRORS = 1;
    }
}
