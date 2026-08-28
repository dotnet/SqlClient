// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Interop.Windows.Sni;

namespace Microsoft.Data.SqlClient.Parser;

internal sealed class SNIPacket : SafeHandle
{
    internal SNIPacket(SafeHandle sniHandle) : base(IntPtr.Zero, true)
    {
        SniNativeWrapper.SniPacketAllocate(sniHandle, IoType.WRITE, ref base.handle);
        if (IntPtr.Zero == base.handle)
        {
            throw SQL.SNIPacketAllocationFailure();
        }
    }

    public override bool IsInvalid
    {
        get
        {
            return (IntPtr.Zero == base.handle);
        }
    }

    protected override bool ReleaseHandle()
    {
        // NOTE: The SafeHandle class guarantees this will be called exactly once.
        IntPtr ptr = base.handle;
        base.handle = IntPtr.Zero;
        if (IntPtr.Zero != ptr)
        {
            SniNativeWrapper.SniPacketRelease(ptr);
        }
        return true;
    }
}
