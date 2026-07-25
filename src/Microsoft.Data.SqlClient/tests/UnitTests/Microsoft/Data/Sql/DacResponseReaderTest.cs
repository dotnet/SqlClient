// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using Xunit;

#nullable enable

namespace Microsoft.Data.Sql.UnitTests;

public class DacResponseProcessorTest
{
    [Theory]
    [MemberData(nameof(SsrpPacketTestData.EmptyPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_EmptyBuffer_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        bool containsDacResponse = DacResponseReader.TryReadFirst(packetBuffers, out DacResponse response);

        Assert.False(containsDacResponse);
        Assert.Equal(0, response.DacPort);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidSvrRespDacPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_InvalidDacResponse_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        bool containsDacResponse = DacResponseReader.TryReadFirst(packetBuffers, out DacResponse response);

        Assert.False(containsDacResponse);
        Assert.Equal(0, response.DacPort);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.ValidSvrRespDacPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_ValidDacResponse_ReturnsData(ReadOnlySequence<byte> packetBuffers, int expectedDacPort)
    {
        bool containsDacResponse = DacResponseReader.TryReadFirst(packetBuffers, out DacResponse response);

        Assert.True(containsDacResponse);
        Assert.Equal(expectedDacPort, response.DacPort);
    }
}
