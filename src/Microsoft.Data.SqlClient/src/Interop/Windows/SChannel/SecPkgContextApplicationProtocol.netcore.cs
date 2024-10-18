// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET && !NET8_0_OR_GREATER

using System;
using System.Runtime.InteropServices;

namespace Interop_TEMP.Windows.SChannel
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct SecPkgContextApplicationProtocol
    {
        private const int MaxProtocolIdSize = 0xFF;

        public ApplicationProtocolNegotiationStatus ProtoNegoStatus;
        public ApplicationProtocolNegotiationExt ProtoNegoExt;
        public byte ProtocolIdSize;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxProtocolIdSize)]
        public byte[] ProtocolId;
        public byte[] Protocol
        {
            get
            {
                return new Span<byte>(ProtocolId, 0, ProtocolIdSize).ToArray();
            }
        }
    }
}

#endif
