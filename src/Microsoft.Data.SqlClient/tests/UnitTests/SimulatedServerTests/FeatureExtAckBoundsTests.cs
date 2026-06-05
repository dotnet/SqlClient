// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.FeatureExtAck;
using Microsoft.SqlServer.TDS.Servers;
using TDSDoneToken = global::Microsoft.SqlServer.TDS.Done.TDSDoneToken;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests;

/// <summary>
/// Tests that the TDS parser rejects feature extension acknowledgment tokens
/// with data lengths exceeding protocol-reasonable bounds. This prevents a
/// malicious server from causing unbounded memory allocation on the client.
/// </summary>
[Collection("SimulatedServerTests")]
public class FeatureExtAckBoundsTests : IClassFixture<TdsServerFixture>
{
    private readonly TdsServer _server;
    private readonly string _connectionString;

    public FeatureExtAckBoundsTests(TdsServerFixture fixture)
    {
        _server = fixture.TdsServer;
        SqlConnectionStringBuilder builder = new()
        {
            DataSource = $"localhost,{_server.EndPoint.Port}",
            Encrypt = SqlConnectionEncryptOption.Optional,
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
    }

    /// <summary>
    /// Verifies that the TDS parser rejects a FeatureExtAck token whose data length
    /// field exceeds <see cref="TdsEnums.MaxTokenDataLength"/> (1 MB), throwing a
    /// parsing error instead of attempting an unbounded heap allocation.
    /// This guards against CVE denial-of-service via pre-auth memory exhaustion.
    /// </summary>
    [Fact]
    public void FeatureExtAck_OversizedDataLength_ThrowsParsingError()
    {
        // Arrange: inject a malicious FeatureExtAck token with an absurdly large data length
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            // Remove any existing FeatureExtAck token
            var existing = responseMessage.OfType<TDSFeatureExtAckToken>().FirstOrDefault();
            if (existing != null)
            {
                responseMessage.Remove(existing);
            }

            // Insert a malicious token with oversized data length before the DONE token
            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            responseMessage.Insert(doneIndex, new MaliciousFeatureExtAckToken(
                featureId: (TDSFeatureID)TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS,
                claimedDataLen: (uint)(TdsEnums.MaxTokenDataLength + 1)));
        };

        try
        {
            // Act & Assert: connection should fail with a parsing error, NOT an OutOfMemoryException
            using SqlConnection connection = new(_connectionString);
            Exception ex = Assert.ThrowsAny<InvalidOperationException>(connection.Open);

            // The exception message should indicate a corrupted TDS stream (parsing error state 18)
            // with the oversized length value, not an OOM from attempting the allocation.
            Assert.Contains("18", ex.Message); // ParsingErrorState.CorruptedTdsStream = 18
            Assert.Contains((TdsEnums.MaxTokenDataLength + 1).ToString(), ex.Message);
        }
        finally
        {
            _server.OnAuthenticationResponseCompleted = null;
        }
    }

    /// <summary>
    /// Verifies that a FeatureExtAck token with a data length at exactly the
    /// allowed maximum is accepted without error, confirming there is no
    /// off-by-one in the bounds check.
    /// </summary>
    [Fact]
    public void FeatureExtAck_MaxAllowedDataLength_DoesNotThrow()
    {
        // Arrange: inject a FeatureExtAck token whose declared data length equals
        // MaxTokenDataLength exactly. The bounds check should NOT fire for this
        // value. The connection will fail for other reasons (not enough data on
        // the wire), but the error must NOT be state 18 (CorruptedTdsStream).
        _server.OnAuthenticationResponseCompleted = responseMessage =>
        {
            var existing = responseMessage.OfType<TDSFeatureExtAckToken>().FirstOrDefault();
            if (existing != null)
            {
                responseMessage.Remove(existing);
            }

            int doneIndex = responseMessage.FindIndex(t => t is TDSDoneToken);
            if (doneIndex < 0)
            {
                doneIndex = responseMessage.Count;
            }

            // Insert token with dataLen = MaxTokenDataLength (at boundary, not over)
            responseMessage.Insert(doneIndex, new MaliciousFeatureExtAckToken(
                featureId: (TDSFeatureID)TdsEnums.FEATUREEXT_GLOBALTRANSACTIONS,
                claimedDataLen: (uint)TdsEnums.MaxTokenDataLength));
        };

        try
        {
            using SqlConnection connection = new(_connectionString);
            // The connection will fail (insufficient data for the claimed length),
            // but the failure must NOT be the bounds check (state 18 with MaxTokenDataLength+1).
            Exception ex = Assert.ThrowsAny<Exception>(connection.Open);
            // Confirm it's NOT the bounds-check error (which would report MaxTokenDataLength+1)
            Assert.DoesNotContain((TdsEnums.MaxTokenDataLength + 1).ToString(), ex.Message);
        }
        finally
        {
            _server.OnAuthenticationResponseCompleted = null;
        }
    }

    /// <summary>
    /// A custom TDS packet token that writes a FEATUREEXTACK token with a fraudulently
    /// large data length field. This simulates a malicious server attempting to cause
    /// the client to allocate an unbounded byte array.
    /// </summary>
    private sealed class MaliciousFeatureExtAckToken : TDSPacketToken
    {
        private readonly TDSFeatureID _featureId;
        private readonly uint _claimedDataLen;

        public MaliciousFeatureExtAckToken(TDSFeatureID featureId, uint claimedDataLen)
        {
            _featureId = featureId;
            _claimedDataLen = claimedDataLen;
        }

        public override bool Inflate(Stream source) => throw new NotSupportedException();

        public override void Deflate(Stream destination)
        {
            // Write the FEATUREEXTACK token type (0xAE)
            destination.WriteByte((byte)TDSTokenType.FeatureExtAck);

            // Write the feature ID byte
            destination.WriteByte((byte)_featureId);

            // Write the claimed data length (uint32, little-endian) — this is the lie
            byte[] lenBytes = BitConverter.GetBytes(_claimedDataLen);
            destination.Write(lenBytes, 0, 4);

            // Write only 1 byte of actual data (the client will try to read _claimedDataLen bytes
            // but we only provide 1 — the bounds check should fire before the read attempt)
            destination.WriteByte(0x01);

            // Write terminator
            destination.WriteByte((byte)TDSFeatureID.Terminator);
        }
    }
}
