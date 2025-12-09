// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if _WINDOWS

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Sni
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct ConsumerInfo
    {
        internal int defaultBufferSize;
        internal SqlAsyncCallbackDelegate readDelegate;
        internal SqlAsyncCallbackDelegate writeDelegate;
        internal IntPtr key;
    }
}

#endif
