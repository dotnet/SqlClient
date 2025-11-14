// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

public class DacResponseProcessorTest
{
    [Theory]
    [MemberData(nameof(SsrpPacketTestData.EmptyPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_EmptyBuffer_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidSVR_RESP_DACPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_InvalidDacResponse_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.ValidSVR_RESP_DACPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_ValidDacResponse_ReturnsData(ReadOnlySequence<byte> packetBuffers, int expectedDacPort)
    {
        _ = packetBuffers;
        _ = expectedDacPort;
    }
}
