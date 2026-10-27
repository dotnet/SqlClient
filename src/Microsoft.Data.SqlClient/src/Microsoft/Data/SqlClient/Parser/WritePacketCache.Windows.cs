// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Interop.Windows.Sni;

namespace Microsoft.Data.SqlClient.Parser
{
    internal sealed class WritePacketCache : IDisposable
    {
        private bool _disposed;
        private Stack<SNIPacket> _packets;

        public WritePacketCache()
        {
            _disposed = false;
            _packets = new Stack<SNIPacket>();
        }

        public SNIPacket Take(SNIHandle sniHandle)
        {
            SNIPacket packet;
            if (_packets.Count > 0)
            {
                // Success - reset the packet
                packet = _packets.Pop();
                SniNativeWrapper.SniPacketReset(sniHandle, IoType.WRITE, packet, ConsumerNumber.SNI_Consumer_SNI);
            }
            else
            {
                // Failed to take a packet - create a new one
                packet = new SNIPacket(sniHandle);
            }
            return packet;
        }

        public void Add(SNIPacket packet)
        {
            if (!_disposed)
            {
                _packets.Push(packet);
            }
            else
            {
                // If we're disposed, then get rid of any packets added to us
                packet.Dispose();
            }
        }

        public void Clear()
        {
            while (_packets.Count > 0)
            {
                _packets.Pop().Dispose();
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Clear();
            }
        }
    }
}
