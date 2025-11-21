// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NETFRAMEWORK && _WINDOWS

using System;
using System.Runtime.InteropServices;

namespace Interop.Windows.Secur32
{
    internal static class Secur32
    {
        private const string DllName = "secur32.dll";
        
        [DllImport("secur32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern uint QueryContextAttributes(
            ref CredHandle contextHandle,
            [In] ContextAttribute attribute,
            [In] IntPtr buffer);
    }
}

#endif
