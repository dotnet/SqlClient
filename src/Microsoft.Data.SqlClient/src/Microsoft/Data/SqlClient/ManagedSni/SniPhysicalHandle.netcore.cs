// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Data.SqlClient.Utilities;

namespace Microsoft.Data.SqlClient.ManagedSni
{
    internal abstract class SniPhysicalHandle : SniHandle
    {
        protected const int DefaultPoolSize = 4;

#if DEBUG
        private static int s_packetId;
#endif
        private ObjectPool<SniPacket> _pool;

        protected SniPhysicalHandle(int poolSize = DefaultPoolSize)
        {
            _pool = new ObjectPool<SniPacket>(poolSize);
        }

        public override SniPacket RentPacket(int headerSize, int dataSize)
        {
            SniPacket packet;
            if (!_pool.TryGet(out packet))
            {
#if DEBUG
                int id = Interlocked.Increment(ref s_packetId);
                packet = new SniPacket(this, id);
#else
                packet = new SNIPacket();
#endif
            }
#if DEBUG
            else
            {
                Debug.Assert(packet != null, "dequeue returned null SNIPacket");
                Debug.Assert(!packet.IsActive, "SNIPacket _refcount must be 1 or a lifetime issue has occurred, trace with the #TRACE_HISTORY define");
                Debug.Assert(packet.IsInvalid, "dequeue returned valid packet");
                GC.ReRegisterForFinalize(packet);
            }
#if TRACE_HISTORY
            if (packet._history != null)
            {
                packet._history.Add(new SNIPacket.History { Action = SNIPacket.History.Direction.Rent, Stack = GetStackParts(), RefCount = packet._refCount });
            }
#endif
            Interlocked.Add(ref packet._refCount, 1);
            Debug.Assert(packet.IsActive, "SNIPacket _refcount must be 1 or a lifetime issue has occurred, trace with the #TRACE_HISTORY define");
#endif
            packet.Allocate(headerSize, dataSize);
            return packet;
        }

        public override void ReturnPacket(SniPacket packet)
        {
#if DEBUG
            Debug.Assert(packet != null, "releasing null SNIPacket");
            Debug.Assert(packet.IsActive, "SNIPacket _refcount must be 1 or a lifetime issue has occurred, trace with the #TRACE_HISTORY define");
            Debug.Assert(ReferenceEquals(packet._owner, this), "releasing SNIPacket that belongs to another physical handle");
            Debug.Assert(!packet.IsInvalid, "releasing already released SNIPacket");
#endif

            packet.Release();
#if DEBUG
            Interlocked.Add(ref packet._refCount, -1);
            packet._traceTag = null;
#if TRACE_HISTORY
            if (packet._history != null)
            {
                packet._history.Add(new SNIPacket.History { Action = SNIPacket.History.Direction.Return, Stack = GetStackParts(), RefCount = packet._refCount });
            }
#endif
            GC.SuppressFinalize(packet);
#endif
            _pool.Return(packet);
        }

#if DEBUG
        private string GetStackParts()
        {
            // trims off the common parts at the top of the stack so you can see what the actual caller was
            // trims off most of the bottom of the stack because when running under xunit there's a lot of spam
            string[] parts = Environment.StackTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            List<string> take = new List<string>(7);
            for (int index = 3; take.Count < 7 && index < parts.Length; index++)
            {
                take.Add(parts[index]);
            }

            return string.Join(Environment.NewLine, take.ToArray());
        }
#endif
    }
}
