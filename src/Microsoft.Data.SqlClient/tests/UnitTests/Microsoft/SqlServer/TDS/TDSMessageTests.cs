// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.Done;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Verifies simulator response fragments preserve message boundaries and packet numbering.
/// </summary>
public class TDSMessageTests
{
    /// <summary>
    /// Partial and final fragments share a packet sequence; the next message starts a new one.
    /// </summary>
    [Fact]
    public void Deflate_PartialThenFinalResponse_PreservesMessageBoundary()
    {
        // Arrange
        using MemoryStream output = new();
        using TDSStream transport = new(output) { PacketSize = 4096 };
        TDSMessage partial = new(TDSMessageType.Response,
            new TDSDoneToken(TDSDoneTokenStatusType.More))
        {
            IsEndOfMessage = false
        };
        TDSMessage final = new(TDSMessageType.Response,
            new TDSDoneToken(TDSDoneTokenStatusType.Attention));
        TDSMessage next = new(TDSMessageType.Response,
            new TDSDoneToken(TDSDoneTokenStatusType.Final));

        // Act
        partial.Deflate(transport);
        final.Deflate(transport);
        next.Deflate(transport);
        byte[] packets = output.ToArray();

        // Assert
        int finalOffset = (packets[2] << 8) | packets[3];
        int nextOffset = finalOffset + (packets[finalOffset + 2] << 8)
            + packets[finalOffset + 3];
        Assert.Equal(0, packets[1] & (byte)TDSPacketStatus.EndOfMessage);
        Assert.Equal((byte)TDSPacketStatus.EndOfMessage, packets[finalOffset + 1]);
        Assert.Equal((byte)TDSPacketStatus.EndOfMessage, packets[nextOffset + 1]);
        Assert.Equal(1, packets[6]);
        Assert.Equal(2, packets[finalOffset + 6]);
        Assert.Equal(1, packets[nextOffset + 6]);
    }
}
