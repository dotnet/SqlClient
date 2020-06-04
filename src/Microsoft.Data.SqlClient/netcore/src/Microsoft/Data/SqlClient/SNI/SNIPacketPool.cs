// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Threading;

namespace Microsoft.Data.SqlClient.SNI
{
    // this is a very simple threadsafe pool derived from the aspnet/extensions default pool implementation
    // https://github.com/dotnet/extensions/blob/release/3.1/src/ObjectPool/src/DefaultObjectPool.cs
    internal sealed class SNIPacketPool
    {
        private readonly PacketWrapper[] _items;
        private SNIPacket _firstItem;

        public SNIPacketPool(int maximumRetained)
        {
            // -1 due to _firstItem
            _items = new PacketWrapper[maximumRetained - 1];
        }

        public bool TryGet(out SNIPacket packet)
        {
            packet = null;
            SNIPacket item = _firstItem;
            if (item != null && Interlocked.CompareExchange(ref _firstItem, null, item) == item)
            {
                // took first item
                packet = item;
                return true;
            }
            else
            {
                var items = _items;
                for (var i = 0; i < items.Length; i++)
                {
                    item = items[i].Element;
                    if (item != null && Interlocked.CompareExchange(ref items[i].Element, null, item) == item)
                    {
                        packet = item;
                        return true;
                    }
                }
            }
            return false;
        }

        public void Return(SNIPacket packet)
        {
            if (_firstItem != null || Interlocked.CompareExchange(ref _firstItem, packet, null) != null)
            {
                var items = _items;
                for (var i = 0; i < items.Length && Interlocked.CompareExchange(ref items[i].Element, packet, null) != null; ++i)
                {
                }
            }
        }

        // PERF: the struct wrapper avoids array-covariance-checks from the runtime when assigning to elements of the array.
        [DebuggerDisplay("{Element}")]
        private struct PacketWrapper
        {
            public SNIPacket Element;
        }
    }
}
