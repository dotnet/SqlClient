// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

public class DacResponseProcessorTest
{
    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.EmptyPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_EmptyBuffer_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.InvalidSvrRespDacPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_InvalidDacResponse_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.ValidSvrRespDacPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_ValidDacResponse_ReturnsData(ReadOnlySequence<byte> packetBuffers, int expectedDacPort)
    {
        _ = packetBuffers;
        _ = expectedDacPort;
    }
}
