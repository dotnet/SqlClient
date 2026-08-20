// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Covers the <see cref="TdsParserStateObject.StateSnapshot"/> packet node free list.
    ///
    /// A snapshot is taken and released for every async continuation, so the nodes describing the
    /// captured packets are recycled rather than reallocated. Recycling is only safe if a node
    /// carries nothing from its previous use; an incomplete reset of <c>RunningDataSize</c> is what
    /// produced the incorrect offset/length calculations in dotnet/SqlClient#3519.
    ///
    /// The nodes and the list are private by design, so these tests reach them by reflection.
    /// </summary>
    public sealed class StateSnapshotPacketRecyclingTests
    {
        // Must exceed StateSnapshot.MaxSparePacketCount so the bound itself is exercised.
        private const int MaxSparePacketCount = 16;
        private const int PacketsPerSnapshot = 20;

        private const int HeaderLength = 8;
        private const int HeaderLengthFieldOffset = 2;

        #region Tests

        /// <summary>
        /// Regression guard for dotnet/SqlClient#3519. A node returning from the free list must have
        /// <c>RunningDataSize</c> cleared, otherwise the first packet of a new snapshot reports a
        /// running total inherited from the previous snapshot and every offset derived from it is
        /// wrong.
        /// </summary>
        [Fact]
        public void RecycledNodes_DoNotCarryRunningDataSizeFromPreviousSnapshot()
        {
            SnapshotAccessor snapshot = SnapshotAccessor.Create();
            int[] sizes = { 40, 55, 70, 25 };

            // First snapshot: append packets and accumulate a running total on each node.
            AppendPackets(snapshot, sizes);
            AssignRunningDataSizes(snapshot, sizes);

            List<object> firstRound = snapshot.LivePackets();
            Assert.Equal(sizes.Length, firstRound.Count);
            // Sanity check that the running totals really were populated, so that finding zeroes
            // after recycling is meaningful.
            Assert.Equal(sizes.Sum(), SnapshotAccessor.GetRunningDataSize(firstRound[firstRound.Count - 1]));

            snapshot.ClearPackets();

            // Second snapshot: the nodes are served from the free list.
            AppendPackets(snapshot, sizes);
            List<object> secondRound = snapshot.LivePackets();

            Assert.Equal(sizes.Length, secondRound.Count);
            Assert.NotEmpty(secondRound.Intersect(firstRound, ReferenceComparer.Instance));

            // The defect behind #3519: a recycled node kept its running total, so a freshly
            // appended packet claimed data that belonged to the previous snapshot.
            foreach (object packet in secondRound)
            {
                Assert.Equal(0, SnapshotAccessor.GetRunningDataSize(packet));
            }

            // With the totals cleared, the derived offsets and lengths are correct again.
            AssignRunningDataSizes(snapshot, sizes);

            int expectedOffset = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                Assert.Equal(expectedOffset, SnapshotAccessor.GetPacketDataOffset(secondRound[i]));
                Assert.Equal(sizes[i], SnapshotAccessor.GetPacketDataSize(secondRound[i]));
                expectedOffset += sizes[i];
            }
        }

        /// <summary>
        /// A parked node must hold no packet state at all, in particular no reference to the packet
        /// buffer, which the snapshot does not own.
        /// </summary>
        [Fact]
        public void ParkedNodes_ReleaseAllPacketState()
        {
            SnapshotAccessor snapshot = SnapshotAccessor.Create();

            AppendPackets(snapshot, UniformSizes(PacketsPerSnapshot, 64));
            AssignRunningDataSizes(snapshot, UniformSizes(PacketsPerSnapshot, 64));
            snapshot.ClearPackets();

            List<object> spares = snapshot.SparePackets();
            Assert.NotEmpty(spares);

            foreach (object packet in spares)
            {
                Assert.Null(SnapshotAccessor.GetBuffer(packet));
                Assert.Equal(0, SnapshotAccessor.GetRead(packet));
                Assert.Equal(0, SnapshotAccessor.GetRunningDataSize(packet));
                Assert.Null(SnapshotAccessor.GetPrevPacket(packet));
            }
        }

        /// <summary>
        /// The free list is bounded, and its tracked count must stay in step with its real length.
        /// If the two drift, the list either stops recycling or grows without limit.
        /// </summary>
        [Fact]
        public void FreeList_RespectsBoundAndTracksItsOwnCount()
        {
            SnapshotAccessor snapshot = SnapshotAccessor.Create();

            for (int round = 0; round < 5; round++)
            {
                AppendPackets(snapshot, UniformSizes(PacketsPerSnapshot, 32));
                Assert.Equal(0, snapshot.SpareCount);

                snapshot.ClearPackets();

                List<object> spares = snapshot.SparePackets();
                Assert.Equal(MaxSparePacketCount, spares.Count);
                Assert.Equal(spares.Count, snapshot.SpareCount);
                Assert.Equal(spares.Count, spares.Distinct(ReferenceComparer.Instance).Count());
            }
        }

        /// <summary>
        /// The most dangerous failure mode for a free list: a node reachable from both the live
        /// chain and the free list. Two snapshots would then share a node and silently overwrite
        /// each other's packet descriptions.
        /// </summary>
        [Fact]
        public void LiveChainAndFreeList_NeverShareANode()
        {
            SnapshotAccessor snapshot = SnapshotAccessor.Create();

            for (int round = 0; round < 5; round++)
            {
                // Alternate between more and fewer packets than the bound so the list is both
                // saturated and partially drained across rounds.
                int count = round % 2 == 0 ? PacketsPerSnapshot : MaxSparePacketCount / 2;
                AppendPackets(snapshot, UniformSizes(count, 48));

                List<object> live = snapshot.LivePackets();
                List<object> spares = snapshot.SparePackets();

                Assert.Equal(count, live.Count);
                Assert.Empty(live.Intersect(spares, ReferenceComparer.Instance));
                Assert.Equal(live.Count, live.Distinct(ReferenceComparer.Instance).Count());

                snapshot.ClearPackets();
                Assert.Empty(snapshot.LivePackets());
            }
        }

        /// <summary>
        /// Recycling must not damage the doubly linked chain: replay walks it forwards through
        /// <c>NextPacket</c> and the offset maths walks it backwards through <c>PrevPacket</c>.
        /// </summary>
        [Fact]
        public void RebuiltChain_HasIntactLinksAfterRecycling()
        {
            SnapshotAccessor snapshot = SnapshotAccessor.Create();

            AppendPackets(snapshot, UniformSizes(PacketsPerSnapshot, 16));
            snapshot.ClearPackets();
            AppendPackets(snapshot, UniformSizes(PacketsPerSnapshot, 16));

            List<object> live = snapshot.LivePackets();
            Assert.Equal(PacketsPerSnapshot, live.Count);

            Assert.Null(SnapshotAccessor.GetPrevPacket(live[0]));
            Assert.Null(SnapshotAccessor.GetNextPacket(live[live.Count - 1]));
            Assert.Same(live[live.Count - 1], snapshot.LastPacket);

            for (int i = 0; i < live.Count - 1; i++)
            {
                Assert.Same(live[i + 1], SnapshotAccessor.GetNextPacket(live[i]));
                Assert.Same(live[i], SnapshotAccessor.GetPrevPacket(live[i + 1]));
            }
        }

        #endregion

        #region Helpers

        private static int[] UniformSizes(int count, int dataLength) =>
            Enumerable.Repeat(dataLength, count).ToArray();

        private static void AppendPackets(SnapshotAccessor snapshot, int[] dataLengths)
        {
            for (int i = 0; i < dataLengths.Length; i++)
            {
                // Each packet gets its own buffer: the snapshot permits several packets to share a
                // buffer only for partial reads, which is not what these tests cover.
                snapshot.AppendPacketData(CreatePacket(dataLengths[i], (byte)(i + 1)), HeaderLength + dataLengths[i]);
            }
        }

        /// <summary>
        /// Walks the live chain and populates the running totals the way the parser does while
        /// reading columns, so the offset calculations have real data to work from.
        /// </summary>
        private static void AssignRunningDataSizes(SnapshotAccessor snapshot, int[] sizes)
        {
            List<object> live = snapshot.LivePackets();
            for (int i = 0; i < sizes.Length && i < live.Count; i++)
            {
                snapshot.SetCurrent(live[i]);
                snapshot.SetPacketDataSize(sizes[i]);
            }
            snapshot.SetCurrent(null);
        }

        /// <summary>
        /// Builds a well formed TDS packet. The snapshot asserts in DEBUG that the length in the
        /// header matches the number of bytes read, so the header cannot be left blank.
        /// </summary>
        private static byte[] CreatePacket(int dataLength, byte packetId)
        {
            int total = HeaderLength + dataLength;
            byte[] buffer = new byte[total];

            buffer[0] = 4;                                              // MT_TOKENS
            buffer[1] = 1;                                              // ST_EOM
            buffer[HeaderLengthFieldOffset] = (byte)(total >> 8);       // length is big endian and
            buffer[HeaderLengthFieldOffset + 1] = (byte)(total & 0xFF); // includes the header
            buffer[6] = packetId;

            for (int i = HeaderLength; i < total; i++)
            {
                buffer[i] = (byte)(packetId + i);
            }

            return buffer;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceComparer Instance = new();

            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) =>
                System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        /// <summary>
        /// Reflection wrapper over the private members of <see cref="TdsParserStateObject.StateSnapshot"/>
        /// and its nested packet node type.
        /// </summary>
        private sealed class SnapshotAccessor
        {
            private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            private static readonly Type s_snapshotType = typeof(TdsParserStateObject.StateSnapshot);
            private static readonly Type s_packetType = NestedType(s_snapshotType, "PacketData");

            private static readonly FieldInfo s_firstPacket = Field(s_snapshotType, "_firstPacket");
            private static readonly FieldInfo s_lastPacket = Field(s_snapshotType, "_lastPacket");
            private static readonly FieldInfo s_current = Field(s_snapshotType, "_current");
            private static readonly FieldInfo s_sparePackets = Field(s_snapshotType, "_sparePackets");
            private static readonly FieldInfo s_spareCount = Field(s_snapshotType, "_sparePacketCount");
            private static readonly FieldInfo s_stateObj = Field(s_snapshotType, "_stateObj");
            private static readonly MethodInfo s_clearPackets = Method(s_snapshotType, "ClearPackets");

            private static readonly FieldInfo s_buffer = Field(s_packetType, "Buffer");
            private static readonly FieldInfo s_read = Field(s_packetType, "Read");
            private static readonly FieldInfo s_next = Field(s_packetType, "NextPacket");
            private static readonly FieldInfo s_prev = Field(s_packetType, "PrevPacket");
            private static readonly FieldInfo s_runningDataSize = Field(s_packetType, "RunningDataSize");
            private static readonly MethodInfo s_getOffset = Method(s_packetType, "GetPacketDataOffset");
            private static readonly MethodInfo s_getSize = Method(s_packetType, "GetPacketDataSize");

            // These members are private implementation detail, so a rename would silently turn the
            // tests into no-ops. Fail loudly instead of returning null.
            private static Type NestedType(Type owner, string name) =>
                owner.GetNestedType(name, BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"{owner.Name} no longer declares a nested type '{name}'.");

            private static FieldInfo Field(Type owner, string name) =>
                owner.GetField(name, Instance)
                ?? throw new InvalidOperationException($"{owner.Name} no longer declares a field '{name}'.");

            private static MethodInfo Method(Type owner, string name) =>
                owner.GetMethod(name, Instance)
                ?? throw new InvalidOperationException($"{owner.Name} no longer declares a method '{name}'.");

            private readonly TdsParserStateObject.StateSnapshot _snapshot;

            private SnapshotAccessor(TdsParserStateObject.StateSnapshot snapshot) => _snapshot = snapshot;

            internal static SnapshotAccessor Create()
            {
                TdsParserStateObject.StateSnapshot snapshot = new();

                // In DEBUG the snapshot records the owning state object's last stack trace on each
                // appended packet. A real state object needs a live connection, so supply an
                // allocated but unconstructed instance: only the null valued _lastStack is read.
                Type concrete = typeof(TdsParserStateObject).Assembly
                    .GetTypes()
                    .First(t => !t.IsAbstract && typeof(TdsParserStateObject).IsAssignableFrom(t));
#if NETFRAMEWORK
                object stateObj = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(concrete);
#else
                object stateObj = System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(concrete);
#endif
                s_stateObj.SetValue(snapshot, stateObj);

                return new SnapshotAccessor(snapshot);
            }

            internal int SpareCount => (int)s_spareCount.GetValue(_snapshot)!;

            internal object? LastPacket => s_lastPacket.GetValue(_snapshot);

            internal void AppendPacketData(byte[] buffer, int read) => _snapshot.AppendPacketData(buffer, read);

            internal void SetPacketDataSize(int size) => _snapshot.SetPacketDataSize(size);

            internal void SetCurrent(object? packet) => s_current.SetValue(_snapshot, packet);

            internal void ClearPackets() => s_clearPackets.Invoke(_snapshot, null);

            internal List<object> LivePackets() => Walk(s_firstPacket.GetValue(_snapshot));

            internal List<object> SparePackets() => Walk(s_sparePackets.GetValue(_snapshot));

            private static List<object> Walk(object? head)
            {
                List<object> packets = new();
                object? current = head;
                while (current != null)
                {
                    packets.Add(current);
                    current = GetNextPacket(current);

                    // A cycle would otherwise hang the test run rather than fail it.
                    Assert.True(packets.Count <= 1024, "packet chain does not terminate");
                }
                return packets;
            }

            internal static byte[]? GetBuffer(object packet) => (byte[]?)s_buffer.GetValue(packet);

            internal static int GetRead(object packet) => (int)s_read.GetValue(packet)!;

            internal static object? GetNextPacket(object packet) => s_next.GetValue(packet);

            internal static object? GetPrevPacket(object packet) => s_prev.GetValue(packet);

            internal static int GetRunningDataSize(object packet) => (int)s_runningDataSize.GetValue(packet)!;

            internal static int GetPacketDataOffset(object packet) => (int)s_getOffset.Invoke(packet, null)!;

            internal static int GetPacketDataSize(object packet) => (int)s_getSize.Invoke(packet, null)!;
        }

        #endregion
    }
}
