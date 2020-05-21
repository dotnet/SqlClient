// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using System.Text;

namespace Microsoft.Data.SqlClient.SNI
{
    internal abstract class SNIPhysicalHandle : SNIHandle
    {
        protected const int DefaultPoolSize = 4;
#if DEBUG
        private static int s_packetId;
#endif
        private SNIPacketPool _pool;

        protected SNIPhysicalHandle(int poolSize = DefaultPoolSize)
        {
            _pool = new SNIPacketPool(poolSize);
        }

        public override SNIPacket RentPacket(int headerSize, int dataSize)
        {
            SNIPacket packet;
            if (!_pool.TryGet(out packet))
            {
#if DEBUG
                int id = Interlocked.Increment(ref s_packetId);
                packet = new SNIPacket(this, id);
#else
                packet = new SNIPacket();
#endif
            }
#if DEBUG
            else
            {
                Debug.Assert(packet != null, "dequeue returned null SNIPacket");
                Debug.Assert(!packet.IsActive, "SNIPacket _refcount must be 1 or a lifetime issue has occured, trace with the #TRACE_HISTORY define");
                Debug.Assert(packet.IsInvalid, "dequeue returned valid packet");
                GC.ReRegisterForFinalize(packet);
            }
            if (packet._history != null)
            {
                packet._history.Add(new SNIPacket.History { Action = SNIPacket.History.Direction.Rent, Stack = GetStackParts(), RefCount = packet._refCount });
            }
            Interlocked.Add(ref packet._refCount, 1);
            Debug.Assert(packet.IsActive, "SNIPacket _refcount must be 1 or a lifetime issue has occured, trace with the #TRACE_HISTORY define");
#endif
            packet.Allocate(headerSize, dataSize);
            return packet;
        }

        public override void ReturnPacket(SNIPacket packet)
        {
            Debug.Assert(packet != null, "releasing null SNIPacket");
#if DEBUG
            Debug.Assert(packet.IsActive, "SNIPacket _refcount must be 1 or a lifetime issue has occured, trace with the #TRACE_HISTORY define");
            Debug.Assert(ReferenceEquals(packet._owner, this), "releasing SNIPacket that belongs to another physical handle");
#endif
            Debug.Assert(!packet.IsInvalid, "releasing already released SNIPacket");

            packet.Release();
#if DEBUG
            Interlocked.Add(ref packet._refCount, -1);
            packet._traceTag = null;
            if (packet._history != null)
            {
                packet._history.Add(new SNIPacket.History { Action = SNIPacket.History.Direction.Return, Stack = GetStackParts(), RefCount = packet._refCount });
            }
            GC.SuppressFinalize(packet);
#endif
            _pool.Return(packet);
        }

#if DEBUG
        private string GetStackParts()
        {
            return string.Join(Environment.NewLine,
                Environment.StackTrace
                .Split(new string[] { Environment.NewLine },StringSplitOptions.None)
                .Skip(3) // trims off the common parts at the top of the stack so you can see what the actual caller was
                .Take(7) // trims off most of the bottom of the stack because when running under xunit there's a lot of spam
            );
        }
#endif
    }
}
