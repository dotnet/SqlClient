// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

public class SqlDataSourceResponseProcessorTest
{
    [Theory]
    [MemberData(nameof(SsrpPacketTestData.EmptyPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_EmptyBuffer_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidSVR_RESPPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_InvalidSqlDataSourceResponse_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidRESP_DATAPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_InvalidSqlDataSourceResponse_RESP_DATA_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidTCP_INFOPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_InvalidSqlDataSourceResponse_TCP_INFO_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.Invalid_CLNT_UCAST_INST_SVR_RESPPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_InvalidSqlDataSourceResponseToCLNT_UCAST_INST_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        _ = packetBuffers;
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.ValidSVR_RESPPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    [ActiveIssue("https://github.com/dotnet/SqlClient/issues/3700")]
    public void Process_ValidSqlDataSourceResponse_ReturnsData(ReadOnlySequence<byte> packetBuffers, string expectedVersion, int expectedTcpPort, string? expectedPipeName)
    {
        _ = packetBuffers;
        _ = expectedVersion;
        _ = expectedTcpPort;
        _ = expectedPipeName;
    }
}
