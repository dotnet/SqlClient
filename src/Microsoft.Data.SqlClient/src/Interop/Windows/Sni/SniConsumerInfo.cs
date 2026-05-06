// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SniConsumerInfo
    {
        public int DefaultUserDataLength;
        public IntPtr ConsumerKey;
        public IntPtr fnReadComp;
        public IntPtr fnWriteComp;
        public IntPtr fnTrace;
        public IntPtr fnAcceptComp;
        public uint dwNumProts;
        public IntPtr rgListenInfo;
        public IntPtr NodeAffinity;
    }
}

#endif
