// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

public class SqlDataSourceResponseProcessorTest
{
    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.EmptyPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_EmptyBuffer_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.InvalidSvrRespPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_InvalidSqlDataSourceResponse_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.InvalidRespDataPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_InvalidSqlDataSourceResponse_RespData_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.InvalidTcpInfoPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_InvalidSqlDataSourceResponse_TcpInfo_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.InvalidClntUcastInstSvrRespPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_InvalidSqlDataSourceResponseToClntUcastInst_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory(Skip = "Implementation in progress, see GH #3700")]
    [MemberData(nameof(SsrpPacketTestData.ValidSvrRespPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Process_ValidSqlDataSourceResponse_ReturnsData(ReadOnlySequence<byte> packetBuffers, string expectedVersion, int expectedTcpPort, string? expectedPipeName)
    {
        _ = packetBuffers;
        _ = expectedVersion;
        _ = expectedTcpPort;
        _ = expectedPipeName;
    }
}
