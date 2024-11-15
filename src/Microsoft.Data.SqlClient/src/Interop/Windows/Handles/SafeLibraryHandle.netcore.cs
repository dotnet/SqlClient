// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Win32.SafeHandles;

#if NET

namespace Interop.Windows.Handles
{
    sealed internal class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeLibraryHandle() : base(true) { }

        override protected bool ReleaseHandle()
        {
            return Kernel32.Kernel32.FreeLibrary(handle);
        }
    }
}

#endif
