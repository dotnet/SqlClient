// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET && _WINDOWS

using Microsoft.Win32.SafeHandles;

namespace Interop.Windows.Handles
{
    internal sealed class SafeLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeLibraryHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return Kernel32.Kernel32.FreeLibrary(handle);
        }
    }
}

#endif
