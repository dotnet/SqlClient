// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using Xunit;

namespace Microsoft.Data.Sql.UnitTests;

public class SqlDataSourceResponseReaderTest
{
    [Theory]
    [MemberData(nameof(SsrpPacketTestData.EmptyPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_EmptyBuffer_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        bool containsMulticastSsrpResponse = SqlDataSourceResponseReader.TryReadLast(packetBuffers, out _);
        bool containsUnicastSsrpResponse = SqlDataSourceResponseReader.TryReadFirst(packetBuffers, out _);

        Assert.False(containsMulticastSsrpResponse);
        Assert.False(containsUnicastSsrpResponse);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidSvrRespPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_InvalidSqlDataSourceResponse_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        bool containsMulticastSsrpResponse = SqlDataSourceResponseReader.TryReadLast(packetBuffers, out _);
        bool containsUnicastSsrpResponse = SqlDataSourceResponseReader.TryReadFirst(packetBuffers, out _);

        Assert.False(containsMulticastSsrpResponse);
        Assert.False(containsUnicastSsrpResponse);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidRespDataPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_InvalidSqlDataSourceResponse_RespData_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        bool containsMulticastSsrpResponse = SqlDataSourceResponseReader.TryReadLast(packetBuffers, out _);
        bool containsUnicastSsrpResponse = SqlDataSourceResponseReader.TryReadFirst(packetBuffers, out _);

        Assert.False(containsMulticastSsrpResponse);
        Assert.False(containsUnicastSsrpResponse);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidTcpInfoPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_InvalidSqlDataSourceResponse_TcpInfo_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        bool containsMulticastSsrpResponse = SqlDataSourceResponseReader.TryReadLast(packetBuffers, out _);
        bool containsUnicastSsrpResponse = SqlDataSourceResponseReader.TryReadFirst(packetBuffers, out _);

        Assert.False(containsMulticastSsrpResponse);
        Assert.False(containsUnicastSsrpResponse);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.InvalidClntUcastInstSvrRespPackets), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_InvalidSqlDataSourceResponseToClntUcastInst_ReturnsFalse(ReadOnlySequence<byte> packetBuffers)
    {
        const int MaxRESP_DATASizeForResponseToCLNT_UCAST_INST = 1024;

        bool containsUnicastSsrpResponse = SqlDataSourceResponseReader.TryReadFirst(packetBuffers, out _);
        bool containsBroadcastSsrpResponse = SqlDataSourceResponseReader.TryReadLast(packetBuffers, out SqlDataSourceResponse response);

        Assert.False(containsUnicastSsrpResponse);

        Assert.True(containsBroadcastSsrpResponse);
        Assert.Equal("srv1", response.ServerName.ToString());
        Assert.Equal("MSSQLSERVER", response.InstanceName.ToString());

        Assert.True(response.TcpEnabled);
        Assert.Equal(1433, response.TcpPort);
        Assert.True(response.NamedPipeEnabled);
        Assert.True(response.NamedPipe.Length > MaxRESP_DATASizeForResponseToCLNT_UCAST_INST);
    }

    [Theory]
    [MemberData(nameof(SsrpPacketTestData.ValidSvrRespPacketBuffer), MemberType = typeof(SsrpPacketTestData), DisableDiscoveryEnumeration = true)]
    public void Read_ValidSqlDataSourceResponse_ReturnsData(ReadOnlySequence<byte> packetBuffers,
        string expectedBroadcastVersion, int expectedBroadcastTcpPort, string? expectedBroadcastPipeName,
        string expectedUnicastVersion, int expectedUnicastTcpPort, string? expectedUnicastPipeName)
    {
        bool containsBroadcastSsrpResponse = SqlDataSourceResponseReader.TryReadLast(packetBuffers, out SqlDataSourceResponse broadcastResponse);
        bool containsUnicastSsrpResponse = SqlDataSourceResponseReader.TryReadFirst(packetBuffers, out SqlDataSourceResponse unicastResponse);

        AssertResponse(broadcastResponse, containsBroadcastSsrpResponse,
            expectedBroadcastVersion, expectedBroadcastTcpPort, expectedBroadcastPipeName);

        AssertResponse(unicastResponse, containsUnicastSsrpResponse,
            expectedUnicastVersion, expectedUnicastTcpPort, expectedUnicastPipeName);

        static void AssertResponse(SqlDataSourceResponse response, bool containsResponse,
            string expectedVersion, int expectedTcpPort, string? expectedPipeName)
        {
            Assert.True(containsResponse);
            Assert.Equal("srv1", response.ServerName.ToString());
            Assert.Equal("MSSQLSERVER", response.InstanceName.ToString());
            Assert.Equal(expectedVersion, response.Version.ToString());

            Assert.True(response.TcpEnabled);
            Assert.Equal(expectedTcpPort, response.TcpPort);

            if (expectedPipeName is null)
            {
                Assert.False(response.NamedPipeEnabled);
            }
            else
            {
                Assert.True(response.NamedPipeEnabled);
                Assert.Equal(expectedPipeName, response.NamedPipe.ToString());
            }
        }
    }
}
