// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct SniError
    {
        internal Provider provider;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
        internal string errorMessage;
        internal uint nativeError;
        internal uint sniError;
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string fileName;
        [MarshalAs(UnmanagedType.LPWStr)]
        internal string function;
        internal uint lineNumber;
    }
}
